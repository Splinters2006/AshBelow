using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonRun : MonoBehaviour
    {
        public DungeonMap Map { get; private set; }
        public DungeonPlayer Player { get; private set; }
        public List<DungeonEnemy> Enemies { get; } = new List<DungeonEnemy>();
        public bool IsPlaying { get; private set; }
        public bool ChoosingUpgrade { get; private set; }
        public bool ChoosingArtifact { get; private set; }
        public bool IsBossFloor => Floor > 0 && Floor % 5 == 0;
        public DungeonBoss Boss { get; private set; }
        public ArtifactPickup Artifact { get; private set; }
        public string Objective => Artifact != null ? "Claim the glowing artifact  /  F"
            : IsBossFloor && Enemies.Count > 0 ? "Defeat the arena guardian"
            : Enemies.Count == 0 ? "Find the gold stairs  /  F" : "Clear the floor to unlock the stairs";
        private readonly List<PowerupDefinition> upgradeChoices = new List<PowerupDefinition>();
        public IReadOnlyList<PowerupDefinition> UpgradeChoices => upgradeChoices;
        public int Floor { get; private set; }
        public int Kills { get; set; }
        public int Seed { get; private set; }
        public Camera View => view;
        public Transform ProjectileRoot => level;
        public bool IsInMainMenu { get; private set; }
        public IReadOnlyList<CharacterDefinition> Characters => characters;
        public CharacterDefinition SelectedCharacter { get; private set; }
        private CharacterDefinition[] characters;
        private MainMenu menu;
        private DungeonHud hud;
        public bool IsPointerOverHud => hud != null && hud.BlocksPointer(PlayerInput.CursorPosition);
        private Transform level;
        private Camera view;
        private Vector2 exit;
        private StairVisual stairs;
        private bool floorRewardGranted;
        private float nextSaveRetry;
        public PermanentProgress Progress { get; private set; }
        public int RunAshEarned { get; private set; }
        private readonly int[,] distances = new int[DungeonMap.Width, DungeonMap.Height];
        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private Vector2Int lastPlayerCell = new Vector2Int(-1, -1);

        private void Start()
        {
            string saveDirectory = Application.persistentDataPath;
#if UNITY_EDITOR
            // Automated tests must never read or change the player's real wallet.
            if (Application.isBatchMode)
                saveDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ashbelow-tests", System.Guid.NewGuid().ToString("N"));
#endif
            Progress = new PermanentProgress(saveDirectory);
            view = Camera.main;
            if (view == null) view = new GameObject("Dungeon Camera", typeof(Camera)).GetComponent<Camera>();
            view.orthographic = true;
            view.orthographicSize = 8;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.035f, 0.055f, 0.08f);
            hud = gameObject.AddComponent<DungeonHud>();
            hud.Run = this;
            characters = Resources.LoadAll<CharacterDefinition>("Characters");
            System.Array.Sort(characters, (a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            if (characters.Length == 0)
            {
                Debug.LogError("No character assets found in Resources/Characters.");
                return;
            }
            SelectedCharacter = System.Array.Find(characters, character => character.Weapon == WeaponType.Sword) ?? characters[0];
            menu = gameObject.AddComponent<MainMenu>();
            menu.Run = this;
            ShowMainMenu();
        }

        public void SelectCharacter(CharacterDefinition character)
        {
            if (IsInMainMenu && System.Array.IndexOf(characters, character) >= 0) SelectedCharacter = character;
        }

        public void ShowMainMenu()
        {
            IsPlaying = false;
            ChoosingUpgrade = false;
            ChoosingArtifact = false;
            Boss = null;
            Artifact = null;
            Time.timeScale = 1f;
            IsInMainMenu = true;
            if (level != null) { level.gameObject.SetActive(false); Destroy(level.gameObject); level = null; }
            if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); Player = null; }
            Enemies.Clear();
            menu.ResetPage();
        }

        public void Restart()
        {
            if (SelectedCharacter == null) return;
            IsInMainMenu = false;
            if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); }
            Seed = UnityEngine.Random.Range(0, 1000000);
            Floor = 0;
            Kills = 0;
            RunAshEarned = 0;
            Player = DungeonVisuals.Create(SelectedCharacter.DisplayName, transform, Vector2.zero, Vector2.one * 0.65f,
                SelectedCharacter.Color, 4).gameObject.AddComponent<DungeonPlayer>();
            Player.Run = this;
            Player.Initialize(SelectedCharacter);
            NextFloor();
        }

        private void NextFloor()
        {
            Time.timeScale = 1f;
            Boss = null;
            Artifact = null;
            ChoosingArtifact = false;
            upgradeChoices.Clear();
            Player.Powerups.BeginFloor();
            Player.Weapon?.Hide();
            if (level != null) { level.gameObject.SetActive(false); Destroy(level.gameObject); }
            Enemies.Clear();
            Floor++;
            floorRewardGranted = false;
            Map = new DungeonMap(Seed + Floor * 7919, IsBossFloor);
            level = new GameObject("Floor " + Floor).transform;
            level.SetParent(transform);
            DungeonVisuals.DrawMap(Map, level);
            Player.transform.position = (Vector2)Map.Centers[0];
            exit = Map.Centers[Map.Centers.Count - 1];
            stairs = StairVisual.Create(level, exit);
            if (IsBossFloor)
            {
                var enemy = DungeonVisuals.Create("Guardian", level, exit, Vector2.one * 1.4f,
                    new Color(0.65f, 0.3f, 0.55f), 4).gameObject.AddComponent<DungeonEnemy>();
                Boss = enemy.gameObject.AddComponent<DungeonBoss>();
                Boss.Initialize(this);
                Enemies.Add(enemy);
                DungeonVisuals.DecorateArena(level);
            }
            for (int room = 1; !IsBossFloor && room < Map.Centers.Count; room++)
            {
                int count = Mathf.Min(4, 1 + Floor);
                for (int i = 0; i < count; i++)
                {
                    Vector2 position = (Vector2)Map.Centers[room] + new Vector2(i % 2, i / 2);
                    var enemy = DungeonVisuals.Create("Ashling", level, position, Vector2.one * 0.6f,
                        new Color(1f, 0.35f, 0.4f), 3).gameObject.AddComponent<DungeonEnemy>();
                    enemy.Run = this;
                    enemy.Health = EnemyHealthForFloor(Floor);
                    enemy.Speed = Mathf.Min(3.6f, 1.8f + Floor * 0.12f);
                    if (i == 1) enemy.gameObject.AddComponent<EnemyShooter>();
                    else if (i == 0 && room % 2 == 0)
                    {
                        enemy.IsTank = true;
                        enemy.name = "Iron brute";
                        enemy.Health *= 3;
                        enemy.Speed *= 0.6f;
                        enemy.transform.localScale = Vector2.one * 0.9f;
                    }
                    Enemies.Add(enemy);
                }
            }
            lastPlayerCell = new Vector2Int(-1, -1);
            ChoosingUpgrade = false;
            IsPlaying = true;
            view.transform.position = new Vector3(Player.transform.position.x, Player.transform.position.y, -10);
            UpdatePaths();
        }

        private void Update()
        {
            if (Progress.HasUnsavedChanges && Time.unscaledTime >= nextSaveRetry)
            { Progress.Save(); nextSaveRetry = Time.unscaledTime + 5f; }
            if (!IsPlaying) return;
            UpdatePaths();
            if (Artifact != null && Vector2.Distance(Player.transform.position, Artifact.transform.position) < 1.5f && PlayerInput.Interact)
            {
                BeginArtifactChoice();
                return;
            }
            stairs.SetUnlocked(Enemies.Count == 0 && Artifact == null);
            if (Artifact == null && Enemies.Count == 0 && Vector2.Distance(Player.transform.position, exit) < 1.2f && PlayerInput.Interact)
            {
                BeginUpgradeChoice();
            }
        }

        private void LateUpdate()
        {
            if (Player == null) return;
            var target = new Vector3(Player.transform.position.x, Player.transform.position.y, -10);
            view.transform.position = Vector3.Lerp(view.transform.position, target, 1 - Mathf.Exp(-10 * Time.deltaTime));
        }

        public void EnemyDefeated(DungeonEnemy enemy)
        {
            if (!Enemies.Remove(enemy)) return;
            Kills++;
            int reward = enemy.Boss != null ? 50 : 1;
            if (Enemies.Count == 0 && !floorRewardGranted)
            { reward += 10; floorRewardGranted = true; }
            RunAshEarned += reward;
            Progress.AwardAsh(reward);
        }

        public bool TryBuyUpgrade(string id) => IsInMainMenu && Progress.TryPurchase(id);
        private void OnApplicationQuit() { Progress?.Save(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Progress?.Save(); }

        public static int EnemyHealthForFloor(int floor) => 2 + Mathf.Max(0, floor - 3);
        public void EndRun() { IsPlaying = false; Player.Weapon?.Hide(); Time.timeScale = 0f; }
        public void DropArtifact(Vector2 position)
        {
            if (Artifact == null) Artifact = ArtifactPickup.Spawn(level, position);
        }
        public void BeginArtifactChoice()
        {
            if (!IsPlaying || Artifact == null || Enemies.Count != 0) return;
            IsPlaying = false;
            ChoosingArtifact = true;
            Player.Weapon?.Hide();
            Time.timeScale = 0f;
        }
        public bool ChooseArtifact(AbilityType type, int slot)
        {
            if (!ChoosingArtifact || !Player.Abilities.Claim(type, slot)) return false;
            Player.Heal(2);
            FinishArtifactChoice();
            return true;
        }
        public void FinishArtifactChoice()
        {
            if (!ChoosingArtifact) return;
            if (Artifact != null) Destroy(Artifact.gameObject);
            Artifact = null;
            ChoosingArtifact = false;
            IsPlaying = true;
            Time.timeScale = 1f;
        }
        public void BeginUpgradeChoice()
        {
            if (!IsPlaying || Enemies.Count != 0 || Artifact != null) return;
            if (IsBossFloor) { NextFloor(); return; }
            var pool = new List<PowerupDefinition>();
            foreach (var powerup in PowerupCatalog.All)
                if (Player.Powerups.CanTake(powerup.Type)) pool.Add(powerup);
            upgradeChoices.Clear();
            var random = new System.Random(Seed + Floor * 3571);
            var talents = pool.FindAll(powerup => powerup.ClassWeapon.HasValue);
            if (talents.Count > 0)
            {
                var talent = talents[random.Next(talents.Count)];
                upgradeChoices.Add(talent);
                pool.Remove(talent);
            }
            while (upgradeChoices.Count < 3 && pool.Count > 0)
            {
                int index = random.Next(pool.Count);
                upgradeChoices.Add(pool[index]);
                pool.RemoveAt(index);
            }
            IsPlaying = false;
            Player.Weapon?.Hide();
            ChoosingUpgrade = true;
            Time.timeScale = 0f;
        }

        public void ChooseUpgrade(int choice)
        {
            if (!ChoosingUpgrade || choice < 0 || choice >= upgradeChoices.Count) return;
            Player.Upgrade((int)upgradeChoices[choice].Type);
            NextFloor();
        }

        public bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) * 5));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, (float)i / steps);
                if (!Map.IsFloor(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y))) return false;
            }
            return true;
        }

        private void UpdatePaths()
        {
            Vector2Int cell = Vector2Int.RoundToInt(Player.transform.position);
            if (cell == lastPlayerCell) return;
            lastPlayerCell = cell;
            for (int x = 0; x < DungeonMap.Width; x++)
                for (int y = 0; y < DungeonMap.Height; y++) distances[x, y] = int.MaxValue;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(cell);
            distances[cell.x, cell.y] = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (!Map.IsFloor(next.x, next.y) || distances[next.x, next.y] != int.MaxValue) continue;
                    distances[next.x, next.y] = distances[current.x, current.y] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        public Vector2 DirectionToPlayer(Vector2 position)
        {
            var cell = Vector2Int.RoundToInt(position);
            var best = cell;
            foreach (var step in Steps)
            {
                var next = cell + step;
                if (Map.IsFloor(next.x, next.y) && distances[next.x, next.y] < distances[best.x, best.y]) best = next;
            }
            return ((Vector2)best - position).normalized;
        }
    }
}

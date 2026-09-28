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
        private Transform level;
        private Camera view;
        private Vector2 exit;
        private SpriteRenderer stairs;
        private readonly int[,] distances = new int[DungeonMap.Width, DungeonMap.Height];
        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private Vector2Int lastPlayerCell = new Vector2Int(-1, -1);

        private void Start()
        {
            view = Camera.main;
            if (view == null) view = new GameObject("Dungeon Camera", typeof(Camera)).GetComponent<Camera>();
            view.orthographic = true;
            view.orthographicSize = 8;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.035f, 0.055f, 0.08f);
            gameObject.AddComponent<DungeonHud>().Run = this;
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
            Player = DungeonVisuals.Create(SelectedCharacter.DisplayName, transform, Vector2.zero, Vector2.one * 0.65f,
                SelectedCharacter.Color, 4).gameObject.AddComponent<DungeonPlayer>();
            Player.Run = this;
            Player.Initialize(SelectedCharacter);
            NextFloor();
        }

        private void NextFloor()
        {
            upgradeChoices.Clear();
            Player.Powerups.BeginFloor();
            Player.Weapon?.Hide();
            if (level != null) { level.gameObject.SetActive(false); Destroy(level.gameObject); }
            Enemies.Clear();
            Floor++;
            Map = new DungeonMap(Seed + Floor * 7919);
            level = new GameObject("Floor " + Floor).transform;
            level.SetParent(transform);
            DungeonVisuals.DrawMap(Map, level);
            Player.transform.position = (Vector2)Map.Centers[0];
            exit = Map.Centers[Map.Centers.Count - 1];
            stairs = DungeonVisuals.Create("Stairs", level, exit, Vector2.one * 0.85f, Color.gray, 1);
            for (int room = 1; room < Map.Centers.Count; room++)
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
            if (!IsPlaying) return;
            UpdatePaths();
            stairs.color = Enemies.Count == 0 ? new Color(1f, 0.8f, 0.25f) : new Color(0.4f, 0.4f, 0.4f);
            if (Enemies.Count == 0 && Vector2.Distance(Player.transform.position, exit) < 1.2f && PlayerInput.Interact)
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

        public static int EnemyHealthForFloor(int floor) => 2 + Mathf.Max(0, floor - 3);
        public void EndRun() { IsPlaying = false; Player.Weapon?.Hide(); }
        public void BeginUpgradeChoice()
        {
            if (!IsPlaying || Enemies.Count != 0) return;
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

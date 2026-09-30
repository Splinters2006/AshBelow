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
        /// <summary>True on the world-cleared screen after a world's third guardian: go on to the next world or leave.</summary>
        public bool WorldComplete { get; private set; }
        /// <summary>True when another world follows the current one.</summary>
        public bool HasNextWorld => WorldCatalog.HasWorldAfter(Floor);
        /// <summary>The world a new descent begins in, picked on the main menu's travel map (nothing is locked yet).</summary>
        public int StartWorld { get; private set; }
        public bool IsBossFloor => Floor > 0 && Floor % 5 == 0;
        public DungeonBoss Boss { get; private set; }
        public ArtifactPickup Artifact { get; private set; }
        /// <summary>
        /// True in the crystal shop between the floor before a boss and the boss arena. The shop keeps the floor
        /// number of the floor before it, so the guardian's floor still comes next.
        /// </summary>
        public bool InShop { get; private set; }
        public CrystalShop Shop { get; private set; }
        public Vector2 Exit => exit;
        public string Objective => InShop ? ShopObjective
            : Artifact != null ? "Claim the glowing artifact  /  " + KeyBindings.Label(GameAction.Interact)
            : IsBossFloor && Enemies.Count > 0 ? "Defeat the arena guardian"
            : IsWaveFloor ? (Enemies.Count == 0 ? "Level cleared" : waveReserves.Count > 0 ? $"Survive the waves  /  {waveReserves.Count} more to come" : "Survive the final wave")
            : Enemies.Count == 0 ? "Find the gold stairs  /  " + KeyBindings.Label(GameAction.Interact) : "Clear the floor to unlock the stairs";
        private string ShopObjective => Shop != null && Shop.IsNear(Player) ? "Trade crystals with the merchant  /  " + KeyBindings.Label(GameAction.Interact)
            : IsNetworked ? "Spend crystals, then gather the party at the stairs to face the guardian"
            : "Spend crystals at the merchant, then take the stairs to the guardian";
        private readonly List<PowerupDefinition> upgradeChoices = new List<PowerupDefinition>();
        public IReadOnlyList<PowerupDefinition> UpgradeChoices => upgradeChoices;
        public int Floor { get; private set; }
        /// <summary>The world this floor belongs to: 15 floors each (the Ash Below for floors 1-15, then the Neon Arcology, ...).</summary>
        public WorldDefinition World => WorldCatalog.ForFloor(Floor);
        /// <summary>Until when the HUD announces the world just entered.</summary>
        public float WorldBannerUntil { get; private set; }
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
        public FloorMinimap Minimap { get; private set; }
        public bool IsPointerOverHud => hud != null && hud.BlocksPointer(PlayerInput.CursorPosition);
        private Transform level;
        private Camera view;
        private Vector2 exit;
        private StairVisual stairs;
        private bool floorRewardGranted;
        /// <summary>
        /// Guardians beaten in this descent. The Ash shop's record counts these rather than the floor number, so a descent
        /// begun or continued in a later world through the travel map does not count guardians it never fought.
        /// </summary>
        private int guardiansThisRun;
        /// <summary>Set by a travel-map jump so the next floor shows the world banner.</summary>
        private bool arrivingByTravel;
        private float nextSaveRetry;
        public PermanentProgress Progress { get; private set; }
        public int RunAshEarned { get; private set; }
        public CoopSync Coop { get; private set; }
        /// <summary>True during a co-op descent; the local hero is still <see cref="Player"/>.</summary>
        public bool IsNetworked => Coop != null && Coop.Active;
        /// <summary>True on a co-op guest, whose enemies are driven by the host.</summary>
        public bool IsGuest => IsNetworked && !Coop.IsHost;
        public int PartySize { get; private set; } = 1;
        public int EnemyHealthScaled(int health) => ScaleHealth(health, PartySize);
        /// <summary>Each extra hero adds half of an enemy's base health.</summary>
        public static int ScaleHealth(int health, int partySize) => Mathf.CeilToInt(health * (1f + 0.5f * (Mathf.Max(1, partySize) - 1)));
        private readonly int[,] distances = new int[DungeonMap.Width, DungeonMap.Height];
        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private readonly List<Vector2Int> heroCells = new List<Vector2Int>(), lastHeroCells = new List<Vector2Int>();

        private void Start()
        {
            string saveDirectory = Application.persistentDataPath;
#if UNITY_EDITOR
            // Automated tests must never read or change the player's real wallet.
            if (Application.isBatchMode || UnityEditor.SessionState.GetBool("SlopgamePreview", false)
                || UnityEditor.SessionState.GetBool("AdminPreview", false))
                saveDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ashbelow-tests", System.Guid.NewGuid().ToString("N"));
#endif
#if DEBUG || UNITY_EDITOR
            // Scripted co-op smoke runs must never touch a real wallet either.
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-coopSmoke") >= 0)
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
            Minimap = gameObject.AddComponent<FloorMinimap>();
            Minimap.Run = this;
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
            Coop = gameObject.AddComponent<CoopSync>();
            Coop.Run = this;
            ShowMainMenu();
        }

        public void SelectCharacter(CharacterDefinition character)
        {
            if (IsInMainMenu && System.Array.IndexOf(characters, character) >= 0) SelectedCharacter = character;
        }

        /// <summary>Main menu travel map: the world the next descent (solo, or a co-op party this player hosts) begins in.</summary>
        public void SelectStartWorld(int index)
        {
            if (IsInMainMenu) StartWorld = Mathf.Clamp(index, 0, WorldCatalog.All.Length - 1);
        }

        /// <summary>Returns to the main menu, leaving any co-op party (after a disconnect the menu explains why).</summary>
        public void ShowMainMenu() => ShowMainMenu(false);

        public void ShowMainMenu(bool disconnected)
        {
            if (Coop != null && Coop.Session.State != NetState.Offline && !disconnected) Coop.Session.Leave();
            ClearRun();
            menu.ResetPage(disconnected);
        }

        /// <summary>Back to the co-op party screen while staying connected.</summary>
        public void ShowCoopLobby()
        {
            ClearRun();
            menu.ShowCoop();
        }

        private void ClearRun()
        {
            IsPlaying = false;
            ChoosingUpgrade = false;
            ChoosingArtifact = false;
            WorldComplete = false;
            Boss = null;
            Artifact = null;
            InShop = false;
            Shop = null;
            Time.timeScale = 1f;
            ScreenFx.Clear();
            IsInMainMenu = true;
            if (level != null) { level.gameObject.SetActive(false); Destroy(level.gameObject); level = null; }
            if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); Player = null; }
            Enemies.Clear();
            waveReserves.Clear();
            WavesThisLevel = 0;
            PartySize = 1;
        }

        public void Restart()
        {
            if (IsNetworked) { Coop.HostBeginRun(); return; }
            BeginRun(UnityEngine.Random.Range(0, 1000000), 1, StartWorld);
        }

        /// <summary>Starts a co-op descent; every machine calls this with the same seed, party size and starting world.</summary>
        public void StartCoopRun(CharacterDefinition character, int seed, int partySize, int startWorld = 0)
        {
            SelectedCharacter = character;
            BeginRun(seed, partySize, startWorld);
        }

        private void BeginRun(int seed, int partySize, int startWorld)
        {
            if (SelectedCharacter == null) return;
            IsInMainMenu = false;
            if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); }
            Seed = seed;
            PartySize = Mathf.Max(1, partySize);
            // NextFloor below steps onto the world's first floor (floor 1 for the Ash Below).
            Floor = WorldCatalog.FirstFloor(startWorld) - 1;
            InShop = false;
            WorldBannerUntil = 0f;
            Kills = 0;
            RunAshEarned = 0;
            guardiansThisRun = 0;
            Player = DungeonVisuals.Create(SelectedCharacter.DisplayName, transform, Vector2.zero, Vector2.one * 0.65f,
                SelectedCharacter.Color, 4).gameObject.AddComponent<DungeonPlayer>();
            Player.Run = this;
            Player.Initialize(SelectedCharacter);
            NextFloor();
        }

        /// <summary>Co-op: the host decided everyone descends now.</summary>
        public void AdvanceCoopFloor(int partySize)
        {
            PartySize = Mathf.Max(1, partySize);
            NextFloor();
        }

        private void NextFloor()
        {
            Time.timeScale = 1f;
            ScreenFx.Clear();
            Boss = null;
            Artifact = null;
            Shop = null;
            ChoosingArtifact = false;
            WorldComplete = false;
            upgradeChoices.Clear();
            Player.Powerups.BeginFloor();
            Player.Weapon?.Hide();
            // A wave floor's crystals and coins still on the ground go straight into the hero's pouch and purse.
            if (level != null && IsWaveFloor) BankDrops();
            if (level != null) { level.gameObject.SetActive(false); Destroy(level.gameObject); }
            Enemies.Clear();
            // The floor before every boss leads into the crystal shop, and the shop's stairs lead to the boss.
            InShop = !InShop && IsShopNext(Floor);
            if (!InShop) Floor++;
            floorRewardGranted = false;
            // Wave worlds fight every wave in the open arena the guardians use.
            Map = InShop ? DungeonMap.Shop() : new DungeonMap(Seed + Floor * 7919, IsBossFloor || IsWaveFloor);
            waveClearedAt = -1f;
            waveReserves.Clear();
            WavesThisLevel = 0;
            WaveBannerUntil = 0f;
            level = new GameObject(InShop ? "Crystal shop" : "Floor " + Floor).transform;
            level.SetParent(transform);
            view.backgroundColor = World.Background;
            DungeonVisuals.DrawMap(Map, level, World, InShop);
            Player.Crystals.BeginFloor(Floor, InShop);
            Player.transform.position = (Vector2)Map.Centers[0];
            exit = Map.Centers[Map.Centers.Count - 1];
            stairs = StairVisual.Create(level, exit);
            if (InShop) Shop = CrystalShop.Create(this, level);
            if (IsBossFloor)
            {
                var enemy = DungeonVisuals.Create("Guardian", level, exit, Vector2.one * 1.4f,
                    new Color(0.65f, 0.3f, 0.55f), 4).gameObject.AddComponent<DungeonEnemy>();
                Boss = enemy.gameObject.AddComponent<DungeonBoss>();
                Boss.Initialize(this);
                Enemies.Add(enemy);
                DungeonVisuals.DecorateArena(level, World);
            }
            // Past the third guardian the stairs lead down into the next world.
            // Travelling on the map announces the world too, even the first one.
            bool travelled = arrivingByTravel;
            arrivingByTravel = false;
            if (!InShop && (WorldCatalog.EntersWorld(Floor) || travelled))
            {
                ScreenFx.Flash(FlameMesh.Alpha(World.Accent, 0.6f), 1.2f);
                WorldBannerUntil = Time.time + 4f;
                HeroVfx.Pulse(level, Player.transform.position, 3f, World.Accent, 0.9f);
            }
            // Seeded variants: skitters from floor 2, husks and world specialists from floor 3.
            var variants = new System.Random(Seed + Floor * 6151);
            if (IsWaveFloor)
            {
                DungeonVisuals.DecorateArena(level, World);
                SpawnWave(variants);
            }
            for (int room = 1; !IsBossFloor && !InShop && !IsWaveFloor && room < Map.Centers.Count; room++)
            {
                int count = Mathf.Min(4, 1 + Floor);
                for (int i = 0; i < count; i++)
                    // Introduce one specialist in the first combat room, then seed additional ones.
                    SpawnEnemy((Vector2)Map.Centers[room] + new Vector2(i % 2, i / 2), i == 1, i == 0 && room % 2 == 0, room == 1 && i == 2,
                        variants, EnemyHealthScaled(EnemyHealthForFloor(Floor)));
            }
            lastHeroCells.Clear();
            ChoosingUpgrade = false;
            IsPlaying = true;
            if (IsNetworked)
            {
                var all = new List<DungeonEnemy>(Enemies);
                foreach (var wave in waveReserves) all.AddRange(wave);
                Coop.RegisterFloor(all);
            }
            view.transform.position = new Vector3(Player.transform.position.x, Player.transform.position.y, -10);
            UpdatePaths();
        }

        /// <summary>
        /// One regular enemy: a caster, an armoured brute, or a basic enemy that may roll a variant (skitter, husk or the
        /// world's specialist; <paramref name="guaranteeSpecialist"/> forces the specialist from floor 3).
        /// </summary>
        private DungeonEnemy SpawnEnemy(Vector2 position, bool shooter, bool tank, bool guaranteeSpecialist, System.Random variants, int health,
            List<DungeonEnemy> into = null)
        {
            var enemy = DungeonVisuals.Create(World.BasicName, level, position, Vector2.one * 0.6f,
                World.BasicTint, 3).gameObject.AddComponent<DungeonEnemy>();
            enemy.Run = this;
            enemy.Health = health;
            enemy.Speed = Mathf.Min(4.3f, 2.25f + Floor * 0.15f);
            if (shooter) enemy.gameObject.AddComponent<EnemyShooter>();
            else if (tank)
            {
                enemy.IsTank = true;
                enemy.name = World.BruteName;
                enemy.Health *= 3;
                enemy.Speed *= 0.6f;
                enemy.transform.localScale = Vector2.one * 0.9f;
            }
            else
            {
                double roll = variants.NextDouble();
                bool specialist = Floor >= 3 && (guaranteeSpecialist || roll >= HuskChance + SkitterChance && roll < HuskChance + SkitterChance + SpecialistChance);
                EnemyVariant variant = specialist
                    ? (World.HighTech ? (EnemyVariant)enemy.gameObject.AddComponent<NeonLancer>() : enemy.gameObject.AddComponent<EmberFanatic>())
                    : Floor >= 3 && roll < HuskChance ? enemy.gameObject.AddComponent<CinderHusk>()
                    : Floor >= 2 && roll < HuskChance + SkitterChance ? enemy.gameObject.AddComponent<AshSkitter>() : null;
                variant?.Configure(enemy);
            }
            // A later wave's enemies go to its reserve instead of into the fight.
            (into ?? Enemies).Add(enemy);
            return enemy;
        }

        // ---------------------------------------------------------------- wave worlds

        /// <summary>True on a wave world's regular levels: waves of enemies in the arena instead of rooms to explore.</summary>
        public bool IsWaveFloor => World.IsWaveWorld && !IsBossFloor && !InShop && Floor > 0;
        /// <summary>The level's number within its world, 1-15 (guardians on levels 5, 10 and 15).</summary>
        public int LevelNumber => (Floor - 1) % WorldCatalog.FloorsPerWorld + 1;
        /// <summary>How many waves this level sends (0 away from wave levels).</summary>
        public int WavesThisLevel { get; private set; }
        /// <summary>The wave now being fought, 1 to <see cref="WavesThisLevel"/>.</summary>
        public int CurrentWave => WavesThisLevel - waveReserves.Count;
        /// <summary>Enemies still waiting in the level's later waves.</summary>
        public int WaveReserveCount { get { int count = 0; foreach (var wave in waveReserves) count += wave.Count; return count; } }
        /// <summary>Until when the HUD announces the wave that just arrived.</summary>
        public float WaveBannerUntil { get; private set; }
        /// <summary>Seconds between the last kill of a level and the talent pick opening.</summary>
        public const float WaveBreather = 1.25f;
        /// <summary>Enemies spawn between these distances from where the heroes start a wave: never on top of them, and
        /// always inside the range enemies give chase from (14 units).</summary>
        public const float WaveSpawnClearance = 6f, WaveSpawnReach = 13f;
        /// <summary>After a wave, how long the talent pick waits at most for the pulled-in crystals and coins to arrive.</summary>
        public const float WaveDropWait = 2.5f;
        /// <summary>
        /// The most enemies in one wave. With the next wave arriving while a quarter of this one still stands, at most
        /// 45 enemies are in play, which keeps the co-op enemy snapshot inside one unfragmented packet.
        /// </summary>
        public const int MaxWaveEnemies = 36;
        private float waveClearedAt = -1f;
        /// <summary>The next wave arrives once the fight is down to this many enemies.</summary>
        private int waveReleaseAt;
        /// <summary>The level's later waves, built with the level and held out of play until their turn.</summary>
        private readonly List<List<DungeonEnemy>> waveReserves = new List<List<DungeonEnemy>>();

        /// <summary>Waves per level: 3, and 4 from the world's level 8.</summary>
        public static int WavesForLevel(int level) => level >= 8 ? 4 : 3;

        /// <summary>
        /// A wave's size: 6 + the level number, times the party's multiplier. Difficulty follows the party through the
        /// number of enemies (their health is not party-scaled), so it grows or shrinks as heroes join or leave between levels.
        /// </summary>
        public static int WaveEnemyCount(int level, int partySize) => Mathf.Min(MaxWaveEnemies, Mathf.RoundToInt((6 + level) * WavePartyMultiplier(partySize)));

        /// <summary>A wave arrives once the fight is down to a quarter of a wave's size (at least 2 enemies).</summary>
        public static int WaveReleaseThreshold(int waveSize) => Mathf.Max(2, waveSize / 4);

        /// <summary>More heroes fight better together than alone, so enemies grow slower than the party: 1x, 1.6x, 2.1x, 2.5x.</summary>
        public static float WavePartyMultiplier(int partySize) => partySize <= 1 ? 1f : partySize == 2 ? 1.6f : partySize == 3 ? 2.1f : 2.5f;

        /// <summary>
        /// Builds the level's waves around the arena, away from the heroes. The first fights at once; the later ones are
        /// built now too (seeded, so every co-op machine numbers the same enemies) and wait out of play for their turn.
        /// </summary>
        private void SpawnWave(System.Random variants)
        {
            var spots = new System.Random(Seed + Floor * 7717);
            Vector2 start = Map.Centers[0];
            int count = WaveEnemyCount(LevelNumber, PartySize);
            int health = EnemyHealthForFloor(Floor);
            WavesThisLevel = WavesForLevel(LevelNumber);
            waveReleaseAt = WaveReleaseThreshold(count);
            for (int wave = 0; wave < WavesThisLevel; wave++)
            {
                var reserve = wave == 0 ? null : new List<DungeonEnemy>();
                // Every fourth a caster (1, 5, 9, ...), every sixth a brute (3, 9 is a caster, 15, ...), and one specialist.
                for (int i = 0; i < count; i++)
                {
                    var enemy = SpawnEnemy(WaveSpawnPoint(spots, start), i % 4 == 1, i % 6 == 3, wave == 0 && i == 2, variants, health, reserve);
                    if (reserve != null) enemy.gameObject.SetActive(false);
                }
                if (reserve != null) waveReserves.Add(reserve);
            }
        }

        /// <summary>
        /// Called on every kill: once the fight is down to the threshold, the next wave arrives around the heroes. Kills
        /// reach every co-op machine in the same order, so each one releases the same wave at the same kill; the host's
        /// enemy snapshots then settle any difference in where the newcomers stand.
        /// </summary>
        private void ReleaseNextWave()
        {
            if (waveReserves.Count == 0 || Enemies.Count > waveReleaseAt) return;
            var wave = waveReserves[0];
            waveReserves.RemoveAt(0);
            var spots = new System.Random(Seed + Floor * 7717 + CurrentWave * 131);
            foreach (var enemy in wave)
            {
                if (enemy == null) continue;
                enemy.transform.position = ReinforcementPoint(spots);
                enemy.gameObject.SetActive(true);
                Enemies.Add(enemy);
                HeroVfx.Pulse(level, enemy.transform.position, 0.7f, World.Accent, 0.35f);
            }
            WaveBannerUntil = Time.time + 2f;
        }

        /// <summary>Where a later wave's enemy arrives: in the arena, not on top of any hero, but within chase range of one.</summary>
        private Vector2 ReinforcementPoint(System.Random spots)
        {
            var arena = DungeonMap.Arena;
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var point = new Vector2(arena.xMin + 1 + spots.Next(arena.width - 2), arena.yMin + 1 + spots.Next(arena.height - 2));
                float distance = Vector2.Distance(point, NearestHero(point));
                if (distance >= WaveSpawnClearance && distance <= WaveSpawnReach && Map.CanStand(point, 0.45f)) return point;
            }
            return exit;
        }

        private Vector2 WaveSpawnPoint(System.Random spots, Vector2 start)
        {
            var arena = DungeonMap.Arena;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var point = new Vector2(arena.xMin + 1 + spots.Next(arena.width - 2), arena.yMin + 1 + spots.Next(arena.height - 2));
                float distance = Vector2.Distance(point, start);
                if (distance >= WaveSpawnClearance && distance <= WaveSpawnReach && Map.CanStand(point, 0.45f)) return point;
            }
            return exit;
        }

        /// <summary>
        /// A cleared wave pulls every crystal and coin in the arena to the hero, then opens the talent pick by itself after a
        /// short breather, once the drops have arrived or <see cref="WaveDropWait"/> has passed (in co-op the host opens it).
        /// </summary>
        private bool UpdateWave()
        {
            if (!IsWaveFloor || Enemies.Count > 0 || Artifact != null) return false;
            if (waveClearedAt < 0f)
            {
                waveClearedAt = Time.time;
                foreach (var crystal in level.GetComponentsInChildren<Crystal>()) crystal.PullToHero();
                foreach (var coin in level.GetComponentsInChildren<GoldCoin>()) coin.PullToHero();
                return false;
            }
            float waited = Time.time - waveClearedAt;
            if (waited < WaveBreather || (IsNetworked && !Coop.IsHost)) return false;
            bool dropsOut = level.GetComponentsInChildren<Crystal>().Length > 0
                || (Player.Weapon is GamblerAttack && level.GetComponentsInChildren<GoldCoin>().Length > 0);
            if (dropsOut && Player.Health > 0 && waited < WaveDropWait) return false;
            waveClearedAt = float.MaxValue;
            if (IsNetworked) Coop.RequestInteract(CoopChoice.Upgrade);
            else BeginUpgradeChoice();
            return true;
        }

        /// <summary>Banks every crystal and coin left on the floor (drops can land far from the hero in a wave arena).</summary>
        private void BankDrops()
        {
            foreach (var crystal in level.GetComponentsInChildren<Crystal>()) crystal.CollectNow();
            if (Player.Weapon is GamblerAttack)
                foreach (var coin in level.GetComponentsInChildren<GoldCoin>()) coin.CollectNow();
        }

        /// <summary>True when the floor after <paramref name="floor"/> is a boss floor, so the crystal shop comes first.</summary>
        public static bool IsShopNext(int floor) => floor > 0 && (floor + 1) % 5 == 0;
        /// <summary>Variant chances for eligible basic enemies; the first combat room also guarantees a specialist from floor 3.</summary>
        public const double HuskChance = 0.2, SkitterChance = 0.3, SpecialistChance = 0.18;

        private void Update()
        {
            if (PlayerInput.DebugToggle) DebugMode.Toggle();
            if (Progress.HasUnsavedChanges && Time.unscaledTime >= nextSaveRetry)
            { Progress.Save(); nextSaveRetry = Time.unscaledTime + 5f; }
            if (!IsPlaying) return;
            UpdatePaths();
            if (UpdateWave()) return;
            bool canInteract = Player.Health > 0 && PlayerInput.Interact;
            if (Artifact != null && canInteract && Vector2.Distance(Player.transform.position, Artifact.transform.position) < 1.5f)
            {
                if (IsNetworked) Coop.RequestInteract(CoopChoice.Artifact);
                else BeginArtifactChoice();
                return;
            }
            if (Shop != null && canInteract && Shop.IsNear(Player))
            {
                Shop.Toggle();
                return;
            }
            stairs.SetUnlocked(Enemies.Count == 0 && Artifact == null);
            if (Artifact == null && Enemies.Count == 0 && canInteract && Vector2.Distance(Player.transform.position, exit) < 1.2f)
            {
                if (IsNetworked) Coop.RequestInteract(CoopChoice.Upgrade);
                else BeginUpgradeChoice();
            }
        }

        private void LateUpdate()
        {
            if (Player == null) return;
            // A fallen co-op hero watches a living teammate until the next floor.
            Transform follow = Player.Health <= 0 && IsNetworked && Coop.SpectateTarget != null ? Coop.SpectateTarget.transform : Player.transform;
            var target = new Vector3(follow.position.x, follow.position.y, -10);
            view.transform.position = Vector3.Lerp(view.transform.position - (Vector3)cameraShake, target, 1 - Mathf.Exp(-10 * Time.deltaTime));
            cameraShake = ScreenFx.Offset;
            view.transform.position += (Vector3)cameraShake;
        }

        private Vector2 cameraShake;

        /// <summary>Solo debug mode only: abandons the current floor (boss arenas included) and builds the next one.</summary>
        public bool CanSkipRoom => DebugMode.Enabled && !IsNetworked && IsPlaying;

        public void DebugSkipRoom()
        {
            if (!CanSkipRoom) return;
            Player.Charge.Cancel();
            NextFloor();
        }

        public void EnemyDefeated(DungeonEnemy enemy)
        {
            if (!Enemies.Remove(enemy)) return;
            Kills++;
            // Wave levels: the next wave arrives before the fight runs dry, so the level is only clear after its last wave.
            ReleaseNextWave();
            int reward = enemy.Boss != null ? 50 : 1;
            if (enemy.Boss != null) Progress.RecordGuardian(++guardiansThisRun, Player != null ? Player.ClassWeapon : (WeaponType?)null);
            if (Enemies.Count == 0 && !floorRewardGranted)
            { reward += 10; floorRewardGranted = true; }
            RunAshEarned += reward;
            Progress.AwardAsh(reward);
        }

        public bool TryBuyUpgrade(string id) => IsInMainMenu && Progress.TryPurchase(id);
        private void OnApplicationQuit() { Progress?.Save(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Progress?.Save(); }

        public static int EnemyHealthForFloor(int floor) => 2 + Mathf.Max(0, floor - 3);
        public void EndRun()
        {
            IsPlaying = false;
            ChoosingUpgrade = false;
            ChoosingArtifact = false;
            WorldComplete = false;
            Player.Weapon?.Hide();
            if (!IsNetworked) Time.timeScale = 0f;
        }

        /// <summary>Co-op: the local hero fell but teammates fight on; they revive on the next floor.</summary>
        public void LocalHeroDied()
        {
            Player.Weapon?.Hide();
            Player.Charge.Cancel();
            Coop.LocalDied();
        }

        /// <summary>A taunting Knight counts as this many times closer when enemies pick a target.</summary>
        public const float AggroPull = 3f;

        /// <summary>Squared distance for targeting, shrunk for a hero drawing aggro with Shield Taunt.</summary>
        private static float TargetDistance(Vector2 hero, Vector2 from, bool aggro)
            => Vector2.SqrMagnitude(hero - from) / (aggro ? AggroPull * AggroPull : 1f);

        /// <summary>The living heroes enemies can target: the local hero plus teammates.</summary>
        public Vector2 NearestHero(Vector2 from)
        {
            Vector2 best = Player.transform.position;
            float bestDistance = Player.Health > 0 ? TargetDistance(best, from, Player.DrawsAggro) : float.PositiveInfinity;
            if (IsNetworked)
                foreach (var hero in Coop.RemoteHeroes)
                {
                    if (hero == null || !hero.IsAlive) continue;
                    float distance = TargetDistance(hero.transform.position, from, hero.IsTaunting);
                    if (distance < bestDistance) { bestDistance = distance; best = hero.transform.position; }
                }
            return best;
        }

        /// <summary>
        /// The nearest hero enemies can see: heroes in Shadow Veil are skipped. False when every living hero is hidden,
        /// in which case enemies should neither chase nor turn.
        /// </summary>
        public bool TryNearestVisibleHero(Vector2 from, out Vector2 best)
        {
            best = from;
            float bestDistance = float.PositiveInfinity;
            if (Player.Health > 0 && !Player.IsVeiled)
            {
                best = Player.transform.position;
                bestDistance = TargetDistance(best, from, Player.DrawsAggro);
            }
            if (IsNetworked)
                foreach (var hero in Coop.RemoteHeroes)
                {
                    if (hero == null || !hero.IsAlive || hero.IsVeiled) continue;
                    float distance = TargetDistance(hero.transform.position, from, hero.IsTaunting);
                    if (distance < bestDistance) { bestDistance = distance; best = hero.transform.position; }
                }
            return !float.IsPositiveInfinity(bestDistance);
        }
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
            if (!IsNetworked) Time.timeScale = 0f;
        }
        public bool ChooseArtifact(AbilityType type, int slot)
        {
            if (!ChoosingArtifact || (IsNetworked && Coop.WaitingForTeam) || !Player.Abilities.Claim(type, slot)) return false;
            Player.Heal(2);
            FinishArtifactChoice();
            return true;
        }
        /// <summary>Crystals the merchant's rivals pay for an artifact left unclaimed.</summary>
        public const int LeftArtifactCrystals = 40;

        /// <summary>Leaves the guardian's artifact behind for a pile of crystals instead of an ability.</summary>
        public bool LeaveArtifact()
        {
            if (!ChoosingArtifact || (IsNetworked && Coop.WaitingForTeam)) return false;
            Player.Crystals.Add(LeftArtifactCrystals);
            if (ProjectileRoot != null)
                HeroVfx.Motes(ProjectileRoot, Player.transform.position, 0.9f, CrystalPouch.CrystalColor, 20, 1.1f);
            FinishArtifactChoice();
            return true;
        }

        public void FinishArtifactChoice()
        {
            if (!ChoosingArtifact) return;
            if (IsNetworked)
            {
                // Every teammate claims their own relic; play resumes once all have chosen.
                if (!Coop.WaitingForTeam) Coop.FinishChoice();
                return;
            }
            CloseCoopArtifact();
        }

        /// <summary>Removes the claimed artifact and resumes play.</summary>
        public void CloseCoopArtifact()
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
            // The third guardian's stairs end the world: a cleared screen offers the next world or the menu.
            if (IsBossFloor && WorldCatalog.CompletesWorld(Floor)) { ShowWorldComplete(); return; }
            if (IsBossFloor || InShop) { NextFloor(); return; }
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
            if (!IsNetworked) Time.timeScale = 0f;
        }

        /// <summary>
        /// Opens the world-cleared screen. Callers check the floor is clear first; in co-op the host calls it for the party,
        /// so a guest applies it as soon as the host's message arrives.
        /// </summary>
        public void ShowWorldComplete()
        {
            if (WorldComplete || IsInMainMenu || Player == null) return;
            IsPlaying = false;
            ChoosingUpgrade = false;
            WorldComplete = true;
            Player.Weapon?.Hide();
            Player.Charge.Cancel();
            if (!IsNetworked) Time.timeScale = 0f;
        }

        /// <summary>
        /// From the world-cleared screen, go on to the next world; past the last world the descent carries on endlessly
        /// (in co-op only the host decides).
        /// </summary>
        public void ContinueFromWorldComplete()
        {
            if (!WorldComplete) return;
            if (HasNextWorld) { TravelToWorld(World.Index + 1); return; }
            if (IsNetworked) { if (Coop.IsHost) Coop.HostContinueFromWorld(); return; }
            NextFloor();
        }

        /// <summary>From the world-cleared screen's travel map, go to any world (nothing is locked yet; co-op: the host decides).</summary>
        public void TravelToWorld(int index)
        {
            if (!WorldComplete || index < 0 || index >= WorldCatalog.All.Length) return;
            if (IsNetworked) { if (Coop.IsHost) Coop.HostTravel(index); return; }
            JumpToWorld(index, PartySize);
        }

        /// <summary>Builds the first floor of world <paramref name="index"/>; in co-op every machine calls this together.</summary>
        public void JumpToWorld(int index, int partySize)
        {
            if (IsInMainMenu || Player == null) return;
            PartySize = Mathf.Max(1, partySize);
            InShop = false;
            Floor = WorldCatalog.FirstFloor(index) - 1;
            arrivingByTravel = true;
            NextFloor();
        }

        public void ChooseUpgrade(int choice)
        {
            if (!ChoosingUpgrade || choice < 0 || choice >= upgradeChoices.Count) return;
            if (IsNetworked)
            {
                if (Coop.WaitingForTeam) return;
                Player.Upgrade((int)upgradeChoices[choice].Type);
                Coop.FinishChoice();
                return;
            }
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

        /// <summary>A flow field from every living hero, so each enemy paths toward whoever is closest.</summary>
        private void UpdatePaths()
        {
            heroCells.Clear();
            // While a Knight taunts, every enemy's path leads to him.
            bool taunted = Player.Health > 0 && Player.DrawsAggro;
            if (IsNetworked)
                foreach (var hero in Coop.RemoteHeroes)
                    taunted |= hero != null && hero.IsAlive && hero.IsTaunting;
            if (Player.Health > 0 && (!taunted || Player.DrawsAggro)) AddHeroCell(Player.transform.position);
            if (IsNetworked)
                foreach (var hero in Coop.RemoteHeroes)
                    if (hero != null && hero.IsAlive && (!taunted || hero.IsTaunting)) AddHeroCell(hero.transform.position);
            if (heroCells.Count == 0) AddHeroCell(Player.transform.position);
            if (heroCells.Count == lastHeroCells.Count && heroCells.TrueForAll(lastHeroCells.Contains)) return;
            lastHeroCells.Clear();
            lastHeroCells.AddRange(heroCells);
            BuildFlowField(Map, heroCells, distances);
        }

        private void AddHeroCell(Vector2 position)
        {
            var cell = Vector2Int.RoundToInt(position);
            if (Map.IsFloor(cell.x, cell.y) && !heroCells.Contains(cell)) heroCells.Add(cell);
        }

        /// <summary>Breadth-first distances (in cells) from the nearest source cell; walls stay at int.MaxValue.</summary>
        public static void BuildFlowField(DungeonMap map, IReadOnlyList<Vector2Int> sources, int[,] distances)
        {
            for (int x = 0; x < DungeonMap.Width; x++)
                for (int y = 0; y < DungeonMap.Height; y++) distances[x, y] = int.MaxValue;
            var queue = new Queue<Vector2Int>();
            foreach (var cell in sources)
            {
                if (distances[cell.x, cell.y] == 0) continue;
                distances[cell.x, cell.y] = 0;
                queue.Enqueue(cell);
            }
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (!map.IsFloor(next.x, next.y) || distances[next.x, next.y] != int.MaxValue) continue;
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

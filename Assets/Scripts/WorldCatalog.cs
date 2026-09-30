using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// One world of the descent: how its floors, walls and arena look and what its enemies are called and look like.
    /// Every world has three guardians; beating the third opens the world-cleared screen and its travel map.
    /// </summary>
    public sealed class WorldDefinition
    {
        public int Index { get; }
        public string Name { get; }
        /// <summary>True for the sci-fi world: robots, plasma and neon instead of ash and embers.</summary>
        public bool HighTech { get; }
        public Color Background { get; }
        public Color FloorA { get; }
        public Color FloorB { get; }
        public Color Wall { get; }
        /// <summary>Wall trim, floor lights and arena inlay.</summary>
        public Color Accent { get; }
        public Color ArenaRune { get; }
        public string BasicName { get; }
        public string CasterName { get; }
        public string BruteName { get; }
        public Color BasicTint { get; }
        public Color CasterTint { get; }
        public Color BruteTint { get; }
        /// <summary>What the world's casters fire.</summary>
        public BoltKind Bolts { get; }
        /// <summary>The hero this world belongs to (it will unlock them later); null for the starting world.</summary>
        public WeaponType? Hero { get; }
        /// <summary>True while the world reuses the Ash Below's basic enemies and guardians under its own names and colours (its specialists are its own, see <see cref="WorldBestiary"/>).</summary>
        public bool IsPlaceholder { get; }
        /// <summary>Where the world sits on the travel map, 0-1 across and down the map. Tweak the layout here.</summary>
        public Vector2 MapPosition { get; }
        /// <summary>How the world's floors are carved: dungeon rooms, city blocks, caverns, ...</summary>
        public MapLayout Layout { get; }
        /// <summary>The environmental traps planted on the world's combat floors.</summary>
        public TrapTheme[] Traps { get; }
        /// <summary>True when the world's combat floors carry lava pools that burn heroes (the Infernal Court).</summary>
        public bool HasLava { get; }
        /// <summary>
        /// What the world's reused guardians strike with (the archdemon's bolts follow <see cref="Bolts"/>): only the
        /// Ash Below and the Infernal Court use hellfire.
        /// </summary>
        public HazardStyle Element { get; }
        /// <summary>The first of the world's three guardians; worlds without their own reuse the Ash Below's.</summary>
        public BossKind FirstGuardian { get; }
        /// <summary>
        /// True for a wave world: its 15 levels are fought in the arena as waves of enemies, 3 or 4 per level (guardians
        /// still on levels 5, 10 and 15), with the wave size following the party size.
        /// </summary>
        public bool IsWaveWorld { get; }
        /// <summary>
        /// How much deadlier the world is than normal (1 = normal). Enemy health, speed, attack pace, numbers and
        /// specialists, and guardian health all scale with it; see the multipliers below.
        /// </summary>
        public float Threat { get; }
        public float EnemyHealthMultiplier => Threat;
        public float EnemySpeedMultiplier => 1f + (Threat - 1f) * 0.2f;
        /// <summary>How much faster every enemy's attack clock runs here (guardians included).</summary>
        public float EnemyTempo => 1f + (Threat - 1f) * 0.25f;
        public float GuardianHealthMultiplier => 1f + (Threat - 1f) * 0.6f;
        /// <summary>Extra enemies in every combat room.</summary>
        public int ExtraEnemiesPerRoom => Threat >= 1.5f ? 1 : 0;

        public WorldDefinition(int index, string name, bool highTech, Color background, Color floorA, Color floorB, Color wall, Color accent,
            Color arenaRune, string basicName, string casterName, string bruteName, Color basicTint, Color casterTint, Color bruteTint, BoltKind bolts,
            WeaponType? hero = null, bool placeholder = false, Vector2 mapPosition = default, MapLayout layout = MapLayout.Dungeon, TrapTheme[] traps = null, bool lava = false, HazardStyle element = HazardStyle.Hellfire,
            BossKind firstGuardian = BossKind.AshWarden, bool waves = false, float threat = 1f)
        {
            Index = index; Name = name; HighTech = highTech; Background = background; FloorA = floorA; FloorB = floorB; Wall = wall; Accent = accent;
            ArenaRune = arenaRune; BasicName = basicName; CasterName = casterName; BruteName = bruteName;
            BasicTint = basicTint; CasterTint = casterTint; BruteTint = bruteTint; Bolts = bolts;
            Hero = hero; IsPlaceholder = placeholder; MapPosition = mapPosition; Layout = layout; Traps = traps ?? new TrapTheme[0]; HasLava = lava; Element = element; FirstGuardian = firstGuardian; IsWaveWorld = waves;
            Threat = Mathf.Max(1f, threat);
        }
    }

    public static class WorldCatalog
    {
        /// <summary>Floors per world: three guardians, on floors 5, 10 and 15 of each.</summary>
        public const int FloorsPerWorld = 15;
        public static readonly Color Neon = new Color(0.25f, 0.95f, 1f);
        public static readonly Color NeonPink = new Color(1f, 0.25f, 0.8f);
        /// <summary>World 3 is much deadlier than the rest: 60% more enemy health, faster enemies that attack sooner, an extra enemy per room, more specialists and tougher guardians.</summary>
        public const float InfernalCourtThreat = 1.6f;

        public static readonly WorldDefinition[] All =
        {
            new WorldDefinition(0, "THE ASH BELOW", false, new Color(0.035f, 0.055f, 0.08f),
                new Color(0.12f, 0.17f, 0.21f), new Color(0.14f, 0.19f, 0.23f), new Color(0.29f, 0.38f, 0.43f),
                new Color(0.56f, 0.38f, 0.22f), new Color(0.55f, 0.3f, 0.4f),
                "Ashling", "Ember caster", "Iron brute",
                new Color(1f, 0.35f, 0.4f), new Color(1f, 0.65f, 0.2f), new Color(0.65f, 0.7f, 0.8f), BoltKind.Ember,
                null, false, new Vector2(0.04f, 0.72f), MapLayout.Dungeon, new[]
                {
                    new TrapTheme("Fire vent", TrapKind.Vent, HazardStyle.Hellfire, 1.1f),
                    new TrapTheme("Spike trap", TrapKind.Vent, HazardStyle.Spikes, 0.9f),
                }),
            // The Augment's world, fought in waves.
            new WorldDefinition(1, "THE NEON ARCOLOGY", true, new Color(0.015f, 0.01f, 0.05f),
                new Color(0.07f, 0.08f, 0.16f), new Color(0.09f, 0.1f, 0.19f), new Color(0.2f, 0.22f, 0.32f),
                Neon, NeonPink,
                "Drone", "Laser drone", "Heavy mech",
                new Color(0.55f, 0.95f, 1f), new Color(1f, 0.4f, 0.85f), new Color(0.72f, 0.76f, 0.9f), BoltKind.Plasma,
                WeaponType.Beam, false, new Vector2(0.22f, 0.25f), MapLayout.CityBlocks, new[]
                {
                    new TrapTheme("Plasma laser", TrapKind.Jet, HazardStyle.Plasma, 9f),
                    new TrapTheme("Arc floor panel", TrapKind.Vent, HazardStyle.Circuit, 1.2f),
                }, element: HazardStyle.Plasma, firstGuardian: BossKind.GridOverseer, waves: true),
            // Placeholder hero worlds: their own names, colours and specialists, with the Ash Below's basic enemies and guardians until each is designed.
            new WorldDefinition(2, "THE INFERNAL COURT", false, new Color(0.06f, 0.015f, 0.03f),
                new Color(0.16f, 0.06f, 0.08f), new Color(0.19f, 0.07f, 0.1f), new Color(0.36f, 0.12f, 0.2f),
                new Color(0.85f, 0.25f, 0.55f), new Color(0.6f, 0.2f, 0.7f),
                "Imp", "Hex witch", "Hellhound",
                new Color(1f, 0.3f, 0.3f), new Color(0.8f, 0.35f, 1f), new Color(0.6f, 0.25f, 0.3f), BoltKind.Ember,
                WeaponType.Tail, true, new Vector2(0.41f, 0.7f), MapLayout.Caverns, new[]
                {
                    new TrapTheme("Brimstone geyser", TrapKind.Vent, HazardStyle.Hellfire, 1.5f),
                    new TrapTheme("Fire jet", TrapKind.Jet, HazardStyle.Hellfire, 5f),
                }, lava: true, firstGuardian: BossKind.HexMatriarch, threat: InfernalCourtThreat),
            new WorldDefinition(3, "THE ARCANE SPIRE", false, new Color(0.03f, 0.03f, 0.08f),
                new Color(0.1f, 0.1f, 0.2f), new Color(0.12f, 0.12f, 0.23f), new Color(0.26f, 0.26f, 0.45f),
                new Color(0.55f, 0.6f, 1f), new Color(0.4f, 0.85f, 1f),
                "Wisp", "Apprentice", "Arcane golem",
                new Color(0.6f, 0.8f, 1f), new Color(0.75f, 0.55f, 1f), new Color(0.6f, 0.6f, 0.75f), BoltKind.Frost,
                WeaponType.Staff, true, new Vector2(0.6f, 0.22f), MapLayout.Chambers, new[]
                {
                    new TrapTheme("Frost rune", TrapKind.Rune, HazardStyle.Frost, 2.8f),
                    new TrapTheme("Arcane ray", TrapKind.Jet, HazardStyle.Void, 8f),
                }, element: HazardStyle.Frost, waves: true),
            new WorldDefinition(4, "THE SHADOW MARKET", false, new Color(0.02f, 0.04f, 0.035f),
                new Color(0.09f, 0.12f, 0.11f), new Color(0.11f, 0.14f, 0.13f), new Color(0.22f, 0.28f, 0.26f),
                new Color(0.45f, 0.95f, 0.7f), new Color(0.3f, 0.7f, 0.5f),
                "Cutpurse", "Knife thrower", "Enforcer",
                new Color(0.7f, 0.9f, 0.75f), new Color(0.5f, 1f, 0.7f), new Color(0.55f, 0.65f, 0.6f), BoltKind.Blade,
                WeaponType.Daggers, true, new Vector2(0.78f, 0.68f), MapLayout.Alleys, new[]
                {
                    new TrapTheme("Spike plate", TrapKind.Vent, HazardStyle.Spikes, 0.9f),
                }, element: HazardStyle.Steel),
            new WorldDefinition(5, "THE SAVAGE WILDS", false, new Color(0.03f, 0.05f, 0.025f),
                new Color(0.12f, 0.17f, 0.09f), new Color(0.14f, 0.19f, 0.1f), new Color(0.3f, 0.36f, 0.2f),
                new Color(0.95f, 0.6f, 0.25f), new Color(0.6f, 0.85f, 0.35f),
                "Wild boar", "Spear hunter", "Ape brute",
                new Color(0.85f, 0.6f, 0.45f), new Color(0.7f, 0.9f, 0.4f), new Color(0.55f, 0.45f, 0.35f), BoltKind.Venom,
                WeaponType.Fists, true, new Vector2(0.96f, 0.26f), MapLayout.Clearings, new[]
                {
                    new TrapTheme("Spore pod", TrapKind.Vent, HazardStyle.Venom, 1.4f),
                }, element: HazardStyle.Venom),
        };

        /// <summary>The world a floor belongs to; floors past the last world stay in it.</summary>
        public static int IndexForFloor(int floor) => Mathf.Clamp((floor - 1) / FloorsPerWorld, 0, All.Length - 1);
        public static WorldDefinition ForFloor(int floor) => All[IndexForFloor(floor)];
        /// <summary>True on the first floor of any world after the first, where the hero arrives in a new world.</summary>
        public static bool EntersWorld(int floor) => floor > 1 && (floor - 1) % FloorsPerWorld == 0 && (floor - 1) / FloorsPerWorld < All.Length;
        /// <summary>True on a world's third guardian floor (15, 30, ...): beating it clears that world. Endless floors past the last world never do.</summary>
        public static bool CompletesWorld(int floor) => floor > 0 && floor % FloorsPerWorld == 0 && floor / FloorsPerWorld <= All.Length;
        /// <summary>True when another world follows the one <paramref name="floor"/> belongs to.</summary>
        public static bool HasWorldAfter(int floor) => IndexForFloor(floor) + 1 < All.Length;
        /// <summary>The first floor of a world: 1, 16, 31, ...</summary>
        public static int FirstFloor(int index) => Mathf.Clamp(index, 0, All.Length - 1) * FloorsPerWorld + 1;
    }
}

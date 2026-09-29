using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// One world of the descent: how its floors, walls and arena look and what its enemies are called and look like.
    /// Every world has three guardians; beating the third leads down into the next world.
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

        public WorldDefinition(int index, string name, bool highTech, Color background, Color floorA, Color floorB, Color wall, Color accent,
            Color arenaRune, string basicName, string casterName, string bruteName, Color basicTint, Color casterTint, Color bruteTint, BoltKind bolts)
        {
            Index = index; Name = name; HighTech = highTech; Background = background; FloorA = floorA; FloorB = floorB; Wall = wall; Accent = accent;
            ArenaRune = arenaRune; BasicName = basicName; CasterName = casterName; BruteName = bruteName;
            BasicTint = basicTint; CasterTint = casterTint; BruteTint = bruteTint; Bolts = bolts;
        }
    }

    public static class WorldCatalog
    {
        /// <summary>Floors per world: three guardians, on floors 5, 10 and 15 of each.</summary>
        public const int FloorsPerWorld = 15;
        public static readonly Color Neon = new Color(0.25f, 0.95f, 1f);
        public static readonly Color NeonPink = new Color(1f, 0.25f, 0.8f);

        public static readonly WorldDefinition[] All =
        {
            new WorldDefinition(0, "THE ASH BELOW", false, new Color(0.035f, 0.055f, 0.08f),
                new Color(0.12f, 0.17f, 0.21f), new Color(0.14f, 0.19f, 0.23f), new Color(0.29f, 0.38f, 0.43f),
                new Color(0.56f, 0.38f, 0.22f), new Color(0.55f, 0.3f, 0.4f),
                "Ashling", "Ember caster", "Iron brute",
                new Color(1f, 0.35f, 0.4f), new Color(1f, 0.65f, 0.2f), new Color(0.65f, 0.7f, 0.8f), BoltKind.Ember),
            new WorldDefinition(1, "THE NEON ARCOLOGY", true, new Color(0.015f, 0.01f, 0.05f),
                new Color(0.07f, 0.08f, 0.16f), new Color(0.09f, 0.1f, 0.19f), new Color(0.2f, 0.22f, 0.32f),
                Neon, NeonPink,
                "Drone", "Laser drone", "Heavy mech",
                new Color(0.55f, 0.95f, 1f), new Color(1f, 0.4f, 0.85f), new Color(0.72f, 0.76f, 0.9f), BoltKind.Plasma),
        };

        /// <summary>The world a floor belongs to; floors past the last world stay in it.</summary>
        public static int IndexForFloor(int floor) => Mathf.Clamp((floor - 1) / FloorsPerWorld, 0, All.Length - 1);
        public static WorldDefinition ForFloor(int floor) => All[IndexForFloor(floor)];
        /// <summary>True on the first floor of any world after the first, where the hero arrives in a new world.</summary>
        public static bool EntersWorld(int floor) => floor > 1 && (floor - 1) % FloorsPerWorld == 0 && (floor - 1) / FloorsPerWorld < All.Length;
    }
}

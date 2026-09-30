using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>Where a trap sits and how it strikes.</summary>
    public enum TrapKind : byte
    {
        /// <summary>A floor vent that erupts in a burning patch.</summary>
        Vent,
        /// <summary>A wall emitter that fires a line across the room.</summary>
        Jet,
        /// <summary>A floor rune that pulses an expanding ring.</summary>
        Rune
    }

    /// <summary>One kind of trap a world plants on its floors, e.g. the Arcology's plasma lasers.</summary>
    public readonly struct TrapTheme
    {
        public readonly string Name;
        public readonly TrapKind Kind;
        public readonly HazardStyle Style;
        /// <summary>Vent radius, the longest jet, or a rune's final ring radius.</summary>
        public readonly float Size;

        public TrapTheme(string name, TrapKind kind, HazardStyle style, float size)
        {
            Name = name; Kind = kind; Style = style; Size = size;
        }
    }

    /// <summary>
    /// A themed environmental trap on a combat floor. It sits dormant, then on a steady cycle fires a telegraphed
    /// <see cref="HellfireZone"/> strike. Traps are seeded from the floor, so every co-op machine plants the same ones;
    /// like boss hazards, each machine runs its own copy and judges only its own hero. Traps hurt enemies too (applied
    /// by the host in co-op), so heroes can lure them in.
    /// </summary>
    public sealed class EnvironmentHazard : MonoBehaviour
    {
        public const float Telegraph = 0.9f, WakeDistance = 14f, StartClearance = 6f;
        public const int MaxTraps = 6;
        private static readonly List<EnvironmentHazard> active = new List<EnvironmentHazard>();
        public static IReadOnlyList<EnvironmentHazard> Active => active;
        private static readonly Vector2Int[] Sides = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        private DungeonRun run;
        private HazardSpec spec;
        private float period, clock;
        private SpriteRenderer glow;
        private Color glowColor;
        public TrapTheme Theme { get; private set; }

        /// <summary>Traps per floor: none on the very first floor, then more the deeper into a world the hero gets.</summary>
        public static int CountForFloor(int floor)
        {
            if (floor <= 1) return 0;
            int floorInWorld = (floor - 1) % WorldCatalog.FloorsPerWorld + 1;
            return Mathf.Min(MaxTraps, 2 + floorInWorld / 3);
        }

        public static void Plant(DungeonRun run, Transform parent, DungeonMap map, WorldDefinition world, int floor, Vector2 exit, int seed)
        {
            int wanted = CountForFloor(floor);
            if (wanted == 0 || world.Traps == null || world.Traps.Length == 0) return;
            var random = new System.Random(seed);
            var taken = new List<Vector2>();
            for (int attempt = 0; attempt < 1500 && taken.Count < wanted; attempt++)
            {
                // Themes take turns, so wall jets are as common as vents even in open layouts.
                var theme = world.Traps[taken.Count % world.Traps.Length];
                var cell = new Vector2Int(random.Next(2, DungeonMap.Width - 2), random.Next(2, DungeonMap.Height - 2));
                if (!map.IsFloor(cell.x, cell.y) || Vector2.Distance(cell, map.Centers[0]) < StartClearance
                    || Vector2.Distance(cell, exit) < 2.5f || taken.Exists(other => Vector2.Distance(other, cell) < 4f)) continue;
                var trap = theme.Kind == TrapKind.Jet ? TryJet(map, cell, theme, random) : TryFloorTrap(map, cell, theme);
                if (!trap.HasValue) continue;
                // Jets must not rake across the hero's arrival point.
                if (theme.Kind == TrapKind.Jet && DistanceToSegment(map.Centers[0], trap.Value.Center, trap.Value.Direction, trap.Value.Radius) < 3f) continue;
                taken.Add(cell);
                Create(run, parent, world, theme, trap.Value, 3.4f + (float)random.NextDouble() * 1.6f, (float)random.NextDouble());
            }
        }

        /// <summary>Vents and runes want open ground: every cell around them walkable.</summary>
        private static HazardSpec? TryFloorTrap(DungeonMap map, Vector2Int cell, TrapTheme theme)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (!map.IsFloor(cell.x + dx, cell.y + dy) || map.IsLava(cell.x + dx, cell.y + dy)) return null;
            bool rune = theme.Kind == TrapKind.Rune;
            // Spikes stab and sink back at once; other vents burn a while.
            return new HazardSpec
            {
                Shape = rune ? HazardShape.Ring : HazardShape.Pool, Style = theme.Style, Center = cell, Direction = Vector2.right,
                Radius = theme.Size, Width = rune ? 0.7f : 0f, Telegraph = Telegraph,
                Duration = rune ? 1f : theme.Style == HazardStyle.Spikes ? 0.5f : 1.3f
            };
        }

        /// <summary>A jet hangs on a wall and fires straight out across the floor until the far wall.</summary>
        private static HazardSpec? TryJet(DungeonMap map, Vector2Int cell, TrapTheme theme, System.Random random)
        {
            var walls = new List<Vector2Int>();
            foreach (var candidate in Sides) if (!map.IsFloor(cell.x + candidate.x, cell.y + candidate.y)) walls.Add(candidate);
            if (walls.Count == 0) return null;
            var side = walls[random.Next(walls.Count)];
            var direction = -side;
            int length = 0;
            while (length < theme.Size && map.IsFloor(cell.x + direction.x * (length + 1), cell.y + direction.y * (length + 1))) length++;
            if (length < 3) return null;
            return new HazardSpec
            {
                Shape = HazardShape.Beam, Style = theme.Style, Center = cell - (Vector2)direction * 0.45f, Direction = direction,
                Radius = length + 0.9f, Width = 0.75f, Telegraph = Telegraph, Duration = 0.7f
            };
        }

        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 direction, float length)
        {
            float along = Mathf.Clamp(Vector2.Dot(point - from, direction), 0f, length);
            return Vector2.Distance(point, from + direction * along);
        }

        public static EnvironmentHazard Create(DungeonRun run, Transform parent, WorldDefinition world, TrapTheme theme, HazardSpec spec,
            float period, float phase)
        {
            bool jet = theme.Kind == TrapKind.Jet;
            // The dormant fixture: a wall emitter, a floor grate or a faint rune.
            var fixture = DungeonVisuals.Create(theme.Name, parent, spec.Center,
                jet ? (Mathf.Abs(spec.Direction.x) > 0.5f ? new Vector2(0.3f, 0.7f) : new Vector2(0.7f, 0.3f)) : Vector2.one * (theme.Kind == TrapKind.Rune ? 0.55f : 0.8f),
                jet ? Color.Lerp(world.Wall, Color.black, 0.3f) : FlameMesh.Alpha(Color.Lerp(world.FloorA, Color.black, 0.45f), 0.9f), 1);
            if (theme.Kind == TrapKind.Rune) fixture.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            var hazard = fixture.gameObject.AddComponent<EnvironmentHazard>();
            hazard.run = run;
            hazard.spec = spec;
            hazard.Theme = theme;
            hazard.period = Mathf.Max(spec.Telegraph + spec.Duration + 1f, period);
            hazard.clock = phase * hazard.period;
            hazard.glowColor = HazardColor(theme.Style);
            hazard.glow = DungeonVisuals.Create(theme.Name + " light", parent, spec.Center, Vector2.one * 0.24f,
                FlameMesh.Alpha(hazard.glowColor, 0.3f), 2);
            return hazard;
        }

        public static Color HazardColor(HazardStyle style) => style switch
        {
            HazardStyle.Plasma => WorldCatalog.NeonPink,
            HazardStyle.Circuit => WorldCatalog.Neon,
            HazardStyle.Frost => AbilityCatalog.Ice,
            HazardStyle.Steel => new Color(0.6f, 0.95f, 1f),
            HazardStyle.Venom => new Color(0.55f, 1f, 0.3f),
            HazardStyle.Spikes => new Color(0.85f, 0.88f, 0.92f),
            HazardStyle.Void => new Color(0.75f, 0.35f, 1f),
            _ => FlameMesh.Orange
        };

        /// <summary>Sets off the trap now: its telegraph starts, then it strikes heroes and enemies alike.</summary>
        public HellfireZone Fire()
        {
            var zone = HellfireZone.Spawn(run, spec, false);
            if (zone != null) zone.EnemyDamage = EnemyDamageFor(run);
            return zone;
        }

        /// <summary>A strike takes half a basic enemy's health on this floor, so luring two strikes onto one finishes it.</summary>
        public static int EnemyDamageFor(DungeonRun run) =>
            Mathf.Max(1, Mathf.CeilToInt(run.EnemyHealthScaled(DungeonRun.EnemyHealthForFloor(run.Floor)) * 0.5f));

        private void Update()
        {
            if (run == null || !run.IsPlaying || run.Player == null) return;
            clock += Time.deltaTime;
            // The fixture warms up in the last second before each strike.
            float untilFire = period - clock;
            float warm = Mathf.Clamp01(1f - untilFire);
            glow.color = FlameMesh.Alpha(glowColor, 0.25f + 0.75f * warm * (0.6f + 0.4f * Mathf.Sin(Time.time * 20f)));
            if (clock < period) return;
            clock -= period;
            // Traps far off screen stay quiet rather than filling the floor with unseen effects.
            if (Vector2.Distance(run.Player.transform.position, spec.Center) <= WakeDistance) Fire();
        }

        private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        private void OnDisable() => active.Remove(this);
    }
}

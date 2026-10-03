using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// An urn (or, in high-tech worlds, a supply crate) standing in a room. A hero attack smashes it wherever that attack
    /// actually reaches (each weapon tests its own cone, lane or blast here), as do projectiles and dodge rolls. Most hold a crystal or two and a rare few hold a heart. Pots and their loot are seeded from the floor,
    /// so every co-op machine sees the same ones, and they are shared: an urn one hero smashes breaks for the whole
    /// party, and so does the loot it spills (see <see cref="ISharedPickup"/>).
    /// </summary>
    public sealed class Breakable : MonoBehaviour
    {
        public const float HitRadius = 0.4f;
        /// <summary>How far a basic or heavy attack chips Ice Wall blocks.</summary>
        public const float SwingReach = 1.7f, HeavyReach = 2.3f;
        public const double CrystalChance = 0.55, HeartChance = 0.07;
        public const int PerRoomMin = 1, PerRoomMax = 3;
        private static readonly List<Breakable> active = new List<Breakable>();
        public static IReadOnlyList<Breakable> Active => active;
        private DungeonRun run;
        public DungeonRun Run => run;
        /// <summary>The urn's number on its floor, the same on every co-op machine; -1 for one only this machine has.</summary>
        public int NetId { get; private set; } = -1;
        private Color shardColor;
        public int Crystals { get; private set; }
        public bool HoldsHeart { get; private set; }

        /// <summary>Places urns against the walls of each combat room, away from enemy spawns and the stairs.</summary>
        public static void Scatter(DungeonRun run, Transform parent, DungeonMap map, WorldDefinition world, Vector2Int exit, int seed)
        {
            var random = new System.Random(seed);
            var taken = new List<Vector2Int>();
            for (int room = 1; room < map.Centers.Count; room++)
            {
                var center = map.Centers[room];
                int wanted = random.Next(PerRoomMin, PerRoomMax + 1);
                for (int attempt = 0; attempt < 40 && wanted > 0; attempt++)
                {
                    var cell = center + new Vector2Int(random.Next(-5, 6), random.Next(-5, 6));
                    if (!map.IsFloor(cell.x, cell.y) || map.IsLava(cell.x, cell.y) || !HugsWall(map, cell)
                        || cell.x >= center.x - 1 && cell.x <= center.x + 2 && cell.y >= center.y - 1 && cell.y <= center.y + 2
                        || Vector2Int.Distance(cell, exit) < 2f || taken.Exists(other => Vector2Int.Distance(other, cell) < 1.5f)) continue;
                    taken.Add(cell);
                    wanted--;
                    double roll = random.NextDouble();
                    int crystals = roll < CrystalChance ? 1 + random.Next(2) : 0;
                    bool heart = random.NextDouble() < HeartChance;
                    Create(run, parent, cell, world, crystals, heart).NetId = taken.Count - 1;
                }
            }
        }

        private static bool HugsWall(DungeonMap map, Vector2Int cell) =>
            !map.IsFloor(cell.x + 1, cell.y) || !map.IsFloor(cell.x - 1, cell.y) || !map.IsFloor(cell.x, cell.y + 1) || !map.IsFloor(cell.x, cell.y - 1);

        public static Breakable Create(DungeonRun run, Transform parent, Vector2 position, WorldDefinition world, int crystals, bool heart)
        {
            var tint = Color.Lerp(world.Wall, world.Accent, 0.35f) * 1.6f;
            tint.a = 1f;
            var sprite = DungeonVisuals.Create(world.HighTech ? "Supply crate" : "Urn", parent, position, Vector2.one * 0.62f, tint, 2);
            sprite.sprite = world.HighTech ? DungeonVisuals.CrateSprite : DungeonVisuals.UrnSprite;
            var shadow = DungeonVisuals.Create("Urn shadow", sprite.transform, position + Vector2.down * 0.28f, new Vector2(0.6f, 0.3f), Color.white, 1);
            shadow.sprite = DungeonVisuals.CoinShadow;
            var breakable = sprite.gameObject.AddComponent<Breakable>();
            breakable.run = run;
            breakable.shardColor = tint;
            breakable.Crystals = crystals;
            breakable.HoldsHeart = heart;
            return breakable;
        }

        /// <summary>
        /// Smashes every urn a cone-shaped attack reaches: any part of it inside the cone of <paramref name="reach"/> and
        /// <paramref name="cone"/> degrees from <paramref name="origin"/>, with no wall in between (the enemy test of
        /// <see cref="SwordAttack.OverlapsCone"/>).
        /// </summary>
        public static void SmashInCone(DungeonRun run, Vector2 origin, Vector2 aim, float reach, float cone)
        {
            if (run == null || aim.sqrMagnitude < 0.0001f) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var target = active[i];
                if (target == null || target.run != run) continue;
                Vector2 at = target.transform.position;
                if (SwordAttack.OverlapsCone(at - origin, aim, reach, cone, HitRadius) && run.HasLineOfSight(origin, at)) target.Smash(origin);
            }
        }

        /// <summary>
        /// Smashes every urn a straight attack (a punch, thrust or ray) reaches: any part of it inside the
        /// <paramref name="length"/> by 2 x <paramref name="halfWidth"/> lane from <paramref name="origin"/>, with no wall in between.
        /// </summary>
        public static void SmashInLane(DungeonRun run, Vector2 origin, Vector2 aim, float length, float halfWidth)
        {
            if (run == null || aim.sqrMagnitude < 0.0001f) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var target = active[i];
                if (target == null || target.run != run) continue;
                Vector2 at = target.transform.position;
                if (BrawlerAttack.InRectangle(at - origin, aim, length, halfWidth, HitRadius) && run.HasLineOfSight(origin, at)) target.Smash(origin);
            }
        }

        /// <summary>Smashes every urn a projectile, rolling hero or blast touches at <paramref name="point"/>.</summary>
        public static bool SmashAt(DungeonRun run, Vector2 point, float radius)
        {
            bool smashed = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var target = active[i];
                if (target == null || target.run != run || Vector2.Distance(point, target.transform.position) > radius + HitRadius) continue;
                target.Smash(point);
                smashed = true;
            }
            return smashed;
        }

        /// <param name="remote">True when a co-op teammate smashed it on their machine and this one is following suit.</param>
        public void Smash(Vector2 source, bool remote = false)
        {
            if (!active.Remove(this)) return;
            bool shared = NetId >= 0 && run != null && run.IsNetworked;
            if (shared && !remote) run.Coop.ReportSmash(NetId, source);
            Vector2 position = transform.position;
            var root = run != null && run.ProjectileRoot != null ? run.ProjectileRoot : transform.parent;
            HeroVfx.Sparks(root, position, shardColor, 12, 4f, 0.35f, position - source, 200f, 1.3f);
            HeroVfx.Pulse(root, position, 0.5f, new Color(1f, 1f, 1f, 0.5f), 0.18f);
            if (Crystals > 0) Crystal.Drop(run, position, Crystals, shared ? CoopSync.BreakableKey(NetId, false) : 0);
            if (HoldsHeart) HealthPickup.Drop(run, position, HealthPickup.HealAmount, shared ? CoopSync.BreakableKey(NetId, true) : 0);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        private void OnDisable() => active.Remove(this);
    }
}

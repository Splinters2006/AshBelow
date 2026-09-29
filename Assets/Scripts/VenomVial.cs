using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Venom Vial: a potion lobbed at the cursor that shatters into a toxic pool. Enemies standing in the pool are
    /// poisoned for as long as they stay (and a moment after). Teammates see a ghost that deals no damage.
    /// </summary>
    public sealed class VenomVial : MeshEffect
    {
        public const float Range = 5f, FlightTime = 0.4f, Radius = 1.6f, PoolDuration = 4f;
        private const float MinThrow = 1f, TickInterval = 0.25f, FadeTime = 0.4f;
        public static readonly Color Venom = new Color(0.45f, 0.95f, 0.3f);
        private static readonly Color Murk = new Color(0.12f, 0.3f, 0.08f);
        private DungeonPlayer player;
        private DungeonRun run;
        private Vector2 from, landing;
        private float radius, lifetime, nextTick;
        private int splashDamage, poisonDamage;
        private bool ghost, landed;
        public bool HasLanded => landed;
        public Vector2 Landing => landing;

        /// <summary>
        /// Where a vial thrown toward <paramref name="aim"/> lands: up to <see cref="Range"/> units away,
        /// stopping short of the first wall on the way.
        /// </summary>
        public static Vector2 FindLanding(DungeonMap map, Vector2 from, Vector2 aim, float distance)
        {
            if (aim.sqrMagnitude < 0.0001f) return from;
            aim.Normalize();
            distance = Mathf.Clamp(distance, MinThrow, Range);
            Vector2 landing = from;
            for (float travel = 0.1f; travel <= distance + 0.001f; travel += 0.1f)
            {
                Vector2 next = from + aim * Mathf.Min(travel, distance);
                if (!map.CanStand(next, 0.1f)) break;
                landing = next;
            }
            return landing;
        }

        public static VenomVial Throw(DungeonPlayer player, Vector2 landing, float radius, float duration, int splashDamage, int poisonDamage)
        {
            CoopFx.Venom(player.Run, player.transform.position, landing, radius, duration);
            var vial = Create(player.Run, player.transform.position, landing, radius, duration);
            if (vial == null) return null;
            vial.player = player;
            vial.splashDamage = splashDamage;
            vial.poisonDamage = poisonDamage;
            return vial;
        }

        public static VenomVial SpawnGhost(DungeonRun run, Vector2 from, Vector2 landing, float radius, float duration)
        {
            var vial = Create(run, from, landing, radius, duration);
            if (vial != null) vial.ghost = true;
            return vial;
        }

        private static VenomVial Create(DungeonRun run, Vector2 from, Vector2 landing, float radius, float duration)
        {
            var vial = Spawn<VenomVial>(run.ProjectileRoot, FlightTime + duration + FadeTime, 4);
            if (vial == null) return null;
            vial.run = run;
            vial.from = from;
            vial.landing = landing;
            vial.radius = radius;
            vial.lifetime = duration;
            vial.Redraw();
            return vial;
        }

        protected override void LateUpdate()
        {
            if (run == null || transform.parent != run.ProjectileRoot) { Destroy(gameObject); return; }
            if (!landed && Age + Time.deltaTime >= FlightTime) Shatter();
            else if (landed && !ghost && run.IsPlaying && Age < FlightTime + lifetime && Age >= nextTick) Poison();
            base.LateUpdate();
        }

        private void Shatter()
        {
            landed = true;
            nextTick = Age;
            var root = transform.parent;
            HeroVfx.Sparks(root, landing, Venom, 16, 4.5f, 0.4f, null, 360f, 0.9f);
            HeroVfx.Sparks(root, landing, new Color(0.85f, 1f, 0.9f), 8, 3.5f, 0.3f, Vector2.up, 160f, 0.6f);
            HeroVfx.Pulse(root, landing, radius, Venom, 0.35f);
            if (ghost || player == null) return;
            foreach (var enemy in run.Enemies.ToArray())
                if (InPool(enemy)) CombatDamage.Apply(player, enemy, splashDamage, DamageElement.Physical, landing, 0.3f);
        }

        /// <summary>Keeps everyone in the pool poisoned; it ticks once a second and lingers briefly after they leave.</summary>
        private void Poison()
        {
            nextTick = Age + TickInterval;
            foreach (var enemy in run.Enemies.ToArray())
                if (InPool(enemy)) enemy.Burn(2, poisonDamage, AbilityCatalog.Green);
        }

        private bool InPool(DungeonEnemy enemy) => enemy != null && enemy.Health > 0
            && Vector2.Distance(landing, enemy.transform.position) <= radius + enemy.HitRadius * 0.5f
            && run.HasLineOfSight(landing, enemy.transform.position);

        protected override void Draw(float t)
        {
            if (Age < FlightTime) { DrawFlight(Age / FlightTime); return; }
            float poolAge = Age - FlightTime;
            float grow = EaseOut(poolAge / 0.2f), fade = Mathf.Clamp01((FlightTime + lifetime + FadeTime - Age) / FadeTime);
            float r = radius * grow;
            Mesh.Disc(landing, r, FlameMesh.Alpha(Murk, 0.55f * fade), FlameMesh.Alpha(Venom, 0.3f * fade), 40);
            Mesh.Ring(landing, r, 0.08f, FlameMesh.Alpha(Venom, 0.75f * fade), 48);
            // Bubbles swell and pop across the pool.
            for (int i = 0; i < 12; i++)
            {
                float seed = FlameMesh.Hash(i, 8.3f), cycle = Mathf.Repeat(poolAge * (0.9f + seed) + seed, 1f);
                Vector2 p = landing + FlameMesh.Polar(seed * 40f, r * 0.8f * Mathf.Sqrt(FlameMesh.Hash(i, 2.2f)));
                float size = 0.04f + 0.1f * cycle;
                Mesh.Ring(p, size, 0.025f, FlameMesh.Alpha(Venom, fade * (1f - cycle * 0.7f)), 12);
            }
            // Toxic fumes drift up out of it.
            for (int i = 0; i < 8; i++)
            {
                float seed = FlameMesh.Hash(i, 4.9f), rise = Mathf.Repeat(poolAge * 0.6f + seed, 1f);
                Vector2 p = landing + new Vector2((seed - 0.5f) * r * 1.4f + Mathf.Sin(poolAge * 3f + i) * 0.1f, rise * 1.1f);
                Mesh.Disc(p, 0.12f + 0.15f * rise, FlameMesh.Alpha(Venom, 0.25f * fade * Mathf.Sin(rise * Mathf.PI)), FlameMesh.Alpha(Venom, 0f), 12);
            }
        }

        private void DrawFlight(float t)
        {
            Vector2 ground = Vector2.Lerp(from, landing, t);
            Vector2 p = ground + Vector2.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
            Mesh.Ellipse(ground + Vector2.down * 0.2f, 0.18f, 0.07f, FlameMesh.Alpha(Color.black, 0.35f), FlameMesh.Alpha(Color.black, 0f), 14);
            // The spinning bottle: a round flask with a neck and cork.
            float spin = Age * 16f;
            Vector2 up = FlameMesh.Polar(spin, 1f), side = Vector2.Perpendicular(up);
            Mesh.Disc(p, 0.17f, FlameMesh.Alpha(Venom, 1f), FlameMesh.Alpha(Murk, 1f), 16);
            Mesh.Quad(p + up * 0.12f - side * 0.05f, p + up * 0.12f + side * 0.05f, p + up * 0.3f + side * 0.05f, p + up * 0.3f - side * 0.05f,
                new Color(0.8f, 0.95f, 0.9f), new Color(0.8f, 0.95f, 0.9f), new Color(0.8f, 0.95f, 0.9f), new Color(0.8f, 0.95f, 0.9f));
            Mesh.Diamond(p + up * 0.33f, 0.05f, new Color(0.55f, 0.36f, 0.2f));
            Mesh.Diamond(p - side * 0.06f + up * 0.05f, 0.04f, FlameMesh.Alpha(Color.white, 0.8f));
        }
    }
}

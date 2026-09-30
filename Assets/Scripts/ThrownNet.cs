using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Net Shot: a weighted rope net flies from the Archer, spinning and spreading open as it goes. It drops on the first
    /// enemy it reaches (or at the end of its flight, or a wall) and every enemy under it is nicked and rooted. The net
    /// then lies over them until the hold wears off. Teammates see a ghost net fly and fall; only the caster's machine
    /// deals the damage and roots.
    /// </summary>
    public sealed class ThrownNet : MonoBehaviour
    {
        public const float Speed = 15f, StartRadius = 0.35f, OpenRadius = 1.5f, Spokes = 8;
        private static readonly Color PlainRope = new Color(0.8f, 0.72f, 0.5f), Knot = new Color(0.55f, 0.45f, 0.3f), Weight = new Color(0.3f, 0.3f, 0.34f);
        private DungeonRun run;
        private DungeonPlayer player;
        // FlameMesh draws in its owner's local space, so the owner stays at the origin and the net's position lives here.
        private Vector2 direction, position;
        private float range, travelled, hold, landedAt = -1f, spin;
        private int damage;
        private bool ghost;
        private FlameMesh mesh;
        /// <summary>The Elemental Quiver's element the net carries (a crit sets it off); its ropes glow in that colour.</summary>
        private DamageElement infusion;
        private Color rope, glow;

        public static void Fire(DungeonPlayer player, Vector2 aim, float range, float hold, int damage)
        {
            var infusion = ElementalQuiver.InfusionOf(player);
            var net = Create(player.Run, player.transform.position, aim, range, hold, infusion);
            net.player = player;
            net.damage = damage;
            CoopFx.Net(player.Run, player.transform.position, aim, range, hold, infusion);
        }

        public static void SpawnGhost(DungeonRun run, Vector2 origin, Vector2 aim, float range, float hold, DamageElement infusion)
            => Create(run, origin, aim, range, hold, infusion).ghost = true;

        private static ThrownNet Create(DungeonRun run, Vector2 origin, Vector2 aim, float range, float hold, DamageElement infusion)
        {
            var net = new GameObject("Thrown net").AddComponent<ThrownNet>();
            net.transform.SetParent(run.ProjectileRoot, false);
            net.position = origin;
            net.run = run;
            net.direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            net.range = range;
            net.hold = hold;
            net.mesh = new FlameMesh(net.gameObject, 6);
            net.infusion = infusion;
            net.glow = ElementalQuiver.ShotColor(infusion, PlainRope);
            net.rope = infusion != DamageElement.Physical ? Color.Lerp(PlainRope, net.glow, 0.55f) : PlainRope;
            HeroVfx.Sparks(run.ProjectileRoot, origin, net.rope, 6, 3f, 0.2f, net.direction, 60f, 0.7f);
            return net;
        }

        /// <summary>How far open the net is: it starts bunched and spreads over the first stretch of its flight.</summary>
        private float Radius => Mathf.Lerp(StartRadius, OpenRadius, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(travelled / (range * 0.7f))));

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            if (landedAt < 0f) Fly();
            else if (Time.time - landedAt >= hold + 0.3f) { Destroy(gameObject); return; }
            Draw();
        }

        private void Fly()
        {
            spin += Time.deltaTime * 540f;
            float distance = Mathf.Min(Speed * Time.deltaTime, range - travelled);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.1f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = position + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.1f)) { Land(); return; }
                position = next;
                travelled += distance / steps;
                // It drops as soon as its centre is over an enemy.
                foreach (var enemy in run.Enemies)
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(next, enemy.transform.position) <= Radius * 0.5f + enemy.HitRadius) { Land(); return; }
            }
            if (travelled >= range - 0.001f) Land();
        }

        private void Land()
        {
            landedAt = Time.time;
            // It flops fully open as it falls.
            travelled = range;
            Vector2 at = position;
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, at, OpenRadius, FlameMesh.Alpha(rope, 0.35f), 0.2f);
            HeroVfx.Sparks(root, at, rope, 10, 2.5f, 0.25f);
            if (ghost || player == null) return;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || Vector2.Distance(at, enemy.transform.position) > OpenRadius + enemy.HitRadius
                    || !run.HasLineOfSight(at, enemy.transform.position)) continue;
                CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, at, 0f, infusion);
                if (enemy.Health > 0) enemy.Root(hold);
            }
        }

        /// <summary>
        /// A round casting net: ropes from a centre knot out to lead weights, crossed by rings of rope, sagging
        /// between the spokes. It spins in flight and lies still once it has landed, fading as the hold runs out.
        /// </summary>
        private void Draw()
        {
            bool landed = landedAt >= 0f;
            float alpha = landed ? Mathf.Clamp01((hold + 0.3f - (Time.time - landedAt)) / 0.3f) : 1f;
            float radius = Radius;
            // Settling: the net bounces open slightly as it lands.
            if (landed) radius *= 1f + 0.08f * Mathf.Exp(-(Time.time - landedAt) * 12f) * Mathf.Sin((Time.time - landedAt) * 40f);
            Vector2 center = position;
            Color strand = FlameMesh.Alpha(rope, alpha), knot = FlameMesh.Alpha(Knot, alpha);
            float width = landed ? 0.035f : 0.045f;
            mesh.Begin();
            // A faint shadow under the net while it flies.
            if (!landed) mesh.Ellipse(center + Vector2.down * 0.25f, radius * 0.8f, radius * 0.35f, new Color(0f, 0f, 0f, 0.18f), new Color(0f, 0f, 0f, 0f), 16);
            var rim = new Vector2[(int)Spokes];
            for (int i = 0; i < Spokes; i++)
            {
                float angle = (spin + i * 360f / Spokes) * Mathf.Deg2Rad;
                rim[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Line(center, rim[i], width, strand);
            }
            // Rings of mesh that sag inward between the spokes, like rope pulled taut only at the knots.
            foreach (float ring in new[] { 0.45f, 0.8f, 1f })
                for (int i = 0; i < Spokes; i++)
                {
                    Vector2 a = Vector2.Lerp(center, rim[i], ring), b = Vector2.Lerp(center, rim[(i + 1) % rim.Length], ring);
                    Vector2 sag = Vector2.Lerp(a, b, 0.5f);
                    sag += (center - sag) * 0.12f;
                    Line(a, sag, width * 0.85f, strand);
                    Line(sag, b, width * 0.85f, strand);
                    mesh.Diamond(a, 0.03f, knot);
                }
            mesh.Disc(center, 0.07f, knot, knot, 8);
            if (infusion != DamageElement.Physical) mesh.Disc(center, radius * 1.1f, FlameMesh.Alpha(glow, 0.18f * alpha), FlameMesh.Alpha(glow, 0f), 24);
            foreach (var weight in rim) mesh.Disc(weight, 0.06f, FlameMesh.Alpha(Weight, alpha), FlameMesh.Alpha(Weight * 0.6f, alpha), 8);
            mesh.Commit();
        }

        private void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            float length = Vector2.Distance(from, to);
            if (length < 0.001f) return;
            mesh.Bar(from, (to - from) / length, length, width, color, color);
        }

        private void OnDestroy() => mesh?.Release();
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Grapple Arm: a three-pronged steel hook shoots out from the Augment's arm on a chain. It snags the first enemy
    /// it touches and reels it back to him (guardians are too heavy and only take the hit), or reaches the end of its
    /// chain (or a wall) and winds back empty. Teammates see a ghost hook fly and wind back.
    /// </summary>
    public sealed class GrappleHook : MonoBehaviour
    {
        public const float Speed = 20f, ReelSpeed = 16f, RetractSpeed = 26f, HookRadius = 0.2f;
        private static readonly Color Steel = new Color(0.7f, 0.75f, 0.85f), Dark = new Color(0.3f, 0.33f, 0.4f), Glow = new Color(0.35f, 1f, 0.78f);
        private enum State { Out, Reel, Retract }
        private DungeonRun run;
        private DungeonPlayer player;
        private Transform owner;
        private DungeonEnemy caught;
        private Vector2 direction, hook;
        private float range, travelled;
        private int damage;
        private bool ghost;
        private State state;
        private FlameMesh mesh;

        public static void Fire(DungeonPlayer player, Vector2 aim, float range, int damage)
        {
            var grapple = Create(player.Run, player.transform, aim, range);
            grapple.player = player;
            grapple.damage = damage;
            CoopFx.Grapple(player.Run, aim, range);
        }

        public static void SpawnGhost(DungeonRun run, Transform owner, Vector2 aim, float range) => Create(run, owner, aim, range).ghost = true;

        private static GrappleHook Create(DungeonRun run, Transform owner, Vector2 aim, float range)
        {
            // The owner stays at the origin: FlameMesh draws in its local space.
            var grapple = new GameObject("Grapple hook").AddComponent<GrappleHook>();
            grapple.transform.SetParent(run.ProjectileRoot, false);
            grapple.run = run;
            grapple.owner = owner;
            grapple.direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            grapple.hook = owner.position;
            grapple.range = range;
            grapple.mesh = new FlameMesh(grapple.gameObject, 7);
            HeroVfx.Sparks(run.ProjectileRoot, owner.position, Glow, 5, 3f, 0.2f, grapple.direction, 50f, 0.7f);
            return grapple;
        }

        private Vector2 Arm => owner != null ? (Vector2)owner.position + direction * 0.25f : hook;

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null || owner == null) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            switch (state)
            {
                case State.Out: FlyOut(); break;
                case State.Reel: Reel(); break;
                case State.Retract:
                    hook = Vector2.MoveTowards(hook, Arm, RetractSpeed * Time.deltaTime);
                    if (Vector2.Distance(hook, Arm) < 0.05f) { Destroy(gameObject); return; }
                    break;
            }
            Draw();
        }

        private void FlyOut()
        {
            float distance = Mathf.Min(Speed * Time.deltaTime, range - travelled);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.1f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = hook + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.05f))
                {
                    HeroVfx.Sparks(run.ProjectileRoot, hook, Steel, 6, 3f, 0.2f, -direction, 120f, 0.7f);
                    state = State.Retract;
                    return;
                }
                hook = next;
                travelled += distance / steps;
                foreach (var enemy in run.Enemies)
                {
                    if (enemy == null || enemy.Health <= 0 || Vector2.Distance(hook, enemy.transform.position) > enemy.HitRadius + HookRadius) continue;
                    Snag(enemy);
                    return;
                }
            }
            if (travelled >= range - 0.001f) state = State.Retract;
        }

        private void Snag(DungeonEnemy enemy)
        {
            HeroVfx.Pulse(run.ProjectileRoot, hook, 0.45f, Glow, 0.15f);
            HeroVfx.Sparks(run.ProjectileRoot, hook, Steel, 8, 3.5f, 0.2f, -direction, 140f, 0.8f);
            ScreenFx.Shake(0.05f, 0.08f);
            if (!ghost) CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, Arm, 0f);
            // Guardians are too heavy to haul; the hook glances off.
            if (ghost || enemy == null || enemy.Health <= 0 || enemy.Boss != null) { state = State.Retract; return; }
            caught = enemy;
            state = State.Reel;
        }

        /// <summary>Hauls the snagged enemy back to a step in front of the Augment, then stuns it briefly.</summary>
        private void Reel()
        {
            if (caught == null || caught.Health <= 0) { caught = null; state = State.Retract; return; }
            Vector2 goal = Arm + direction * 0.8f;
            Vector2 at = caught.transform.position;
            Vector2 next = Vector2.MoveTowards(at, goal, ReelSpeed * Time.deltaTime);
            caught.transform.position = run.Map.Move(at, next - at);
            hook = caught.transform.position;
            if (Vector2.Distance(hook, goal) > 0.1f && Vector2.Distance(at, hook) > 0.001f) return;
            caught.Stun(0.3f);
            caught = null;
            state = State.Retract;
        }

        /// <summary>The chain as alternating links from the arm to the hook, then the hook's prongs.</summary>
        private void Draw()
        {
            Vector2 arm = Arm, span = hook - arm;
            float length = span.magnitude;
            Vector2 dir = length > 0.001f ? span / length : direction, side = Vector2.Perpendicular(dir);
            mesh.Begin();
            int links = Mathf.FloorToInt(length / 0.16f);
            for (int i = 0; i < links; i++)
            {
                Vector2 at = arm + dir * (i + 0.5f) * 0.16f;
                if (i % 2 == 0) mesh.Ellipse(at, 0.09f, 0.045f, Steel, Dark, 8);
                else mesh.Bar(at - dir * 0.07f, dir, 0.14f, 0.03f, Dark, Steel);
            }
            // A faint energy line along the chain while it is taut.
            if (state != State.Retract && length > 0.1f) mesh.Bar(arm, dir, length, 0.02f, FlameMesh.Alpha(Glow, 0.5f), FlameMesh.Alpha(Glow, 0.1f));
            // Hook: a hub and three prongs curling back.
            Vector2 facing = state == State.Retract ? -dir : dir;
            if (state == State.Out) facing = direction;
            Vector2 across = Vector2.Perpendicular(facing);
            mesh.Disc(hook, 0.08f, Steel, Dark, 10);
            mesh.Triangle(hook + across * 0.05f, hook + facing * 0.22f, hook - across * 0.05f, Steel, Color.white, Steel);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 root = hook + facing * 0.05f + across * s * 0.05f;
                Vector2 elbow = root + (facing * 0.1f + across * s * 0.12f);
                Vector2 tip = elbow - facing * 0.08f + across * s * 0.02f;
                mesh.Bar(root, (elbow - root).normalized, Vector2.Distance(root, elbow), 0.04f, Steel, Steel);
                mesh.Bar(elbow, (tip - elbow).normalized, Vector2.Distance(elbow, tip), 0.035f, Steel, Color.white);
            }
            mesh.Commit();
        }

        private void OnDestroy() => mesh?.Release();
    }
}

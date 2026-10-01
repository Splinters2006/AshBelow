using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Edge's Hook (right click): the hooked blade on the end of his chain flies out, bites the first enemy it
    /// reaches and yanks it to his feet, stunned. Guardians and armoured brutes are too heavy to haul, so the chain
    /// pulls him to them instead. At his second stage it drags a second enemy along with the first. Teammates see a
    /// ghost hook fly out and back.
    /// </summary>
    public sealed class ChainHook : MonoBehaviour
    {
        public const float Speed = 22f, ReelSpeed = 17f, ZipSpeed = 18f, RetractSpeed = 28f, HookRadius = 0.22f, SecondCatchRadius = 1.6f;
        private enum State { Out, Reel, Zip, Retract }
        private SpecimenAttack owner;
        private DungeonPlayer player;
        private DungeonRun run;
        private readonly List<DungeonEnemy> caught = new List<DungeonEnemy>();
        private DungeonEnemy anchor;
        private Vector2 direction, hook;
        private float range, travelled, zipUntil;
        private int catches;
        private State state;
        private FlameMesh mesh;

        public static ChainHook Fire(SpecimenAttack owner, Vector2 aim, float range, int catches)
        {
            var player = owner.Player;
            var chain = new GameObject("Chain hook").AddComponent<ChainHook>();
            chain.transform.SetParent(player.Run.ProjectileRoot, false);
            chain.owner = owner;
            chain.player = player;
            chain.run = player.Run;
            chain.direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            chain.hook = player.transform.position;
            chain.range = range;
            chain.catches = Mathf.Max(1, catches);
            chain.mesh = new FlameMesh(chain.gameObject, 7);
            CoopFx.Grapple(player.Run, chain.direction, range);
            HeroVfx.Sparks(player.Run.ProjectileRoot, player.transform.position, SpecimenCatalog.Keen, 5, 3f, 0.2f, chain.direction, 50f, 0.7f);
            return chain;
        }

        private Vector2 Hand => player != null ? (Vector2)player.transform.position + direction * 0.3f : hook;

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null || player == null) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            switch (state)
            {
                case State.Out: FlyOut(); break;
                case State.Reel: Reel(); break;
                case State.Zip: Zip(); break;
                case State.Retract:
                    hook = Vector2.MoveTowards(hook, Hand, RetractSpeed * Time.deltaTime);
                    if (Vector2.Distance(hook, Hand) < 0.05f) { Destroy(gameObject); return; }
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
                    HeroVfx.Sparks(run.ProjectileRoot, hook, SpecimenCatalog.Steel, 6, 3f, 0.2f, -direction, 120f, 0.7f);
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
            HeroVfx.Pulse(run.ProjectileRoot, hook, 0.45f, SpecimenCatalog.Keen, 0.15f);
            HeroVfx.Sparks(run.ProjectileRoot, hook, SpecimenCatalog.Steel, 8, 3.5f, 0.2f, -direction, 140f, 0.8f);
            ScreenFx.Shake(0.05f, 0.08f);
            CombatDamage.Apply(player, enemy, player.Damage, DamageElement.Physical, Hand, 0f);
            if (enemy == null || enemy.Health <= 0) { state = State.Retract; return; }
            // Too heavy to haul: the chain pulls him to it instead.
            if (enemy.Boss != null || enemy.IsTank)
            {
                anchor = enemy;
                state = State.Zip;
                zipUntil = Time.time + 0.6f;
                player.Occupy(0.6f);
                player.Protect(0.4f);
                return;
            }
            caught.Add(enemy);
            // His second stage drags a second enemy along with the first.
            if (catches > 1)
            {
                DungeonEnemy second = null;
                float best = SecondCatchRadius;
                foreach (var other in run.Enemies)
                {
                    if (other == null || other == enemy || other.Health <= 0 || other.Boss != null || other.IsTank) continue;
                    float distance = Vector2.Distance(enemy.transform.position, other.transform.position);
                    if (distance <= best) { best = distance; second = other; }
                }
                if (second != null) caught.Add(second);
            }
            state = State.Reel;
        }

        /// <summary>Hauls every catch to a step in front of him, then stuns them (and Reel In lands a blow).</summary>
        private void Reel()
        {
            caught.RemoveAll(enemy => enemy == null || enemy.Health <= 0);
            if (caught.Count == 0) { state = State.Retract; return; }
            bool arrived = true;
            Vector2 side = Vector2.Perpendicular(direction);
            for (int i = 0; i < caught.Count; i++)
            {
                var enemy = caught[i];
                Vector2 goal = Hand + direction * (0.6f + enemy.HitRadius) + side * (caught.Count > 1 ? (i == 0 ? -0.35f : 0.35f) : 0f);
                Vector2 at = enemy.transform.position;
                Vector2 next = Vector2.MoveTowards(at, goal, ReelSpeed * Time.deltaTime);
                enemy.transform.position = run.Map.Move(at, next - at, enemy.MoveRadius);
                if (i == 0) hook = enemy.transform.position;
                if (Vector2.Distance(enemy.transform.position, goal) > 0.1f && Vector2.Distance(at, enemy.transform.position) > 0.001f) arrived = false;
            }
            if (!arrived) return;
            int landing = owner != null ? owner.ReelInDamage : 0;
            foreach (var enemy in caught)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                if (landing > 0) CombatDamage.Apply(player, enemy, landing, DamageElement.Physical, Hand, 0f);
                if (enemy != null && enemy.Health > 0) enemy.Stun(SpecimenAttack.HookStun);
            }
            caught.Clear();
            state = State.Retract;
        }

        /// <summary>The chain pulls him across to a heavy enemy.</summary>
        private void Zip()
        {
            if (anchor == null || anchor.Health <= 0 || Time.time >= zipUntil) { state = State.Retract; return; }
            Vector2 at = player.transform.position, target = anchor.transform.position;
            Vector2 toward = target - at;
            float stop = anchor.HitRadius + player.HitRadius + 0.15f;
            hook = target;
            if (toward.magnitude <= stop + 0.05f) { state = State.Retract; return; }
            Vector2 step = toward.normalized * Mathf.Min(ZipSpeed * Time.deltaTime, toward.magnitude - stop);
            Vector2 moved = run.Map.Move(at, step);
            player.transform.position = moved;
            direction = toward.normalized;
            if (Vector2.Distance(at, moved) < step.magnitude * 0.3f) state = State.Retract;
        }

        /// <summary>The chain as alternating links from his hand to the hook, then the hooked blade.</summary>
        private void Draw()
        {
            Vector2 hand = Hand, span = hook - hand;
            float length = span.magnitude;
            Vector2 dir = length > 0.001f ? span / length : direction;
            mesh.Begin();
            Color steel = SpecimenCatalog.Steel, dark = new Color(0.32f, 0.36f, 0.44f);
            int links = Mathf.FloorToInt(length / 0.15f);
            for (int i = 0; i < links; i++)
            {
                Vector2 at = hand + dir * (i + 0.5f) * 0.15f;
                if (i % 2 == 0) mesh.Ellipse(at, 0.085f, 0.04f, steel, dark, 8);
                else mesh.Bar(at - dir * 0.065f, dir, 0.13f, 0.028f, dark, steel);
            }
            Vector2 facing = state == State.Retract ? -dir : state == State.Out ? direction : dir;
            Vector2 across = Vector2.Perpendicular(facing);
            // A curved blade: a spine along the chain, sweeping back into a hook.
            mesh.Disc(hook, 0.07f, steel, dark, 10);
            mesh.Triangle(hook + across * 0.06f, hook + facing * 0.24f, hook - across * 0.04f, steel, Color.white, steel);
            Vector2 elbow = hook + facing * 0.16f + across * 0.14f, tip = elbow - facing * 0.12f + across * 0.06f;
            mesh.Bar(hook + facing * 0.12f, (elbow - hook - facing * 0.12f).normalized, Vector2.Distance(hook + facing * 0.12f, elbow), 0.045f, steel, Color.white);
            mesh.Bar(elbow, (tip - elbow).normalized, Vector2.Distance(elbow, tip), 0.035f, Color.white, SpecimenCatalog.Keen);
            mesh.Commit();
        }

        private void OnDestroy() => mesh?.Release();
    }
}

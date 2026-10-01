using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A skeleton raised by the Reaper's <see cref="ArmyOfTheDead"/>. It picks its fights: enemies that threaten it or
    /// its master, casters, and anything not already held come first, and skeletons share targets out between them
    /// rather than piling onto one. It finds its way around walls, keeps apart from the others, never strays far from
    /// its master (and digs itself back up beside him if left behind). Its blows are dealt in the Reaper's name, so his
    /// talents apply to them, and its health and damage are fractions of his. Every blow strikes fear into what it hits.
    /// Enemies it stands against wear it down.
    /// It lasts until it is destroyed or the floor ends.
    /// </summary>
    public sealed class SkeletonMinion : MonoBehaviour
    {
        public const float Sight = 9f, Reach = 0.75f, AttackInterval = 0.9f, FollowDistance = 1.6f, HurtInterval = 1f, Size = 0.8f, HitFear = 1f;
        /// <summary>How far from its master it will fight, how far before it gives up walking and re-rises beside him, and how often it rethinks.</summary>
        public const float Leash = 10f, RecallDistance = 15f, ThinkInterval = 0.25f, Spacing = 0.7f;
        private static readonly List<SkeletonMinion> All = new List<SkeletonMinion>();
        private static readonly Vector2Int[] Steps =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };
        // Its own route around walls: steps to the goal from every floor cell, rebuilt when the goal moves out of sight.
        private readonly int[,] route = new int[DungeonMap.Width, DungeonMap.Height];
        private readonly Vector2Int[] routeGoal = new Vector2Int[1];
        private bool routed;
        private DungeonEnemy target;
        private float nextThink;
        private DungeonPlayer master;
        private DungeonRun run;
        private SpriteRenderer body;
        private float readyAt, nextHurtAt, flashUntil, raisedAt, struckAt = float.NegativeInfinity;
        private SpriteRenderer shadow;
        private const float RiseTime = 0.35f;
        private int wounds;
        private static Sprite sprite;

        /// <summary>A third of the Reaper's maximum health, rounded up.</summary>
        public int MaxHealth => Mathf.Max(1, Mathf.CeilToInt(master.MaxHealth / 3f));
        public int Health => MaxHealth - wounds;
        /// <summary>Half the Reaper's damage, rounded up.</summary>
        public int Damage => Mathf.Max(1, Mathf.CeilToInt(master.Damage * 0.5f));

        // W bone, w shaded bone, D outline, G soul-fire in the eye sockets, K rusted blade, B hilt.
        public static Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite("Skeleton", new[]
        {
            "....DDDDDD......", "...DWWWWWWD.....", "..DWWWWWWWwD....", "..DWGGWWGGwD....",
            "..DWGGWWGGwD..K.", "..DWWWDDWWwD..K.", "...DWWWWWWD...K.", "...DWDWDWDD...K.",
            "....DDDDDD...DKD", "..DWDwWWwDWD.DKD", ".DWDDWDDWDDWDBBB", ".DWD.DWWD.DWWDB.",
            "..D..DwwD..DD...", "....DWDDWD......", "....DWD.DWD.....", "...DWWD.DWWD...."
        }, key => key == 'W' ? ReaperAttack.Bone : key == 'w' ? new Color(0.68f, 0.66f, 0.58f) : key == 'D' ? new Color(0.1f, 0.11f, 0.14f)
            : key == 'G' ? ReaperAttack.Soul : key == 'K' ? new Color(0.66f, 0.7f, 0.74f) : key == 'B' ? new Color(0.45f, 0.3f, 0.18f) : Color.clear);

        public static SkeletonMinion Raise(DungeonPlayer master, Vector2 position)
        {
            var renderer = DungeonVisuals.Create("Skeleton", master.Run.ProjectileRoot, position, Vector2.one * Size, Color.white, 4);
            renderer.sprite = Sprite;
            var skeleton = renderer.gameObject.AddComponent<SkeletonMinion>();
            skeleton.master = master;
            skeleton.run = master.Run;
            skeleton.body = renderer;
            skeleton.readyAt = Time.time + 0.4f;
            skeleton.nextHurtAt = Time.time + HurtInterval;
            skeleton.raisedAt = Time.time;
            // The shadow lives beside it, so the skeleton's own stretch and squash leave it alone.
            skeleton.shadow = DungeonVisuals.Create("Skeleton shadow", master.Run.ProjectileRoot, position, new Vector2(0.5f, 0.14f), new Color(0.01f, 0.02f, 0.04f, 0.35f), 3);
            renderer.transform.localScale = new Vector3(Size, 0f, 1f);
            All.Add(skeleton);
            return skeleton;
        }

        private void Update()
        {
            if (run == null || master == null || !run.IsPlaying) return;
            if (master.Health <= 0 || Health <= 0) { Crumble(); return; }
            Vector2 position = transform.position, home = master.transform.position;
            // Left behind (or walled off): it sinks into the ground and claws its way up beside its master again.
            if (Vector2.Distance(position, home) > RecallDistance) { Recall(home); position = transform.position; }
            if (Time.time >= nextThink || (target != null && (target.Health <= 0 || target.IsInvulnerable)))
            {
                nextThink = Time.time + ThinkInterval;
                target = ChooseTarget(position, home);
                Vector2 heading = target != null ? (Vector2)target.transform.position : home;
                // In plain sight it walks straight; otherwise it plots a way around the walls.
                routed = !run.HasLineOfSight(position, heading) && PlotRoute(heading);
                // An enemy it can neither see nor walk to is left alone; it heads back to its master instead.
                if (target != null && !routed && !run.HasLineOfSight(position, heading))
                {
                    target = null;
                    routed = !run.HasLineOfSight(position, home) && PlotRoute(home);
                }
            }
            Vector2 goal = target != null ? (Vector2)target.transform.position : home;
            float stop = target != null ? Reach + target.HitRadius - 0.1f : FollowDistance;
            Vector2 toGoal = goal - position;
            if (toGoal.magnitude > stop)
            {
                float speed = master.Speed * 0.9f * master.Buffs.MoveMultiplier;
                Vector2 direction = routed ? RouteDirection(position, toGoal.normalized) : toGoal.normalized;
                transform.position = position = run.Map.Move(position, (direction + Separation(position) * 0.6f).normalized * speed * Time.deltaTime, 0.25f);
            }
            else
            {
                // Standing its ground, it still shuffles apart from the other skeletons.
                Vector2 apart = Separation(position);
                if (apart.sqrMagnitude > 0.01f) transform.position = position = run.Map.Move(position, apart * master.Speed * 0.4f * Time.deltaTime, 0.25f);
            }
            if (Mathf.Abs(toGoal.x) > 0.05f) body.flipX = toGoal.x < 0f;
            // It climbs up out of the ground, sways as it walks and leans into each blow.
            float rise = Mathf.Clamp01((Time.time - raisedAt) / RiseTime), strike = 1f - Mathf.Clamp01((Time.time - struckAt) / 0.18f);
            bool walking = toGoal.magnitude > stop;
            float sway = walking ? Mathf.Sin(Time.time * 13f) : 0f;
            transform.localScale = new Vector3(Size * (1f + 0.18f * strike), Size * (rise * rise * (3f - 2f * rise)) * (1f - 0.12f * strike + 0.04f * Mathf.Abs(sway)), 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, sway * 5f - (body.flipX ? -1f : 1f) * 14f * strike);
            if (shadow != null) shadow.transform.position = position + Vector2.down * Size * 0.5f;
            body.color = Time.time < flashUntil ? PlayerHurt : Color.Lerp(new Color(0.6f, 0.6f, 0.6f), Color.white, Health / (float)MaxHealth);
            if (target != null && Time.time >= readyAt && Vector2.Distance(position, target.transform.position) <= Reach + target.HitRadius)
            {
                readyAt = Time.time + AttackInterval * master.Powerups.AttackIntervalMultiplier;
                struckAt = Time.time;
                Vector2 aim = ((Vector2)target.transform.position - position).normalized;
                HeroVfx.Slash(run.ProjectileRoot, position, aim, Reach + 0.3f, 90f, ReaperAttack.Bone, 0.15f);
                CombatDamage.Apply(master, target, Damage, DamageElement.Physical, position, 0.3f);
                if (target != null && target.Health > 0) target.Fear(position, HitFear);
                // Its victim is frightened stiff now: look at once for someone who is not.
                nextThink = 0f;
            }
            // Whatever it stands toe to toe with hits back, once a second.
            if (Time.time < nextHurtAt) return;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.IsHeld || enemy.IsRanged
                    || Vector2.Distance(position, enemy.transform.position) > enemy.HitRadius + 0.5f) continue;
                nextHurtAt = Time.time + HurtInterval;
                wounds++;
                flashUntil = Time.time + 0.15f;
                HeroVfx.Sparks(run.ProjectileRoot, position, ReaperAttack.Bone, 6, 3f, 0.25f, position - (Vector2)enemy.transform.position, 120f);
                break;
            }
        }

        private void OnDestroy() { All.Remove(this); if (shadow != null) Destroy(shadow.gameObject); }

        private static readonly Color PlayerHurt = new Color(1f, 0.35f, 0.35f);

        /// <summary>
        /// The enemy most worth fighting, within sight of the skeleton and the leash of its master. Nearer is better (to
        /// it, and to its master); casters and melee enemies breathing down its neck come first; enemies already held
        /// (feared, frozen, stunned) or already taken by another skeleton come last. It leans toward keeping the target it has.
        /// </summary>
        private DungeonEnemy ChooseTarget(Vector2 from, Vector2 home)
        {
            DungeonEnemy best = null;
            float bestScore = float.MaxValue;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.IsInvulnerable) continue;
                Vector2 at = enemy.transform.position;
                float distance = Vector2.Distance(from, at), fromHome = Vector2.Distance(home, at);
                if (distance > Sight || fromHome > Leash) continue;
                float score = distance + fromHome * 0.4f;
                if (enemy.IsHeld) score += 2.5f;
                else if (!enemy.IsRanged && distance <= enemy.HitRadius + 1.5f) score -= 2f;
                if (enemy.IsRanged) score -= 1f;
                if (enemy == target) score -= 1f;
                foreach (var other in All)
                    if (other != null && other != this && other.master == master && other.target == enemy) score += 2f;
                if (score < bestScore) { bestScore = score; best = enemy; }
            }
            return best;
        }

        /// <summary>Plots the way to <paramref name="goal"/> around walls; false when there is no way there on foot.</summary>
        private bool PlotRoute(Vector2 goal)
        {
            var cell = Vector2Int.RoundToInt(goal);
            if (!run.Map.IsFloor(cell.x, cell.y)) return false;
            routeGoal[0] = cell;
            DungeonRun.BuildFlowField(run.Map, routeGoal, route);
            var here = Vector2Int.RoundToInt((Vector2)transform.position);
            return run.Map.IsFloor(here.x, here.y) && route[here.x, here.y] != int.MaxValue;
        }

        /// <summary>The next step along the plotted route: toward whichever neighbouring cell is closest to the goal.</summary>
        private Vector2 RouteDirection(Vector2 position, Vector2 fallback)
        {
            var map = run.Map;
            var cell = Vector2Int.RoundToInt(position);
            if (!map.IsFloor(cell.x, cell.y)) return fallback;
            var best = cell;
            foreach (var step in Steps)
            {
                var next = cell + step;
                // No cutting corners: a diagonal step needs both cells beside it open.
                if (!map.IsFloor(next.x, next.y) || (step.x != 0 && step.y != 0 && (!map.IsFloor(cell.x + step.x, cell.y) || !map.IsFloor(cell.x, cell.y + step.y)))) continue;
                if (route[next.x, next.y] < route[best.x, best.y]) best = next;
            }
            return best == cell ? fallback : ((Vector2)best - position).normalized;
        }

        /// <summary>A push away from the master's other skeletons standing too close, so they fan out instead of stacking.</summary>
        private Vector2 Separation(Vector2 position)
        {
            Vector2 push = Vector2.zero;
            foreach (var other in All)
            {
                if (other == null || other == this || other.master != master) continue;
                Vector2 away = position - (Vector2)other.transform.position;
                float distance = away.magnitude;
                if (distance >= Spacing) continue;
                // Two on exactly the same spot split along a direction of their own.
                push += (distance > 0.01f ? away / distance : FlameMesh.Polar(GetInstanceID() * 1.7f, 1f)) * (1f - distance / Spacing);
            }
            return push;
        }

        /// <summary>Crumbles where it stands and rises again at its master's side.</summary>
        private void Recall(Vector2 home)
        {
            HeroVfx.Sparks(run.ProjectileRoot, transform.position, ReaperAttack.Bone, 8, 3f, 0.3f);
            Vector2 spot = home;
            float start = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 8; i++)
            {
                Vector2 candidate = home + FlameMesh.Polar(start + i * Mathf.PI / 4f, 1.1f);
                if (!run.Map.CanStand(candidate, 0.25f)) continue;
                spot = candidate;
                break;
            }
            transform.position = spot;
            raisedAt = Time.time;
            target = null;
            nextThink = 0f;
            HeroVfx.Pulse(run.ProjectileRoot, spot, 0.9f, ReaperAttack.Soul, 0.4f);
        }

        private void Crumble()
        {
            HeroVfx.Sparks(run.ProjectileRoot, transform.position, ReaperAttack.Bone, 12, 3.5f, 0.4f);
            HeroVfx.Pulse(run.ProjectileRoot, transform.position, 0.6f, ReaperAttack.Shade, 0.3f);
            if (shadow != null) Destroy(shadow.gameObject);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}

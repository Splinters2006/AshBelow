using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Specimen's boss artifacts. Heartbeat and Fight or Flight work in any form (and change with it); the Behemoth's
    /// four (Bulldoze, Boulder Toss, Iron Skin, Giant Swing) and the Edge's four (Swing Line, Ankle Wrap, Bind, Round-Up)
    /// only work in their own form and sleep otherwise.
    /// </summary>
    public static class SpecimenArts
    {
        public const float HeartbeatRadius = 2.5f, BehemothHeartbeatRadius = 3.2f, HeartbeatStun = 1f, HeartbeatExposure = 2f;
        public const float FlightTime = 4f;
        public const float BulldozeDistance = 5f, BulldozeTime = 0.38f, FreightTrainStun = 1f;
        public const float BoulderRange = 7f, CoverTime = 4f, CoverWidth = 2f;
        public const float IronSkinTime = 4f;
        public const int IronSkinHits = 3;
        public const float SwingReach = 1.8f, SwingTime = 0.6f, SwingRadius = 1.4f, FlingDistance = 4f;
        public const float SwingLineRange = 6f, AnkleWrapStun = 1f, BindTime = 2f, BindReach = 3f, BindCursorRange = 8f;
        public const float RoundUpReach = 5f, RoundUpCone = 70f, RoundUpStun = 1f;
        public const int RoundUpCatches = 4;

        public static bool Cast(SpecimenAttack specimen, AbilityType type, Vector2 aim, int rank, float cursorDistance)
        {
            var player = specimen.Player;
            if (player == null || !player.Run.IsPlaying || player.IsRolling || player.IsBusy || !specimen.CanUseAbility(type)) return false;
            rank = Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank);
            switch (type)
            {
                case AbilityType.Heartbeat: Heartbeat(specimen, rank); return true;
                case AbilityType.FightOrFlight:
                    specimen.BeginFlight(FlightTime + (rank - 1));
                    HeroVfx.Motes(player.Run.ProjectileRoot, player.transform.position, 0.8f, SpecimenCatalog.Vital, 14, 0.7f);
                    return true;
                case AbilityType.Bulldoze: specimen.StartCoroutine(Bulldoze(specimen, aim, rank)); return true;
                case AbilityType.BoulderToss:
                    ThrownBoulder.Throw(specimen, aim, Mathf.Min(BoulderRange, Mathf.Max(1.5f, cursorDistance)), player.Damage * 3 + rank - 1, player.Damage,
                        CoverTime * (player.Powerups.Count(PowerupType.RubbleWall) > 0 ? 2f : 1f), CoverWidth * (player.Powerups.Count(PowerupType.RubbleWall) > 0 ? 2f : 1f));
                    return true;
                case AbilityType.IronSkin:
                    specimen.BeginIronSkin(IronSkinTime + 0.5f * (rank - 1), IronSkinHits);
                    HeroVfx.Pulse(player.Run.ProjectileRoot, player.transform.position, 1.4f * specimen.BodyScale, SpecimenCatalog.Stone, 0.4f);
                    CoopFx.Pulse(player.Run, player.transform.position, 1.4f * specimen.BodyScale, SpecimenCatalog.Stone, 0.4f);
                    return true;
                case AbilityType.GiantSwing: return GiantSwing(specimen, aim, rank);
                case AbilityType.SwingLine: return SwingLine(specimen, aim, rank);
                case AbilityType.AnkleWrap: AnkleWrap(specimen, aim, rank); return true;
                case AbilityType.Bind: return Bind(specimen, aim, rank, cursorDistance);
                case AbilityType.RoundUp: return RoundUp(specimen, aim, rank);
                default: return false;
            }
        }

        private static bool InReach(DungeonRun run, Vector2 center, DungeonEnemy enemy, float radius) => enemy != null && enemy.Health > 0
            && Vector2.Distance(center, enemy.transform.position) <= radius + enemy.HitRadius && run.HasLineOfSight(center, enemy.transform.position);

        /// <summary>
        /// Heartbeat: a thump of force knocks everything near him back. The Behemoth's reaches farther, wall-slams and
        /// stuns; the Edge's leaves every enemy it hits exposed to his critical hits.
        /// </summary>
        private static void Heartbeat(SpecimenAttack specimen, int rank)
        {
            var player = specimen.Player;
            var run = player.Run;
            var form = specimen.Form;
            Vector2 center = player.transform.position;
            float radius = (form == SpecimenForm.Behemoth ? BehemothHeartbeatRadius : HeartbeatRadius) * Mathf.Max(1f, specimen.BodyScale * 0.85f);
            int damage = player.Damage + rank - 1;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (!InReach(run, center, enemy, radius)) continue;
                specimen.HitAndShove(enemy, damage, center, 3f, form == SpecimenForm.Behemoth);
                if (enemy == null || enemy.Health <= 0) continue;
                if (form == SpecimenForm.Behemoth) enemy.Stun(HeartbeatStun + 0.25f * (rank - 1));
                else if (form == SpecimenForm.Edge) specimen.Expose(enemy, HeartbeatExposure + 0.5f * (rank - 1));
            }
            var color = SpecimenCatalog.FormColor(form);
            CombatVfx.Ring(run.ProjectileRoot, center, radius, SpecimenCatalog.Vital, 0.35f);
            HeroVfx.Pulse(run.ProjectileRoot, center, radius, FlameMesh.Alpha(color, 0.6f), 0.35f);
            CoopFx.Ring(run, center, radius, SpecimenCatalog.Vital, 0.35f);
            CoopFx.Pulse(run, center, radius, color, 0.35f);
            ScreenFx.Shake(0.1f, 0.15f);
        }

        /// <summary>
        /// Bulldoze: he charges with his arms crossed, carrying every enemy in his path, and slams them all where the
        /// charge stops (against a wall, the slam is a wall slam). Bolts hitting his arms on the way become Force.
        /// </summary>
        private static IEnumerator Bulldoze(SpecimenAttack specimen, Vector2 aim, int rank)
        {
            var player = specimen.Player;
            var run = player.Run;
            var root = run.ProjectileRoot;
            bool freight = player.Powerups.Count(PowerupType.FreightTrain) > 0;
            float distance = BulldozeDistance * (freight ? 1.5f : 1f), duration = BulldozeTime * (freight ? 1.3f : 1f);
            aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : player.AimDirection;
            player.Occupy(duration + 0.05f);
            player.Protect(duration + 0.15f);
            specimen.IsBulldozing = true;
            var carried = new List<DungeonEnemy>();
            Vector2 start = player.transform.position;
            bool blocked = false;
            float travelled = 0f, nextDust = 0f;
            try
            {
                while (travelled < distance - 0.001f)
                {
                    if (!run.IsPlaying || player.Health <= 0 || root != run.ProjectileRoot) yield break;
                    float step = Mathf.Min(distance - travelled, distance / duration * Time.deltaTime);
                    Vector2 here = player.transform.position, there = run.Map.Move(here, aim * step);
                    float moved = Vector2.Distance(here, there);
                    player.transform.position = there;
                    travelled += step;
                    // Scoop up whatever is in front; guardians stop him dead.
                    float reach = player.HitRadius + 0.45f;
                    foreach (var enemy in run.Enemies.ToArray())
                    {
                        if (enemy == null || enemy.Health <= 0 || carried.Contains(enemy)) continue;
                        Vector2 offset = (Vector2)enemy.transform.position - there;
                        if (offset.magnitude > reach + enemy.HitRadius || Vector2.Dot(offset, aim) < -0.1f) continue;
                        if (enemy.Boss != null) { blocked = true; break; }
                        carried.Add(enemy);
                    }
                    foreach (var enemy in carried)
                        if (enemy != null && enemy.Health > 0)
                        {
                            Vector2 at = enemy.transform.position, goal = there + aim * (reach + enemy.HitRadius * 0.6f) + Vector2.Perpendicular(aim) * Vector2.Dot((Vector2)at - there, Vector2.Perpendicular(aim)) * 0.9f;
                            enemy.transform.position = run.Map.Move(at, goal - at, enemy.MoveRadius);
                        }
                    if (Time.time >= nextDust)
                    {
                        nextDust = Time.time + 0.05f;
                        HeroVfx.Sparks(root, there - aim * 0.3f * specimen.BodyScale, SpecimenCatalog.Stone, 3, 2f, 0.3f, -aim, 70f);
                    }
                    if (blocked || moved < step * 0.4f) { blocked = true; break; }
                    yield return null;
                }
            }
            finally { specimen.IsBulldozing = false; }
            // The slam where the charge stops.
            Vector2 end = player.transform.position;
            int damage = player.Damage * 2 + rank - 1;
            float extraStun = freight ? FreightTrainStun : 0f;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                bool hauled = carried.Contains(enemy);
                if (!hauled && !(blocked && enemy.Boss != null && Vector2.Distance(end, enemy.transform.position) <= player.HitRadius + enemy.HitRadius + 0.6f)) continue;
                // Whatever he hauled into a wall is pinned there: the shove can't move it, so it is wall-slammed.
                specimen.HitAndShove(enemy, damage, end, 3f, true, extraStun);
            }
            CombatVfx.Ring(root, end, 1.4f * specimen.BodyScale, SpecimenCatalog.Stone, 0.3f);
            HeroVfx.Pulse(root, end, 1.4f * specimen.BodyScale, SpecimenCatalog.Amber, 0.3f);
            CoopFx.Bolt(run, start, end, SpecimenCatalog.Amber, true);
            CoopFx.Ring(run, end, 1.4f * specimen.BodyScale, SpecimenCatalog.Stone, 0.3f);
            ScreenFx.Shake(0.18f, 0.22f);
        }

        /// <summary>
        /// Giant Swing: grab the nearest enemy, swing it in a full circle as a club, then fling it (a throw into a wall
        /// is a wall slam). A guardian can't be lifted and takes a huge punch instead.
        /// </summary>
        private static bool GiantSwing(SpecimenAttack specimen, Vector2 aim, int rank)
        {
            var player = specimen.Player;
            var run = player.Run;
            Vector2 center = player.transform.position;
            float reach = SwingReach * Mathf.Max(1f, specimen.BodyScale * 0.85f);
            DungeonEnemy held = null;
            float best = float.MaxValue;
            foreach (var enemy in run.Enemies)
            {
                if (!InReach(run, center, enemy, reach) || enemy.IsInvulnerable) continue;
                float distance = Vector2.Distance(center, enemy.transform.position);
                if (distance < best) { best = distance; held = enemy; }
            }
            if (held == null) return false;
            if (held.Boss != null)
            {
                CombatDamage.Apply(player, held, player.Damage * 4 + rank - 1, DamageElement.Physical, center, 0f);
                HeroVfx.Sparks(run.ProjectileRoot, held.transform.position, SpecimenCatalog.Amber, 16, 5f, 0.4f, (Vector2)held.transform.position - center, 90f);
                CoopFx.Pulse(run, held.transform.position, 1f, SpecimenCatalog.Amber, 0.3f);
                ScreenFx.Shake(0.2f, 0.25f);
                return true;
            }
            specimen.StartCoroutine(Swing(specimen, held, aim, rank));
            return true;
        }

        private static IEnumerator Swing(SpecimenAttack specimen, DungeonEnemy held, Vector2 aim, int rank)
        {
            var player = specimen.Player;
            var run = player.Run;
            var root = run.ProjectileRoot;
            player.Occupy(SwingTime + 0.1f);
            var struck = new HashSet<DungeonEnemy> { held };
            // Held fast in his grip for the whole swing.
            held.Stun(SwingTime + 0.15f);
            Vector2 center = player.transform.position;
            float radius = SwingRadius * Mathf.Max(1f, specimen.BodyScale * 0.85f);
            Vector2 grab = (Vector2)held.transform.position - center;
            float startAngle = Mathf.Atan2(grab.y, grab.x), finalAngle = Mathf.Atan2(aim.y, aim.x);
            // One full turn, ending pointed at the cursor.
            float sweep = Mathf.PI * 2f + Mathf.Repeat(finalAngle - startAngle, Mathf.PI * 2f);
            int clubDamage = player.Damage * 2 + rank - 1;
            for (float t = 0f; t < SwingTime; t += Time.deltaTime)
            {
                if (!run.IsPlaying || player.Health <= 0 || root != run.ProjectileRoot || held == null || held.Health <= 0) yield break;
                center = player.transform.position;
                float angle = startAngle + sweep * (t / SwingTime);
                Vector2 goal = center + FlameMesh.Polar(angle, radius);
                Vector2 at = held.transform.position;
                held.transform.position = run.Map.Move(at, goal - at, held.MoveRadius);
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || struck.Contains(enemy) || enemy.Health <= 0) continue;
                    if (Vector2.Distance(held.transform.position, enemy.transform.position) > held.HitRadius + enemy.HitRadius + 0.15f) continue;
                    struck.Add(enemy);
                    specimen.HitAndShove(enemy, clubDamage, center, 2.5f, true);
                }
                HeroVfx.Sparks(root, held.transform.position, SpecimenCatalog.Stone, 1, 1.5f, 0.2f);
                yield return null;
            }
            if (held == null || held.Health <= 0) yield break;
            // The fling.
            Vector2 from = held.transform.position, landing = run.Map.Move(from, aim * FlingDistance, held.MoveRadius);
            held.transform.position = landing;
            bool wall = Vector2.Distance(from, landing) < FlingDistance * 0.7f;
            CombatDamage.Apply(player, held, player.Damage * 3 + rank - 1, DamageElement.Physical, from, 0f);
            if (wall) specimen.WallSlam(held);
            CombatVfx.GlowBolt(root, from, landing, SpecimenCatalog.Amber);
            HeroVfx.Sparks(root, landing, SpecimenCatalog.Stone, 12, 4f, 0.35f);
            CoopFx.Bolt(run, from, landing, SpecimenCatalog.Amber, true);
            ScreenFx.Shake(0.15f, 0.2f);
        }

        /// <summary>
        /// Swing Line: the hook bites into the first enemy or wall toward the cursor and the chain zips him there, kicking
        /// whatever waits at the end. With Zipline, a killing kick has it ready again at once.
        /// </summary>
        private static bool SwingLine(SpecimenAttack specimen, Vector2 aim, int rank)
        {
            var player = specimen.Player;
            var run = player.Run;
            Vector2 from = player.transform.position;
            DungeonEnemy target = null;
            Vector2 anchor = from;
            bool found = false;
            for (float travel = 0.2f; travel <= SwingLineRange + 0.001f && !found; travel += 0.1f)
            {
                Vector2 point = from + aim * travel;
                if (!run.Map.CanStand(point, 0.05f)) { anchor = point - aim * 0.1f; found = true; break; }
                foreach (var enemy in run.Enemies)
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(point, enemy.transform.position) <= enemy.HitRadius + 0.2f)
                    { target = enemy; anchor = enemy.transform.position; found = true; break; }
            }
            if (!found) return false;
            float stop = target != null ? target.HitRadius + 0.5f : 0.45f;
            // Moved like walking (at his full clearance), so a shallow hook along a wall never leaves him wedged in it.
            Vector2 landing = run.Map.Move(from, aim * Mathf.Max(0f, Vector2.Distance(from, anchor) - stop));
            player.transform.position = landing;
            player.Protect(0.35f);
            int damage = player.Damage * 2 + rank - 1;
            bool killed = false;
            if (target != null && target.Health > 0)
            {
                CombatDamage.Apply(player, target, damage, DamageElement.Physical, landing, 2.5f);
                killed = target == null || target.Health <= 0;
            }
            else
                foreach (var enemy in run.Enemies.ToArray())
                    if (InReach(run, landing, enemy, 1.2f))
                    {
                        CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, landing, 2.5f);
                        killed |= enemy == null || enemy.Health <= 0;
                    }
            if (killed && player.Powerups.Count(PowerupType.Zipline) > 0) specimen.RequestRefund(AbilityType.SwingLine);
            var root = run.ProjectileRoot;
            CombatVfx.Bolt(root, from, anchor, SpecimenCatalog.Steel, 0.06f, 0.25f);
            HeroVfx.Sparks(root, landing + aim * 0.4f, SpecimenCatalog.Keen, 10, 4f, 0.3f, aim, 80f);
            CoopFx.Bolt(run, from, anchor, SpecimenCatalog.Steel);
            CoopFx.Windstep(run, from, landing);
            return true;
        }

        /// <summary>Ankle Wrap: a low lash across a half circle trips every enemy in reach.</summary>
        private static void AnkleWrap(SpecimenAttack specimen, Vector2 aim, int rank)
        {
            var player = specimen.Player;
            var run = player.Run;
            Vector2 origin = player.transform.position;
            float reach = specimen.LashReachNow, stun = AnkleWrapStun + 0.25f * (rank - 1);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 at = enemy.transform.position;
                if (!SwordAttack.OverlapsCone(at - origin, aim, reach, 180f, enemy.HitRadius) || !run.HasLineOfSight(origin, at)) continue;
                CombatDamage.Apply(player, enemy, player.Damage + rank - 1, DamageElement.Physical, origin, 0.2f);
                if (enemy == null || enemy.Health <= 0) continue;
                if (enemy.Stun(stun)) specimen.Trip(enemy, stun);
            }
            SpecimenVfx.Cyclone(run.ProjectileRoot, player.transform, aim, reach, SpecimenCatalog.Steel, 180f);
            CoopFx.Slash(run, origin, aim, reach, 180f, SpecimenCatalog.Steel);
        }

        /// <summary>Bind: the chain wraps the enemy nearest the cursor, rooting it; every hit on it while bound crits.</summary>
        private static bool Bind(SpecimenAttack specimen, Vector2 aim, int rank, float cursorDistance)
        {
            var player = specimen.Player;
            var run = player.Run;
            var target = player.Abilities.NearestEnemy((Vector2)player.transform.position + aim * Mathf.Min(cursorDistance, BindCursorRange), BindReach);
            if (target == null || target.IsInvulnerable) return false;
            float duration = BindTime + 0.5f * (rank - 1);
            // Guardians are too massive to root, but they are still bound for his crits.
            target.Root(duration);
            specimen.Bind(target, duration);
            CombatVfx.Bolt(run.ProjectileRoot, player.transform.position, target.transform.position, SpecimenCatalog.Steel, 0.07f, 0.35f);
            CombatVfx.Ring(run.ProjectileRoot, target.transform.position, target.HitRadius + 0.2f, SpecimenCatalog.Steel, 0.4f);
            CoopFx.Bolt(run, player.transform.position, target.transform.position, SpecimenCatalog.Steel);
            CoopFx.Ring(run, target.transform.position, target.HitRadius + 0.2f, SpecimenCatalog.Steel, 0.4f);
            return true;
        }

        /// <summary>Round-Up: hooks up to four enemies in a wide cone and smashes them together in front of him.</summary>
        private static bool RoundUp(SpecimenAttack specimen, Vector2 aim, int rank)
        {
            var player = specimen.Player;
            var run = player.Run;
            Vector2 origin = player.transform.position;
            var caught = new List<DungeonEnemy>();
            int damage = player.Damage * 2 + rank - 1;
            bool any = false;
            var candidates = new List<DungeonEnemy>();
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 at = enemy.transform.position;
                if (!SwordAttack.OverlapsCone(at - origin, aim, RoundUpReach, RoundUpCone, enemy.HitRadius) || !run.HasLineOfSight(origin, at)) continue;
                candidates.Add(enemy);
            }
            candidates.Sort((a, b) => Vector2.Distance(origin, a.transform.position).CompareTo(Vector2.Distance(origin, b.transform.position)));
            foreach (var enemy in candidates)
            {
                any = true;
                // Guardians only take the hit.
                if (enemy.Boss != null) { CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, origin, 0f); continue; }
                if (caught.Count < RoundUpCatches) caught.Add(enemy);
            }
            if (!any) return false;
            foreach (var enemy in caught)
            {
                CombatVfx.Bolt(run.ProjectileRoot, origin, enemy.transform.position, SpecimenCatalog.Steel, 0.05f, 0.25f);
                CoopFx.Bolt(run, origin, enemy.transform.position, SpecimenCatalog.Steel);
            }
            if (caught.Count > 0) specimen.StartCoroutine(Gather(specimen, caught, aim, damage));
            return true;
        }

        private static IEnumerator Gather(SpecimenAttack specimen, List<DungeonEnemy> caught, Vector2 aim, int damage)
        {
            var player = specimen.Player;
            var run = player.Run;
            var root = run.ProjectileRoot;
            const float PullTime = 0.25f;
            var starts = new Dictionary<DungeonEnemy, Vector2>();
            foreach (var enemy in caught) if (enemy != null) starts[enemy] = enemy.transform.position;
            for (float t = 0f; t < PullTime; t += Time.deltaTime)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot) yield break;
                Vector2 point = (Vector2)player.transform.position + aim * (1.2f * Mathf.Max(1f, specimen.BodyScale));
                float k = t / PullTime;
                foreach (var enemy in caught)
                {
                    if (enemy == null || enemy.Health <= 0 || !starts.ContainsKey(enemy)) continue;
                    Vector2 at = enemy.transform.position, goal = Vector2.Lerp(starts[enemy], point, k * k);
                    enemy.transform.position = run.Map.Move(at, goal - at, enemy.MoveRadius);
                }
                yield return null;
            }
            Vector2 crash = (Vector2)player.transform.position + aim * (1.2f * Mathf.Max(1f, specimen.BodyScale));
            foreach (var enemy in caught)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, crash, 0.3f);
                if (enemy != null && enemy.Health > 0) enemy.Stun(RoundUpStun);
            }
            HeroVfx.Sparks(root, crash, SpecimenCatalog.Keen, 16, 4.5f, 0.35f);
            CombatVfx.Ring(root, crash, 1f, SpecimenCatalog.Steel, 0.3f);
            CoopFx.Ring(run, crash, 1f, SpecimenCatalog.Steel, 0.3f);
            ScreenFx.Shake(0.15f, 0.2f);
        }
    }
}

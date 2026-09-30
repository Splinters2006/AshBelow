using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Brawler's expansion techniques: Thunder Clap, Haymaker Dash and Suplex, plus Sandwich Special's shockwave.</summary>
    public sealed partial class BrawlerAttack
    {
        public const float ClapRange = 5.5f, ClapCone = 55f, ClapStun = 0.75f;
        public const float HaymakerDistance = 4f, HaymakerLaunch = 3f, HaymakerSplash = 1.3f, LaunchTime = 0.3f, LaunchHeight = 1.3f;
        public const float SuplexReach = 2.5f, SuplexRadius = 1.6f, SuplexThrow = 1.6f, SuplexTime = 0.35f;
        private static readonly Color ClapColor = new Color(0.8f, 0.9f, 1f), SuplexColor = new Color(0.85f, 0.7f, 0.5f), Dust = new Color(0.75f, 0.68f, 0.58f, 0.7f);

        /// <summary>
        /// Thunder Clap: she claps and a shockwave rolls out from her across a cone. It strikes each enemy as its front
        /// reaches them, throwing them back and stunning them.
        /// </summary>
        private IEnumerator ThunderClap(Vector2 aim, int rank)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            BrawlerVfx.Move(run.ProjectileRoot, PunchVfx.Style.Clap, origin, aim, ClapRange, ClapCone * 0.5f, ClapColor);
            CoopFx.BrawlerMove(run, PunchVfx.Style.Clap, origin, aim, ClapRange, ClapCone * 0.5f, ClapColor);
            ScreenFx.Shake(0.18f, 0.2f);
            int damage = Player.Damage * 2 + rank - 1;
            var hit = new HashSet<DungeonEnemy>();
            // The same timing and easing as the wave drawn by PunchVfx, so enemies are struck as the front passes them.
            float travel = BrawlerVfx.ClapTime * PunchVfx.ClapTravel;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                if (!run.IsPlaying) yield break;
                float progress = Mathf.Clamp01(t / travel), front = ClapRange * (1f - (1f - progress) * (1f - progress));
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || hit.Contains(enemy)) continue;
                    Vector2 offset = (Vector2)enemy.transform.position - origin;
                    if (offset.magnitude - enemy.HitRadius > front || !SwordAttack.ContainsTarget(offset, aim, ClapRange + enemy.HitRadius, ClapCone)
                        || !run.HasLineOfSight(origin, enemy.transform.position)) continue;
                    hit.Add(enemy);
                    CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, origin, 2.5f);
                    if (enemy != null && enemy.Health > 0) enemy.Stun(ClapStun);
                    HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, ClapColor, 6, 3f, 0.2f, offset, 80f, 0.8f);
                }
                if (progress >= 1f) yield break;
                yield return null;
            }
        }

        /// <summary>
        /// Haymaker Dash: she rushes forward and the first enemy she reaches eats a huge uppercut that launches it in an
        /// arc into the enemies behind it.
        /// </summary>
        private IEnumerator HaymakerDash(Vector2 aim, int rank)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            Vector2 from = transform.position;
            Player.Occupy(0.3f);
            Player.Protect(0.35f);
            DungeonEnemy victim = null;
            const float Speed = 18f;
            for (float travelled = 0f; travelled < HaymakerDistance && victim == null; )
            {
                if (!run.IsPlaying || Player.Health <= 0) yield break;
                Vector2 before = transform.position;
                transform.position = run.Map.Move(before, aim * Speed * Time.deltaTime);
                travelled += Vector2.Distance(before, transform.position);
                HeroVfx.Sparks(root, (Vector2)transform.position + Vector2.down * 0.3f, Dust, 1, 1.5f, 0.3f, -aim, 70f, 0.8f);
                foreach (var enemy in run.Enemies)
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(transform.position, enemy.transform.position) <= enemy.HitRadius + 0.6f) { victim = enemy; break; }
                if (Vector2.Distance(before, transform.position) < 0.01f) break;
                yield return null;
            }
            float dashed = Vector2.Distance(from, transform.position);
            if (dashed > 0.1f)
            {
                BrawlerVfx.Move(root, PunchVfx.Style.Dash, from, aim, dashed, 0.35f, PunchColor);
                CoopFx.BrawlerMove(run, PunchVfx.Style.Dash, from, aim, dashed, 0.35f, PunchColor);
            }
            if (victim == null) yield break;

            // The uppercut: the glove scoops up through the victim; the blow lands when it connects.
            Color color = PunchColor;
            Player.Occupy(BrawlerVfx.UppercutTime * 0.5f);
            Player.Protect(BrawlerVfx.UppercutTime * 0.5f);
            BrawlerVfx.Move(root, PunchVfx.Style.Uppercut, victim.transform.position, aim, 1f, 0.6f, color);
            CoopFx.BrawlerMove(run, PunchVfx.Style.Uppercut, victim.transform.position, aim, 1f, 0.6f, color);
            yield return new WaitForSeconds(BrawlerVfx.UppercutTime * PunchVfx.UppercutImpact);
            if (!run.IsPlaying || victim == null || victim.Health <= 0) yield break;
            ScreenFx.Shake(0.32f, 0.25f);
            ScreenFx.Flash(new Color(1f, 0.95f, 0.85f, 0.14f), 0.08f);
            CombatDamage.Apply(Player, victim, Player.Damage * 3 + rank, DamageElement.Physical, transform.position, 0f);
            if (victim == null || victim.Health <= 0 || victim.Boss != null) yield break;

            // Launched: it sails up and over in an arc and crashes into whatever stands behind it.
            victim.Stun(LaunchTime + 0.4f);
            Vector2 start = victim.transform.position;
            Vector2 landing = run.Map.Move(start, aim * HaymakerLaunch, victim.MoveRadius);
            for (float t = 0f; t < LaunchTime; t += Time.deltaTime)
            {
                if (!run.IsPlaying || victim == null || victim.Health <= 0) yield break;
                float progress = t / LaunchTime;
                victim.transform.position = Vector2.Lerp(start, landing, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * LaunchHeight;
                if (Time.frameCount % 2 == 0) HeroVfx.Sparks(root, victim.transform.position, Color.Lerp(color, Color.white, 0.5f), 1, 1.2f, 0.25f, -aim, 60f, 0.8f);
                yield return null;
            }
            if (victim == null) yield break;
            victim.transform.position = landing;
            HeroVfx.Pulse(root, landing, HaymakerSplash, color, 0.3f);
            HeroVfx.Sparks(root, landing, Dust, 14, 4f, 0.35f, null, 360f, 1f);
            CoopFx.Pulse(run, landing, HaymakerSplash, color, 0.3f);
            ScreenFx.Shake(0.18f, 0.15f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy != victim && enemy.Health > 0 && Vector2.Distance(landing, enemy.transform.position) <= HaymakerSplash + enemy.HitRadius)
                    CombatDamage.Apply(Player, enemy, Player.Damage * 2 + rank - 1, DamageElement.Physical, landing, 1.5f);
        }

        /// <summary>
        /// Suplex: grabs the nearest enemy and heaves it up and over a short way toward the cursor, slamming it down and
        /// hurting everything where it lands.
        /// </summary>
        private IEnumerator Suplex(DungeonEnemy victim, int rank)
        {
            var run = Player.Run;
            Vector2 start = victim.transform.position;
            Vector2 toCursor = Player.CursorPoint - (Vector2)transform.position;
            Vector2 throwAim = toCursor.sqrMagnitude > 0.0001f ? toCursor.normalized : Player.AimDirection;
            Vector2 landing = PlayerAbilities.FindGroundLanding(run.Map, transform.position, throwAim, SuplexThrow);
            Player.Occupy(SuplexTime + 0.05f);
            Player.Protect(SuplexTime + 0.1f);
            const float height = 1.4f;
            for (float t = 0f; t < SuplexTime; t += Time.deltaTime)
            {
                if (!run.IsPlaying || victim == null || victim.Health <= 0) yield break;
                float progress = t / SuplexTime;
                victim.transform.position = Vector2.Lerp(start, landing, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * height;
                yield return null;
            }
            if (victim == null) yield break;
            victim.transform.position = landing;
            CombatVfx.Ring(run.ProjectileRoot, landing, SuplexRadius, SuplexColor, 0.4f);
            HeroVfx.Sparks(run.ProjectileRoot, landing, SuplexColor, 20, 5f, 0.4f);
            CoopFx.Ring(run, landing, SuplexRadius, SuplexColor, 0.4f);
            ScreenFx.Shake(0.25f, 0.25f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(landing, enemy.transform.position) <= SuplexRadius + enemy.HitRadius)
                    CombatDamage.Apply(Player, enemy, Player.Damage * 3 + rank, DamageElement.Physical, landing, enemy == victim ? 0f : 1.5f);
            if (victim != null && victim.Health > 0) victim.Stun(0.5f);
        }

        /// <summary>The nearest non-guardian enemy within reach (guardians are too heavy to throw).</summary>
        private DungeonEnemy FindSuplexTarget()
        {
            DungeonEnemy best = null;
            float bestDistance = SuplexReach;
            foreach (var enemy in Player.Run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.Boss != null) continue;
                float distance = Vector2.Distance(transform.position, enemy.transform.position) - enemy.HitRadius;
                if (distance <= bestDistance) { best = enemy; bestDistance = distance; }
            }
            return best;
        }

        /// <summary>Sandwich Special: Knuckle Sandwich's blow sends a shockwave rolling on past its box.</summary>
        private IEnumerator SandwichShockwave(Vector2 from, Vector2 aim, float halfWidth, int damage)
        {
            var run = Player.Run;
            var hit = new HashSet<DungeonEnemy>();
            const float Distance = 5f, Duration = 0.4f;
            for (float t = 0f; t <= Duration; t += Time.deltaTime)
            {
                if (!run.IsPlaying) yield break;
                Vector2 front = from + aim * Distance * (t / Duration);
                HeroVfx.Sparks(run.ProjectileRoot, front, BrawlerAttack.Glove, 3, 2.5f, 0.2f, aim, 90f);
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || hit.Contains(enemy)
                        || !InRectangle((Vector2)enemy.transform.position - from, aim, Distance * (t / Duration), halfWidth, enemy.HitRadius)) continue;
                    hit.Add(enemy);
                    CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, from, 2f);
                }
                yield return null;
            }
            CoopFx.Punch(run, from, aim, Distance, halfWidth, BrawlerAttack.Glove);
        }
    }
}

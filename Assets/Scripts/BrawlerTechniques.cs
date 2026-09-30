using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Brawler's expansion techniques: Thunder Clap, Haymaker Dash and Suplex, plus Sandwich Special's shockwave.</summary>
    public sealed partial class BrawlerAttack
    {
        public const float ClapRange = 4f, ClapCone = 90f, ClapStun = 0.75f;
        public const float HaymakerDistance = 4f, HaymakerLaunch = 3f, HaymakerSplash = 1.3f;
        public const float SuplexReach = 2.5f, SuplexRadius = 1.6f;

        /// <summary>Thunder Clap: a cone shockwave that throws enemies back and stuns them.</summary>
        private void ThunderClap(Vector2 aim, int rank)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            Color color = new Color(0.8f, 0.9f, 1f);
            HeroVfx.Slash(run.ProjectileRoot, origin, aim, ClapRange, ClapCone, color, 0.3f);
            CoopFx.Slash(run, origin, aim, ClapRange, ClapCone, color);
            HeroVfx.Pulse(run.ProjectileRoot, origin, 1.2f, Color.white, 0.25f);
            ScreenFx.Shake(0.18f, 0.2f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || !SwordAttack.ContainsTarget(enemy.transform.position - transform.position, aim, ClapRange + enemy.HitRadius, ClapCone)
                    || !run.HasLineOfSight(origin, enemy.transform.position)) continue;
                CombatDamage.Apply(Player, enemy, Player.Damage * 2 + rank - 1, DamageElement.Physical, origin, 2.5f);
                if (enemy != null && enemy.Health > 0) enemy.Stun(ClapStun);
            }
        }

        /// <summary>Haymaker Dash: a dash that ends in an uppercut, launching the first enemy into the ones behind it.</summary>
        private IEnumerator HaymakerDash(Vector2 aim, int rank)
        {
            var run = Player.Run;
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
                foreach (var enemy in run.Enemies)
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(transform.position, enemy.transform.position) <= enemy.HitRadius + 0.6f) { victim = enemy; break; }
                if (Vector2.Distance(before, transform.position) < 0.01f) break;
                yield return null;
            }
            CombatVfx.GlowBolt(run.ProjectileRoot, from, transform.position, PunchColor);
            CoopFx.Bolt(run, from, transform.position, PunchColor, true);
            if (victim == null) yield break;
            DrawPunch(transform.position, aim, 1.2f, 0.5f, Color.Lerp(PunchColor, Color.white, 0.3f), 0.22f);
            CombatDamage.Apply(Player, victim, Player.Damage * 3 + rank, DamageElement.Physical, transform.position, 0f);
            if (victim == null || victim.Health <= 0 || victim.Boss != null) yield break;
            // Launched: it flies on and crashes into whatever stands behind it.
            Vector2 landing = run.Map.Move(victim.transform.position, aim * HaymakerLaunch, victim.MoveRadius);
            victim.transform.position = landing;
            HeroVfx.Pulse(run.ProjectileRoot, landing, HaymakerSplash, PunchColor, 0.3f);
            CoopFx.Pulse(run, landing, HaymakerSplash, PunchColor, 0.3f);
            ScreenFx.Shake(0.15f, 0.15f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy != victim && enemy.Health > 0 && Vector2.Distance(landing, enemy.transform.position) <= HaymakerSplash + enemy.HitRadius)
                    CombatDamage.Apply(Player, enemy, Player.Damage * 2 + rank - 1, DamageElement.Physical, landing, 1.5f);
        }

        /// <summary>Suplex: grabs the nearest enemy and slams it down behind her, hurting everything where it lands.</summary>
        private IEnumerator Suplex(DungeonEnemy victim, Vector2 aim, int rank)
        {
            var run = Player.Run;
            Vector2 start = victim.transform.position;
            Vector2 landing = PlayerAbilities.FindGroundLanding(run.Map, transform.position, -aim, 1.6f);
            Player.Occupy(0.35f);
            Player.Protect(0.4f);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                if (!run.IsPlaying || victim == null || victim.Health <= 0) yield break;
                float progress = t / 0.3f;
                victim.transform.position = Vector2.Lerp(start, landing, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * 1.4f;
                yield return null;
            }
            if (victim == null) yield break;
            victim.transform.position = landing;
            Color color = new Color(0.85f, 0.7f, 0.5f);
            CombatVfx.Ring(run.ProjectileRoot, landing, SuplexRadius, color, 0.4f);
            HeroVfx.Sparks(run.ProjectileRoot, landing, color, 20, 5f, 0.4f);
            CoopFx.Ring(run, landing, SuplexRadius, color, 0.4f);
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

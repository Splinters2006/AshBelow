using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Demoness's expansion arts: Wing Dash, Soul Siphon and Nightmare Snap, plus Torment's tally.</summary>
    public sealed partial class DemonessAttack
    {
        public const float WingDashDistance = 4.5f, WingDashParalysis = 2f, SiphonRadius = 5f, SiphonTime = 5f, SnapRadius = 4f;
        /// <summary>How much remaining hold makes Nightmare Snap's tether flare at full width.</summary>
        private const float SnapTetherFullAt = 1f;
        private readonly Dictionary<DungeonEnemy, int> torment = new Dictionary<DungeonEnemy, int>();

        /// <summary>Torment: each vital stab on the same enemy adds +1 more than the last (no limit).</summary>
        private int TormentBonus(DungeonEnemy enemy)
        {
            if (Player.Powerups.Count(PowerupType.Torment) == 0 || enemy == null) return 0;
            int stabs = torment.TryGetValue(enemy, out int count) ? count : 0;
            torment[enemy] = stabs + 1;
            return stabs;
        }

        /// <summary>Wing Dash: her demon wings beat once and she darts forward, paralysing every enemy she passes through.</summary>
        private IEnumerator WingDash(Vector2 aim, float distance)
        {
            var run = Player.Run;
            Vector2 from = transform.position;
            var struck = new HashSet<DungeonEnemy>();
            const float Speed = 16f;
            Player.Occupy(distance / Speed);
            Player.Protect(distance / Speed + 0.2f);
            HeroVfx.Pulse(run.ProjectileRoot, from, 1.2f, Violet, 0.3f);
            // Her wings spread and beat through the dash, on every machine.
            float flight = distance / Speed + 0.1f;
            WingFlapVfx.Play(run.ProjectileRoot, transform, flight, aim);
            CoopFx.Wings(run, flight, aim);
            for (float travelled = 0f; travelled < distance; )
            {
                if (!run.IsPlaying || Player.Health <= 0) yield break;
                Vector2 before = transform.position;
                transform.position = run.Map.Move(before, aim * Mathf.Min(Speed * Time.deltaTime, distance - travelled));
                float step = Vector2.Distance(before, transform.position);
                travelled += step;
                HeroVfx.Sparks(run.ProjectileRoot, transform.position, Abyss, 2, 1.5f, 0.3f, -aim, 90f, 0.8f);
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || struck.Contains(enemy) || Vector2.Distance(transform.position, enemy.transform.position) > enemy.HitRadius + 0.7f) continue;
                    struck.Add(enemy);
                    CombatDamage.Apply(Player, enemy, Player.Damage, DamageElement.Demonic, transform.position, 0.3f);
                    ParalyzeCounted(enemy, WingDashParalysis);
                }
                if (step < 0.01f) break;
                yield return null;
            }
        }

        /// <summary>Soul Siphon: for a few seconds she drains immobilized enemies nearby, healing 1 HP per enemy each second.</summary>
        private IEnumerator SoulSiphon(float duration)
        {
            var run = Player.Run;
            var vfx = SoulSiphonVfx.Play(run, transform, duration, SiphonRadius);
            CoopFx.SoulSiphon(run, duration, SiphonRadius);
            for (float t = 0f; t < duration; t += 1f)
            {
                yield return new WaitForSeconds(1f);
                if (!run.IsPlaying || Player.Health <= 0) yield break;
                int drained = 0;
                Vector2 at = transform.position;
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || !enemy.IsImmobilized || Vector2.Distance(at, enemy.transform.position) > SiphonRadius + enemy.HitRadius) continue;
                    drained++;
                    HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, Pale, 6, 2.5f, 0.3f, at - (Vector2)enemy.transform.position, 60f);
                    CombatDamage.Apply(Player, enemy, Player.Damage, DamageElement.Demonic, at, 0f);
                }
                if (drained == 0) continue;
                Player.Heal(drained);
                if (vfx != null) vfx.Flare(drained);
            }
        }

        /// <summary>
        /// Nightmare Snap (Imp Summon, reworked): every hold on every enemy nearby (paralysis, freeze, stun or root) snaps at
        /// once; each takes demonic damage that grows with how long it still had to be held. Nothing happens if no one nearby is immobilized.
        /// </summary>
        private bool NightmareSnap(int rank)
        {
            var run = Player.Run;
            Vector2 at = transform.position;
            var victims = new List<DungeonEnemy>();
            foreach (var enemy in run.Enemies)
                if (enemy != null && enemy.Health > 0 && enemy.IsImmobilized && Vector2.Distance(at, enemy.transform.position) <= SnapRadius + enemy.HitRadius)
                    victims.Add(enemy);
            if (victims.Count == 0) return false;
            NightmareSnapVfx.Snap(run.ProjectileRoot, at, SnapRadius);
            CoopFx.NightmareSnap(run, at, SnapRadius);
            ScreenFx.Shake(0.2f, 0.2f);
            foreach (var enemy in victims)
            {
                float remaining = enemy.ConsumeHolds();
                int damage = Player.Damage * (2 + rank - 1 + Mathf.CeilToInt(remaining * 3f));
                // Each victim's shackle snaps; the longer it had left to be held, the wider the nightmare's eye.
                float strength = remaining / SnapTetherFullAt;
                NightmareSnapVfx.Tether(run.ProjectileRoot, at, enemy.transform.position, strength);
                CoopFx.SnapTether(run, at, enemy.transform.position, strength);
                HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, Pale, 12, 4.5f, 0.35f);
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Demonic, at, 0.8f);
            }
            return true;
        }
    }
}

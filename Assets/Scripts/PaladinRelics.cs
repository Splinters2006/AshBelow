using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Paladin's timed relic abilities: Judgment's holy smite after a windup, and Sanctuary's moving bubble.</summary>
    public sealed class PaladinRelics : MonoBehaviour
    {
        public const float JudgmentRadius = 3f, JudgmentWindup = 0.7f, JudgmentRange = 7f;
        public const float SanctuaryRadius = 3f, SanctuaryDuration = 4f;
        public DungeonPlayer Player { get; set; }
        private HolyBubble sanctuary;
        /// <summary>The Paladin cannot attack while holding up their Sanctuary.</summary>
        public bool IsSanctuaryActive => sanctuary != null && sanctuary.IsActive;

        /// <summary>Marks the ground at <paramref name="center"/> (the aimed spot); holy light smites it once the windup ends.</summary>
        public void Judgment(Vector2 center, int damage, float slow)
        {
            HolyLightVfx.Play(Player.Run.ProjectileRoot, center, JudgmentRadius, JudgmentWindup);
            CoopFx.Holy(Player.Run, center, JudgmentRadius, JudgmentWindup);
            StartCoroutine(Smite(center, damage, slow));
        }

        private IEnumerator Smite(Vector2 center, int damage, float slow)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            yield return new WaitForSeconds(JudgmentWindup);
            if (!run.IsPlaying || root != run.ProjectileRoot) yield break;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || Vector2.Distance(center, enemy.transform.position) > JudgmentRadius + enemy.HitRadius
                    || !run.HasLineOfSight(center, enemy.transform.position)) continue;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, center);
                if (enemy.Health > 0 && slow > 0f) enemy.Chill(slow);
            }
            // Blessed Judgment: the light also blesses every hero near the mark.
            if (Player.Powerups.Count(PowerupType.BlessedJudgment) == 0) yield break;
            float duration = PaladinAttack.BlessingDuration + Player.Permanent.BlessingDuration + Player.Powerups.Count(PowerupType.PatientFaith);
            foreach (var ally in FindObjectsByType<DungeonPlayer>())
                if (ally.Run == run && ally.Health > 0 && Vector2.Distance(center, ally.transform.position) <= JudgmentRadius)
                    ally.Blessing.Apply(PaladinAttack.BlessingDamage, duration, Player);
            run.Coop?.SupportAllies(center, JudgmentRadius, SupportKind.Bless, PaladinAttack.BlessingDamage, duration);
        }

        /// <summary>
        /// A bubble of holy light around the Paladin that moves with them, on every machine. It destroys every projectile
        /// inside or crossing it, shoves enemies out as it forms and slows those that wander back in.
        /// </summary>
        public HolyBubble Sanctuary(float radius, float duration)
        {
            Vector2 center = transform.position;
            CoopFx.Sanctuary(Player.Run, center, radius, duration);
            Player.Weapon?.Hide();
            sanctuary = HolyBubble.Sanctuary(Player.Run, center, radius, duration, transform);
            return sanctuary;
        }

        /// <summary>Recasting Sanctuary drops it early, on every machine.</summary>
        public void EndSanctuary()
        {
            if (!IsSanctuaryActive) return;
            HolyBubble.EndFollowing(transform);
            CoopFx.SanctuaryEnd(Player.Run);
            // Healing Sanctuary: dropping the bubble heals everyone who was inside.
            if (Player.Powerups.Count(PowerupType.HealingSanctuary) == 0) return;
            float radius = SanctuaryRadius + Player.Powerups.Count(PowerupType.SanctuarySize) * 0.5f;
            Player.Heal(1);
            Player.Run.Coop?.SupportAllies(transform.position, radius, SupportKind.Heal, 1, 0f);
            HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, radius * 0.8f, AbilityCatalog.Gold, 18, 1f);
        }
    }
}

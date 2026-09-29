using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Paladin's timed relic abilities: Judgment's holy smite after a windup, and Sanctuary's bubble.</summary>
    public sealed class PaladinRelics : MonoBehaviour
    {
        public const float JudgmentRadius = 3f, JudgmentWindup = 0.7f;
        public const float SanctuaryRadius = 3f, SanctuaryDuration = 4f;
        public DungeonPlayer Player { get; set; }

        /// <summary>Marks the ground where the Paladin stands; holy light smites it once the windup ends.</summary>
        public void Judgment(int damage, float slow)
        {
            Vector2 center = transform.position;
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
        }

        /// <summary>A bubble of holy light that stops every projectile crossing its edge, on every machine.</summary>
        public HolyBubble Sanctuary(float duration)
        {
            Vector2 center = transform.position;
            CoopFx.Sanctuary(Player.Run, center, SanctuaryRadius, duration);
            return HolyBubble.Sanctuary(Player.Run.ProjectileRoot, center, SanctuaryRadius, duration);
        }
    }
}

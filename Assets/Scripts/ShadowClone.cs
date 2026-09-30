using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Shadow Clone: for a while after it is cast, every backstab the Assassin lands summons a shadow copy of her behind
    /// the victim that lunges in and backstabs it again. Each clone stab adds a Sharpened Dagger stack.
    /// </summary>
    public sealed class ShadowClone : MonoBehaviour
    {
        public const float Duration = 7.5f, Delay = 0.25f;
        private DungeonPlayer player;
        private float until;
        /// <summary>True while a clone's own stab resolves, so it does not summon clones of its own.</summary>
        private static bool striking;
        /// <summary>A clone's stab is resolving; it sharpens the dagger itself, so the hit must not count it again.</summary>
        public static bool IsStriking => striking;

        public static void Activate(DungeonPlayer player, float duration)
        {
            var clone = player.GetComponent<ShadowClone>() ?? player.gameObject.AddComponent<ShadowClone>();
            clone.player = player;
            clone.until = Time.time + duration;
            HeroVfx.Pulse(player.Run.ProjectileRoot, player.transform.position, 1.2f, ShadowstepVfx.Violet, 0.4f);
        }

        /// <summary>A backstab landed: while the clone art is active, a clone steps out behind the victim and stabs again.</summary>
        public static void OnBackstab(DungeonPlayer player, DungeonEnemy enemy, int damage)
        {
            if (striking || enemy == null || enemy.Health <= 0) return;
            var clone = player.GetComponent<ShadowClone>();
            if (clone == null || Time.time >= clone.until) return;
            clone.StartCoroutine(clone.Stab(enemy, damage));
        }

        private IEnumerator Stab(DungeonEnemy enemy, int damage)
        {
            var run = player.Run;
            Vector2 behind = (Vector2)enemy.transform.position - enemy.Facing.Direction * 0.9f;
            // A shadow copy of the Assassin herself steps out of the smoke behind the victim and lunges in.
            ShadowCloneVfx.Play(run.ProjectileRoot, player.transform, behind, enemy.transform.position, Delay);
            CoopFx.ShadowClone(run, behind, enemy.transform.position, Delay);
            yield return new WaitForSeconds(Delay);
            if (enemy == null || enemy.Health <= 0 || !run.IsPlaying) yield break;
            striking = true;
            // From behind, so it is itself a backstab.
            try { CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, (Vector2)enemy.transform.position - enemy.Facing.Direction, 0.3f); }
            finally { striking = false; }
            // Every clone's stab sharpens the Assassin's dagger like one of her own backstabs.
            player.Mechanic?.OnBackstab();
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Shadow Clone: for a while after it is cast, every backstab the Assassin lands summons a shadow clone behind the
    /// victim that backstabs it again.
    /// </summary>
    public sealed class ShadowClone : MonoBehaviour
    {
        public const float Duration = 7.5f, Delay = 0.2f;
        private DungeonPlayer player;
        private float until;
        /// <summary>True while a clone's own stab resolves, so it does not summon clones of its own.</summary>
        private static bool striking;

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
            Vector2 behind = (Vector2)enemy.transform.position - enemy.Facing.Direction * 0.7f;
            var shade = DungeonVisuals.Create("Shadow clone", run.ProjectileRoot, behind, Vector2.one * 0.65f, new Color(0.35f, 0.2f, 0.55f, 0.75f), 4);
            shade.sprite = HeroSprites.Body(WeaponType.Daggers);
            shade.gameObject.AddComponent<FadingSprite>().Duration = Delay + 0.35f;
            ShadowstepVfx.Puff(run.ProjectileRoot, behind);
            CoopFx.Shadowstep(run, behind, behind);
            yield return new WaitForSeconds(Delay);
            if (enemy == null || enemy.Health <= 0 || !run.IsPlaying) yield break;
            striking = true;
            // From behind, so it is itself a backstab.
            try { CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, (Vector2)enemy.transform.position - enemy.Facing.Direction, 0.3f); }
            finally { striking = false; }
        }
    }
}

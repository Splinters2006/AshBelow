using UnityEngine;

namespace Slopgame
{
    /// <summary>Lightning Storm: for a few seconds bolts strike down from above onto enemies around the Wizard.</summary>
    public sealed class LightningStorm : MonoBehaviour
    {
        public const float Duration = 3f, Interval = 0.35f, Range = 8f, StrikeRadius = 1.1f, Warning = 0.3f;
        private DungeonPlayer player;
        private float until, nextStrike;
        private int damage;

        public static void Call(DungeonPlayer player, float duration, int damage)
        {
            var storm = new GameObject("Lightning storm").AddComponent<LightningStorm>();
            storm.transform.SetParent(player.Run.ProjectileRoot, false);
            storm.player = player;
            storm.until = Time.time + duration;
            storm.damage = damage;
            ScreenFx.Flash(new Color(0.7f, 0.85f, 1f, 0.12f), 0.2f);
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null || Time.time >= until) { Destroy(gameObject); return; }
            if (!run.IsPlaying || Time.time < nextStrike) return;
            nextStrike = Time.time + Interval;
            // A bolt for a random enemy in range, else a random spot nearby.
            Vector2 hero = player.transform.position;
            DungeonEnemy target = null;
            int seen = 0;
            foreach (var enemy in run.Enemies)
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(hero, enemy.transform.position) <= Range && Random.Range(0, ++seen) == 0) target = enemy;
            Vector2 spot = target != null ? (Vector2)target.transform.position
                : PlayerAbilities.FindGroundLanding(run.Map, hero, Random.insideUnitCircle.normalized, Random.Range(1f, Range * 0.6f));
            StartCoroutine(Strike(run, spot));
        }

        private System.Collections.IEnumerator Strike(DungeonRun run, Vector2 spot)
        {
            CombatVfx.Ring(run.ProjectileRoot, spot, StrikeRadius, FlameMesh.Alpha(CombatDamage.ShockColor, 0.6f), Warning);
            CoopFx.Ring(run, spot, StrikeRadius, FlameMesh.Alpha(CombatDamage.ShockColor, 0.6f), Warning);
            yield return new WaitForSeconds(Warning);
            if (!run.IsPlaying || player == null) yield break;
            Vector2 sky = spot + Vector2.up * 7f;
            CombatVfx.GlowBolt(run.ProjectileRoot, sky, spot, CombatDamage.ShockColor);
            CoopFx.Bolt(run, sky, spot, CombatDamage.ShockColor, true);
            HeroVfx.Sparks(run.ProjectileRoot, spot, CombatDamage.ShockColor, 12, 4.5f, 0.3f, null, 360f, 1.2f);
            ScreenFx.Shake(0.08f, 0.1f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(spot, enemy.transform.position) <= StrikeRadius + enemy.HitRadius)
                    CombatDamage.Apply(player, enemy, damage, DamageElement.Lightning, spot, 0.3f);
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>Ball Lightning: a slow orb that drifts forward for a few seconds, zapping every enemy it passes.</summary>
    public sealed class BallLightning : MonoBehaviour
    {
        public const float Speed = 2.2f, Duration = 4f, Reach = 1.4f, ZapInterval = 0.4f;
        private DungeonPlayer player;
        private Vector2 direction;
        private float until, nextZap;
        private int damage;

        public static void Launch(DungeonPlayer player, Vector2 aim, float duration, int damage)
        {
            var orb = DungeonVisuals.Create("Ball lightning", player.Run.ProjectileRoot, player.transform.position, Vector2.one * 0.55f, CombatDamage.ShockColor, 7);
            var ball = orb.gameObject.AddComponent<BallLightning>();
            ball.player = player;
            ball.direction = aim.normalized;
            ball.until = Time.time + duration;
            ball.damage = damage;
            CombatVfx.Trail(orb.gameObject, new Color(0.75f, 0.9f, 1f, 0.5f), 0.3f, 0.3f);
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null || Time.time >= until) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            Vector2 position = transform.position;
            Vector2 next = position + direction * Speed * Time.deltaTime;
            if (run.Map.CanStand(next, 0.2f)) transform.position = next;
            transform.localScale = Vector3.one * (0.5f + 0.08f * Mathf.Sin(Time.time * 30f));
            if (Time.time < nextZap) return;
            nextZap = Time.time + ZapInterval;
            HeroVfx.Sparks(run.ProjectileRoot, position, CombatDamage.ShockColor, 4, 2.5f, 0.2f);
            CoopFx.Pulse(run, position, 0.6f, CombatDamage.ShockColor, 0.2f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || Vector2.Distance(position, enemy.transform.position) > Reach + enemy.HitRadius) continue;
                CombatVfx.Bolt(run.ProjectileRoot, position, enemy.transform.position, CombatDamage.ShockColor);
                CoopFx.Bolt(run, position, enemy.transform.position, CombatDamage.ShockColor);
                // Every Wizard ability sets off its element.
                CombatDamage.Apply(player, enemy, damage, DamageElement.Lightning, position, 0.2f, guaranteedEffect: true);
            }
        }
    }
}

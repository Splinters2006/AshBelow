using UnityEngine;

namespace Slopgame
{
    /// <summary>Whirlwind: the Knight spins with his sword for a while, cutting everything around him; he can still walk.</summary>
    public sealed class Whirlwind : MonoBehaviour
    {
        public const float Radius = 1.9f, Tick = 0.25f, Duration = 2f;
        private static readonly Color Steel = new Color(0.55f, 1f, 0.9f);
        private DungeonPlayer player;
        private float until, nextTick, angle;
        private int damage;

        public static void Spin(DungeonPlayer player, float duration, int damage)
        {
            var spin = player.GetComponent<Whirlwind>() ?? player.gameObject.AddComponent<Whirlwind>();
            spin.player = player;
            spin.damage = damage;
            spin.until = Time.time + duration;
            spin.nextTick = Time.time;
        }

        public bool IsSpinning => Time.time < until;

        private void Update()
        {
            if (player == null || !player.Run.IsPlaying || player.Health <= 0 || Time.time >= until) return;
            angle += Time.deltaTime * 1080f;
            if (Time.time < nextTick) return;
            nextTick += Tick;
            var run = player.Run;
            Vector2 center = player.transform.position;
            Vector2 aim = Quaternion.Euler(0, 0, angle) * Vector2.right;
            HeroVfx.Slash(run.ProjectileRoot, center, aim, Radius, 200f, Steel, 0.18f);
            CoopFx.Slash(run, center, aim, Radius, 200f, Steel);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= Radius + enemy.HitRadius
                    && run.HasLineOfSight(center, enemy.transform.position))
                    CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, center, 0.4f);
        }
    }
}

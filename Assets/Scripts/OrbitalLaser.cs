using UnityEngine;

namespace Slopgame
{
    /// <summary>Orbital Laser: a beam from the sky that follows the Augment's cursor for a few seconds, burning what it touches.</summary>
    public sealed class OrbitalLaser : MonoBehaviour
    {
        public const float Duration = 3f, Radius = 0.9f, Speed = 7f, Tick = 0.2f;
        private DungeonPlayer player;
        private float until, nextTick;
        private int damage;

        public static void Call(DungeonPlayer player, Vector2 start, float duration, int damage)
        {
            var laser = new GameObject("Orbital laser").AddComponent<OrbitalLaser>();
            laser.transform.SetParent(player.Run.ProjectileRoot, false);
            laser.transform.position = start;
            laser.player = player;
            laser.until = Time.time + duration;
            laser.damage = damage;
            ScreenFx.Flash(FlameMesh.Alpha(CyborgAttack.Plasma, 0.15f), 0.2f);
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null || Time.time >= until) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            transform.position = Vector2.MoveTowards(transform.position, player.CursorPoint, Speed * Time.deltaTime);
            if (Time.time < nextTick) return;
            nextTick = Time.time + Tick;
            Vector2 at = transform.position;
            Vector2 sky = at + Vector2.up * 8f;
            CombatVfx.GlowBolt(run.ProjectileRoot, sky, at, CyborgAttack.Plasma);
            CoopFx.Bolt(run, sky, at, CyborgAttack.Plasma, true);
            CombatVfx.Ring(run.ProjectileRoot, at, Radius, CyborgAttack.Core, Tick);
            HeroVfx.Sparks(run.ProjectileRoot, at, CyborgAttack.Plasma, 5, 3f, 0.2f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= Radius + enemy.HitRadius)
                    CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, at + Vector2.up, 0f);
        }
    }
}

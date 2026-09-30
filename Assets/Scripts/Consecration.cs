using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Consecration: sanctified ground around the Paladin for a few seconds. Enemies inside take holy damage every second;
    /// heroes inside are blessed.
    /// </summary>
    public sealed class Consecration : MonoBehaviour
    {
        public const float Radius = 2.6f, Duration = 5f, Interval = 1f;
        private DungeonPlayer player;
        private float until, nextTick;
        private int damage;

        public static void Sanctify(DungeonPlayer player, float duration, int damage)
        {
            var ground = new GameObject("Consecration").AddComponent<Consecration>();
            ground.transform.SetParent(player.Run.ProjectileRoot, false);
            ground.transform.position = player.transform.position;
            ground.player = player;
            ground.until = Time.time + duration;
            ground.damage = damage;
            HeroVfx.Pulse(player.Run.ProjectileRoot, player.transform.position, Radius, AbilityCatalog.Gold, 0.5f);
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null || Time.time >= until) { Destroy(gameObject); return; }
            if (!run.IsPlaying || Time.time < nextTick) return;
            nextTick = Time.time + Interval;
            Vector2 center = transform.position;
            CombatVfx.Ring(run.ProjectileRoot, center, Radius, FlameMesh.Alpha(AbilityCatalog.Gold, 0.7f), Interval);
            CoopFx.Ring(run, center, Radius, FlameMesh.Alpha(AbilityCatalog.Gold, 0.7f), Interval);
            HeroVfx.Motes(run.ProjectileRoot, center, Radius * 0.9f, AbilityCatalog.Gold, 12, 1f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= Radius + enemy.HitRadius)
                    CombatDamage.Apply(player, enemy, damage, DamageElement.Holy, center, 0f);
            foreach (var ally in FindObjectsByType<DungeonPlayer>())
                if (ally.Run == run && ally.Health > 0 && Vector2.Distance(center, ally.transform.position) <= Radius)
                    ally.Blessing.Apply(PaladinAttack.BlessingDamage, Interval + 0.3f, player);
            run.Coop?.SupportAllies(center, Radius, SupportKind.Bless, PaladinAttack.BlessingDamage, Interval + 0.3f);
        }
    }
}

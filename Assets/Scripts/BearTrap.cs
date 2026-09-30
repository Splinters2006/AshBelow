using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>Bear Trap: set at the cursor (two at most). The first enemy to step in is rooted and hurt; the trap is spent.</summary>
    public sealed class BearTrap : MonoBehaviour
    {
        public const int MaxTraps = 2;
        public const float Range = 5f, ArmTime = 0.4f, TriggerRadius = 0.55f, Lifetime = 40f;
        private static readonly List<BearTrap> set = new List<BearTrap>();
        private static readonly Color Iron = new Color(0.62f, 0.62f, 0.68f);
        private DungeonPlayer player;
        private int damage;
        private float hold, armedAt, expiresAt;

        public static void Set(DungeonPlayer player, Vector2 at, int damage, float hold)
        {
            set.RemoveAll(trap => trap == null);
            // Setting a third trap springs the oldest.
            while (set.Count >= MaxTraps) { if (set[0] != null) Destroy(set[0].gameObject); set.RemoveAt(0); }
            var run = player.Run;
            var plate = DungeonVisuals.Create("Bear trap", run.ProjectileRoot, at, new Vector2(0.6f, 0.2f), Iron, 2);
            for (int i = -2; i <= 2; i++)
            {
                var tooth = DungeonVisuals.Create("Trap tooth", plate.transform, at + new Vector2(i * 0.12f, 0.12f), new Vector2(0.05f, 0.14f), Color.white, 3);
                tooth.transform.localScale = new Vector3(0.08f, 0.7f, 1f);
            }
            var trap = plate.gameObject.AddComponent<BearTrap>();
            trap.player = player;
            trap.damage = damage;
            trap.hold = hold;
            trap.armedAt = Time.time + ArmTime;
            trap.expiresAt = Time.time + Lifetime;
            set.Add(trap);
            CoopFx.Ring(run, at, 0.6f, Iron, 0.3f);
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null || Time.time >= expiresAt) { Destroy(gameObject); return; }
            if (!run.IsPlaying || Time.time < armedAt) return;
            Vector2 at = transform.position;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.Boss != null || Vector2.Distance(at, enemy.transform.position) > TriggerRadius + enemy.HitRadius * 0.5f) continue;
                enemy.Root(hold);
                CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, at, 0f);
                HeroVfx.Sparks(run.ProjectileRoot, at, Iron, 12, 4f, 0.3f);
                CombatVfx.Ring(run.ProjectileRoot, at, 0.7f, Color.white, 0.25f);
                CoopFx.Ring(run, at, 0.7f, Color.white, 0.25f);
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy() => set.Remove(this);
    }
}

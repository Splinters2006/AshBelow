using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class SpellProjectile : MonoBehaviour
    {
        private DungeonPlayer player;
        private Vector2 direction;
        private int damage, pierces;
        private float remaining, radius;
        private DamageElement element;
        private Color color;
        private readonly HashSet<DungeonEnemy> hits = new HashSet<DungeonEnemy>();
        public bool IsSpent { get; private set; }

        public static SpellProjectile Spawn(DungeonPlayer player, Vector2 direction, int damage,
            DamageElement element, Color color, float range = 6f, float radius = 0f, int pierces = 0)
        {
            var sprite = DungeonVisuals.CreateEmberBolt(player.Run.ProjectileRoot, player.transform.position);
            sprite.color = color;
            var shot = sprite.gameObject.AddComponent<SpellProjectile>();
            shot.player = player;
            shot.direction = direction.normalized;
            shot.damage = damage;
            shot.element = element;
            shot.color = color;
            shot.remaining = range;
            shot.radius = radius;
            shot.pierces = pierces;
            shot.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            return shot;
        }

        private void Update() { Advance(Time.deltaTime); }

        public void Advance(float deltaTime)
        {
            if (IsSpent || !player.Run.IsPlaying || deltaTime <= 0f) return;
            float distance = Mathf.Min(10f * deltaTime, remaining);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + direction * (distance / steps);
                if (!player.Run.Map.CanStand(next, 0.1f)) { Explode(); return; }
                transform.position = next;
                for (int j = player.Run.Enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = player.Run.Enemies[j];
                    if (hits.Contains(enemy) || Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                    if (radius > 0f) { Explode(); return; }
                    hits.Add(enemy);
                    CombatDamage.Apply(player, enemy, damage, element, next - direction);
                    if (pierces-- <= 0) { Finish(); return; }
                }
            }
            remaining -= distance;
            if (remaining <= 0f) Explode();
        }

        private void Explode()
        {
            if (radius > 0f) player.Abilities.AreaAttack(transform.position, radius, damage, element, color);
            else CombatVfx.Ring(player.Run.ProjectileRoot, transform.position, 0.25f, color, 0.18f);
            Finish();
        }

        private void Finish() { IsSpent = true; gameObject.SetActive(false); Destroy(gameObject); }
    }
}

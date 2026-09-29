using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class SpellProjectile : MonoBehaviour
    {
        private DungeonPlayer player;
        private DungeonRun run;
        private bool ghost;
        private Vector2 direction;
        private int damage, pierces;
        private float remaining, radius, pulsePhase;
        private DamageElement element;
        private Color color;
        private readonly HashSet<DungeonEnemy> hits = new HashSet<DungeonEnemy>();
        public bool IsSpent { get; private set; }

        public static SpellProjectile Spawn(DungeonPlayer player, Vector2 direction, int damage,
            DamageElement element, Color color, float range = 6f, float radius = 0f, int pierces = 0)
        {
            CoopFx.Spell(player.Run, player.transform.position, direction, color, range, radius, pierces);
            var shot = Create(player.Run, player.transform.position, direction, color, range, radius, pierces);
            shot.player = player;
            shot.damage = damage;
            shot.element = element;
            return shot;
        }

        /// <summary>A teammate's spell: the same flight and pop, without damage; their area burst arrives as its own effect.</summary>
        public static SpellProjectile SpawnGhost(DungeonRun run, Vector2 position, Vector2 direction, Color color, float range, float radius, int pierces)
        {
            var shot = Create(run, position, direction, color, range, radius, pierces);
            shot.ghost = true;
            return shot;
        }

        private static SpellProjectile Create(DungeonRun run, Vector2 position, Vector2 direction, Color color, float range, float radius, int pierces)
        {
            var sprite = DungeonVisuals.CreateEmberBolt(run.ProjectileRoot, position);
            sprite.color = color;
            var shot = sprite.gameObject.AddComponent<SpellProjectile>();
            shot.run = run;
            shot.direction = direction.normalized;
            shot.color = color;
            shot.remaining = range;
            shot.radius = radius;
            shot.pierces = pierces;
            shot.pulsePhase = Random.value * Mathf.PI * 2f;
            shot.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            CombatVfx.Trail(shot.gameObject, shot.GlowColor, radius > 0f ? 0.3f : 0.18f, 0.16f);
            return shot;
        }

        // Plain white casts use the ember sprite's own colours, so glow orange like it.
        private Color GlowColor => color == Color.white ? new Color(1f, 0.55f, 0.18f) : color;

        private void Update()
        {
            Advance(Time.deltaTime);
            if (!IsSpent) transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.time * 30f + pulsePhase));
        }

        public void Advance(float deltaTime)
        {
            if (IsSpent || !run.IsPlaying || deltaTime <= 0f) return;
            float distance = Mathf.Min(10f * deltaTime, remaining);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.1f) || HolyBubble.Blocks(transform.position, next)) { Explode(); return; }
                transform.position = next;
                for (int j = run.Enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = run.Enemies[j];
                    if (hits.Contains(enemy) || Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                    if (radius > 0f) { Explode(); return; }
                    hits.Add(enemy);
                    if (!ghost) CombatDamage.Apply(player, enemy, damage, element, next - direction);
                    if (pierces-- <= 0) { Finish(); return; }
                }
            }
            remaining -= distance;
            if (remaining <= 0f) Explode();
        }

        private void Explode()
        {
            if (radius > 0f) { if (!ghost) player.Abilities.AreaAttack(transform.position, radius, damage, element, color); }
            else
            {
                CombatVfx.Ring(run.ProjectileRoot, transform.position, 0.25f, color, 0.18f);
                HeroVfx.Sparks(run.ProjectileRoot, transform.position, GlowColor, 8, 3f, 0.28f);
            }
            Finish();
        }

        private void Finish() { IsSpent = true; gameObject.SetActive(false); Destroy(gameObject); }
    }
}

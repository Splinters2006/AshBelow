using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class SpellProjectile : MonoBehaviour
    {
        private DungeonPlayer player;
        private DungeonRun run;
        private bool ghost, guaranteedEffect;
        private Vector2 direction;
        private int damage, pierces;
        private float remaining, radius, pulsePhase, baseScale = 1f;
        private DamageElement element;
        private Color color;
        private readonly HashSet<DungeonEnemy> hits = new HashSet<DungeonEnemy>();
        public bool IsSpent { get; private set; }

        /// <param name="origin">Where the spell starts; the caster's position by default (Wild Storm casts from its cloud).</param>
        /// <param name="guaranteedEffect">Skip the effect roll: every hit sets off its element (Wild Storm, Inferno Orb).</param>
        public static SpellProjectile Spawn(DungeonPlayer player, Vector2 direction, int damage,
            DamageElement element, Color color, float range = 6f, float radius = 0f, int pierces = 0, Vector2? origin = null,
            bool guaranteedEffect = false)
        {
            Vector2 from = origin ?? (Vector2)player.transform.position;
            CoopFx.Spell(player.Run, from, direction, color, range, radius, pierces);
            var shot = Create(player.Run, from, direction, color, range, radius, pierces);
            shot.player = player;
            shot.damage = damage;
            shot.element = element;
            shot.guaranteedEffect = guaranteedEffect;
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
            CombatVfx.Trail(shot.gameObject, shot.GlowColor, radius > 0f ? 0.45f : 0.18f, radius > 0f ? 0.28f : 0.16f);
            if (radius > 0f)
            {
                // The Inferno Orb is a big, roaring ball of fire rather than a bolt.
                shot.baseScale = 1.7f;
                InfernoVfx.Wreathe(run.ProjectileRoot, shot.transform, direction, 0.22f);
                HeroVfx.Sparks(run.ProjectileRoot, position, FlameMesh.Orange, 10, 4f, 0.3f, direction, 70f, 1.1f);
            }
            else if (shot.IsFireball)
            {
                // The Wizard's basic fireball: a smaller wreath of flame than the Inferno Orb, with a muzzle puff.
                shot.baseScale = 1.2f;
                InfernoVfx.Wreathe(run.ProjectileRoot, shot.transform, direction, 0.13f);
                HeroVfx.Sparks(run.ProjectileRoot, position + direction.normalized * 0.4f, FlameMesh.Orange, 5, 2.5f, 0.2f, direction, 50f, 0.8f);
            }
            return shot;
        }

        // Plain white casts use the ember sprite's own colours, so glow orange like it.
        private Color GlowColor => color == Color.white ? new Color(1f, 0.55f, 0.18f) : color;
        private bool IsFireball => color == Color.white && radius <= 0f;
        private float nextEmber;

        private void Update()
        {
            Advance(Time.deltaTime);
            if (!IsSpent && IsFireball && Time.time >= nextEmber)
            {
                // Sheds a couple of embers as it flies.
                nextEmber = Time.time + 0.05f;
                HeroVfx.Sparks(run.ProjectileRoot, transform.position, Color.Lerp(FlameMesh.Yellow, FlameMesh.Orange, Random.value), 1, 1.2f, 0.28f, -direction, 70f, 0.7f);
            }
            if (!IsSpent) transform.localScale = Vector3.one * baseScale * (1f + 0.12f * Mathf.Sin(Time.time * 30f + pulsePhase));
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
                    if (!ghost) CombatDamage.Apply(player, enemy, damage, element, next - direction, guaranteedEffect: guaranteedEffect);
                    if (pierces-- <= 0) { Finish(); return; }
                }
            }
            remaining -= distance;
            if (remaining <= 0f) Explode();
        }

        private void Explode()
        {
            if (radius > 0f)
            {
                InfernoVfx.Blast(run.ProjectileRoot, transform.position, radius);
                HeroVfx.Sparks(run.ProjectileRoot, transform.position, FlameMesh.Yellow, 24, 6.5f, 0.5f, null, 360f, 1.4f);
                if (!ghost)
                {
                    player.Abilities.AreaAttack(transform.position, radius, damage, element, color, guaranteedEffect: guaranteedEffect);
                    ScreenFx.Shake(0.28f, 0.28f);
                    ScreenFx.Flash(new Color(1f, 0.55f, 0.2f, 0.12f), 0.12f);
                }
            }
            else if (IsFireball)
            {
                InfernoVfx.Blast(run.ProjectileRoot, transform.position, 0.45f);
                HeroVfx.Sparks(run.ProjectileRoot, transform.position, GlowColor, 10, 3.5f, 0.3f);
            }
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

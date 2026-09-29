using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Knight's timed relic abilities: a slow, unstoppable Shield Rush, Earthshatter's five quake rings,
    /// and Aegis's bubble.
    /// </summary>
    public sealed class KnightRelics : MonoBehaviour
    {
        public const float RushDistance = 4.5f, RushDuration = 0.8f, RushWidth = 1f;
        public const int QuakeRings = 5;
        public const float QuakeInterval = 0.13f;
        private static readonly Color Dust = new Color(0.72f, 0.62f, 0.48f);
        public DungeonPlayer Player { get; set; }
        public bool IsRushing => rush != null;
        private Coroutine rush;

        public void ShieldRush(Vector2 aim, int damage) => rush = StartCoroutine(Rush(aim.normalized, damage));

        private IEnumerator Rush(Vector2 aim, int damage)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            Player.Occupy(RushDuration);
            Player.Protect(RushDuration + 0.2f);
            Player.Charge.Cancel();
            Vector2 from = transform.position;
            var hit = new HashSet<DungeonEnemy>();
            var shield = DungeonVisuals.Create("Rush shield", root, from, new Vector2(0.2f, 1.15f), AbilityCatalog.Ice, 7);
            var glow = DungeonVisuals.Create("Rush glow", shield.transform, from, new Vector2(2.4f, 1.3f), new Color(0.6f, 0.9f, 1f, 0.35f), 6);
            glow.transform.localPosition = Vector2.zero;
            HeroVfx.Pulse(root, from, 1.2f, AbilityCatalog.Ice, 0.3f);
            float speed = RushDistance / RushDuration, nextDust = 0f;
            for (float t = 0f; t < RushDuration; t += Time.deltaTime)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot || Player.Health <= 0) break;
                Vector2 before = transform.position;
                transform.position = run.Map.Move(before, aim * speed * Time.deltaTime);
                Vector2 now = transform.position;
                shield.transform.position = now + aim * 0.55f;
                shield.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                if (t >= nextDust)
                {
                    nextDust = t + 0.06f;
                    HeroVfx.Sparks(root, now - aim * 0.3f + Vector2.down * 0.3f, Dust, 4, 2.2f, 0.3f, -aim, 110f, 0.8f);
                }
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || hit.Contains(enemy) || enemy.Health <= 0
                        || Vector2.Distance(now + aim * 0.3f, enemy.transform.position) > RushWidth + enemy.HitRadius) continue;
                    hit.Add(enemy);
                    CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, now - aim, 2.6f);
                    if (enemy.Health > 0) enemy.Chill(1.2f);
                    ScreenFx.Shake(0.2f, 0.2f);
                    HeroVfx.Pulse(root, enemy.transform.position, 1f, AbilityCatalog.Ice, 0.25f);
                    CoopFx.Pulse(run, enemy.transform.position, 1f, AbilityCatalog.Ice, 0.25f);
                }
                // Slamming into a wall ends the charge early.
                if (Vector2.Distance(before, now) < speed * Time.deltaTime * 0.25f && t > 0.1f) { ScreenFx.Shake(0.15f, 0.15f); break; }
                yield return null;
            }
            if (shield != null) Destroy(shield.gameObject);
            Vector2 end = transform.position;
            CombatVfx.GlowBolt(root, from, end, AbilityCatalog.Ice);
            CoopFx.Bolt(run, from, end, AbilityCatalog.Ice, true);
            CombatVfx.Ring(root, end, 1.1f, AbilityCatalog.Ice, 0.35f);
            CoopFx.Ring(run, end, 1.1f, AbilityCatalog.Ice, 0.35f);
            rush = null;
        }

        /// <summary>Five rings ripple outward from where the Knight stomped; each enemy is struck by the first ring to reach it.</summary>
        public void Earthshatter(float radius, int damage, Color color, float slow) => StartCoroutine(Quake(transform.position, radius, damage, color, slow));

        private IEnumerator Quake(Vector2 center, float radius, int damage, Color color, float slow)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            var hit = new HashSet<DungeonEnemy>();
            for (int ring = 1; ring <= QuakeRings; ring++)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot) yield break;
                float r = radius * ring / QuakeRings;
                CombatVfx.Ring(root, center, r, color, 0.35f);
                HeroVfx.Pulse(root, center, r, Color.Lerp(color, Dust, 0.5f), 0.3f);
                CoopFx.Ring(run, center, r, color, 0.35f);
                CoopFx.Pulse(run, center, r, Color.Lerp(color, Dust, 0.5f), 0.3f);
                for (int i = 0; i < 5; i++)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    Vector2 spot = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    HeroVfx.Sparks(root, spot, Dust, 3, 2.4f, 0.3f, Vector2.up, 90f, 0.8f);
                }
                ScreenFx.Shake(0.06f + 0.035f * ring, 0.22f);
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || hit.Contains(enemy)
                        || Vector2.Distance(center, enemy.transform.position) > r + enemy.HitRadius
                        || !run.HasLineOfSight(center, enemy.transform.position)) continue;
                    hit.Add(enemy);
                    CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, center);
                    if (enemy.Health > 0 && slow > 0f) enemy.Chill(slow);
                }
                yield return new WaitForSeconds(QuakeInterval);
            }
        }

        /// <summary>Aegis: invulnerability shown as a bubble that everyone in the party can see.</summary>
        public void Aegis(float duration)
        {
            Player.Protect(duration);
            HolyBubble.Wrap(Player.Run.ProjectileRoot, transform, duration, AbilityCatalog.Ice);
            CoopFx.Aegis(Player.Run, duration);
        }
    }
}

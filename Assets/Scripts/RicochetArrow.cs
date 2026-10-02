using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Ricochet Arrow: a gilded arrow that glances off each enemy it strikes toward the nearest one it hasn't hit yet,
    /// up to three times, hitting harder after each ricochet. It also glances off walls. Every ricochet flashes gold
    /// and the arrow burns brighter. Teammates see a ghost that ricochets the same way without dealing damage.
    /// </summary>
    public sealed class RicochetArrow : MonoBehaviour
    {
        public const float Speed = 17.5f, Range = 22f, SeekRange = 6f;
        public const int MaxRicochets = 3, MaxWallBounces = 3;
        public static readonly Color PlainTint = new Color(1f, 0.85f, 0.45f);
        private static readonly Color Hot = new Color(1f, 0.97f, 0.82f);
        /// <summary>The Elemental Quiver's element this arrow carries (a crit sets it off) and the colour it burns in.</summary>
        private DamageElement infusion;
        private Color tint;
        private DungeonRun run;
        private DungeonPlayer player;
        private Vector2 direction;
        private int damage, bonusPerBounce, ricochets, wallBounces;
        private float remaining = Range;
        private bool ghost;
        private SpriteRenderer glow;
        private TrailRenderer trail;
        private readonly HashSet<DungeonEnemy> struck = new HashSet<DungeonEnemy>();
        // The shot's attack: every enemy it bounces through counts together for Massacre.
        private int attack;

        public static void Fire(DungeonPlayer player, Vector2 aim, int damage, int bonusPerBounce)
        {
            var infusion = ElementalQuiver.InfusionOf(player);
            var arrow = Create(player.Run, player.transform.position, aim, infusion);
            arrow.player = player;
            arrow.damage = damage;
            arrow.bonusPerBounce = bonusPerBounce;
            arrow.attack = player.Powerups.ActiveAttack;
            CoopFx.Ricochet(player.Run, player.transform.position, aim, infusion);
        }

        /// <summary>A teammate's arrow: flies and ricochets like theirs, their machine deals the damage.</summary>
        public static void SpawnGhost(DungeonRun run, Vector2 origin, Vector2 aim, DamageElement infusion) => Create(run, origin, aim, infusion).ghost = true;

        private static RicochetArrow Create(DungeonRun run, Vector2 origin, Vector2 aim, DamageElement infusion)
        {
            var tint = ElementalQuiver.ShotColor(infusion, PlainTint);
            var sprite = DungeonVisuals.CreateArrow("Ricochet arrow", run.ProjectileRoot, origin, 0.75f, tint, 7);
            var arrow = sprite.gameObject.AddComponent<RicochetArrow>();
            arrow.infusion = infusion;
            arrow.tint = tint;
            arrow.run = run;
            arrow.direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            // A soft golden halo around the head.
            arrow.glow = DungeonVisuals.Create("Ricochet glow", sprite.transform, origin, Vector2.one, FlameMesh.Alpha(tint, 0.3f), 6);
            arrow.glow.sprite = DungeonVisuals.GlowSprite;
            arrow.glow.transform.localPosition = new Vector2(0.3f, 0f);
            arrow.glow.transform.localScale = new Vector3(0.9f, 0.55f, 1f);
            arrow.trail = CombatVfx.Trail(sprite.gameObject, FlameMesh.Alpha(tint, 0.85f), 0.12f, 0.22f);
            arrow.Face();
            HeroVfx.Sparks(run.ProjectileRoot, origin, tint, 6, 3f, 0.2f, arrow.direction, 50f, 0.8f);
            return arrow;
        }

        private void Face() => transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            // The halo shimmers, brighter with every ricochet.
            float heat = ricochets / (float)MaxRicochets;
            if (glow != null) glow.color = FlameMesh.Alpha(Color.Lerp(tint, Hot, heat), 0.25f + 0.15f * heat + 0.08f * Mathf.Sin(Time.time * 40f));
            float distance = Mathf.Min(Speed * Time.deltaTime, remaining);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 position = transform.position;
                Vector2 next = position + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.08f) || IceWall.StopsShot(position, next, !ghost))
                {
                    if (wallBounces >= MaxWallBounces) { Spend(position); return; }
                    BounceOffWall(position);
                    continue;
                }
                transform.position = next;
                if (!ghost) Breakable.SmashAt(run, next, 0.1f);
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || struck.Contains(enemy) || Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                    struck.Add(enemy);
                    if (!ghost) using (player.Powerups.ResumeAttack(attack)) CombatDamage.Apply(player, enemy, damage + bonusPerBounce * ricochets, DamageElement.Physical, next - direction, 0.8f, infusion);
                    if (ricochets >= MaxRicochets || !Ricochet(next)) { Spend(next); return; }
                    break;
                }
            }
            remaining -= distance;
            if (remaining <= 0f) Destroy(gameObject);
        }

        /// <summary>Glances toward the nearest enemy in sight that it hasn't hit yet; false when there is none.</summary>
        private bool Ricochet(Vector2 at)
        {
            DungeonEnemy target = null;
            float best = SeekRange;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || struck.Contains(enemy)) continue;
                float distance = Vector2.Distance(at, enemy.transform.position);
                if (distance < best && run.HasLineOfSight(at, enemy.transform.position)) { best = distance; target = enemy; }
            }
            if (target == null) return false;
            Vector2 old = direction;
            direction = ((Vector2)target.transform.position - at).normalized;
            ricochets++;
            remaining = Mathf.Max(remaining, SeekRange + 1f);
            Face();
            Flash(at, old);
            if (trail != null) trail.startColor = FlameMesh.Alpha(Color.Lerp(tint, Hot, ricochets / (float)MaxRicochets), 0.9f);
            return true;
        }

        /// <summary>Reflects off whichever face of the wall it met (both, in a corner).</summary>
        private void BounceOffWall(Vector2 position)
        {
            Vector2 old = direction;
            bool blockedX = !run.Map.CanStand(position + new Vector2(direction.x * 0.12f, 0f), 0.08f);
            bool blockedY = !run.Map.CanStand(position + new Vector2(0f, direction.y * 0.12f), 0.08f);
            if (blockedX || !blockedY) direction.x = -direction.x;
            if (blockedY || !blockedX) direction.y = -direction.y;
            wallBounces++;
            Face();
            Flash(position, old);
        }

        /// <summary>A ricochet's flash: a gold burst sprayed along the new heading and a white glint where it turned.</summary>
        private void Flash(Vector2 at, Vector2 incoming)
        {
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, at, 0.55f, FlameMesh.Alpha(tint, 0.8f), 0.18f);
            HeroVfx.Sparks(root, at, tint, 9, 4f, 0.25f, direction, 70f, 0.9f);
            HeroVfx.Sparks(root, at, Hot, 4, 2.5f, 0.2f, -incoming, 90f, 0.6f);
            CombatVfx.Ring(root, at, 0.35f, Hot, 0.15f);
        }

        private void Spend(Vector2 at)
        {
            HeroVfx.Sparks(run.ProjectileRoot, at, tint, 7, 3f, 0.25f, -direction, 140f, 0.8f);
            Destroy(gameObject);
        }
    }
}

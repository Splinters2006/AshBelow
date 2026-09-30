using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Shield Throw: the Knight's shield (a steel-rimmed heater shield with a gold cross) spins through the air at the
    /// nearest enemy ahead, rings off it onto up to two more, then comes back. He cannot parry until it returns.
    /// Teammates see a ghost shield fly the same way and return to him.
    /// </summary>
    public sealed class ThrownShield : MonoBehaviour
    {
        public const float Speed = 15f, SeekRange = 9f, BounceRange = 6f, StraightRange = 7f, MaxFlight = 5f;
        public const int MaxTargets = 3;
        private static Sprite shieldSprite;
        private DungeonRun run;
        private DungeonPlayer player;
        private Transform owner;
        private bool ghost;
        private SpriteRenderer glow;
        private DungeonEnemy target;
        private readonly HashSet<DungeonEnemy> struck = new HashSet<DungeonEnemy>();
        private Vector2 straightEnd;
        private bool returning;
        private int damage;
        private float age;

        /// <summary>A heater shield: dark outline, bright steel rim, blue field with a highlight, and a gold cross.</summary>
        private static Sprite ShieldSprite => shieldSprite != null ? shieldSprite : shieldSprite = DungeonVisuals.PaletteSprite("Knight shield", new[]
        {
            ".OOOOOOOOOO.",
            "OSSSSSSSSSSO",
            "OSWBBGGBBBSO",
            "OSWBBGGBBBSO",
            "OSGGGGGGGGSO",
            "OSBBBGGBBBSO",
            "OSBBBGGBBBSO",
            ".OSBBGGBBSO.",
            ".OSBBGGBBSO.",
            "..OSBGGBSO..",
            "..OSBBBBSO..",
            "...OSBBSO...",
            "....OSSO....",
            ".....OO.....",
        }, key => key switch
        {
            'O' => new Color(0.16f, 0.18f, 0.24f),
            'S' => new Color(0.82f, 0.88f, 0.95f),
            'B' => new Color(0.2f, 0.42f, 0.8f),
            'W' => new Color(0.45f, 0.65f, 1f),
            'G' => new Color(1f, 0.82f, 0.3f),
            _ => Color.clear
        });

        public static bool Throw(DungeonPlayer player, Vector2 aim, int damage)
        {
            if (player.Shield == null || player.Shield.IsThrown) return false;
            var shield = Create(player.Run, player.transform, aim);
            shield.player = player;
            shield.damage = damage;
            shield.target = Seek(player.Run, player.transform.position, aim, null, SeekRange);
            player.Shield.IsThrown = true;
            CoopFx.Shield(player.Run, aim);
            return true;
        }

        /// <summary>A teammate's shield: it seeks and bounces the same way, but deals nothing.</summary>
        public static void SpawnGhost(DungeonRun run, Transform owner, Vector2 aim)
        {
            var shield = Create(run, owner, aim);
            shield.ghost = true;
            shield.target = Seek(run, owner.position, aim, null, SeekRange);
        }

        private static ThrownShield Create(DungeonRun run, Transform owner, Vector2 aim)
        {
            var sprite = DungeonVisuals.Create("Thrown shield", run.ProjectileRoot, owner.position, Vector2.one * 0.62f, Color.white, 7);
            sprite.sprite = ShieldSprite;
            var shield = sprite.gameObject.AddComponent<ThrownShield>();
            shield.run = run;
            shield.owner = owner;
            shield.straightEnd = PlayerAbilities.FindGroundLanding(run.Map, owner.position, aim, StraightRange);
            shield.glow = DungeonVisuals.Create("Shield glow", sprite.transform, owner.position, Vector2.one * 1.8f, FlameMesh.Alpha(AbilityCatalog.Ice, 0.35f), 6);
            shield.glow.sprite = DungeonVisuals.GlowSprite;
            CombatVfx.Trail(sprite.gameObject, new Color(0.5f, 0.85f, 1f, 0.6f), 0.3f, 0.18f);
            HeroVfx.Sparks(run.ProjectileRoot, owner.position, AbilityCatalog.Ice, 6, 3f, 0.2f, aim, 60f, 0.8f);
            return shield;
        }

        /// <summary>The nearest living, visible enemy in range (ahead of <paramref name="aim"/> when one is given) not yet struck.</summary>
        private static DungeonEnemy Seek(DungeonRun run, Vector2 from, Vector2 aim, HashSet<DungeonEnemy> skip, float range)
        {
            DungeonEnemy best = null;
            float bestDistance = range;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || (skip != null && skip.Contains(enemy))) continue;
                Vector2 offset = (Vector2)enemy.transform.position - from;
                if (offset.magnitude > bestDistance || (aim != Vector2.zero && Vector2.Dot(offset.normalized, aim.normalized) < 0.6f)
                    || !run.HasLineOfSight(from, enemy.transform.position)) continue;
                best = enemy;
                bestDistance = offset.magnitude;
            }
            return best;
        }

        private void Update()
        {
            if (run == null || owner == null || (!ghost && player == null)) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            age += Time.deltaTime;
            // Spins flat as it flies, its rim catching the light.
            transform.rotation = Quaternion.Euler(0, 0, age * 720f);
            if (glow != null) glow.color = FlameMesh.Alpha(AbilityCatalog.Ice, 0.3f + 0.1f * Mathf.Sin(age * 25f));
            if (age > MaxFlight) returning = true;
            Vector2 position = transform.position;
            Vector2 goal = returning ? (Vector2)owner.position : target != null && target.Health > 0 ? (Vector2)target.transform.position : straightEnd;
            transform.position = Vector2.MoveTowards(position, goal, Speed * Time.deltaTime);
            if (Vector2.Distance(transform.position, goal) > (returning ? 0.4f : target != null ? target.HitRadius : 0.2f)) return;
            if (returning) { Catch(); return; }
            if (target != null && target.Health > 0)
            {
                struck.Add(target);
                if (!ghost) CombatDamage.Apply(player, target, damage, DamageElement.Physical, position, 1.2f);
                Ring(transform.position, (Vector2)transform.position - position);
                target = struck.Count < MaxTargets ? Seek(run, transform.position, Vector2.zero, struck, BounceRange) : null;
                if (target != null) return;
            }
            returning = true;
        }

        /// <summary>The shield rings off a target: a steel flash, sparks thrown back along its path and a bright clang ring.</summary>
        private void Ring(Vector2 at, Vector2 travel)
        {
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, at, 0.7f, FlameMesh.Alpha(AbilityCatalog.Ice, 0.8f), 0.18f);
            HeroVfx.Sparks(root, at, Color.white, 6, 4.5f, 0.2f, -travel, 120f, 0.8f);
            HeroVfx.Sparks(root, at, AbilityCatalog.Ice, 10, 4f, 0.3f);
            CombatVfx.Ring(root, at, 0.45f, Color.white, 0.15f);
            if (!ghost) ScreenFx.Shake(0.05f, 0.08f);
        }

        private void Catch()
        {
            HeroVfx.Pulse(owner, owner.position, 0.8f, AbilityCatalog.Ice, 0.2f);
            HeroVfx.Sparks(run.ProjectileRoot, owner.position, AbilityCatalog.Ice, 5, 2f, 0.2f);
            Destroy(gameObject);
        }

        // Whatever ends the flight (the floor changing included), the Knight gets his shield back.
        private void OnDestroy()
        {
            if (!ghost && player != null && player.Shield != null) player.Shield.IsThrown = false;
        }
    }
}

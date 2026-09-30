using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Shield Throw: the Knight's shield flies at the nearest enemy ahead, bounces on to up to two more, then comes back.
    /// He cannot parry until it returns.
    /// </summary>
    public sealed class ThrownShield : MonoBehaviour
    {
        public const float Speed = 15f, SeekRange = 9f, BounceRange = 6f, StraightRange = 7f, MaxFlight = 5f;
        public const int MaxTargets = 3;
        private DungeonPlayer player;
        private DungeonEnemy target;
        private readonly HashSet<DungeonEnemy> struck = new HashSet<DungeonEnemy>();
        private Vector2 straightEnd;
        private bool returning;
        private int damage;
        private float age;

        public static bool Throw(DungeonPlayer player, Vector2 aim, int damage)
        {
            if (player.Shield == null || player.Shield.IsThrown) return false;
            var sprite = DungeonVisuals.Create("Thrown shield", player.Run.ProjectileRoot, player.transform.position, new Vector2(0.55f, 0.55f), AbilityCatalog.Ice, 7);
            var shield = sprite.gameObject.AddComponent<ThrownShield>();
            shield.player = player;
            shield.damage = damage;
            shield.target = Seek(player, player.transform.position, aim, null, SeekRange);
            shield.straightEnd = PlayerAbilities.FindGroundLanding(player.Run.Map, player.transform.position, aim, StraightRange);
            CombatVfx.Trail(sprite.gameObject, new Color(0.5f, 0.85f, 1f, 0.6f), 0.18f, 0.2f);
            player.Shield.IsThrown = true;
            return true;
        }

        /// <summary>The nearest living, visible enemy in range (ahead of <paramref name="aim"/> when one is given) not yet struck.</summary>
        private static DungeonEnemy Seek(DungeonPlayer player, Vector2 from, Vector2 aim, HashSet<DungeonEnemy> skip, float range)
        {
            DungeonEnemy best = null;
            float bestDistance = range;
            foreach (var enemy in player.Run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || (skip != null && skip.Contains(enemy))) continue;
                Vector2 offset = (Vector2)enemy.transform.position - from;
                if (offset.magnitude > bestDistance || (aim != Vector2.zero && Vector2.Dot(offset.normalized, aim.normalized) < 0.6f)
                    || !player.Run.HasLineOfSight(from, enemy.transform.position)) continue;
                best = enemy;
                bestDistance = offset.magnitude;
            }
            return best;
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null || !run.IsPlaying) return;
            age += Time.deltaTime;
            transform.rotation = Quaternion.Euler(0, 0, age * 900f);
            if (age > MaxFlight) returning = true;
            Vector2 position = transform.position;
            Vector2 goal = returning ? (Vector2)player.transform.position : target != null && target.Health > 0 ? (Vector2)target.transform.position : straightEnd;
            transform.position = Vector2.MoveTowards(position, goal, Speed * Time.deltaTime);
            if (Vector2.Distance(transform.position, goal) > (returning ? 0.4f : target != null ? target.HitRadius : 0.2f)) return;
            if (returning) { Catch(); return; }
            if (target != null && target.Health > 0)
            {
                struck.Add(target);
                CombatDamage.Apply(player, target, damage, DamageElement.Physical, position, 1.2f);
                HeroVfx.Sparks(run.ProjectileRoot, transform.position, AbilityCatalog.Ice, 10, 4f, 0.3f);
                CoopFx.Pulse(run, transform.position, 0.8f, AbilityCatalog.Ice, 0.25f);
                target = struck.Count < MaxTargets ? Seek(player, transform.position, Vector2.zero, struck, BounceRange) : null;
                if (target != null) return;
            }
            returning = true;
        }

        private void Catch()
        {
            HeroVfx.Pulse(player.transform, player.transform.position, 0.8f, AbilityCatalog.Ice, 0.2f);
            Destroy(gameObject);
        }

        // Whatever ends the flight (the floor changing included), the Knight gets his shield back.
        private void OnDestroy()
        {
            if (player != null && player.Shield != null) player.Shield.IsThrown = false;
        }
    }
}

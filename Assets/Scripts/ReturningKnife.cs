using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A Fan of Knives blade. It pierces outward (stopping at walls), hangs spinning, and one second after the throw
    /// flies back to its thrower wherever they are, passing through walls. It can strike each enemy once on the way
    /// out and once on the way back. A teammate's knives are ghosts here: they fly the same path but deal no damage.
    /// </summary>
    public sealed class ReturningKnife : MonoBehaviour
    {
        public const float ReturnDelay = 1f, OutwardSpeed = 12f, ReturnSpeed = 16f, MaxLifetime = 6f;
        public static readonly Color Blade = new Color(0.78f, 0.62f, 1f);
        private DungeonRun run;
        private Transform owner;
        private DungeonPlayer thrower;
        private Vector2 direction;
        private float remainingRange, age;
        private int damage;
        private bool ghost, returning, stuck;
        private readonly HashSet<DungeonEnemy> hitOut = new HashSet<DungeonEnemy>(), hitBack = new HashSet<DungeonEnemy>();
        public bool IsReturning => returning;
        public bool IsSpent { get; private set; }
        /// <summary>True once the knife reached its thrower (rather than expiring).</summary>
        public bool ReturnedHome { get; private set; }

        public static ReturningKnife Throw(DungeonPlayer thrower, Vector2 direction, int damage, float range = PlayerProjectile.MaxRange)
        {
            CoopFx.Knife(thrower.Run, thrower.transform.position, direction, range);
            var knife = Create(thrower.Run, thrower.transform, thrower.transform.position, direction, range);
            knife.thrower = thrower;
            knife.damage = damage;
            return knife;
        }

        /// <summary>A teammate's knife; it returns to their hero on this machine.</summary>
        public static ReturningKnife SpawnGhost(DungeonRun run, Transform owner, Vector2 position, Vector2 direction, float range)
        {
            var knife = Create(run, owner, position, direction, range);
            knife.ghost = true;
            return knife;
        }

        private static ReturningKnife Create(DungeonRun run, Transform owner, Vector2 position, Vector2 direction, float range)
        {
            var knife = DungeonVisuals.Create("Returning knife", run.ProjectileRoot, position, new Vector2(0.42f, 0.09f), Blade, 6)
                .gameObject.AddComponent<ReturningKnife>();
            knife.run = run;
            knife.owner = owner;
            knife.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            knife.remainingRange = Mathf.Max(0f, range);
            knife.Face(knife.direction);
            CombatVfx.Trail(knife.gameObject, new Color(Blade.r, Blade.g, Blade.b, 0.8f), 0.07f, 0.12f);
            return knife;
        }

        private void Update() { Advance(Time.deltaTime); }

        public void Advance(float deltaTime)
        {
            if (IsSpent || !run.IsPlaying || deltaTime <= 0f) return;
            age += deltaTime;
            if (owner == null || age > MaxLifetime || (thrower != null && thrower.Health <= 0)) { Consume(); return; }
            if (!returning && age >= ReturnDelay)
            {
                returning = true;
                HeroVfx.Sparks(run.ProjectileRoot, transform.position, Blade, 4, 2f, 0.2f);
            }
            if (returning) FlyHome(deltaTime);
            else if (!stuck && remainingRange > 0f) FlyOut(deltaTime);
            else transform.Rotate(0f, 0f, 900f * deltaTime);
        }

        private void FlyOut(float deltaTime)
        {
            float distance = Mathf.Min(OutwardSpeed * deltaTime, remainingRange);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.08f))
                {
                    stuck = true;
                    HeroVfx.Sparks(run.ProjectileRoot, transform.position, new Color(0.85f, 0.85f, 0.7f), 5, 2.5f, 0.2f, -direction, 140f, 0.7f);
                    return;
                }
                transform.position = next;
                Strike(next, direction, hitOut);
            }
            remainingRange -= distance;
        }

        private void FlyHome(float deltaTime)
        {
            // Homes on the thrower's current position and ignores walls, so it always finds its way back.
            Vector2 position = transform.position, home = owner.position;
            Vector2 offset = home - position;
            float distance = Mathf.Min(ReturnSpeed * deltaTime, offset.magnitude);
            if (offset.magnitude <= 0.35f) { Catch(); return; }
            Vector2 heading = offset.normalized;
            Face(heading);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                position += heading * (distance / steps);
                transform.position = position;
                Strike(position, heading, hitBack);
            }
            if (Vector2.Distance(position, owner.position) <= 0.35f) Catch();
        }

        private void Strike(Vector2 point, Vector2 heading, HashSet<DungeonEnemy> alreadyHit)
        {
            for (int j = run.Enemies.Count - 1; j >= 0; j--)
            {
                var enemy = run.Enemies[j];
                if (enemy == null || alreadyHit.Contains(enemy) || Vector2.Distance(point, enemy.transform.position) > enemy.HitRadius) continue;
                alreadyHit.Add(enemy);
                if (!ghost) CombatDamage.Apply(run.Player, enemy, damage, DamageElement.Physical, point - heading);
            }
        }

        private void Catch()
        {
            ReturnedHome = true;
            HeroVfx.Sparks(run.ProjectileRoot, owner.position, Blade, 3, 1.6f, 0.15f);
            Consume();
        }

        private void Face(Vector2 heading) => transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);

        private void Consume()
        {
            IsSpent = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}

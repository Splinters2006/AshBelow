using UnityEngine;

namespace Slopgame
{
    // An intentionally overpowered hero; normal enemy death/reward paths still apply.
    public sealed class AdminAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float NightfallCooldown = 2.5f;
        public const float NightfallRadius = 9f;
        public DungeonPlayer Player { get; set; }
        private float readyAt, nightfallReadyAt, reignUntil;
        private Transform reignFloor;
        public bool IsHeavyAttacking => false;
        public bool CanAttack => Player != null && Player.Run.IsPlaying && !Player.IsRolling && Time.time >= readyAt;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, nightfallReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => nightfallReadyAt = Cooldowns.Shorten(nightfallReadyAt, seconds);
        public bool IsReigning => Time.time < reignUntil && reignFloor == Player.Run.ProjectileRoot;
        public float RiftRange(float charge) => Mathf.Lerp(8f, 12f, Mathf.Clamp01(charge))
            + Player.Powerups.Count(PowerupType.RiftReach) * 1.5f;
        public int RiftDamage(float charge) => (Player.Charge.Damage(charge)
            + Mathf.RoundToInt(Player.Powerups.Count(PowerupType.AbyssalPower) * 16f * Mathf.Lerp(1f, 3f, Mathf.Clamp01(charge))))
            * (IsReigning ? 2 : 1);

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            charge = Mathf.Clamp01(charge);
            Rift(aim.normalized, RiftRange(charge), Mathf.Lerp(0.65f, 1.7f, charge), RiftDamage(charge), charge);
            readyAt = Time.time + 0.22f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!CanAttack || HeavyCooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            Player.Charge.Cancel();
            ExecuteArea(NightfallRadius);
            Player.Protect(0.7f);
            nightfallReadyAt = Time.time + NightfallCooldown;
            readyAt = Time.time + 0.2f;
            return true;
        }

        public bool CastRelic(AbilityType type, Vector2 aim, int rank)
        {
            if (Player == null || !Player.Run.IsPlaying || Player.IsRolling || aim.sqrMagnitude < 0.001f) return false;
            rank = Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank);
            switch (type)
            {
                case AbilityType.Eclipse:
                    float radius = 11f + rank - 1 + Player.Powerups.Count(PowerupType.EclipseRadius);
                    ExecuteArea(radius);
                    ShadowVfx.Singularity(Player.Run.ProjectileRoot, transform.position, radius);
                    CoopFx.Singularity(Player.Run, transform.position, radius);
                    Player.Protect(1f);
                    break;
                case AbilityType.SoulRend:
                    Rift(aim.normalized, 14f, 1.8f, RiftDamage(1f) * (2 + rank + Player.Powerups.Count(PowerupType.SoulRendPower)), 1f);
                    break;
                case AbilityType.ShadowReign:
                    float duration = 3f + rank - 1 + Player.Powerups.Count(PowerupType.ReignDuration);
                    reignUntil = Time.time + duration;
                    reignFloor = Player.Run.ProjectileRoot;
                    Player.Protect(duration);
                    ShadowVfx.Singularity(reignFloor, transform.position, 6f, 1.4f);
                    CoopFx.Singularity(Player.Run, transform.position, 6f, 1.4f);
                    foreach (var enemy in Player.Run.Enemies.ToArray())
                        if (InSight(enemy, 6f)) Strike(enemy, RiftDamage(1f), transform.position);
                    break;
                default: return false;
            }
            readyAt = Time.time + 0.15f;
            return true;
        }

        private void Rift(Vector2 aim, float range, float halfWidth, int damage, float charge)
        {
            Vector2 origin = transform.position;
            Vector2 destination = origin;
            // Clip the visual and damage ray to walls, so the attack's reach stays legible.
            int steps = Mathf.CeilToInt(range / 0.12f);
            for (int i = 1; i <= steps; i++)
            {
                Vector2 candidate = origin + aim * (range * i / steps);
                if (!Player.Run.Map.CanStand(candidate, 0.04f)) break;
                destination = candidate;
            }
            float length = Vector2.Distance(origin, destination);
            ShadowVfx.Rift(Player.Run.ProjectileRoot, origin, destination, 0.6f + charge * 1.4f);
            CoopFx.Rift(Player.Run, origin, destination, 0.6f + charge * 1.4f);
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                float along = Vector2.Dot(offset, aim);
                if (along < 0f || along > length + enemy.HitRadius
                    || Mathf.Abs(Vector2.Dot(offset, Vector2.Perpendicular(aim))) > halfWidth + enemy.HitRadius
                    || !Player.Run.HasLineOfSight(origin, enemy.transform.position)) continue;
                Strike(enemy, damage, origin);
            }
        }

        private bool InSight(DungeonEnemy enemy, float radius) => enemy != null && enemy.Health > 0
            && Vector2.Distance(transform.position, enemy.transform.position) <= radius + enemy.HitRadius
            && Player.Run.HasLineOfSight(transform.position, enemy.transform.position);

        private void ExecuteArea(float radius)
        {
            Vector2 origin = transform.position;
            ShadowVfx.Execution(Player.Run.ProjectileRoot, origin, radius);
            CoopFx.Execution(Player.Run, origin, radius);
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                if (!InSight(enemy, radius)) continue;
                ShadowVfx.Rift(Player.Run.ProjectileRoot, origin, enemy.transform.position, 0.65f);
                CoopFx.Rift(Player.Run, origin, enemy.transform.position, 0.65f);
                Strike(enemy, enemy.Health, origin);
            }
        }

        private void Strike(DungeonEnemy enemy, int damage, Vector2 origin)
        {
            Vector2 position = enemy.transform.position;
            CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, origin);
            if (enemy.Health <= 0) ShadowVfx.Death(Player.Run.ProjectileRoot, position);
        }

        public void Hide() { Player.Charge.Cancel(); }
    }
}

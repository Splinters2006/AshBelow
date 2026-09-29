using UnityEngine;

namespace Slopgame
{
    public sealed class BowAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float HeavyCooldown = 6f;
        // Triple shot: a tight, long-range spread.
        public const float SpreadAngle = 6f;
        public const float HeavyRange = 8.5f;
        public const float ChargedRangeBonus = 1f;
        public static float RangeForCharge(float charge) => PlayerProjectile.MaxRange + ChargedRangeBonus * Mathf.Clamp01(charge);
        public DungeonPlayer Player { get; set; }
        public bool IsHeavyAttacking => false;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0, heavyReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => heavyReadyAt = Cooldowns.Shorten(heavyReadyAt, seconds);
        private float readyAt, heavyReadyAt;
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && Time.time >= readyAt;

        private bool CanFire(Vector2 aim) => Player.Run.IsPlaying && !Player.IsRolling
            && Time.time >= readyAt && aim.sqrMagnitude > 0.001f;

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanFire(aim)) return false;
            Fire(aim.normalized, Player.Charge.Damage(charge), RangeForCharge(charge));
            readyAt = Time.time + 0.35f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!CanFire(aim) || HeavyCooldownRemaining > 0) return false;
            Player.Charge.Cancel();
            for (int i = -1; i <= 1; i++)
                Fire(Quaternion.Euler(0, 0, SpreadAngle * i) * aim.normalized, Player.Damage, HeavyRange);
            heavyReadyAt = Time.time + HeavyCooldown;
            readyAt = Time.time + 0.35f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        private void Fire(Vector2 direction, int damage, float range = PlayerProjectile.MaxRange)
        {
            PlayerProjectile.Spawn(Player.Run, transform.position, direction, damage, range);
        }

        public void Hide() { Player.Charge.Cancel(); }
    }
}

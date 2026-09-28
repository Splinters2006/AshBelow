using UnityEngine;

namespace Slopgame
{
    public sealed class BowAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float HeavyCooldown = 6f;
        public const float SpreadAngle = 15f;
        public DungeonPlayer Player { get; set; }
        public bool IsHeavyAttacking => false;
        public float HeavyCooldownRemaining => Mathf.Max(0, heavyReadyAt - Time.time);
        private float readyAt, heavyReadyAt;
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && Time.time >= readyAt;

        private bool CanFire(Vector2 aim) => Player.Run.IsPlaying && !Player.IsRolling
            && Time.time >= readyAt && aim.sqrMagnitude > 0.001f;

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanFire(aim)) return false;
            Fire(aim.normalized, Player.Charge.Damage(charge));
            readyAt = Time.time + 0.35f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!CanFire(aim) || HeavyCooldownRemaining > 0) return false;
            Player.Charge.Cancel();
            for (int i = -1; i <= 1; i++)
                Fire(Quaternion.Euler(0, 0, SpreadAngle * i) * aim.normalized, Player.Damage);
            heavyReadyAt = Time.time + HeavyCooldown;
            readyAt = Time.time + 0.35f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        private void Fire(Vector2 direction, int damage)
        {
            PlayerProjectile.Spawn(Player.Run, transform.position, direction, damage);
        }

        public void Hide() { Player.Charge.Cancel(); }
    }
}

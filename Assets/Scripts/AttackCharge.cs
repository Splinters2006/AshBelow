using UnityEngine;

namespace Slopgame
{
    public sealed class AttackCharge : MonoBehaviour
    {
        public DungeonPlayer Player { get; set; }
        public bool IsCharging { get; private set; }
        private float startedAt;
        private bool wasHeld;
        public float Duration => (Player.ClassWeapon == WeaponType.Bow
            ? Player.Powerups.DrawTimeMultiplier : Player.ClassWeapon == WeaponType.Hammer ? PaladinAttack.ChargeDuration : 1.2f) * Player.Powerups.AttackIntervalMultiplier;
        public float Amount => IsCharging ? Mathf.Clamp01((Time.time - startedAt) / Duration) : 0f;

        public void Tick(bool held, bool allowed)
        {
            if (!allowed) Cancel();
            else if (held && !wasHeld && Player.Weapon.CanAttack)
            {
                startedAt = Time.time;
                IsCharging = true;
            }
            else if (!held && IsCharging)
            {
                float charge = Amount;
                Cancel();
                Player.Weapon.TryAttack(Player.AimDirection, charge);
            }
            wasHeld = held;
        }

        public int Damage(float charge)
        {
            float cap = Player.ClassWeapon == WeaponType.Bow ? Player.Powerups.ArrowChargeMultiplier : 3f;
            return Player.Damage + Mathf.FloorToInt(Player.Damage * (cap - 1f) * Mathf.Clamp01(charge) + 0.0001f);
        }

        public void Cancel() { IsCharging = false; }
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    }
}

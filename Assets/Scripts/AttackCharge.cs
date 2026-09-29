using UnityEngine;

namespace Slopgame
{
    public sealed class AttackCharge : MonoBehaviour
    {
        public DungeonPlayer Player { get; set; }
        public bool IsCharging { get; private set; }
        private float startedAt;
        private bool wasHeld, fullPinged;
        public float Duration => (Player.ClassWeapon == WeaponType.Bow
            ? Player.Powerups.DrawTimeMultiplier : Player.ClassWeapon == WeaponType.Hammer ? PaladinAttack.ChargeDuration
            : Player.ClassWeapon == WeaponType.Daggers ? 1.2f / 1.5f : Player.ClassWeapon == WeaponType.Fists ? BrawlerAttack.ChargeDuration
            : Player.ClassWeapon == WeaponType.Sword ? KnightChargeDuration : 1.2f)
            * Player.Powerups.AttackIntervalMultiplier * Player.Buffs.ChargeDurationMultiplier;
        public const float KnightChargeDuration = 0.75f;
        public float Amount => IsCharging ? Mathf.Clamp01((Time.time - startedAt) / Duration) : 0f;

        public void Tick(bool held, bool allowed)
        {
            if (!allowed) Cancel();
            else if (held && !wasHeld && Player.Weapon.CanAttack)
            {
                startedAt = Time.time;
                IsCharging = true;
                fullPinged = false;
            }
            else if (!held && IsCharging)
            {
                float charge = Amount;
                Cancel();
                Player.Weapon.TryAttack(Player.AimDirection, charge);
            }
            wasHeld = held;
            if (IsCharging && !fullPinged && Amount >= 1f)
            {
                fullPinged = true;
                HeroVfx.Pulse(Player.transform, Player.transform.position, 0.85f, AbilityCatalog.Gold, 0.3f);
            }
        }

        public int Damage(float charge)
        {
            float cap = Player.ClassWeapon == WeaponType.Bow ? Player.Powerups.ArrowChargeMultiplier
                : Player.ClassWeapon == WeaponType.Daggers ? 5f : 3f;
            return Player.Damage + Mathf.FloorToInt(Player.Damage * (cap - 1f) * Mathf.Clamp01(charge) + 0.0001f);
        }

        public void Cancel() { IsCharging = false; }
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    }
}

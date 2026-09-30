using UnityEngine;

namespace Slopgame
{
    public sealed class AttackCharge : MonoBehaviour
    {
        public DungeonPlayer Player { get; set; }
        public bool IsCharging { get; private set; }
        private float startedAt, fullAt;
        // wasAutofired: autofire let the attack go while the button stayed down, so charging may start again without a new press.
        private bool wasHeld, fullPinged, wasAutofired;
        public float Duration => (Player.ClassWeapon == WeaponType.Bow
            ? Player.Powerups.DrawTimeMultiplier : Player.ClassWeapon == WeaponType.Hammer ? PaladinAttack.ChargeDuration
            : Player.ClassWeapon == WeaponType.Daggers ? 1.2f / 1.5f : Player.ClassWeapon == WeaponType.Fists ? BrawlerAttack.ChargeDuration
            : Player.ClassWeapon == WeaponType.Tail ? DemonessAttack.ChargeDuration
            : Player.ClassWeapon == WeaponType.Beam ? CyborgAttack.ChargeDuration
            : Player.ClassWeapon == WeaponType.Sword ? KnightChargeDuration : 1.2f)
            * Player.Powerups.AttackIntervalMultiplier * Player.Buffs.ChargeDurationMultiplier;
        public const float KnightChargeDuration = 0.75f, WizardChargeMultiplier = 4f;
        // Retaliation: after a parry the Knight's next slash is fully charged the moment he starts it.
        public float Amount => IsCharging ? (Player.Shield != null && Player.Shield.RetaliationReady ? 1f : Mathf.Clamp01((Time.time - startedAt) / Duration)) : 0f;

        public void Tick(bool held, bool allowed)
        {
            if (!allowed) Cancel();
            else if (held && (!wasHeld || wasAutofired) && Player.Weapon.CanAttack)
            {
                startedAt = Time.time;
                IsCharging = true;
                fullPinged = false;
                wasAutofired = false;
            }
            else if (!held && IsCharging) Release();
            else if (held && IsCharging && fullPinged && GameSettings.Autofire && Time.time - fullAt >= GameSettings.AutofireDelay)
            {
                Release();
                wasAutofired = true;
            }
            wasHeld = held;
            if (!held) wasAutofired = false;
            if (IsCharging && !fullPinged && Amount >= 1f)
            {
                fullPinged = true;
                fullAt = Time.time;
                HeroVfx.Pulse(Player.transform, Player.transform.position, 0.85f, AbilityCatalog.Gold, 0.3f);
            }
        }

        private void Release()
        {
            float charge = Amount;
            Cancel();
            if (Player.Powerups.BasicAttack(() => Player.Weapon.TryAttack(Player.AimDirection, charge)))
                Breakable.SmashInArc(Player, Player.AimDirection, Breakable.SwingReach);
        }

        public int Damage(float charge)
        {
            float cap = Player.ClassWeapon == WeaponType.Bow ? Player.Powerups.ArrowChargeMultiplier
                : Player.ClassWeapon == WeaponType.Daggers ? 5f : Player.ClassWeapon == WeaponType.Staff ? WizardChargeMultiplier : 3f;
            return Player.Damage + Mathf.FloorToInt(Player.Damage * (cap - 1f) * Mathf.Clamp01(charge) + 0.0001f);
        }

        public void Cancel() { IsCharging = false; }
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    }
}

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
            : Player.ClassWeapon == WeaponType.Scythe ? ReaperAttack.ChargeDuration
            : Player.ClassWeapon == WeaponType.Sword ? KnightChargeDuration : 1.2f)
            * Player.Powerups.AttackIntervalMultiplier * Player.Buffs.ChargeDurationMultiplier
            // Seeing Red: the upgraded Super Angry charges barrages faster still.
            * (Player.Mechanic is SuperAngry angry ? angry.ChargeDurationMultiplier : 1f);
        public const float KnightChargeDuration = 0.75f, WizardChargeMultiplier = 4f;
        // Retaliation: after a parry the Knight's next slash is fully charged the moment he starts it.
        // Razor's Edge: so is every stab while the Assassin's upgraded Sharpened Dagger is up.
        public float Amount => IsCharging ? ((Player.Shield != null && Player.Shield.RetaliationReady) || (Player.Mechanic is SharpenedDagger edge && edge.InstantCharge) ? 1f : Mathf.Clamp01((Time.time - startedAt) / Duration)) : 0f;

        /// <summary>True while a left-click attack is landing its hits (Killer Instinct tells basic backstabs from the rest).</summary>
        public bool IsStriking { get; private set; }
        /// <summary>Overflowing Faith: a full charge that is held on overcharges over this share of the charge time again.</summary>
        public const float OverchargeShare = 0.5f;
        private bool CanOvercharge => Player.ClassWeapon == WeaponType.Hammer && Player.Permanent != null && Player.Permanent.HasPassive(WeaponType.Hammer);
        /// <summary>How far past full the Paladin's blessing is overcharged, 0-1.</summary>
        public float Overcharge => IsCharging && fullPinged && CanOvercharge ? Mathf.Clamp01((Time.time - fullAt) / (Duration * OverchargeShare)) : 0f;
        /// <summary>Whether the attack being released was fully overcharged.</summary>
        public bool ReleasedOvercharged { get; private set; }
        private bool overPinged;

        public void Tick(bool held, bool allowed)
        {
            if (!allowed) Cancel();
            else if (held && (!wasHeld || wasAutofired) && Player.Weapon.CanAttack)
            {
                startedAt = Time.time;
                IsCharging = true;
                fullPinged = false;
                overPinged = false;
                wasAutofired = false;
            }
            else if (!held && IsCharging) Release();
            // Autofire lets an overcharge finish first.
            else if (held && IsCharging && fullPinged && GameSettings.Autofire && Time.time - fullAt >= GameSettings.AutofireDelay && (!CanOvercharge || Overcharge >= 1f))
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
            if (IsCharging && !overPinged && Overcharge >= 1f)
            {
                overPinged = true;
                HeroVfx.Pulse(Player.transform, Player.transform.position, 1.3f, Color.white, 0.35f);
            }
        }

        private void Release()
        {
            float charge = Amount;
            ReleasedOvercharged = Overcharge >= 1f;
            Cancel();
            IsStriking = true;
            bool thrown;
            try { thrown = Player.Powerups.BasicAttack(() => Player.Weapon.TryAttack(Player.AimDirection, charge)); }
            finally { IsStriking = false; ReleasedOvercharged = false; }
            if (thrown) Breakable.SmashInArc(Player, Player.AimDirection, Breakable.SwingReach);
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

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
            : Player.ClassWeapon == WeaponType.Katana ? SamuraiAttack.ChargeDuration
            : Player.ClassWeapon == WeaponType.Mutation ? SpecimenAttack.ChargeTimeFor(Player)
            : Player.ClassWeapon == WeaponType.Sword ? KnightChargeDuration : 1.2f)
            * Player.Powerups.AttackIntervalMultiplier * Player.Buffs.ChargeDurationMultiplier
            // Seeing Red: the upgraded Super Angry charges barrages faster still.
            * (Player.Mechanic is SuperAngry angry ? angry.ChargeDurationMultiplier : 1f)
            // Swift as the Wind: the Samurai's flurry winds up faster too.
            * (Player.Weapon is SamuraiAttack samurai ? samurai.SwiftIntervalMultiplier : 1f)
            // Hot Streak: the attack after a kill charges faster.
            * Player.Powerups.HotStreakChargeMultiplier;
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
            // Autofire (switched on per hero): the attack goes the moment it is fully charged, and charging starts again
            // while the button stays down. A Paladin who can overcharge waits for the overcharge to finish first.
            else if (held && IsCharging && fullPinged && GameSettings.AutofireFor(Player.ClassWeapon) && (!CanOvercharge || Overcharge >= 1f))
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
                // Autofire: a charge that just filled goes straight away rather than a frame later.
                if (held && allowed && GameSettings.AutofireFor(Player.ClassWeapon) && !CanOvercharge)
                {
                    Release();
                    wasAutofired = true;
                }
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
            // Hot Streak is spent before the hits land, so a kill by this attack quickens the next one.
            Player.Powerups.ConsumeHotStreak();
            IsStriking = true;
            bool thrown;
            try { thrown = Player.Powerups.BasicAttack(() => Player.Weapon.TryAttack(Player.AimDirection, charge)); }
            finally { IsStriking = false; ReleasedOvercharged = false; }
            // Urns break where the attack itself reaches (see Breakable); the swing still chips Ice Wall blocks.
            if (thrown) IceWall.HitInArc(Player.Run, Player.transform.position, Player.AimDirection.normalized, Breakable.SwingReach);
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

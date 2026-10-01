using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A hero's class mechanic, used with the Mechanic key (R by default). Each is bought in the Ash shop once that
    /// class has felled the third guardian. Combat code reports events through the
    /// On... hooks, which each mechanic uses to charge itself.
    /// </summary>
    public abstract class ClassMechanic : MonoBehaviour
    {
        public DungeonPlayer Player { get; set; }
        public abstract string Name { get; }
        public virtual Color Color => Player.Run.SelectedCharacter != null ? Player.Run.SelectedCharacter.Color : Color.white;
        /// <summary>How close to usable, 0-1: a charge filling up or a cooldown running down.</summary>
        public abstract float Readiness { get; }
        /// <summary>A short HUD label such as "READY", "4 / 7" or "12.0s".</summary>
        public abstract string Status { get; }
        /// <summary>Extra damage added to every attack (Sharpened Dagger, Loaded Dice).</summary>
        public virtual int BonusDamage => 0;
        public abstract bool TryActivate(Vector2 aim);

        public virtual void OnDamaged() { }
        public virtual void OnBackstab() { }
        /// <summary>The hero immobilized an enemy (paralysed, froze, stunned or rooted it).</summary>
        public virtual void OnImmobilized() { }
        public virtual void OnBlessedHit(int bonus) { }
        public virtual void OnElementalEffect() { }
        /// <summary>A guardian fell in this descent (the Gambler's safe pays interest).</summary>
        public virtual void OnGuardianDefeated() { }
        /// <summary>Kill talents shorten a mechanic that runs on a cooldown; charged mechanics ignore it.</summary>
        public virtual void ReduceCooldown(float seconds) { }

        /// <summary>True once the mechanic's R upgrade has been bought in the Ash shop.</summary>
        protected bool IsUpgraded => Player.Permanent != null && Player.Permanent.MechanicUpgraded;

        protected bool CanAct => Player.Run.IsPlaying && Player.Health > 0 && !Player.IsRolling && !Player.IsBusy;

        protected static string Seconds(float value) => $"{value:0.0}s";

        /// <summary>Adds the hero's mechanic if they own it; null otherwise.</summary>
        public static ClassMechanic Attach(DungeonPlayer player)
        {
            if (player.Permanent == null || !player.Permanent.MechanicUnlocked) return null;
            ClassMechanic mechanic = player.ClassWeapon switch
            {
                WeaponType.Sword => player.gameObject.AddComponent<ShieldTaunt>(),
                WeaponType.Bow => player.gameObject.AddComponent<ElementalQuiver>(),
                WeaponType.Staff => player.gameObject.AddComponent<WildStorm>(),
                WeaponType.Daggers => player.gameObject.AddComponent<SharpenedDagger>(),
                WeaponType.Hammer => player.gameObject.AddComponent<HeavenlyHost>(),
                WeaponType.Fists => player.gameObject.AddComponent<SuperAngry>(),
                WeaponType.Tail => player.gameObject.AddComponent<DemonicPower>(),
                WeaponType.Coins => player.gameObject.AddComponent<GamblerPurse>(),
                WeaponType.Beam => player.gameObject.AddComponent<Overclock>(),
                WeaponType.Scythe => player.gameObject.AddComponent<ArmyOfTheDead>(),
                WeaponType.Katana => player.gameObject.AddComponent<TruePoser>(),
                WeaponType.Mutation => player.gameObject.AddComponent<BreakingPoint>(),
                _ => null
            };
            if (mechanic != null) mechanic.Player = player;
            return mechanic;
        }
    }

    /// <summary>A mechanic that fills from combat events and empties when used.</summary>
    public abstract class ChargedMechanic : ClassMechanic
    {
        public int Charge { get; private set; }
        public abstract int Required { get; }
        public bool IsCharged => DebugMode.Enabled || Charge >= Required;
        public override float Readiness => IsCharged ? 1f : Charge / (float)Required;
        public override string Status => IsCharged ? "READY" : $"{Charge} / {Required}";

        protected void AddCharge(int amount) { if (amount > 0) Charge = Mathf.Min(Required, Charge + amount); }

        public override bool TryActivate(Vector2 aim)
        {
            if (!CanAct || !IsCharged || !Activate(aim)) return false;
            Charge = 0;
            return true;
        }

        /// <summary>The effect itself; false keeps the charge (for example when there is nothing to affect).</summary>
        protected abstract bool Activate(Vector2 aim);
    }
}

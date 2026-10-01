namespace Slopgame
{
    // Snapshot purchases at the start of a run; temporary boons keep their own stack limits.
    public sealed class PermanentBonuses
    {
        public int Health { get; }
        public int Damage { get; }
        public float Speed { get; }
        public float AttackSpeed { get; }
        public float DodgeMultiplier { get; } = 1f;
        public float DrawMultiplier { get; } = 1f;
        public int ReflectionDamage { get; }
        public int LightningDamage { get; }
        public float EffectChance { get; private set; }
        public float PhysicalCritChance { get; private set; }
        public float BlessingDuration { get; }
        public int BarragePunches { get; }
        public int ParalyzedDamage { get; }
        /// <summary>Deep Pockets: added to the coins the Gambler's purse never lets him drop below.</summary>
        public int PurseFloor { get; }
        /// <summary>Added to the Gambler's odds of winning a gamble.</summary>
        public float GambleLuck { get; }
        /// <summary>Soul Echo's chance, by rank, for each soul the Reaper gains to bring an extra one.</summary>
        public static readonly float[] ExtraSoulChances = { 0f, 0.1f, 0.25f, 0.5f };
        /// <summary>Soul Echo: the chance for each soul the Reaper gains to bring an extra one.</summary>
        public float ExtraSoulChance { get; }
        /// <summary>Capacitor Bank: scales the Augment's plasma cannon cooldown.</summary>
        public float CannonCooldownMultiplier { get; } = 1f;
        /// <summary>The class mechanic on R, bought in the Ash shop.</summary>
        public bool MechanicUnlocked { get; }
        /// <summary>The mechanic's R upgrade, bought in the Ash shop (the Reaper's Avatar of Death).</summary>
        public bool MechanicUpgraded { get; }
        /// <summary>The class passive (see <see cref="ClassPassiveCatalog"/>), active once its world is cleared.</summary>
        public bool PassiveUnlocked { get; }
        private readonly WeaponType weapon;
        /// <summary>True when this hero is the given class and its passive is unlocked.</summary>
        public bool HasPassive(WeaponType passive) => PassiveUnlocked && weapon == passive;
        /// <summary>Scales all damage (the Ash shop's Infernal Pact).</summary>
        public float DamageMultiplier { get; } = 1f;
        /// <summary>Scales maximum HP (the Ash shop's Infernal Pact).</summary>
        public float MaxHealthMultiplier { get; } = 1f;
        /// <summary>The Ash shop's Backup Drive: once per descent, a killing blow leaves the hero at 1 HP.</summary>
        public bool BackupDrive { get; }
        /// <summary>The Ash shop's Apex Predator: extra damage share against guardians and armoured brutes.</summary>
        public float GuardianDamage { get; }

        public PermanentBonuses(PermanentProgress progress, WeaponType weapon)
        {
            this.weapon = weapon;
            if (progress == null) return;
            MechanicUnlocked = progress.Rank(PermanentUpgradeCatalog.MechanicId(weapon)) > 0;
            MechanicUpgraded = MechanicUnlocked && progress.Rank(PermanentUpgradeCatalog.MechanicUpgradeId(weapon)) > 0;
            PassiveUnlocked = ClassPassiveCatalog.IsUnlocked(progress, weapon);
            Health = progress.Rank("health");
            Damage = progress.Rank("damage");
            Speed = progress.Rank("speed") * 0.2f;
            AttackSpeed = progress.Rank("attack") * 0.05f;
            DodgeMultiplier = 1f - progress.Rank("dodge") * 0.03f;
            BackupDrive = progress.Rank(PermanentUpgradeCatalog.BackupDriveId) > 0;
            GuardianDamage = progress.Rank(PermanentUpgradeCatalog.ApexPredatorId) * 0.1f;
            float chip = progress.Rank(PermanentUpgradeCatalog.TargetingChipId) * 0.03f;
            PhysicalCritChance += chip;
            EffectChance += chip;
            if (!progress.IsSwitchedOff(PermanentUpgradeCatalog.InfernalPactId))
            {
                int pact = progress.Rank(PermanentUpgradeCatalog.InfernalPactId);
                DamageMultiplier = 1f + pact * 0.1f;
                MaxHealthMultiplier = 1f - pact * 0.1f;
            }
            switch (weapon)
            {
                case WeaponType.Sword:
                    Health += progress.Rank("knight_health");
                    ReflectionDamage = progress.Rank("knight_reflect"); break;
                case WeaponType.Bow:
                    Damage += progress.Rank("archer_damage");
                    DrawMultiplier = 1f - progress.Rank("archer_draw") * 0.05f; break;
                case WeaponType.Staff:
                    LightningDamage = progress.Rank("wizard_lightning");
                    EffectChance += progress.Rank("wizard_effect") * 0.03f; break;
                case WeaponType.Daggers:
                    Speed += progress.Rank("assassin_speed") * 0.15f;
                    PhysicalCritChance += progress.Rank("assassin_crit") * 0.03f; break;
                case WeaponType.Hammer:
                    Health += progress.Rank("paladin_health");
                    BlessingDuration = progress.Rank("paladin_blessing"); break;
                case WeaponType.Fists:
                    Health += progress.Rank("brawler_health");
                    BarragePunches = progress.Rank("brawler_barrage"); break;
                case WeaponType.Tail:
                    Health += progress.Rank("demoness_health");
                    ParalyzedDamage = progress.Rank("demoness_paralysis"); break;
                case WeaponType.Coins:
                    PurseFloor = progress.Rank("gambler_pockets");
                    GambleLuck = progress.Rank("gambler_luck") * 0.04f; break;
                case WeaponType.Scythe:
                    Health += progress.Rank("reaper_health");
                    ExtraSoulChance = ExtraSoulChances[UnityEngine.Mathf.Clamp(progress.Rank("reaper_souls"), 0, ExtraSoulChances.Length - 1)]; break;
                case WeaponType.Beam:
                    Health += progress.Rank("augment_health");
                    CannonCooldownMultiplier = 1f - progress.Rank("augment_capacitor") * 0.1f; break;
            }
        }
    }
}

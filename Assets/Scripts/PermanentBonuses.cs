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
        public float EffectChance { get; }
        public float PhysicalCritChance { get; }
        public float BlessingDuration { get; }
        public int BarragePunches { get; }
        public int ParalyzedDamage { get; }
        /// <summary>The class mechanic on R, bought in the Ash shop. The Gambler's purse shop comes free.</summary>
        public bool MechanicUnlocked { get; }

        public PermanentBonuses(PermanentProgress progress, WeaponType weapon)
        {
            MechanicUnlocked = weapon == WeaponType.Coins;
            if (progress == null) return;
            MechanicUnlocked |= progress.Rank(PermanentUpgradeCatalog.MechanicId(weapon)) > 0;
            Health = progress.Rank("health");
            Damage = progress.Rank("damage");
            Speed = progress.Rank("speed") * 0.2f;
            AttackSpeed = progress.Rank("attack") * 0.05f;
            DodgeMultiplier = 1f - progress.Rank("dodge") * 0.03f;
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
                    EffectChance = progress.Rank("wizard_effect") * 0.03f; break;
                case WeaponType.Daggers:
                    Speed += progress.Rank("assassin_speed") * 0.15f;
                    PhysicalCritChance = progress.Rank("assassin_crit") * 0.03f; break;
                case WeaponType.Hammer:
                    Health += progress.Rank("paladin_health");
                    BlessingDuration = progress.Rank("paladin_blessing"); break;
                case WeaponType.Fists:
                    Health += progress.Rank("brawler_health");
                    BarragePunches = progress.Rank("brawler_barrage"); break;
                case WeaponType.Tail:
                    Health += progress.Rank("demoness_health");
                    ParalyzedDamage = progress.Rank("demoness_paralysis"); break;
            }
        }
    }
}

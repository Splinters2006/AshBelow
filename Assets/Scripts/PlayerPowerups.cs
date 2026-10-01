using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class PlayerPowerups : MonoBehaviour
    {
        private readonly Dictionary<PowerupType, int> stacks = new Dictionary<PowerupType, int>();
        private int harvestKills, soulShieldKills;
        public WeaponType ClassWeapon { get; set; }
        public PermanentBonuses Permanent { get; set; } = new PermanentBonuses(null, WeaponType.Sword);
        public PlayerAbilities Abilities { get; set; }
        public float DrawTimeMultiplier => (1f - 0.15f * Count(PowerupType.QuickDraw)) * Permanent.DrawMultiplier;
        public float ArrowChargeMultiplier => 3f + 0.5f * Count(PowerupType.Bodkin);
        public int ReflectionDamage => 2 + Count(PowerupType.Riposte) + Permanent.ReflectionDamage;
        public int ArmorCharges { get; private set; }
        /// <summary>Frenzy's attack and charge speed bonus.</summary>
        public const float FrenzyAttackSpeed = 0.25f;
        public float AttackIntervalMultiplier => 1f / (1f + 0.2f * Count(PowerupType.AttackSpeed) + FrenzyAttackSpeed * Count(PowerupType.Frenzy) + Permanent.AttackSpeed);
        /// <summary>The Wizard starts with a 15% base chance (crits and elemental effects); everyone else with 5%.</summary>
        public float CritChance => (ClassWeapon == WeaponType.Staff ? 0.15f : 0.05f) + Count(PowerupType.CriticalHits) * 0.1f;
        public float ElementalEffectChance => Mathf.Min(0.9f, CritChance + Permanent.EffectChance + Count(PowerupType.Stormcraft) * 0.05f);
        public float PhysicalCritChance => Mathf.Min(0.9f, CritChance + Permanent.PhysicalCritChance
            + (ClassWeapon == WeaponType.Daggers ? 0.1f + Count(PowerupType.AssassinCrit) * 0.05f : 0f));
        public float DodgeCooldownMultiplier => (1f - 0.1f * Count(PowerupType.DodgeRecovery)) * Permanent.DodgeMultiplier;
        public int Count(PowerupType type) => stacks.TryGetValue(type, out int count) ? count : 0;
        public bool CanTake(PowerupType type) => Count(type) < PowerupCatalog.Get(type).MaxStacks
            && (!PowerupCatalog.Get(type).ClassWeapon.HasValue || PowerupCatalog.Get(type).ClassWeapon == ClassWeapon)
            && (PowerupCatalog.Get(type).RequiredAbility == AbilityType.None
                || (Abilities != null && Abilities.IsLearned(PowerupCatalog.Get(type).RequiredAbility)));

        public bool Add(PowerupType type)
        {
            if (!CanTake(type)) return false;
            stacks[type] = Count(type) + 1;
            if (type == PowerupType.Armor || type == PowerupType.PaladinWard) ArmorCharges++;
            return true;
        }

        public float CriticalMultiplier => 2f + Count(PowerupType.DeadlyPrecision) * 0.25f;
        public float RelicCooldownMultiplier => 1f - Count(PowerupType.RelicTraining) * 0.08f;
        public float SkillCooldownMultiplier => 1f - 0.1f * Count(ClassWeapon switch
        {
            WeaponType.Sword => PowerupType.GuardDrills,
            WeaponType.Bow => PowerupType.VolleyDrills,
            WeaponType.Staff => PowerupType.StormRhythm,
            WeaponType.Daggers => PowerupType.ShadowDance,
            WeaponType.Hammer => PowerupType.DivineCadence,
            WeaponType.Fists => PowerupType.SecondRound,
            WeaponType.Tail => PowerupType.TailRhythm,
            WeaponType.Coins => PowerupType.QuickDeal,
            WeaponType.Beam => PowerupType.HeatSink,
            _ => PowerupType.NightCycle
        });
        public int CriticalDamage(int damage) => Mathf.RoundToInt(damage * CriticalMultiplier);
        public int DamageForRoll(int damage, float roll) => roll < PhysicalCritChance ? CriticalDamage(damage) : damage;
        public int RollDamage(int damage) => DamageForRoll(damage, Random.value);
        public void BeginFloor() { ArmorCharges = Count(PowerupType.Armor) + Count(PowerupType.PaladinWard); }
        /// <summary>An extra ward for the rest of this floor (Shield Taunt blocks, the Gambler's Lucky Charm).</summary>
        public void AddWard() => ArmorCharges++;
        public bool AbsorbHit()
        {
            if (ArmorCharges <= 0) return false;
            ArmorCharges--;
            return true;
        }

        // ---------------------------------------------------------------- universal expansion talents

        public const float LastStandSpeed = 1.2f, CloseCallCooldown = 5f, ThornsRadius = 1f;
        public const int RhythmBeat = 4, SpellbladeStrikes = 3, SpellbladeBonus = 2;
        private int rhythmCount, spellbladeStrikes;
        private bool elementalPrimed, inBasicAttack, rhythmBeat, spellbladeActive;
        private float closeCallReadyAt;
        public const int TipJarCoins = 25;
        private int tipJar;

        /// <summary>Tip Jar: every 25 gold coins picked up heal 1 HP.</summary>
        public void OnCoinsPicked(DungeonPlayer player, int coins)
        {
            if (Count(PowerupType.TipJar) == 0) return;
            tipJar += coins;
            while (tipJar >= TipJarCoins) { tipJar -= TipJarCoins; player.Heal(1); HeroVfx.Motes(player.Run.ProjectileRoot, player.transform.position, 0.6f, GamblerAttack.Gold, 10, 0.8f); }
        }

        public const int ZealStacks = 10;
        /// <summary>Zeal: stacks from the Paladin's hits; a full count doubles the next blessing.</summary>
        public int Zeal { get; private set; }
        public void AddZeal() { if (Count(PowerupType.Zeal) > 0 && Zeal < ZealStacks) Zeal++; }
        public bool ConsumeZeal()
        {
            if (Zeal < ZealStacks) return false;
            Zeal = 0;
            return true;
        }

        /// <summary>Ambush: the next hit after the hero was hidden deals double damage.</summary>
        public bool AmbushReady { get; set; }
        private int bloodTrailCrits;

        /// <summary>Blood Trail: every 10th critical hit heals 1 HP.</summary>
        public void OnCritical(DungeonPlayer player)
        {
            if (Count(PowerupType.BloodTrail) == 0 || ++bloodTrailCrits < 10) return;
            bloodTrailCrits = 0;
            player.Heal(1);
            HeroVfx.Motes(player.Run.ProjectileRoot, player.transform.position, 0.6f, DungeonEnemy.BleedColor, 8, 0.7f);
        }

        /// <summary>Soul Fury: extra damage per rank for every soul held.</summary>
        public const float SoulFuryPerSoul = 0.01f;
        /// <summary>Cheat Death's once-per-world save is spent.</summary>
        public bool CheatDeathSpent { get; set; }

        /// <summary>
        /// Damage multiplier from the hero's own state: Glass Cannon, Last Stand, Berserker, Soul Fury and the Ash shop's
        /// Infernal Pact, plus Rhythm's double beat while a basic attack is being thrown.
        /// </summary>
        public float DamageMultiplier(DungeonPlayer player)
        {
            float multiplier = Permanent.DamageMultiplier;
            if (Count(PowerupType.GlassCannon) > 0) multiplier *= 1.5f;
            if (Count(PowerupType.LastStand) > 0 && player.Health == 1) multiplier *= 2f;
            if (Count(PowerupType.Berserker) > 0 && player.MaxHealth > 0)
                multiplier *= 1f + (player.MaxHealth - Mathf.Max(0, player.Health)) / (float)player.MaxHealth;
            if (inBasicAttack && rhythmBeat) multiplier *= 2f;
            // Soul Fury: every soul the Reaper holds makes him hit harder.
            if (Count(PowerupType.SoulFury) > 0 && player.Weapon is ReaperAttack reaper)
                multiplier *= 1f + SoulFuryPerSoul * Count(PowerupType.SoulFury) * reaper.Souls;
            return multiplier;
        }
        /// <summary>Flat damage added while a basic attack is being thrown (Spellblade).</summary>
        public int BasicAttackBonus => inBasicAttack && spellbladeActive ? SpellbladeBonus : 0;
        /// <summary>Glass Cannon and the Ash shop's Infernal Pact scale maximum HP.</summary>
        public float MaxHealthMultiplier => (Count(PowerupType.GlassCannon) > 0 ? 0.5f : 1f) * Permanent.MaxHealthMultiplier;
        public float MoveMultiplier(DungeonPlayer player) => Count(PowerupType.LastStand) > 0 && player.Health == 1 ? LastStandSpeed : 1f;

        /// <summary>
        /// Wraps a basic attack or class skill (left or right click) so Rhythm and Spellblade apply to the damage it
        /// deals as it is thrown. Returns whether the attack went off; only then does it count toward the beat.
        /// </summary>
        public bool BasicAttack(System.Func<bool> attack)
        {
            rhythmBeat = Count(PowerupType.Rhythm) > 0 && (rhythmCount + 1) % RhythmBeat == 0;
            spellbladeActive = spellbladeStrikes > 0;
            inBasicAttack = true;
            bool thrown;
            try { thrown = attack(); }
            finally { inBasicAttack = false; }
            if (!thrown) return false;
            if (Count(PowerupType.Rhythm) > 0) rhythmCount++;
            if (spellbladeActive) spellbladeStrikes--;
            return true;
        }

        /// <summary>A Q or E ability was used: Spellblade charges the next basic attacks.</summary>
        public void OnAbilityUsed() { if (Count(PowerupType.Spellblade) > 0) spellbladeStrikes = SpellbladeStrikes; }

        /// <summary>Elemental Kills: whether this elemental hit sets off its element for free (and spends the charge).</summary>
        public bool ConsumeElementalPrime()
        {
            if (!elementalPrimed) return false;
            elementalPrimed = false;
            return true;
        }

        /// <summary>Close Call: a roll just slipped through an enemy bolt.</summary>
        public void OnCloseCall(DungeonPlayer player)
        {
            if (Count(PowerupType.CloseCall) == 0 || Time.time < closeCallReadyAt) return;
            closeCallReadyAt = Time.time + CloseCallCooldown;
            AddWard();
            HeroVfx.Pulse(player.transform, player.transform.position, 0.9f, AbilityCatalog.Ice, 0.35f);
        }

        /// <summary>Thorns: when the hero is hit, everything within a unit takes their damage.</summary>
        public void OnStruck(DungeonPlayer player)
        {
            if (Count(PowerupType.Thorns) == 0) return;
            Vector2 center = player.transform.position;
            HeroVfx.Pulse(player.Run.ProjectileRoot, center, ThornsRadius, new Color(0.6f, 0.9f, 0.4f), 0.25f);
            foreach (var enemy in player.Run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= ThornsRadius + enemy.HitRadius)
                    CombatDamage.Apply(player, enemy, player.Damage, DamageElement.Physical, center, 0.8f);
        }

        public const float IronGripMultiplier = 1.25f, NumbingChill = 2f, DominoRadius = 1.5f, DominoStun = 0.75f;
        private float elementalImmobilizationReadyAt;
        /// <summary>Elemental Immobilization: true if it is off its cooldown, which this starts.</summary>
        public bool TryElementalImmobilization()
        {
            if (Time.time < elementalImmobilizationReadyAt) return false;
            elementalImmobilizationReadyAt = Time.time + CombatDamage.ElementalImmobilizationCooldown;
            return true;
        }

        /// <summary>Iron Grip: how much longer every paralysis, freeze, stun and root the hero inflicts lasts.</summary>
        public float HoldDurationMultiplier => Count(PowerupType.IronGrip) > 0 ? IronGripMultiplier : 1f;

        /// <summary>
        /// The hero just immobilized an enemy that was moving freely (repeat holds on an already held enemy do not count):
        /// Searing Hold, Static Hold and Numbing Hold set off their elements. <paramref name="duration"/> is the hold's length.
        /// A <paramref name="harmless"/> hold (Sow) only numbs: the damaging talents stay quiet.
        /// </summary>
        public void OnImmobilized(DungeonPlayer player, DungeonEnemy enemy, float duration, bool harmless = false)
        {
            int hit = player.Damage;
            if (!harmless && Count(PowerupType.StaticHold) > 0) CombatDamage.ApplyEffect(player, enemy, DamageElement.Lightning, hit);
            if (enemy.Health <= 0) return;
            if (!harmless && Count(PowerupType.SearingHold) > 0) CombatDamage.ApplyEffect(player, enemy, DamageElement.Fire, hit);
            // The chill runs out past the hold, so the enemy crawls for a while once it breaks free.
            if (Count(PowerupType.NumbingHold) > 0) enemy.Chill(duration + NumbingChill);
        }

        /// <summary>Domino: a held enemy's death stuns everything close around it, which may set off the hold talents again.</summary>
        private static void Domino(DungeonPlayer player, DungeonEnemy dead)
        {
            var run = player.Run;
            Vector2 center = dead.transform.position;
            HeroVfx.Pulse(run.ProjectileRoot, center, DominoRadius, DungeonEnemy.StunnedTint, 0.3f);
            CoopFx.Pulse(run, center, DominoRadius, DungeonEnemy.StunnedTint, 0.3f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy != dead && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= DominoRadius + enemy.HitRadius)
                    enemy.Stun(DominoStun);
        }

        public const float KillStreakWindow = 1f, KillCooldownCut = 0.5f, PyreRadius = 2f;
        public const int MassacreKills = 5, MomentumKills = 2;
        private readonly Queue<float> recentKills = new Queue<float>();

        /// <summary>Kill talents, applied only on the killer's machine (<paramref name="enemy"/> is still in place, statuses intact).</summary>
        public void OnKill(DungeonPlayer player, DungeonEnemy enemy)
        {
            if (Count(PowerupType.SoulShield) > 0 && ++soulShieldKills >= 8)
            {
                soulShieldKills = 0;
                if (ArmorCharges < 3) AddWard();
            }
            int rank = Count(PowerupType.LifeSteal);
            if (rank > 0 && ++harvestKills >= 6 - rank) { harvestKills = 0; player.Heal(1); }

            while (recentKills.Count > 0 && Time.time - recentKills.Peek() > KillStreakWindow) recentKills.Dequeue();
            recentKills.Enqueue(Time.time);
            // Each streak pays out once, when it reaches its count, rather than on every kill after that.
            if (recentKills.Count == MomentumKills && Count(PowerupType.Momentum) > 0) player.ResetDodge();
            if (recentKills.Count == MassacreKills && Count(PowerupType.Massacre) > 0) player.ResetClassSkill();

            // Nerve Snap, Still Hunter and Domino count any immobilized enemy: paralysed, frozen, stunned or rooted.
            bool held = enemy != null && enemy.IsImmobilized;
            if (Count(PowerupType.ElementalKills) > 0) elementalPrimed = true;
            if (held && Count(PowerupType.NerveSnap) > 0) player.ResetClassSkill();
            float cut = (Count(PowerupType.Bloodrush) > 0 ? KillCooldownCut : 0f) + (held && Count(PowerupType.StillHunter) > 0 ? KillCooldownCut : 0f);
            if (cut > 0f) player.ReduceCooldowns(cut);

            if (enemy != null && enemy.IsBurning && Count(PowerupType.PyreBurst) > 0) PyreBurst(player, enemy);
            if (held && Count(PowerupType.Domino) > 0) Domino(player, enemy);
        }

        /// <summary>A burning enemy bursts into flame: fire damage to everything around it, which may light them too.</summary>
        private static void PyreBurst(DungeonPlayer player, DungeonEnemy dead)
        {
            var run = player.Run;
            Vector2 center = dead.transform.position;
            var color = CombatDamage.ElementColor(DamageElement.Fire);
            HeroVfx.Pulse(run.ProjectileRoot, center, PyreRadius, color, 0.35f);
            HeroVfx.Sparks(run.ProjectileRoot, center, color, 14, 5f, 0.4f, null, 360f, 1.2f);
            CoopFx.Pulse(run, center, PyreRadius, color, 0.35f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy != dead && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= PyreRadius + enemy.HitRadius)
                    CombatDamage.Apply(player, enemy, player.Damage, DamageElement.Fire, center, 0.6f);
        }

        /// <summary>Prospector: whether a fallen enemy leaves a second helping of crystals.</summary>
        public bool RollExtraCrystals() => Count(PowerupType.Prospector) > 0 && Random.value <= 0.5f * Count(PowerupType.Prospector);
        /// <summary>Haggler: what the crystal merchant charges as a fraction of his price.</summary>
        public float ShopPriceMultiplier => 1f - 0.25f * Count(PowerupType.Haggler);
        public int ShopRerolls => Count(PowerupType.MerchantsFavor);

        public string Summary
        {
            get
            {
                var names = new List<string>();
                foreach (var powerup in PowerupCatalog.All)
                    if (Count(powerup.Type) > 0) names.Add(powerup.Name + " x" + Count(powerup.Type));
                return names.Count == 0 ? "No boons yet" : string.Join("  /  ", names);
            }
        }
    }
}

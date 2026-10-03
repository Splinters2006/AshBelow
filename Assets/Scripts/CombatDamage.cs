using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// What a hit is made of. Fire, lightning and ice can set off their element; holy (the Paladin's light) and demonic
    /// (the Demoness's powers) are their own kinds of damage that neither crit nor set off an effect.
    /// </summary>
    public enum DamageElement { Physical, Fire, Lightning, Ice, Holy, Demonic }

    public static class CombatDamage
    {
        public static int ShadowstepDamageForRoll(DungeonPlayer player, float roll)
        {
            int backstab = player.Charge.Damage(1f) * 2 + player.Powerups.Count(PowerupType.Backstab);
            float chance = Mathf.Clamp01(player.Powerups.PhysicalCritChance * 2f);
            return roll < chance ? player.Powerups.CriticalDamage(backstab) : backstab;
        }

        /// <summary>Killer Instinct (the Assassin's passive): what a basic attack's backstab is multiplied by, and every other backstab.</summary>
        public const float BasicBackstabMultiplier = 2f, OtherBackstabMultiplier = 1.15f;

        /// <summary>Killer Instinct, applied on top of the backstab's own bonus: double for a left-click stab, 15% more (rounded up) otherwise.</summary>
        public static int KillerInstinct(DungeonPlayer player, int backstab)
        {
            if (player == null || player.Permanent == null || !player.Permanent.HasPassive(WeaponType.Daggers)) return backstab;
            bool basic = player.Charge != null && player.Charge.IsStriking && !ShadowClone.IsStriking;
            return Mathf.CeilToInt(backstab * (basic ? BasicBackstabMultiplier : OtherBackstabMultiplier) - 0.0001f);
        }

        public static void ApplyShadowstep(DungeonPlayer player, DungeonEnemy enemy)
        {
            if (enemy == null || enemy.Health <= 0) return;
            if (enemy.IsInvulnerable) { enemy.Hit(0, player.transform.position); return; }
            // Shadowstep emerges behind its victim, regardless of their turn during the blink.
            Vector2 source = (Vector2)enemy.transform.position - enemy.Facing.Direction;
            HitVfx(player, enemy.transform.position, source, new Color(0.8f, 0.5f, 1f), true);
            RearHitMarker.Show(player.Run, enemy);
            int damage = KillerInstinct(player, AssassinBonus(player, enemy, ShadowstepDamageForRoll(player, Random.value)));
            bool seize = SeizesTouched(player, enemy);
            enemy.Hit(damage, source);
            if (enemy.Health <= 0) FadeAway(player);
            if (seize) enemy.Stun(ElementalImmobilizationStun);
            player.Mechanic?.OnBackstab();
            OnBackstab(player, enemy, damage);
            CreditBlessing(player);
        }

        /// <summary>Impact sparks sprayed away from the attacker; critical hits add a gold flash.</summary>
        private static void HitVfx(DungeonPlayer player, Vector2 position, Vector2 source, Color color, bool critical)
        {
            if (player == null || player.Run == null || player.Run.ProjectileRoot == null) return;
            var root = player.Run.ProjectileRoot;
            HeroVfx.Sparks(root, position, color, critical ? 12 : 6, critical ? 5.5f : 3.8f, critical ? 0.35f : 0.25f,
                position - source, 120f, critical ? 1.3f : 1f);
            if (critical) HeroVfx.Pulse(root, position, 0.7f, AbilityCatalog.Gold, 0.25f);
        }

        public static Color ElementColor(DamageElement element) => element == DamageElement.Fire ? new Color(1f, 0.55f, 0.15f)
            : element == DamageElement.Ice ? AbilityCatalog.Ice : element == DamageElement.Lightning ? ShockColor
            : element == DamageElement.Holy ? AbilityCatalog.Gold : element == DamageElement.Demonic ? DemonessAttack.Violet : new Color(1f, 0.95f, 0.8f);

        /// <summary>Fire, lightning and ice: the elements with a status effect.</summary>
        public static bool HasEffect(DamageElement element) => element == DamageElement.Fire || element == DamageElement.Lightning || element == DamageElement.Ice;

        public const float SittingDuckMultiplier = 1.3f, ElementalImmobilizationStun = 1f, ElementalImmobilizationCooldown = 3f;

        /// <summary>
        /// Elemental Immobilization: whether this hit seizes an enemy an element has touched. An enemy that is already
        /// held keeps the touch for the first hit after it breaks free, and so does any enemy hit while the talent is
        /// still on its cooldown.
        /// </summary>
        private static bool SeizesTouched(DungeonPlayer player, DungeonEnemy enemy)
            => player != null && player.Powerups.Count(PowerupType.ElementalImmobilization) > 0 && !enemy.IsImmobilized && enemy.ElementTouched
                && player.Powerups.TryElementalImmobilization() && enemy.ConsumeElementTouch();

        /// <summary>Damage bonuses that depend on the target: Executioner, Sitting Duck and the Ash shop's Apex Predator.</summary>
        public static int ScaleForTarget(DungeonPlayer player, DungeonEnemy enemy, int damage)
        {
            if (player == null) return damage;
            float multiplier = 1f;
            if (player.Powerups.Count(PowerupType.Executioner) > 0 && enemy.HealthFraction < 0.25f) multiplier *= 1.5f;
            if (player.Powerups.Count(PowerupType.SittingDuck) > 0 && enemy.IsImmobilized) multiplier *= SittingDuckMultiplier;
            if (enemy.Boss != null || enemy.IsTank) multiplier *= 1f + player.Permanent.GuardianDamage;
            // Blood in the Water: the Samurai's attacks bite deeper into whatever is already bleeding.
            if (enemy.IsBleeding) multiplier *= 1f + BloodInTheWaterBonus[Mathf.Clamp(player.Powerups.Count(PowerupType.BloodInTheWater), 0, BloodInTheWaterBonus.Length - 1)];
            return multiplier == 1f ? damage : Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
        }

        /// <summary>Blood in the Water's extra damage to bleeding enemies, by rank.</summary>
        public static readonly float[] BloodInTheWaterBonus = { 0f, 0.1f, 0.15f, 0.25f };
        /// <summary>Deep Wounds' extra cuts per bleed, by rank.</summary>
        public static readonly int[] DeepWoundsTicks = { 0, 1, 3, 5 };
        public const float JaggedBladeChance = 0.1f;

        /// <summary>Bled Dry's stun once a bleed has run out, by rank.</summary>
        public static readonly float[] BledDryStun = { 0f, 0.5f, 1f, 1.5f };

        /// <summary>How many times a bleed this hero opens cuts: the base ten plus Deep Wounds, over the same five seconds.</summary>
        public static int BleedTicksFor(DungeonPlayer player)
            => DungeonEnemy.BleedTicks + (player != null ? DeepWoundsTicks[Mathf.Clamp(player.Powerups.Count(PowerupType.DeepWounds), 0, DeepWoundsTicks.Length - 1)] : 0);

        /// <summary>Opens a bleed on <paramref name="enemy"/> from a <paramref name="hit"/> this hero landed.</summary>
        public static void InflictBleed(DungeonPlayer player, DungeonEnemy enemy, int hit)
        {
            if (enemy == null || enemy.Health <= 0 || hit <= 0) return;
            float stun = player != null ? BledDryStun[Mathf.Clamp(player.Powerups.Count(PowerupType.BledDry), 0, BledDryStun.Length - 1)] : 0f;
            // Pinned Wounds: the Samurai's bleeds cut deeper while their victim cannot move.
            float held = player != null && player.Powerups.Count(PowerupType.PinnedWounds) > 0 ? PinnedWoundsMultiplier : 1f;
            enemy.Bleed(hit, DungeonEnemy.BleedDuration, BleedTicksFor(player), stun, held);
        }

        /// <summary>Pinned Wounds: how much harder a bleed cuts while its victim is immobilized.</summary>
        public const float PinnedWoundsMultiplier = 1.5f;

        /// <summary>The whole damage a bleed this hero opens from <paramref name="hit"/> would deal on <paramref name="enemy"/>, every cut together.</summary>
        public static float BleedTotal(DungeonPlayer player, DungeonEnemy enemy, int hit)
        {
            if (enemy == null || hit <= 0) return 0f;
            float held = player != null && enemy.IsImmobilized && player.Powerups.Count(PowerupType.PinnedWounds) > 0 ? PinnedWoundsMultiplier : 1f;
            return hit * DungeonEnemy.BleedTickShare * BleedTicksFor(player) * held;
        }

        /// <summary>
        /// A physical hit that leaves its victim bleeding. If the hit kills before the wound opens, the enemy still died
        /// bleeding (see <see cref="DungeonEnemy.BleedPending"/>).
        /// </summary>
        public static void ApplyBleeding(DungeonPlayer player, DungeonEnemy enemy, int damage, Vector2 source, float knockback = 1f)
        {
            if (enemy == null || enemy.Health <= 0) return;
            float bleed = BleedTotal(player, enemy, damage);
            enemy.BeginBleedingBlow(bleed);
            try { Apply(player, enemy, damage, DamageElement.Physical, source, knockback); }
            finally { enemy.EndBleedingBlow(bleed); }
            InflictBleed(player, enemy, damage);
        }

        /// <summary>The Samurai's katana: Blood Shall Flow bleeds on every hit and Jagged Blade on one in ten. A hit can open both.</summary>
        private static int KatanaWounds(DungeonPlayer player)
        {
            if (player == null || !(player.Weapon is SamuraiAttack samurai)) return 0;
            int wounds = samurai.IsBloodFlowing ? 1 : 0;
            if (player.Powerups.Count(PowerupType.JaggedBlade) > 0 && Random.value < JaggedBladeChance) wounds++;
            return wounds;
        }

        public const float OverkillBaseRadius = 1f, OverkillRadiusPerDamage = 0.25f, OverkillMaxRadius = 3.5f;

        /// <summary>Overkill: what a killing blow had left over hits the nearest enemy, reaching farther the bigger the overkill.</summary>
        private static void Overkill(DungeonPlayer player, DungeonEnemy dead, int excess)
        {
            if (player == null || excess <= 0 || player.Powerups.Count(PowerupType.Overkill) == 0) return;
            Vector2 from = dead.transform.position;
            float radius = Mathf.Min(OverkillMaxRadius, OverkillBaseRadius + excess * OverkillRadiusPerDamage);
            DungeonEnemy nearest = null;
            float best = float.MaxValue;
            foreach (var enemy in player.Run.Enemies)
            {
                if (enemy == null || enemy == dead || enemy.Health <= 0) continue;
                float distance = Vector2.Distance(from, enemy.transform.position) - enemy.HitRadius;
                if (distance <= radius && distance < best) { best = distance; nearest = enemy; }
            }
            if (nearest == null) return;
            var color = new Color(1f, 0.4f, 0.3f);
            CombatVfx.Bolt(player.Run.ProjectileRoot, from, nearest.transform.position, color);
            CoopFx.Bolt(player.Run, from, nearest.transform.position, color);
            nearest.Hit(excess, from, 0.5f);
        }

        /// <param name="knockback">Scales how far the hit shoves the enemy (1 = normal).</param>
        /// <param name="infusion">
        /// An element carried by a physical hit (the Archer's Elemental Quiver): it still crits normally, and a critical
        /// hit also sets off that element's effect (only ever the element loaded when the arrow was loosed).
        /// </param>
        /// <param name="guaranteedEffect">An elemental hit skips its effect roll and always sets off its element (Wild Storm, a storm-charged Inferno Orb, a full plasma cannon).</param>
        /// <param name="guaranteedCrit">A physical hit that is always critical (the tip of the Specimen's chain whip).</param>
        public static void Apply(DungeonPlayer player, DungeonEnemy enemy, int damage, DamageElement element, Vector2 source, float knockback = 1f,
            DamageElement infusion = DamageElement.Physical, bool guaranteedEffect = false, bool guaranteedCrit = false)
        {
            if (enemy == null || enemy.Health <= 0) return;
            // Demonic Runes: every hit the Demoness lands on an immobilized guardian may shake a rune loose.
            if (enemy.Boss != null && !enemy.IsInvulnerable && enemy.IsImmobilized) DemonicRune.TryDropFromGuardian(player, enemy);
            // Under a demonic rune (or as the Avatar of Death) every hit paralyses whatever survives it, so a hit that kills
            // still counts its victim as held (see DungeonEnemy.HoldPending).
            bool holding = player != null && player.Buffs != null && !enemy.IsInvulnerable
                && ((player.Buffs.IsRuneEmpowered && player.Weapon is DemonessAttack) || player.Buffs.IsIncarnate);
            if (!holding) { ApplyHit(player, enemy, damage, element, source, knockback, infusion, guaranteedEffect, guaranteedCrit); return; }
            enemy.BeginHoldingBlow();
            try { ApplyHit(player, enemy, damage, element, source, knockback, infusion, guaranteedEffect, guaranteedCrit); }
            finally { enemy.EndHoldingBlow(); }
        }

        private static void ApplyHit(DungeonPlayer player, DungeonEnemy enemy, int damage, DamageElement element, Vector2 source, float knockback,
            DamageElement infusion, bool guaranteedEffect, bool guaranteedCrit)
        {
            if (enemy.IsInvulnerable) { enemy.Hit(0, source); return; }
            damage = ScaleForTarget(player, enemy, damage);
            int healthBefore = enemy.Health;
            // Checked before this hit's own element lands, so the hit that touches an enemy is never the one that seizes it.
            bool seize = SeizesTouched(player, enemy);
            // Opening Strike: the first hit on an unhurt enemy always crits (or, if elemental, sets off its element).
            bool opening = player != null && player.Powerups.Count(PowerupType.OpeningStrike) > 0 && enemy.IsUnhurt;
            // Smoke Bomb: hits on enemies inside the Assassin's smoke always count as backstabs.
            bool behind = enemy.Facing.IsBehind(source) || (player != null && player.ClassWeapon == WeaponType.Daggers && SmokeCloud.Covers(enemy.transform.position));
            if (behind) RearHitMarker.Show(player != null ? player.Run : null, enemy);
            if (element == DamageElement.Physical)
            {
                if (player.ClassWeapon == WeaponType.Daggers && behind)
                    damage = KillerInstinct(player, damage * 2 + player.Powerups.Count(PowerupType.Backstab));
                damage = AssassinBonus(player, enemy, damage) + BrawlBonus(player);
                // The Specimen's exposed, bound and tripped enemies take a critical hit from every blow.
                bool forced = guaranteedCrit || (player.Weapon is SpecimenAttack marks && marks.ForcesCrit(enemy));
                int rolled = opening || forced ? player.Powerups.CriticalDamage(damage) : player.Powerups.RollDamage(damage);
                bool critical = rolled > damage;
                if (critical) player.Powerups.OnCritical(player);
                HitVfx(player, enemy.transform.position, source, infusion != DamageElement.Physical ? ElementColor(infusion) : new Color(1f, 0.95f, 0.8f), critical);
                // Barbed Arrows (the Archer's second passive): her critical strikes open a bleed.
                bool barbed = critical && player.ClassWeapon == WeaponType.Bow && player.Permanent.HasSecondPassive(WeaponType.Bow);
                int katanaWounds = KatanaWounds(player);
                // Every wound this hit opens once it lands, counted first so a hit that kills still killed a bleeding enemy.
                int wounds = katanaWounds + (barbed ? 1 : 0)
                    + (critical && player.Weapon is SpecimenAttack edge && edge.BleedsOnCritical ? 1 : 0)
                    + (player.ClassWeapon == WeaponType.Daggers && behind && player.Powerups.Count(PowerupType.Bleed) > 0 ? 1 : 0);
                float bleed = wounds * BleedTotal(player, enemy, rolled);
                enemy.BeginBleedingBlow(bleed);
                try { enemy.Hit(rolled, source, knockback); }
                finally { enemy.EndBleedingBlow(bleed); }
                if (seize) enemy.Stun(ElementalImmobilizationStun);
                if (critical && player.Weapon is SpecimenAttack specimen) specimen.OnCritical(enemy, rolled);
                if (barbed) InflictBleed(player, enemy, rolled);
                if (player.ClassWeapon == WeaponType.Daggers && behind)
                {
                    if (!ShadowClone.IsStriking) player.Mechanic?.OnBackstab();
                    OnBackstab(player, enemy, rolled);
                    if (enemy.Health <= 0) FadeAway(player);
                }
                CreditBlessing(player);
                IncarnateFear(player, enemy);
                RuneParalysis(player, enemy);
                for (int i = 0; i < katanaWounds; i++) InflictBleed(player, enemy, rolled);
                // Elemental Surge: while the Archer's quiver surges, every infused hit sets off its element, not only crits.
                if (infusion != DamageElement.Physical && (critical || ElementalQuiver.IsImbued(player))) ApplyEffect(player, enemy, infusion, rolled);
                if (enemy.Health <= 0) Overkill(player, enemy, rolled - healthBefore);
                return;
            }
            // Pyromancer: burning enemies take +1 from lightning.
            if (element == DamageElement.Lightning && enemy.IsBurning && player.Powerups.Count(PowerupType.Pyromancer) > 0) damage++;
            bool wasBurning = enemy.IsBurning, wasFrozen = enemy.IsFrozen;
            HitVfx(player, enemy.transform.position, source, ElementColor(element), false);
            enemy.Hit(damage, source, knockback);
            if (seize) enemy.Stun(ElementalImmobilizationStun);
            CreditBlessing(player);
            IncarnateFear(player, enemy);
            RuneParalysis(player, enemy);
            if (enemy.Health <= 0) Overkill(player, enemy, damage - healthBefore);
            if (player.Powerups.Count(PowerupType.ElementalClash) > 0 && HasEffect(element))
            {
                if (wasBurning && element != DamageElement.Fire) Clash(player, enemy, DamageElement.Fire, damage);
                else if (wasFrozen && element != DamageElement.Ice) Clash(player, enemy, DamageElement.Ice, damage);
            }
            // Holy and demonic damage have no status effect; the rest roll for one instead of critical damage.
            if (!HasEffect(element)) return;
            // Elemental Kills' charge is spent on the next elemental hit, even one that would have set off anyway.
            bool primed = player.Powerups.ConsumeElementalPrime();
            if (guaranteedEffect || primed || opening || Random.value < player.Powerups.ElementalEffectChance) ApplyEffect(player, enemy, element, damage);
        }

        public const float FadeAwayTime = 1f;

        /// <summary>Fade Away (the Assassin's second passive): a backstab that kills hides her from enemies for a moment.</summary>
        private static void FadeAway(DungeonPlayer player)
        {
            if (player == null || player.Health <= 0 || player.ClassWeapon != WeaponType.Daggers || player.Permanent == null
                || !player.Permanent.HasSecondPassive(WeaponType.Daggers)) return;
            player.Veil(FadeAwayTime);
            if (player.Run != null && player.Run.ProjectileRoot != null)
            {
                var shadow = new Color(0.45f, 0.25f, 0.7f);
                HeroVfx.Motes(player.Run.ProjectileRoot, player.transform.position, 0.6f, shadow, 12, 0.7f);
                HeroVfx.Pulse(player.Run.ProjectileRoot, player.transform.position, 0.9f, FlameMesh.Alpha(shadow, 0.6f), 0.3f);
            }
        }

        /// <summary>The Assassin's bonuses to her own physical hits: Ambush (spent) and Poisoner.</summary>
        private static int AssassinBonus(DungeonPlayer player, DungeonEnemy enemy, int damage)
        {
            if (player == null || player.ClassWeapon != WeaponType.Daggers) return damage;
            if (player.Powerups.AmbushReady) { player.Powerups.AmbushReady = false; damage *= 2; }
            if (player.Powerups.Count(PowerupType.Poisoner) > 0 && enemy.HasDamageOverTime) damage++;
            return damage;
        }

        public const float BrawlRadius = 2f;

        /// <summary>Brawl: +1 for every enemy crowding the Brawler (a guardian counts as 3).</summary>
        private static int BrawlBonus(DungeonPlayer player)
        {
            if (player == null || player.ClassWeapon != WeaponType.Fists || player.Powerups.Count(PowerupType.Brawl) == 0) return 0;
            int bonus = 0;
            Vector2 at = player.transform.position;
            foreach (var enemy in player.Run.Enemies)
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= BrawlRadius + enemy.HitRadius)
                    bonus += enemy.Boss != null ? 3 : 1;
            return bonus;
        }

        /// <summary>A backstab landed: Bleed opens a wound and Shadow Clone sends a clone to stab again.</summary>
        private static void OnBackstab(DungeonPlayer player, DungeonEnemy enemy, int damage)
        {
            if (enemy == null || enemy.Health <= 0) return;
            if (player.Powerups.Count(PowerupType.Bleed) > 0) InflictBleed(player, enemy, damage);
            ShadowClone.OnBackstab(player, enemy, Mathf.Max(1, damage / 2));
        }

        public const float ClashRadius = 2f;

        /// <summary>Elemental Clash: a second element on a burning or frozen enemy spreads the first to everything within 2 units.</summary>
        private static void Clash(DungeonPlayer player, DungeonEnemy origin, DamageElement first, int hit)
        {
            var run = player.Run;
            Vector2 center = origin.transform.position;
            var color = ElementColor(first);
            HeroVfx.Pulse(run.ProjectileRoot, center, ClashRadius, color, 0.3f);
            CoopFx.Pulse(run, center, ClashRadius, color, 0.3f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy == origin || enemy.Health <= 0 || Vector2.Distance(center, enemy.transform.position) > ClashRadius + enemy.HitRadius) continue;
                if (first == DamageElement.Fire) enemy.Burn(BurnTicksFor(player), BurnTickDamage(hit));
                else enemy.Freeze(FreezeDurationFor(player));
            }
        }

        /// <summary>The incarnation of death: every hit the Reaper lands strikes fear into whatever survives it.</summary>
        private static void IncarnateFear(DungeonPlayer player, DungeonEnemy enemy)
        {
            if (player != null && player.Buffs != null && player.Buffs.IsIncarnate && enemy != null && enemy.Health > 0)
                enemy.Fear(player.transform.position, ArmyOfTheDead.IncarnationFear);
        }

        /// <summary>A demonic rune: every hit the Demoness lands paralyses whatever survives it.</summary>
        private static void RuneParalysis(DungeonPlayer player, DungeonEnemy enemy)
        {
            if (player != null && player.Buffs != null && player.Buffs.IsRuneEmpowered && enemy != null && enemy.Health > 0
                && player.Weapon is DemonessAttack tail) tail.RuneParalyze(enemy);
        }

        /// <summary>Hits dealt while blessed charge the Paladin who gave the blessing; the Paladin's own hits build Zeal.</summary>
        private static void CreditBlessing(DungeonPlayer player)
        {
            if (player != null && player.ClassWeapon == WeaponType.Hammer) player.Powerups.AddZeal();
            if (player != null && player.Blessing != null && player.Blessing.BonusDamage > 0) player.Blessing.Credit(player);
        }

        // ---------------------------------------------------------------- elemental effects

        public const int BurnTicks = 3;
        public const float FreezeDuration = 1.5f;
        public const float ShockRadius = 2f, ShockShare = 0.25f;
        public static readonly Color ShockColor = new Color(0.75f, 0.9f, 1f);
        /// <summary>Elemental Mastery (the Wizard's passive): what his shock radius, burn ticks and freeze time start from.</summary>
        public const float MasteryMultiplier = 1.5f;
        private static bool HasMastery(DungeonPlayer player) => player != null && player.Permanent != null && player.Permanent.HasPassive(WeaponType.Staff);
        /// <summary>Scales a freeze the hero inflicts (Elemental Mastery).</summary>
        public static float FreezeScale(DungeonPlayer player) => HasMastery(player) ? MasteryMultiplier : 1f;
        /// <summary>How many times the hero's burn ticks: the base (half as many again with Elemental Mastery, rounded up) plus Slow Burn.</summary>
        public static int BurnTicksFor(DungeonPlayer player)
            => (HasMastery(player) ? Mathf.CeilToInt(BurnTicks * MasteryMultiplier) : BurnTicks) + player.Powerups.Count(PowerupType.SlowBurn);
        public static float FreezeDurationFor(DungeonPlayer player)
            => FreezeDuration * FreezeScale(player) + player.Powerups.Count(PowerupType.Permafrost) * 0.3f;
        public static float ShockRadiusFor(DungeonPlayer player)
            => ShockRadius * (HasMastery(player) ? MasteryMultiplier : 1f) + player.Powerups.Count(PowerupType.StaticField) * 0.4f;
        /// <summary>Each of a burn's three ticks deals half of the hit that lit it.</summary>
        public static int BurnTickDamage(int hit) => Mathf.Max(1, hit / 2);
        /// <summary>A shock arcs a quarter of the triggering hit into every enemy nearby.</summary>
        public static int ShockDamage(int hit) => Mathf.Max(1, Mathf.RoundToInt(hit * ShockShare));

        /// <summary>
        /// The status an element inflicts once its effect roll (or an infused critical hit) succeeds. Fire burns,
        /// ice freezes solid, and lightning shocks every other enemy within <see cref="ShockRadius"/> of the target.
        /// </summary>
        public static void ApplyEffect(DungeonPlayer player, DungeonEnemy enemy, DamageElement element, int hit)
        {
            if (enemy == null || !HasEffect(element)) return;
            player.Mechanic?.OnElementalEffect();
            enemy.TouchWithElement();
            // Kindling: freezes and shocks set the target alight as well.
            bool kindle = element != DamageElement.Fire && player.Powerups.Count(PowerupType.Kindling) > 0;
            if (element == DamageElement.Lightning) Shock(player, enemy.transform.position, enemy, hit);
            if (enemy.Health <= 0) return;
            if (element == DamageElement.Fire || kindle) enemy.Burn(BurnTicksFor(player), BurnTickDamage(hit));
            if (element == DamageElement.Ice) enemy.Freeze(FreezeDurationFor(player));
        }

        /// <summary>Lightning bolts leap from <paramref name="center"/> to every other enemy in range. The shock does not chain further.</summary>
        public static void Shock(DungeonPlayer player, Vector2 center, DungeonEnemy origin, int hit)
        {
            var run = player.Run;
            int damage = ShockDamage(hit);
            float radius = ShockRadiusFor(player);
            HeroVfx.Pulse(run.ProjectileRoot, center, radius, ShockColor, 0.2f);
            CoopFx.Pulse(run, center, radius, ShockColor, 0.2f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy == origin || enemy.Health <= 0
                    || Vector2.Distance(center, enemy.transform.position) > radius + enemy.HitRadius
                    || !run.HasLineOfSight(center, enemy.transform.position)) continue;
                Vector2 target = enemy.transform.position;
                CombatVfx.Bolt(run.ProjectileRoot, center, target, ShockColor);
                CoopFx.Bolt(run, center, target, ShockColor);
                HeroVfx.Sparks(run.ProjectileRoot, target, ShockColor, 5, 3f, 0.2f);
                enemy.TouchWithElement();
                enemy.Hit(damage, center, 0.3f);
            }
        }
    }
}

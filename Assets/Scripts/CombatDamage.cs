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

        public static void ApplyShadowstep(DungeonPlayer player, DungeonEnemy enemy)
        {
            if (enemy == null || enemy.Health <= 0) return;
            if (enemy.IsInvulnerable) { enemy.Hit(0, player.transform.position); return; }
            // Shadowstep emerges behind its victim, regardless of their turn during the blink.
            Vector2 source = (Vector2)enemy.transform.position - enemy.Facing.Direction;
            HitVfx(player, enemy.transform.position, source, new Color(0.8f, 0.5f, 1f), true);
            RearHitMarker.Show(player.Run, enemy);
            int damage = AssassinBonus(player, enemy, ShadowstepDamageForRoll(player, Random.value));
            enemy.Hit(damage, source);
            player.Mechanic?.OnBackstab();
            OnBackstab(player, enemy, damage);
            CreditBlessing(player);
        }

        /// <summary>Impact sparks sprayed away from the attacker; critical hits add a gold flash.</summary>
        private static void HitVfx(DungeonPlayer player, Vector2 position, Vector2 source, Color color, bool critical)
        {
            // The Admin class keeps its own shadow effects.
            if (player == null || player.ClassWeapon == WeaponType.Shadow || player.Run == null || player.Run.ProjectileRoot == null) return;
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

        public const float SittingDuckMultiplier = 1.3f;

        /// <summary>Damage bonuses that depend on the target: Executioner, Sitting Duck and the Ash shop's Apex Predator.</summary>
        public static int ScaleForTarget(DungeonPlayer player, DungeonEnemy enemy, int damage)
        {
            if (player == null) return damage;
            float multiplier = 1f;
            if (player.Powerups.Count(PowerupType.Executioner) > 0 && enemy.HealthFraction < 0.25f) multiplier *= 1.5f;
            if (player.Powerups.Count(PowerupType.SittingDuck) > 0 && enemy.IsImmobilized) multiplier *= SittingDuckMultiplier;
            if (enemy.Boss != null || enemy.IsTank) multiplier *= 1f + player.Permanent.GuardianDamage;
            return multiplier == 1f ? damage : Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
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
        /// hit also sets off that element's effect.
        /// </param>
        /// <param name="guaranteedEffect">An elemental hit skips its effect roll and always sets off its element (Wild Storm, Inferno Orb, Lightning, Frost Nova).</param>
        public static void Apply(DungeonPlayer player, DungeonEnemy enemy, int damage, DamageElement element, Vector2 source, float knockback = 1f,
            DamageElement infusion = DamageElement.Physical, bool guaranteedEffect = false)
        {
            if (enemy == null || enemy.Health <= 0) return;
            if (enemy.IsInvulnerable) { enemy.Hit(0, source); return; }
            damage = ScaleForTarget(player, enemy, damage);
            int healthBefore = enemy.Health;
            // Opening Strike: the first hit on an unhurt enemy always crits (or, if elemental, sets off its element).
            bool opening = player != null && player.Powerups.Count(PowerupType.OpeningStrike) > 0 && enemy.IsUnhurt;
            // Smoke Bomb: hits on enemies inside the Assassin's smoke always count as backstabs.
            bool behind = enemy.Facing.IsBehind(source) || (player != null && player.ClassWeapon == WeaponType.Daggers && SmokeCloud.Covers(enemy.transform.position));
            if (behind) RearHitMarker.Show(player != null ? player.Run : null, enemy);
            if (element == DamageElement.Physical)
            {
                if (player.ClassWeapon == WeaponType.Daggers && behind)
                    damage = damage * 2 + player.Powerups.Count(PowerupType.Backstab);
                damage = AssassinBonus(player, enemy, damage) + BrawlBonus(player);
                int rolled = opening ? player.Powerups.CriticalDamage(damage) : player.Powerups.RollDamage(damage);
                bool critical = rolled > damage;
                if (critical) player.Powerups.OnCritical(player);
                HitVfx(player, enemy.transform.position, source, infusion != DamageElement.Physical ? ElementColor(infusion) : new Color(1f, 0.95f, 0.8f), critical);
                enemy.Hit(rolled, source, knockback);
                if (player.ClassWeapon == WeaponType.Daggers && behind)
                {
                    if (!ShadowClone.IsStriking) player.Mechanic?.OnBackstab();
                    OnBackstab(player, enemy, rolled);
                }
                CreditBlessing(player);
                if (critical && infusion != DamageElement.Physical) ApplyEffect(player, enemy, infusion, rolled);
                if (enemy.Health <= 0) Overkill(player, enemy, rolled - healthBefore);
                return;
            }
            // Pyromancer: burning enemies take +1 from lightning.
            if (element == DamageElement.Lightning && enemy.IsBurning && player.Powerups.Count(PowerupType.Pyromancer) > 0) damage++;
            bool wasBurning = enemy.IsBurning, wasFrozen = enemy.IsFrozen;
            HitVfx(player, enemy.transform.position, source, ElementColor(element), false);
            enemy.Hit(damage, source, knockback);
            CreditBlessing(player);
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
            if (player.Powerups.Count(PowerupType.Bleed) > 0) enemy.Bleed(3, Mathf.Max(1, damage / 3));
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
                if (first == DamageElement.Fire) enemy.Burn(BurnTicks + player.Powerups.Count(PowerupType.SlowBurn), BurnTickDamage(hit));
                else enemy.Freeze(FreezeDuration + player.Powerups.Count(PowerupType.Permafrost) * 0.3f);
            }
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
            // Kindling: freezes and shocks set the target alight as well.
            bool kindle = element != DamageElement.Fire && player.Powerups.Count(PowerupType.Kindling) > 0;
            if (element == DamageElement.Lightning) Shock(player, enemy.transform.position, enemy, hit);
            if (enemy.Health <= 0) return;
            if (element == DamageElement.Fire || kindle) enemy.Burn(BurnTicks + player.Powerups.Count(PowerupType.SlowBurn), BurnTickDamage(hit));
            if (element == DamageElement.Ice) enemy.Freeze(FreezeDuration + player.Powerups.Count(PowerupType.Permafrost) * 0.3f);
        }

        /// <summary>Lightning bolts leap from <paramref name="center"/> to every other enemy in range. The shock does not chain further.</summary>
        public static void Shock(DungeonPlayer player, Vector2 center, DungeonEnemy origin, int hit)
        {
            var run = player.Run;
            int damage = ShockDamage(hit);
            float radius = ShockRadius + player.Powerups.Count(PowerupType.StaticField) * 0.4f;
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
                enemy.Hit(damage, center, 0.3f);
            }
        }
    }
}

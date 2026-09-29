using UnityEngine;

namespace Slopgame
{
    public enum DamageElement { Physical, Fire, Lightning, Ice }

    public static class CombatDamage
    {
        public static int ShadowstepDamageForRoll(DungeonPlayer player, float roll)
        {
            int backstab = player.Charge.Damage(1f) * 2 + player.Powerups.Count(PowerupType.Backstab);
            float chance = Mathf.Clamp01(player.Powerups.PhysicalCritChance * 2f);
            return roll < chance ? backstab * 2 : backstab;
        }

        public static void ApplyShadowstep(DungeonPlayer player, DungeonEnemy enemy)
        {
            if (enemy == null || enemy.Health <= 0) return;
            if (enemy.IsInvulnerable) { enemy.Hit(0, player.transform.position); return; }
            // Shadowstep emerges behind its victim, regardless of their turn during the blink.
            Vector2 source = (Vector2)enemy.transform.position - enemy.Facing.Direction;
            HitVfx(player, enemy.transform.position, source, new Color(0.8f, 0.5f, 1f), true);
            RearHitMarker.Show(player.Run, enemy);
            enemy.Hit(ShadowstepDamageForRoll(player, Random.value), source);
            player.Mechanic?.OnBackstab();
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
            : element == DamageElement.Ice ? AbilityCatalog.Ice : element == DamageElement.Lightning ? ShockColor : new Color(1f, 0.95f, 0.8f);

        /// <param name="knockback">Scales how far the hit shoves the enemy (1 = normal).</param>
        /// <param name="infusion">
        /// An element carried by a physical hit (the Archer's Elemental Quiver): it still crits normally, and a critical
        /// hit also sets off that element's effect.
        /// </param>
        public static void Apply(DungeonPlayer player, DungeonEnemy enemy, int damage, DamageElement element, Vector2 source, float knockback = 1f,
            DamageElement infusion = DamageElement.Physical)
        {
            if (enemy == null || enemy.Health <= 0) return;
            if (enemy.IsInvulnerable) { enemy.Hit(0, source); return; }
            bool behind = enemy.Facing.IsBehind(source);
            if (behind) RearHitMarker.Show(player != null ? player.Run : null, enemy);
            if (element == DamageElement.Physical)
            {
                if (player.ClassWeapon == WeaponType.Daggers && behind)
                    damage = damage * 2 + player.Powerups.Count(PowerupType.Backstab);
                int rolled = player.Powerups.RollDamage(damage);
                bool critical = rolled > damage;
                HitVfx(player, enemy.transform.position, source, infusion != DamageElement.Physical ? ElementColor(infusion) : new Color(1f, 0.95f, 0.8f), critical);
                enemy.Hit(rolled, source, knockback);
                if (player.ClassWeapon == WeaponType.Daggers && behind) player.Mechanic?.OnBackstab();
                CreditBlessing(player);
                if (critical && infusion != DamageElement.Physical) ApplyEffect(player, enemy, infusion, rolled);
                return;
            }
            // Elemental attacks roll for a status effect instead of critical damage.
            HitVfx(player, enemy.transform.position, source, ElementColor(element), false);
            enemy.Hit(damage, source, knockback);
            CreditBlessing(player);
            if (Random.value < player.Powerups.ElementalEffectChance) ApplyEffect(player, enemy, element, damage);
        }

        /// <summary>Hits dealt while blessed charge the Paladin who gave the blessing.</summary>
        private static void CreditBlessing(DungeonPlayer player)
        {
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
            if (enemy == null || element == DamageElement.Physical) return;
            player.Mechanic?.OnElementalEffect();
            if (element == DamageElement.Lightning) { Shock(player, enemy.transform.position, enemy, hit); return; }
            if (enemy.Health <= 0) return;
            if (element == DamageElement.Fire) enemy.Burn(BurnTicks, BurnTickDamage(hit));
            else if (element == DamageElement.Ice) enemy.Freeze(FreezeDuration);
        }

        /// <summary>Lightning bolts leap from <paramref name="center"/> to every other enemy in range. The shock does not chain further.</summary>
        public static void Shock(DungeonPlayer player, Vector2 center, DungeonEnemy origin, int hit)
        {
            var run = player.Run;
            int damage = ShockDamage(hit);
            HeroVfx.Pulse(run.ProjectileRoot, center, ShockRadius, ShockColor, 0.2f);
            CoopFx.Pulse(run, center, ShockRadius, ShockColor, 0.2f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy == origin || enemy.Health <= 0
                    || Vector2.Distance(center, enemy.transform.position) > ShockRadius + enemy.HitRadius
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

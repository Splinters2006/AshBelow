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

        /// <param name="knockback">Scales how far the hit shoves the enemy (1 = normal).</param>
        public static void Apply(DungeonPlayer player, DungeonEnemy enemy, int damage, DamageElement element, Vector2 source, float knockback = 1f)
        {
            if (enemy == null || enemy.Health <= 0) return;
            if (enemy.IsInvulnerable) { enemy.Hit(0, source); return; }
            if (enemy.Facing.IsBehind(source)) RearHitMarker.Show(player != null ? player.Run : null, enemy);
            if (element == DamageElement.Physical)
            {
                if (player.ClassWeapon == WeaponType.Daggers && enemy.Facing.IsBehind(source))
                    damage = damage * 2 + player.Powerups.Count(PowerupType.Backstab);
                int rolled = player.Powerups.RollDamage(damage);
                HitVfx(player, enemy.transform.position, source, new Color(1f, 0.95f, 0.8f), rolled > damage);
                enemy.Hit(rolled, source, knockback);
                return;
            }
            // Elemental attacks roll for a status effect instead of critical damage.
            HitVfx(player, enemy.transform.position, source, element == DamageElement.Fire ? new Color(1f, 0.55f, 0.15f)
                : element == DamageElement.Ice ? AbilityCatalog.Ice : Color.Lerp(AbilityCatalog.Ice, Color.white, 0.5f), false);
            enemy.Hit(damage, source, knockback);
            if (element == DamageElement.Lightning) return; // WizardAttack rolls the chain's overload once per cast.
            if (enemy.Health <= 0 || Random.value >= player.Powerups.ElementalEffectChance) return;
            if (element == DamageElement.Fire) enemy.Burn(3, Mathf.Max(1, damage / 3));
            if (element == DamageElement.Ice) enemy.Chill(3f);
        }
    }
}

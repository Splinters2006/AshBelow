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
            // Shadowstep emerges behind its victim, regardless of their turn during the blink.
            Vector2 source = (Vector2)enemy.transform.position - enemy.Facing.Direction;
            enemy.Hit(ShadowstepDamageForRoll(player, Random.value), source);
        }

        public static void Apply(DungeonPlayer player, DungeonEnemy enemy, int damage, DamageElement element, Vector2 source)
        {
            if (enemy == null || enemy.Health <= 0) return;
            if (element == DamageElement.Physical)
            {
                if (player.ClassWeapon == WeaponType.Daggers && enemy.Facing.IsBehind(source))
                    damage = damage * 2 + player.Powerups.Count(PowerupType.Backstab);
                enemy.Hit(player.Powerups.RollDamage(damage), source);
                return;
            }
            // Elemental attacks roll for a status effect instead of critical damage.
            enemy.Hit(damage, source);
            if (element == DamageElement.Lightning) return; // WizardAttack rolls the chain's overload once per cast.
            if (enemy.Health <= 0 || Random.value >= player.Powerups.ElementalEffectChance) return;
            if (element == DamageElement.Fire) enemy.Burn(3, Mathf.Max(1, damage / 3));
            if (element == DamageElement.Ice) enemy.Chill(3f);
        }
    }
}

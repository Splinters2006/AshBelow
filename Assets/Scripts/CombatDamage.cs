using UnityEngine;

namespace Slopgame
{
    public enum DamageElement { Physical, Fire, Lightning, Ice }

    public static class CombatDamage
    {
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

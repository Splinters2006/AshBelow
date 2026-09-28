using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class WizardAttack : MonoBehaviour, IPlayerWeapon
    {
        public DungeonPlayer Player { get; set; }
        private float readyAt, lightningReadyAt;
        public bool IsHeavyAttacking => false;
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && Time.time >= readyAt;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, lightningReadyAt - Time.time));
        public float LightningRange => 6f + Player.Powerups.Count(PowerupType.LightningRange);
        public float JumpRange => 2.5f + Player.Powerups.Count(PowerupType.LightningRange) * 0.5f;

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            SpellProjectile.Spawn(Player, aim, Player.Charge.Damage(charge), DamageElement.Fire, Color.white);
            readyAt = Time.time + 0.45f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!CanAttack || HeavyCooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            var struck = new HashSet<DungeonEnemy>();
            Vector2 origin = transform.position;
            var target = FindTarget(origin, aim.normalized, LightningRange, struck, true);
            if (target == null) return false;
            Player.Charge.Cancel();
            HeroVfx.Pulse(Player.Run.ProjectileRoot, origin, 0.7f, AbilityCatalog.Ice, 0.25f);
            int chains = Player.Powerups.Count(PowerupType.LightningChains);
            int count = 1 + chains;
            // Conductivity unlocks chaining; overload only extends an unlocked chain.
            bool overload = chains > 0 && Random.value < Player.Powerups.ElementalEffectChance;
            if (overload) count += 2;
            for (int i = 0; i < count && target != null; i++)
            {
                Vector2 destination = target.transform.position;
                struck.Add(target);
                CombatVfx.GlowBolt(Player.Run.ProjectileRoot, origin, destination, AbilityCatalog.Ice);
                HeroVfx.Sparks(Player.Run.ProjectileRoot, destination, Color.Lerp(AbilityCatalog.Ice, Color.white, 0.4f), 7, 4f, 0.25f);
                CombatDamage.Apply(Player, target, Player.Damage + 1 + Player.Powerups.Count(PowerupType.LightningPower) + Player.Permanent.LightningDamage,
                    DamageElement.Lightning, origin);
                origin = destination;
                target = FindTarget(origin, aim, i >= count - 3 && overload ? 1.75f : JumpRange, struck, false);
            }
            lightningReadyAt = Time.time + 3f;
            readyAt = Time.time + 0.25f;
            return true;
        }

        private DungeonEnemy FindTarget(Vector2 origin, Vector2 aim, float range, HashSet<DungeonEnemy> struck, bool aimed)
        {
            DungeonEnemy best = null;
            float distance = range;
            foreach (var enemy in Player.Run.Enemies)
            {
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                if (enemy.Health <= 0 || struck.Contains(enemy) || offset.magnitude > distance
                    || (aimed && Vector2.Dot(offset.normalized, aim) < 0.7f)
                    || !Player.Run.HasLineOfSight(origin, enemy.transform.position)) continue;
                best = enemy;
                distance = offset.magnitude;
            }
            return best;
        }

        public void Hide() { Player.Charge.Cancel(); }
    }
}

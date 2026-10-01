using UnityEngine;

namespace Slopgame
{
    public sealed class BowAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float HeavyCooldown = 6f;
        // Triple shot: a tight, long-range spread.
        public const float SpreadAngle = 6f;
        public const float HeavyRange = 8.5f;
        public const float ChargedRangeBonus = 1f;
        public static float RangeForCharge(float charge) => PlayerProjectile.MaxRange + ChargedRangeBonus * Mathf.Clamp01(charge);
        public DungeonPlayer Player { get; set; }
        public float ArrowRange(float charge) => RangeForCharge(charge) + Player.Powerups.Count(PowerupType.Farshot);
        public bool IsHeavyAttacking => false;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0, heavyReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => heavyReadyAt = Cooldowns.Shorten(heavyReadyAt, seconds);
        private float readyAt, heavyReadyAt;
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && Time.time >= readyAt;

        private bool CanFire(Vector2 aim) => Player.Run.IsPlaying && !Player.IsRolling
            && Time.time >= readyAt && aim.sqrMagnitude > 0.001f;

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanFire(aim)) return false;
            // Quick Nock: every 5th arrow flies fully charged.
            if (Player.Powerups.Count(PowerupType.QuickNock) > 0 && ++arrowsLoosed % QuickNockEvery == 0) charge = 1f;
            if (RollsSteadyHand(Player)) charge = 1f;
            var arrow = Fire(aim.normalized, Player.Charge.Damage(charge), ArrowRange(charge));
            arrow.FullyCharged = charge >= 1f;
            readyAt = Time.time + 0.35f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!CanFire(aim) || HeavyCooldownRemaining > 0) return false;
            Player.Charge.Cancel();
            for (int i = -1; i <= 1; i++)
            {
                // Steady Hand: each arrow of the spread may fly fully charged.
                bool charged = RollsSteadyHand(Player);
                Fire(Quaternion.Euler(0, 0, SpreadAngle * i) * aim.normalized, charged ? ChargedAbilityDamage(Player.Damage) : Player.Damage,
                    HeavyRange + Player.Powerups.Count(PowerupType.Farshot)).FullyCharged = charged;
            }
            heavyReadyAt = Time.time + HeavyCooldown * Player.Powerups.SkillCooldownMultiplier;
            readyAt = Time.time + 0.35f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        public const int QuickNockEvery = 5;
        /// <summary>Steady Hand (her passive): the chance for any arrow or arrow ability to be fully charged, and what that adds to anything but a basic arrow.</summary>
        public const float SteadyHandChance = 0.2f, SteadyHandDamage = 1.25f;

        public static bool RollsSteadyHand(DungeonPlayer player)
            => player != null && player.Permanent != null && player.Permanent.HasPassive(WeaponType.Bow) && Random.value < SteadyHandChance;

        /// <summary>The damage of an arrow ability, 25% higher (and never less than +1) when Steady Hand charges it fully.</summary>
        public static int SteadyHandAbilityDamage(DungeonPlayer player, int damage)
        {
            if (!RollsSteadyHand(player)) return damage;
            HeroVfx.Pulse(player.transform, player.transform.position, 0.85f, AbilityCatalog.Gold, 0.3f);
            return ChargedAbilityDamage(damage);
        }

        public static int ChargedAbilityDamage(int damage) => Mathf.CeilToInt(damage * SteadyHandDamage - 0.0001f);
        private int arrowsLoosed;

        private PlayerProjectile Fire(Vector2 direction, int damage, float range = PlayerProjectile.MaxRange)
            => PlayerProjectile.Spawn(Player.Run, transform.position, direction, damage, range);

        public void Hide() { Player.Charge.Cancel(); }
    }
}

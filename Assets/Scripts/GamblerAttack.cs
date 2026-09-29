using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Gambler's coins. Left click throws a coin (charge it for more damage) without spending it; every enemy that
    /// dies drops a gold coin to pick up, and the magical purse gives one back whenever he runs dry. Right click throws
    /// a volley with one coin per coin he carries across a 90-degree cone, also without spending any.
    /// </summary>
    public sealed class GamblerAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float CoinRange = 6f, VolleyRange = 5.5f, VolleyCone = 90f, VolleyCooldown = 6f;
        public const int MaxVolley = 40;
        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        private DungeonPlayer player;
        /// <summary>Setting the hero fills the purse with the starting coins (Deep Pockets adds more).</summary>
        public DungeonPlayer Player
        {
            get => player;
            set { player = value; coins = 1 + (value != null && value.Permanent != null ? value.Permanent.StartingCoins : 0); }
        }
        private int coins = 1;
        /// <summary>Lady Luck: added to the odds of every gamble.</summary>
        public float Luck => Player != null && Player.Permanent != null ? Player.Permanent.GambleLuck : 0f;
        private float readyAt, volleyReadyAt;
        /// <summary>Never below one: the magical purse refills an empty pocket.</summary>
        public int Coins => Mathf.Max(1, coins);
        public bool IsHeavyAttacking => false;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, volleyReadyAt - Time.time));
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && !Player.IsBusy && Time.time >= readyAt;

        public void AddCoins(int amount) { if (amount > 0) coins = Coins + amount; }

        /// <summary>Spends coins if he has enough. The purse tops an empty pocket back up to one.</summary>
        public bool Spend(int amount)
        {
            if (amount <= 0 || Coins < amount) return false;
            coins = Coins - amount;
            if (coins <= 0)
            {
                coins = 1;
                HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.4f, Gold, 6, 0.6f);
            }
            return true;
        }

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            PlayerProjectile.Spawn(Player.Run, transform.position, aim.normalized, Player.Charge.Damage(charge), CoinRange, ProjectileStyle.Coin);
            readyAt = Time.time + 0.4f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || HeavyCooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            Player.Charge.Cancel();
            aim.Normalize();
            int count = Mathf.Min(Coins, MaxVolley);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : -VolleyCone * 0.5f + VolleyCone * i / (count - 1);
                PlayerProjectile.Spawn(Player.Run, transform.position, Quaternion.Euler(0, 0, angle) * aim, Player.Damage, VolleyRange, ProjectileStyle.Coin);
            }
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, Gold, 10, 3.5f, 0.3f, aim, VolleyCone);
            volleyReadyAt = Time.time + VolleyCooldown;
            readyAt = Time.time + 0.3f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        // ---------------------------------------------------------------- boss artifacts

        public const int WindfallCoins = 5;
        public const float JackpotDuration = 8f;
        public enum JackpotPrize { Nothing, Speed, Damage, Heal }

        public bool CastArtifact(AbilityType type, int rank)
        {
            if (Player == null || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy) return false;
            rank = Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank);
            switch (type)
            {
                case AbilityType.Windfall: Windfall(rank); return true;
                case AbilityType.AllIn: AllIn(rank, Random.value); return true;
                case AbilityType.Jackpot: Jackpot(rank, Random.value, Random.value); return true;
                default: return false;
            }
        }

        public void Windfall(int rank)
        {
            AddCoins(WindfallCoins + rank - 1);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, Gold, 16, 4f, 0.4f, Vector2.up, 120f);
            HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.6f, Gold, 14, 0.8f);
        }

        /// <summary>Double or nothing on every coin carried. True on a win.</summary>
        public bool AllIn(int rank, float roll) => DoubleOrNothing(roll, 0.5f + 0.05f * (rank - 1));

        /// <param name="roll">0-1; below <paramref name="winChance"/> wins.</param>
        public bool DoubleOrNothing(float roll, float winChance = 0.5f)
        {
            int stake = Coins;
            bool won = roll < winChance + Luck;
            if (won) AddCoins(stake);
            else Spend(stake);
            var root = Player.Run.ProjectileRoot;
            if (won) HeroVfx.Sparks(root, transform.position, Gold, 18, 4.5f, 0.4f);
            else HeroVfx.Sparks(root, transform.position, new Color(0.5f, 0.45f, 0.4f), 10, 2.5f, 0.35f);
            return won;
        }

        /// <summary>
        /// Spends every coin. With <paramref name="roll"/> at or above one half (plus Lady Luck) nothing happens; otherwise
        /// <paramref name="pick"/> chooses speed, damage or a heal, each growing with the coins spent.
        /// </summary>
        public JackpotPrize Jackpot(int rank, float roll, float pick)
        {
            int spent = Coins;
            Spend(spent);
            var root = Player.Run.ProjectileRoot;
            if (roll >= 0.5f + Luck)
            {
                HeroVfx.Sparks(root, transform.position, new Color(0.5f, 0.45f, 0.4f), 10, 2.5f, 0.35f);
                return JackpotPrize.Nothing;
            }
            float duration = JackpotDuration + (rank - 1);
            var prize = pick < 1f / 3f ? JackpotPrize.Speed : pick < 2f / 3f ? JackpotPrize.Damage : JackpotPrize.Heal;
            if (prize == JackpotPrize.Speed) Player.Buffs.JackpotHaste(JackpotSpeedFor(spent), duration);
            else if (prize == JackpotPrize.Damage) Player.Buffs.JackpotMight(JackpotDamageFor(spent), duration);
            else Player.Heal(JackpotHealFor(spent));
            HeroVfx.Pulse(root, transform.position, 1.6f, Gold, 0.45f);
            HeroVfx.Sparks(root, transform.position, Gold, 24, 5f, 0.45f);
            CoopFx.Pulse(Player.Run, transform.position, 1.6f, Gold, 0.45f);
            ScreenFx.Flash(new Color(1f, 0.85f, 0.3f, 0.2f), 0.2f);
            return prize;
        }

        /// <summary>+10% movement per coin, up to two and a half times as fast.</summary>
        public static float JackpotSpeedFor(int coins) => Mathf.Min(2.5f, 1f + 0.1f * coins);
        /// <summary>+1 damage for every 3 coins (at least +1).</summary>
        public static int JackpotDamageFor(int coins) => Mathf.Max(1, coins / 3);
        /// <summary>1 HP for every 2 coins (at least 1).</summary>
        public static int JackpotHealFor(int coins) => Mathf.Max(1, (coins + 1) / 2);

        public void Hide() { Player.Charge.Cancel(); }
    }
}

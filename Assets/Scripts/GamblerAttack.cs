using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Gambler's coins. Left click throws a coin (charge it for more damage) without spending it; every enemy that
    /// dies drops a gold coin to pick up, and the magical purse gives one back whenever he runs dry. Right click throws
    /// a volley with one coin per coin he carries across a 90-degree cone, also without spending any.
    /// </summary>
    public sealed class GamblerAttack : MonoBehaviour, IPlayerWeapon, IRunPersistent
    {
        public const float CoinRange = 6f, VolleyRange = 5.5f, VolleyCone = 90f, VolleyCooldown = 6f;
        public const int MaxVolley = 40;
        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        private DungeonPlayer player;
        /// <summary>Setting the hero fills the purse to its floor.</summary>
        public DungeonPlayer Player
        {
            get => player;
            set { player = value; coins = MinCoins; }
        }
        private int coins = 1;
        /// <summary>Lady Luck: added to the odds of every gamble.</summary>
        public float Luck => Player != null && Player.Permanent != null ? Player.Permanent.GambleLuck + Player.Powerups.Count(PowerupType.LuckyStreak) * 0.03f : 0f;
        private float readyAt, volleyReadyAt;
        /// <summary>The fewest coins he can hold: one, plus one per rank of Deep Pockets.</summary>
        public int MinCoins => 1 + (player != null && player.Permanent != null ? player.Permanent.PurseFloor : 0);
        /// <summary>Never below <see cref="MinCoins"/>: the magical purse tops up an emptied pocket.</summary>
        public int Coins => Mathf.Max(MinCoins, coins);
        public bool IsHeavyAttacking => false;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, volleyReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => volleyReadyAt = Cooldowns.Shorten(volleyReadyAt, seconds);
        public const float LooseChangeChancePerRank = 0.05f;
        /// <summary>Loose Change: the chance that a gold coin picked up is worth a second coin.</summary>
        public float LooseChangeChance => LooseChangeChancePerRank * Player.Powerups.Count(PowerupType.LooseChange);
        /// <summary>Coins gained from a gold coin picked up, given a 0-1 roll (Loose Change may add one more).</summary>
        public int PickupCoinsForRoll(float roll) => roll < LooseChangeChance ? 2 : 1;
        /// <summary>Long Toss adds range to thrown coins and the volley.</summary>
        public float ThrowRange => CoinRange + Player.Powerups.Count(PowerupType.LongToss);
        public float VolleyReach => VolleyRange + Player.Powerups.Count(PowerupType.LongToss);
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && !Player.IsBusy && Time.time >= readyAt;

        /// <summary>The purse holds at most this many; Double or Nothing and Windfall cannot push past it (or overflow).</summary>
        public const int MaxCoins = 999999;
        public const int GoldCoinEvery = 10, GoldCoinCap = 50;
        public const float InterestInterval = 30f;
        private int coinsThrown;
        private float nextInterest;

        /// <summary>Card Shark (his second passive): cards left in the deck bought from his purse.</summary>
        public int Cards { get; private set; }
        public void SaveRun(HeroSnapshot hero)
        {
            hero.SetExtra("coins", coins);
            hero.SetExtra("cards", Cards);
        }

        public void LoadRun(HeroSnapshot hero)
        {
            coins = Mathf.Clamp(hero.Extra("coins", coins), 0, MaxCoins);
            Cards = Mathf.Max(0, hero.Extra("cards", Cards));
        }

        public void AddCards(int amount) { if (amount > 0) Cards = (int)System.Math.Min(int.MaxValue, (long)Cards + amount); }
        /// <summary>A card thrown alongside a coin deals this share of the coin's damage (never below 1).</summary>
        public const float CardDamageShare = 0.5f;
        /// <summary>How far the card's flight is turned off the coin's, so the two never hide each other.</summary>
        public const float CardOffset = 6f;

        /// <summary>Every coin he throws takes a card from the deck along with it, for half its damage.</summary>
        private void ThrowCard(Vector2 direction, int coinDamage)
        {
            if (Cards <= 0) return;
            Cards--;
            ThrownCard.Deal(Player, transform.position, Quaternion.Euler(0, 0, Random.value < 0.5f ? CardOffset : -CardOffset) * direction,
                Mathf.Max(1, Mathf.FloorToInt(coinDamage * CardDamageShare)));
        }

        // Compound Interest: the purse grows by a tenth every half minute of play.
        private void Update()
        {
            if (player == null || !player.Run.IsPlaying || player.Powerups.Count(PowerupType.CompoundInterest) == 0) { nextInterest = Time.time + InterestInterval; return; }
            if (Time.time < nextInterest) return;
            nextInterest = Time.time + InterestInterval;
            AddCoins(Mathf.Max(1, Coins / 10));
            HeroVfx.Sparks(player.Run.ProjectileRoot, transform.position, Gold, 10, 3f, 0.3f, Vector2.up, 120f);
        }
        public void AddCoins(int amount) { if (amount > 0) coins = (int)System.Math.Min(MaxCoins, (long)Coins + amount); }

        /// <summary>Spends coins if he has enough. The purse tops an emptied pocket back up to <see cref="MinCoins"/>.</summary>
        public bool Spend(int amount)
        {
            if (amount <= 0 || Coins < amount) return false;
            coins = Coins - amount;
            if (coins < MinCoins)
            {
                coins = MinCoins;
                HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.4f, Gold, 6, 0.6f);
            }
            return true;
        }

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            int damage = Player.Charge.Damage(charge);
            // Gold Coin: every 10th coin is worth its damage times the coins in the purse.
            if (Player.Powerups.Count(PowerupType.GoldCoin) > 0 && ++coinsThrown % GoldCoinEvery == 0)
            {
                damage *= Mathf.Clamp(Coins, 1, GoldCoinCap);
                HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, Gold, 16, 4f, 0.35f, aim, 60f, 1.3f);
            }
            PlayerProjectile.Spawn(Player.Run, transform.position, aim.normalized, damage, ThrowRange, ProjectileStyle.Coin);
            ThrowCard(aim.normalized, damage);
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
                Vector2 direction = Quaternion.Euler(0, 0, angle) * aim;
                PlayerProjectile.Spawn(Player.Run, transform.position, direction, Player.Damage, VolleyReach, ProjectileStyle.Coin);
                ThrowCard(direction, Player.Damage);
            }
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, Gold, 10, 3.5f, 0.3f, aim, VolleyCone);
            volleyReadyAt = Time.time + VolleyCooldown * Player.Powerups.SkillCooldownMultiplier;
            readyAt = Time.time + 0.3f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        // ---------------------------------------------------------------- boss artifacts

        public const int WindfallCoins = 5;
        public const float JackpotDuration = 8f;
        public enum JackpotPrize { Speed, Damage, Heal }

        public bool CastArtifact(AbilityType type, int rank)
        {
            if (Player == null || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy) return false;
            rank = Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank);
            switch (type)
            {
                case AbilityType.Windfall: Windfall(rank); return true;
                case AbilityType.AllIn: AllIn(rank, Random.value); return true;
                case AbilityType.Jackpot: Jackpot(rank, Random.value); return true;
                default: return false;
            }
        }

        public void Windfall(int rank)
        {
            AddCoins(WindfallCoinsFor(rank));
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, Gold, 16, 4f, 0.4f, Vector2.up, 120f);
            HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.6f, Gold, 14, 0.8f);
            CoinRainVfx.Play(Player.Run.ProjectileRoot, transform.position);
            CoopFx.CoinRain(Player.Run, transform.position);
        }

        /// <summary>Windfall's coins: ranks add one, Mint Condition two per stack.</summary>
        public int WindfallCoinsFor(int rank) => WindfallCoins + rank - 1 + Player.Powerups.Count(PowerupType.MintCondition) * 2;

        /// <summary>Double or nothing on every coin carried. True on a win.</summary>
        public bool AllIn(int rank, float roll)
        {
            bool won = DoubleOrNothing(roll, AllInOdds(rank));
            // Snake Eyes: a lost bet makes him greedy for more.
            if (!won && Player.Powerups.Count(PowerupType.SnakeEyes) > 0) Player.Buffs.Greed(SnakeEyesTime);
            CoinFlipVfx.Play(Player.Run.ProjectileRoot, transform, won);
            CoopFx.CoinFlip(Player.Run, won);
            return won;
        }
        /// <summary>All In's base odds: ranks and Rigged Odds each add 5% (Lady Luck is added on top).</summary>
        public const float SnakeEyesTime = 5f;
        public float AllInOdds(int rank) => 0.5f + 0.05f * (rank - 1) + 0.05f * Player.Powerups.Count(PowerupType.RiggedOdds);

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
        /// Spends every coin and always pays out: <paramref name="pick"/> chooses speed, damage or a heal, each growing
        /// with the coins spent. A slot reel over the Gambler's head shows which one.
        /// </summary>
        public JackpotPrize Jackpot(int rank, float pick)
        {
            int spent = Coins;
            Spend(spent);
            var root = Player.Run.ProjectileRoot;
            var prize = pick < 1f / 3f ? JackpotPrize.Speed : pick < 2f / 3f ? JackpotPrize.Damage : JackpotPrize.Heal;
            if (prize == JackpotPrize.Speed) Player.Buffs.JackpotHaste(JackpotSpeedFor(spent), JackpotTime(rank));
            else if (prize == JackpotPrize.Damage) Player.Buffs.JackpotMight(JackpotDamageFor(spent), JackpotTime(rank));
            else Player.Heal(JackpotHealFor(spent));
            Color color = JackpotVfx.PrizeColor(prize);
            HeroVfx.Pulse(root, transform.position, 1.6f, Gold, 0.45f);
            HeroVfx.Sparks(root, transform.position, Gold, 16, 5f, 0.45f);
            HeroVfx.Sparks(root, transform.position, color, 12, 4f, 0.5f);
            CoopFx.Pulse(Player.Run, transform.position, 1.6f, Gold, 0.45f);
            JackpotVfx.Play(root, transform, prize);
            CoopFx.Jackpot(Player.Run, prize);
            ScreenFx.Flash(FlameMesh.Alpha(Color.Lerp(Gold, color, 0.5f), 0.2f), 0.2f);
            return prize;
        }

        /// <summary>How long a Jackpot buff lasts: ranks add a second, High Roller two per stack.</summary>
        public float JackpotTime(int rank) => JackpotDuration + (rank - 1) + Player.Powerups.Count(PowerupType.HighRoller) * 2f;

        /// <summary>
        /// Jackpot prizes have no cap but grow as coins to the power of <see cref="JackpotGrowth"/>, so each extra coin adds a
        /// little less. <paramref name="atReference"/> is what <paramref name="reference"/> coins pay.
        /// </summary>
        public const float JackpotGrowth = 0.7f;
        private static float JackpotCurve(int coins, float reference, float atReference)
            => atReference * Mathf.Pow(Mathf.Max(0, coins) / reference, JackpotGrowth);
        /// <summary>Movement multiplier: x2 at 10 coins, about x3.2 at 30 and x6 at 100.</summary>
        public static float JackpotSpeedFor(int coins) => 1f + JackpotCurve(coins, 10f, 1f);
        /// <summary>Bonus damage: +3 at 9 coins, +7 at 30 (at least +1).</summary>
        public static int JackpotDamageFor(int coins) => Mathf.Max(1, Mathf.RoundToInt(JackpotCurve(coins, 9f, 3f)));
        /// <summary>HP healed: 2 at 4 coins, 6 at 20 (at least 1).</summary>
        public static int JackpotHealFor(int coins) => Mathf.Max(1, Mathf.RoundToInt(JackpotCurve(coins, 4f, 2f)));

        public void Hide() { Player.Charge.Cancel(); }
    }
}

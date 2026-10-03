using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Gambler's class mechanic, bought in the Ash shop like the others: the key opens his purse, a small shop
    /// paid for in coins. Every coin spent here is one fewer in his volley. The game keeps running while it is open.
    /// The free coin he gets when he runs dry belongs to his weapon (GamblerAttack), not to this.
    /// With its R upgrade (The Safe, from the Ash shop) the purse holds a safe: coins go in ten at a time, every
    /// guardian that falls multiplies what is inside by 1.1 (1.25 with his passive, Compound Interest), and each withdrawal takes out exactly half of it.
    /// </summary>
    public sealed class GamblerPurse : ClassMechanic, IRunPersistent
    {
        public enum Ware { Draught, Charm, Dice, CardDeck }

        public sealed class Offer
        {
            public Ware Ware { get; }
            public string Name { get; }
            public string Description { get; }
            public int Cost { get; }
            public Offer(Ware ware, string name, string description, int cost) { Ware = ware; Name = name; Description = description; Cost = cost; }
        }

        public static readonly Offer[] Offers =
        {
            new Offer(Ware.Draught, "Healing Draught", "Restore 2 HP", 40),
            new Offer(Ware.Charm, "Lucky Charm", "+1 ward for this floor", 50),
            new Offer(Ware.Dice, "Loaded Dice", "+1 damage until the next floor", 80),
            // Card Shark (his second passive) puts a deck on the counter.
            new Offer(Ware.CardDeck, "Deck of Cards", "52 cards: every coin you throw also throws a card for half its damage", CardDeckCost),
        };

        public const int CardDeckCost = 52, CardDeckSize = 52;

        /// <summary>Whether this purse sells <paramref name="offer"/> at all (the deck needs Card Shark, his second passive).</summary>
        public bool Sells(Offer offer) => offer.Ware != Ware.CardDeck || (Player.Permanent != null && Player.Permanent.HasSecondPassive(WeaponType.Coins));

        /// <summary>The wares on the purse's counter, in order.</summary>
        public System.Collections.Generic.List<Offer> OnSale
        {
            get
            {
                var wares = new System.Collections.Generic.List<Offer>();
                foreach (var offer in Offers) if (Sells(offer)) wares.Add(offer);
                return wares;
            }
        }

        public const int SafeDeposit = 10;
        public const float SafeInterest = 1.1f, PassiveSafeInterest = 1.25f;
        /// <summary>What a fallen guardian multiplies the safe by: more with Compound Interest (his passive).</summary>
        public float Interest => Player.Permanent != null && Player.Permanent.HasPassive(WeaponType.Coins) ? PassiveSafeInterest : SafeInterest;
        private int diceFloor = -1, dice;
        public bool HasSafe => IsUpgraded;
        /// <summary>Coins locked in the safe for this descent.</summary>
        public int Safe { get; private set; }
        /// <summary>What one withdrawal pays out: half the safe, rounded up so the last coin can come out.</summary>
        public int SafeWithdrawal => (Safe + 1) / 2;
        public bool CanDeposit => HasSafe && Coins != null && Player.Run.IsPlaying && Player.Health > 0 && Coins.Coins >= SafeDeposit && Safe < GamblerAttack.MaxCoins;
        public bool CanWithdraw => HasSafe && Coins != null && Player.Run.IsPlaying && Player.Health > 0 && Safe > 0;

        public void SaveRun(HeroSnapshot hero)
        {
            hero.SetExtra("safe", Safe);
            hero.SetExtra("dice", dice);
            hero.SetExtra("diceFloor", diceFloor);
        }

        public void LoadRun(HeroSnapshot hero)
        {
            Safe = Mathf.Clamp(hero.Extra("safe", Safe), 0, GamblerAttack.MaxCoins);
            dice = Mathf.Max(0, hero.Extra("dice", dice));
            diceFloor = hero.Extra("diceFloor", diceFloor);
        }

        public bool Deposit()
        {
            if (!CanDeposit || !Coins.Spend(SafeDeposit)) return false;
            Safe += SafeDeposit;
            LastResult = $"Clunk. {Safe:N0} in the safe.";
            return true;
        }

        public bool Withdraw()
        {
            if (!CanWithdraw) return false;
            int amount = SafeWithdrawal;
            Safe -= amount;
            Coins.AddCoins(amount);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, GamblerAttack.Gold, 10, 3f, 0.3f, Vector2.up, 120f);
            LastResult = $"Half out: +{amount:N0} coins.";
            return true;
        }

        public override void OnGuardianDefeated()
        {
            if (!HasSafe || Safe <= 0) return;
            int before = Safe;
            Safe = (int)Mathf.Min(GamblerAttack.MaxCoins, Mathf.Round(Safe * Interest));
            LastResult = $"Interest: +{Safe - before:N0} in the safe.";
            HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.7f, GamblerAttack.Gold, 14, 0.9f);
        }

        public bool IsOpen { get; private set; }
        public string LastResult { get; private set; }
        public GamblerAttack Coins => Player.Weapon as GamblerAttack;
        public override string Name => "Purse";
        public override Color Color => GamblerAttack.Gold;
        public override float Readiness => 1f;
        public override string Status => IsOpen ? "OPEN" : $"{Coins?.Coins ?? 1} COINS";
        public override int BonusDamage => Player.Run.Floor == diceFloor ? dice : 0;

        public override bool TryActivate(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.Health <= 0) return false;
            IsOpen = !IsOpen;
            LastResult = null;
            return true;
        }

        public void Close() => IsOpen = false;

        private void Update()
        {
            if (IsOpen && (!Player.Run.IsPlaying || Player.Health <= 0)) IsOpen = false;
        }

        public bool CanBuy(Offer offer)
        {
            var coins = Coins;
            if (coins == null || !Player.Run.IsPlaying || Player.Health <= 0 || !Sells(offer)) return false;
            if (offer.Ware == Ware.Draught && Player.Health >= Player.MaxHealth) return false;
            return coins.Coins >= offer.Cost;
        }

        public bool Buy(Offer offer)
        {
            if (!CanBuy(offer)) return false;
            var coins = Coins;
            var root = Player.Run.ProjectileRoot;
            switch (offer.Ware)
            {
                case Ware.Draught:
                    coins.Spend(offer.Cost);
                    Player.Heal(2);
                    HeroVfx.Motes(root, transform.position, 0.7f, new Color(1f, 0.45f, 0.5f), 12, 0.9f);
                    LastResult = "Glug. +2 HP.";
                    break;
                case Ware.Charm:
                    coins.Spend(offer.Cost);
                    Player.Powerups.AddWard();
                    HeroVfx.Pulse(transform, transform.position, 0.95f, AbilityCatalog.Ice, 0.3f);
                    LastResult = "The charm hums. +1 ward.";
                    break;
                case Ware.Dice:
                    coins.Spend(offer.Cost);
                    if (diceFloor != Player.Run.Floor) dice = 0;
                    diceFloor = Player.Run.Floor;
                    dice++;
                    LastResult = $"Loaded. +{dice} damage this floor.";
                    break;
                case Ware.CardDeck:
                    coins.Spend(offer.Cost);
                    coins.AddCards(CardDeckSize);
                    HeroVfx.Sparks(root, transform.position, Color.white, 12, 3f, 0.3f, Vector2.up, 120f, 0.8f);
                    LastResult = $"A fresh deck. {coins.Cards} cards up your sleeve.";
                    break;
            }
            return true;
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Gambler's class mechanic, bought in the Ash shop like the others: the key opens his purse, a small shop
    /// paid for in coins. Every coin spent here is one fewer in his volley. The game keeps running while it is open.
    /// The free coin he gets when he runs dry belongs to his weapon (GamblerAttack), not to this.
    /// </summary>
    public sealed class GamblerPurse : ClassMechanic
    {
        public enum Ware { Draught, Charm, Dice, DoubleOrNothing }

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
            new Offer(Ware.DoubleOrNothing, "Double or Nothing", "Bet every coin: double them, or lose them all", 0),
        };

        private int diceFloor = -1, dice;
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
            if (coins == null || !Player.Run.IsPlaying || Player.Health <= 0) return false;
            if (offer.Ware == Ware.DoubleOrNothing) return coins.Coins >= 2;
            if (offer.Ware == Ware.Draught && Player.Health >= Player.MaxHealth) return false;
            return coins.Coins >= offer.Cost;
        }

        public bool Buy(Offer offer) => Buy(offer, Random.value);

        /// <param name="roll">0-1; below one half wins Double or Nothing.</param>
        public bool Buy(Offer offer, float roll)
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
                case Ware.DoubleOrNothing:
                    int stake = coins.Coins;
                    LastResult = coins.DoubleOrNothing(roll) ? $"WIN! {stake} coins become {stake * 2}."
                        : $"Bust. {stake} coins gone; the purse spits one back.";
                    break;
            }
            return true;
        }
    }
}

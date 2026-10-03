using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The crystals a hero gathers from fallen enemies during a descent, spent at the crystal shop before each boss,
    /// and the boss boons bought there. Boons last for the next floor only: the guardian's arena.
    /// Each machine tracks its own hero's crystals, like the Gambler's coins.
    /// </summary>
    public sealed class CrystalPouch : MonoBehaviour, IRunPersistent
    {
        public static readonly Color CrystalColor = new Color(0.78f, 0.5f, 1f);
        public const float SwiftnessPerBoon = 0.2f;
        public DungeonPlayer Player { get; set; }
        public int Crystals { get; private set; }
        private readonly Dictionary<CrystalShop.Ware, int> purchases = new Dictionary<CrystalShop.Ware, int>();

        /// <summary>
        /// How many times this hero has bought a ware in this descent. Every ware's price keeps climbing with it, across
        /// shops and worlds; a fresh shop never resets it.
        /// </summary>
        public int Purchases(CrystalShop.Ware ware) => purchases.TryGetValue(ware, out int count) ? count : 0;
        public void RecordPurchase(CrystalShop.Ware ware) => purchases[ware] = Purchases(ware) + 1;
        private int boonFloor = -1, damage, wards, swiftness;

        private bool BoonsActive => Player != null && Player.Run != null && Player.Run.Floor == boonFloor && !Player.Run.InShop;
        /// <summary>Whetstone: flat bonus damage in the boss arena.</summary>
        public int BonusDamage => BoonsActive ? damage : 0;
        /// <summary>Quicksilver: a movement multiplier in the boss arena.</summary>
        public float SpeedMultiplier => BoonsActive ? 1f + SwiftnessPerBoon * swiftness : 1f;
        /// <summary>Boons waiting for the boss arena (shown in the shop).</summary>
        private bool BoonsPending => Player != null && Player.Run != null && boonFloor > Player.Run.Floor;
        public int PendingDamage => BoonsPending ? damage : 0;
        public int PendingWards => BoonsPending ? wards : 0;
        public int PendingSwiftness => BoonsPending ? swiftness : 0;

        public void SaveRun(HeroSnapshot hero)
        {
            hero.crystals = Crystals;
            foreach (var pair in purchases) hero.purchases.Add(new SavedCount(pair.Key.ToString(), pair.Value));
            hero.boonFloor = boonFloor;
            hero.boonDamage = damage;
            hero.boonWards = wards;
            hero.boonSwiftness = swiftness;
        }

        public void LoadRun(HeroSnapshot hero)
        {
            Crystals = Mathf.Max(0, hero.crystals);
            purchases.Clear();
            foreach (var purchase in hero.purchases)
                if (RunSnapshot.TryParse(purchase.id, out CrystalShop.Ware ware)) purchases[ware] = purchase.value;
            boonFloor = hero.boonFloor;
            damage = hero.boonDamage;
            wards = hero.boonWards;
            swiftness = hero.boonSwiftness;
        }

        public void Add(int amount) { if (amount > 0) Crystals += amount; }

        public bool Spend(int amount)
        {
            if (amount < 0 || Crystals < amount) return false;
            Crystals -= amount;
            return true;
        }

        /// <summary>Queues a boon for <paramref name="floor"/>; boons left over from an earlier shop are dropped.</summary>
        public void AddBoon(int floor, int bonusDamage, int bonusWards, int bonusSwiftness)
        {
            if (boonFloor != floor) { damage = wards = swiftness = 0; boonFloor = floor; }
            damage += bonusDamage;
            wards += bonusWards;
            swiftness += bonusSwiftness;
        }

        /// <summary>Called as each floor is built, after wards reset: the boss arena grants the Stoneskin wards.</summary>
        public void BeginFloor(int floor, bool shop)
        {
            if (shop || floor != boonFloor) return;
            for (int i = 0; i < wards; i++) Player.Powerups.AddWard();
        }
    }
}

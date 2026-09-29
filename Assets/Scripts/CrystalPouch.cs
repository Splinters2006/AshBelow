using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The crystals a hero gathers from fallen enemies during a descent, spent at the crystal shop before each boss,
    /// and the boss boons bought there. Boons last for the next floor only: the guardian's arena.
    /// Each machine tracks its own hero's crystals, like the Gambler's coins.
    /// </summary>
    public sealed class CrystalPouch : MonoBehaviour
    {
        public static readonly Color CrystalColor = new Color(0.78f, 0.5f, 1f);
        public const float SwiftnessPerBoon = 0.2f;
        public DungeonPlayer Player { get; set; }
        public int Crystals { get; private set; }
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

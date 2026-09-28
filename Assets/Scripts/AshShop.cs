using UnityEngine;

namespace Slopgame
{
    public sealed class AshShop
    {
        private int tab;
        private Vector2 scroll;
        private string notice;
        private static readonly string[] Tabs = { "All heroes", "Knight", "Archer", "Wizard", "Assassin", "Paladin" };
        private static readonly WeaponType?[] Weapons = { null, WeaponType.Sword, WeaponType.Bow, WeaponType.Staff, WeaponType.Daggers, WeaponType.Hammer };

        public void Draw(DungeonRun run)
        {
            var progress = run.Progress;
            for (int i = 0; i < Tabs.Length; i++)
                if (DungeonUi.Button("shopTab" + i, new Rect(70 + i * 192, 250, 180, 42), Tabs[i], tab == i ? AbilityCatalog.Gold : DungeonUi.Muted))
                { tab = i; scroll = Vector2.zero; notice = null; }
            int count = 0;
            foreach (var item in PermanentUpgradeCatalog.All) if (item.ClassWeapon == Weapons[tab]) count++;
            scroll = GUI.BeginScrollView(new Rect(70, 310, 1140, 276), scroll, new Rect(0, 0, 1110, count * 112));
            int row = 0;
            foreach (var item in PermanentUpgradeCatalog.All)
            {
                if (item.ClassWeapon != Weapons[tab]) continue;
                float y = row++ * 112;
                int rank = progress.Rank(item.Id);
                bool maxed = rank >= item.MaxRank;
                int cost = item.Cost(rank);
                DungeonUi.Panel(new Rect(0, y, 1100, 100), DungeonUi.PanelColor);
                DungeonUi.Label(new Rect(20, y + 12, 620, 28), item.Name, 23, AbilityCatalog.Gold);
                DungeonUi.Label(new Rect(20, y + 50, 680, 40), item.Description, 17, DungeonUi.Muted);
                DungeonUi.Label(new Rect(710, y + 16, 160, 28), $"RANK {rank} / {item.MaxRank}", 16);
                if (DungeonUi.Button("buy_" + item.Id, new Rect(884, y + 20, 196, 60), maxed ? "Maxed" : $"Buy  /  {cost} Ash", AbilityCatalog.Gold,
                    !maxed && !progress.IsReadOnly && progress.Ash >= cost))
                {
                    notice = run.TryBuyUpgrade(item.Id) ? item.Name + " purchased. Applies on your next descent." : progress.LastError ?? "Purchase unavailable.";
                }
            }
            GUI.EndScrollView();
            DungeonUi.Label(new Rect(365, 608, 845, 35), notice ?? "Permanent upgrades survive death and game updates.", 17, DungeonUi.Muted);
        }
    }
}

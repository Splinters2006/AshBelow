using UnityEngine;

namespace Slopgame
{
    public sealed class AshShop
    {
        private int tab;
        private Vector2 scroll;
        private string notice;
        private static readonly string[] Tabs = { "All heroes", "Knight", "Archer", "Wizard", "Assassin", "Paladin", "Brawler", "Demoness" };
        private static readonly WeaponType?[] Weapons = { null, WeaponType.Sword, WeaponType.Bow, WeaponType.Staff, WeaponType.Daggers, WeaponType.Hammer, WeaponType.Fists, WeaponType.Tail };

        public void Draw(DungeonRun run)
        {
            var progress = run.Progress;
            float step = 1152f / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
                if (DungeonUi.Button("shopTab" + i, new Rect(70 + i * step, 250, step - 12, 42), Tabs[i], tab == i ? AbilityCatalog.Gold : DungeonUi.Muted))
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
                bool available = progress.IsAvailable(item);
                int cost = item.Cost(rank);
                DungeonUi.Panel(new Rect(0, y, 1100, 100), DungeonUi.PanelColor);
                DungeonUi.Label(new Rect(20, y + 12, 620, 28), item.Name, 23, AbilityCatalog.Gold);
                DungeonUi.Label(new Rect(20, y + 50, 680, 40), item.Description, 17, DungeonUi.Muted);
                DungeonUi.Label(new Rect(710, y + 16, 160, 28), item.RequiredGuardians > 0 ? "CLASS MECHANIC" : $"RANK {rank} / {item.MaxRank}", 16,
                    item.RequiredGuardians > 0 ? AbilityCatalog.Gold : (Color?)null);
                if (!available)
                {
                    DungeonUi.Label(new Rect(884, y + 20, 196, 60), item.RequiredGuardians == 3 ? "Defeat the third guardian to unlock" : $"Defeat {item.RequiredGuardians} guardians in one descent to unlock", 14,
                        DungeonUi.Muted, TextAnchor.MiddleCenter);
                    continue;
                }
                if (DungeonUi.Button("buy_" + item.Id, new Rect(884, y + 20, 196, 60), maxed ? (item.MaxRank == 1 ? "Owned" : "Maxed") : $"Buy  /  {cost} Ash", AbilityCatalog.Gold,
                    !maxed && !progress.IsReadOnly && progress.Ash >= cost))
                {
                    notice = run.TryBuyUpgrade(item.Id) ? item.Name + " purchased. Applies on your next descent." : progress.LastError ?? "Purchase unavailable.";
                }
            }
            GUI.EndScrollView();
            DungeonUi.Label(new Rect(365, 608, 845, 50), progress.LastError ?? notice ?? "Permanent upgrades survive death and game updates.", 17,
                progress.LastError != null ? new Color(1f, 0.65f, 0.4f) : DungeonUi.Muted);
        }
    }
}

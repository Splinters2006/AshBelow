using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The main menu's Ash shop. Upgrades sit in a two-column grid of compact cards so a long list (the "All heroes" tab
    /// carries every world reward) stays readable: what you can buy comes first, locked items after, maxed ones last
    /// (and they can be hidden). A gold dot on a tab means something there is affordable right now.
    /// </summary>
    public sealed class AshShop
    {
        private static string LockedText(PermanentProgress progress, PermanentUpgradeDefinition item)
        {
            bool worldLocked = item.RequiredWorld >= 0 && item.RequiredWorld < WorldCatalog.All.Length && !progress.HasClearedWorld(item.RequiredWorld);
            // An R upgrade whose world is already cleared is only waiting on the mechanic it upgrades.
            if (item.RequiredUpgrade != null && !worldLocked) return $"Buy {PermanentUpgradeCatalog.Get(item.RequiredUpgrade)?.Name} first";
            if (item.RequiredWorld >= 0 && item.RequiredWorld < WorldCatalog.All.Length)
                return $"Clear {WorldCatalog.All[item.RequiredWorld].Name} or any later world to unlock";
            string hero = item.ClassWeapon.HasValue ? Tabs[System.Array.IndexOf(Weapons, item.ClassWeapon)] : null;
            string guardian = item.RequiredGuardians == 3 ? "the third guardian" : $"{item.RequiredGuardians} guardians in one descent";
            return hero != null ? $"Defeat {guardian} as the {hero} to unlock" : $"Defeat {guardian} to unlock";
        }

        private int tab;
        private Vector2 scroll;
        private string notice;
        private bool hideMaxed;
        private readonly List<PermanentUpgradeDefinition> shown = new List<PermanentUpgradeDefinition>();
        private static readonly string[] Tabs = { "All heroes", "Knight", "Archer", "Wizard", "Assassin", "Paladin", "Brawler", "Demoness", "Gambler", "Augment", "Reaper" };
        private static readonly WeaponType?[] Weapons = { null, WeaponType.Sword, WeaponType.Bow, WeaponType.Staff, WeaponType.Daggers, WeaponType.Hammer, WeaponType.Fists, WeaponType.Tail, WeaponType.Coins, WeaponType.Beam, WeaponType.Scythe };

        // Layout, in the 1280x720 menu space.
        private static readonly Rect ListRect = new Rect(70, 318, 1140, 270);
        private const float CardWidth = 549f, CardHeight = 92f, ColumnGap = 12f, RowStep = 100f;

        private static bool IsMaxed(PermanentProgress progress, PermanentUpgradeDefinition item) => progress.Rank(item.Id) >= item.MaxRank;

        private static bool CanAfford(PermanentProgress progress, PermanentUpgradeDefinition item)
            => !progress.IsReadOnly && progress.IsAvailable(item) && !IsMaxed(progress, item) && progress.Ash >= item.Cost(progress.Rank(item.Id));

        /// <summary>Buyable first, then locked, then maxed; catalog order within each group.</summary>
        private static int SortGroup(PermanentProgress progress, PermanentUpgradeDefinition item)
            => IsMaxed(progress, item) ? 2 : progress.IsAvailable(item) ? 0 : 1;

        public void Draw(DungeonRun run)
        {
            var progress = run.Progress;
            DrawTabs(progress);

            // The tab's upgrades, sorted and (optionally) without the maxed ones.
            shown.Clear();
            int total = 0, affordable = 0, maxed = 0;
            foreach (var item in PermanentUpgradeCatalog.All)
            {
                if (item.ClassWeapon != Weapons[tab]) continue;
                total++;
                if (CanAfford(progress, item)) affordable++;
                if (IsMaxed(progress, item)) { maxed++; if (hideMaxed) continue; }
                shown.Add(item);
            }
            var order = new List<int>();
            for (int i = 0; i < shown.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int group = SortGroup(progress, shown[a]).CompareTo(SortGroup(progress, shown[b]));
                return group != 0 ? group : a.CompareTo(b);
            });

            DungeonUi.Label(new Rect(70, 290, 760, 24),
                $"{total} upgrades   /   {affordable} you can buy now" + (maxed > 0 ? $"   /   {maxed} maxed" : ""), 15, DungeonUi.Muted, TextAnchor.MiddleLeft);
            if (maxed > 0 && DungeonUi.Button("shopHideMaxed", new Rect(990, 284, 220, 30), hideMaxed ? "Show maxed" : "Hide maxed", DungeonUi.Teal))
            { hideMaxed = !hideMaxed; scroll = Vector2.zero; }

            int rows = (order.Count + 1) / 2;
            float contentHeight = Mathf.Max(ListRect.height, rows * RowStep - (RowStep - CardHeight));
            scroll.x = 0f;
            scroll = GUI.BeginScrollView(ListRect, scroll, new Rect(0, 0, ListRect.width - 30, contentHeight));
            for (int i = 0; i < order.Count; i++)
                DrawCard(run, shown[order[i]], new Rect(i % 2 * (CardWidth + ColumnGap), i / 2 * RowStep, CardWidth, CardHeight));
            if (order.Count == 0)
                DungeonUi.Label(new Rect(0, 20, ListRect.width - 30, 30), "Everything here is maxed.", 18, DungeonUi.Muted, TextAnchor.MiddleCenter);
            GUI.EndScrollView();
            if (contentHeight > ListRect.height + 1f && scroll.y < contentHeight - ListRect.height - 2f)
                DungeonUi.Label(new Rect(ListRect.x, ListRect.yMax - 2, ListRect.width - 30, 14), "scroll for more  ▾", 11, DungeonUi.Muted, TextAnchor.UpperRight);

            DungeonUi.Label(new Rect(365, 608, 845, 50), progress.LastError ?? notice ?? "Permanent upgrades survive death and game updates.", 17,
                progress.LastError != null ? new Color(1f, 0.65f, 0.4f) : DungeonUi.Muted);
        }

        /// <summary>One button per hero, each with a gold dot while something on it is affordable.</summary>
        private void DrawTabs(PermanentProgress progress)
        {
            float step = 1152f / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                var rect = new Rect(70 + i * step, 240, step - 12, 40);
                if (DungeonUi.Button("shopTab" + i, rect, Tabs[i], tab == i ? AbilityCatalog.Gold : DungeonUi.Muted))
                { tab = i; scroll = Vector2.zero; notice = null; }
                foreach (var item in PermanentUpgradeCatalog.All)
                {
                    if (item.ClassWeapon != Weapons[i] || !CanAfford(progress, item)) continue;
                    DungeonUi.Panel(new Rect(rect.xMax - 13, rect.y + 5, 8, 8), AbilityCatalog.Gold);
                    break;
                }
            }
        }

        private void DrawCard(DungeonRun run, PermanentUpgradeDefinition item, Rect card)
        {
            var progress = run.Progress;
            int rank = progress.Rank(item.Id);
            bool maxed = rank >= item.MaxRank;
            bool available = progress.IsAvailable(item);
            bool mechanic = item.RequiredGuardians > 0;
            bool passive = item.ClassWeapon.HasValue && ClassPassiveCatalog.Get(item.ClassWeapon.Value)?.Id == item.Id;
            int cost = item.Cost(rank);
            bool pact = item.Id == PermanentUpgradeCatalog.InfernalPactId && rank > 0;
            DungeonUi.Panel(card, DungeonUi.PanelColor);
            DungeonUi.Panel(new Rect(card.x, card.y, 4, card.height), maxed ? DungeonUi.Muted * 0.6f : available ? AbilityCatalog.Gold : DungeonUi.Muted * 0.35f);
            float textWidth = card.width - 200f;
            DungeonUi.Label(new Rect(card.x + 18, card.y + 10, textWidth, 26), item.Name, 20, available ? AbilityCatalog.Gold : DungeonUi.Muted);
            DungeonUi.Label(new Rect(card.x + 18, card.y + 40, textWidth, 48), item.Description, 14, DungeonUi.Muted);
            var side = new Rect(card.xMax - 176, card.y, 160, card.height);
            // The Infernal Pact can be switched off and on again once owned; its switch takes the rank's place.
            if (pact && available)
            {
                bool on = !progress.IsSwitchedOff(item.Id);
                if (DungeonUi.Button("toggle_" + item.Id, new Rect(side.x, side.y + 8, side.width, 30), on ? $"Pact: ON ({rank}/{item.MaxRank})" : "Pact: OFF",
                        on ? new Color(1f, 0.4f, 0.3f) : DungeonUi.Muted))
                    progress.Switch(item.Id, !on);
            }
            else
                DungeonUi.Label(new Rect(side.x, side.y + 12, side.width, 20), passive ? "PASSIVE" : item.IsMechanicUpgrade ? "R UPGRADE" : mechanic ? "CLASS MECHANIC" : $"RANK {rank} / {item.MaxRank}", 13,
                    passive || mechanic || item.IsMechanicUpgrade ? AbilityCatalog.Gold : DungeonUi.Muted, TextAnchor.MiddleRight);
            var buy = new Rect(side.x, side.y + 46, side.width, 36);
            if (!available)
            {
                DungeonUi.Label(new Rect(side.x, side.y + 38, side.width, 50), LockedText(progress, item), 12, DungeonUi.Muted, TextAnchor.MiddleRight);
                return;
            }
            string label = maxed ? (item.MaxRank == 1 ? "Owned" : "Maxed") : $"Buy  /  {cost} Ash";
            if (DungeonUi.Button("buy_" + item.Id, buy, label, AbilityCatalog.Gold, !maxed && !progress.IsReadOnly && progress.Ash >= cost))
                notice = run.TryBuyUpgrade(item.Id) ? item.Name + " purchased. Applies on your next descent." : progress.LastError ?? "Purchase unavailable.";
        }
    }
}

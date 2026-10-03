using UnityEngine;

namespace Slopgame
{
    /// <summary>The main menu's part of updating: the Check for updates button, what it found, and the prompt to install.</summary>
    public sealed class UpdateMenu
    {
        /// <summary>The landing page's button, with what the last check found written beside it in <paramref name="note"/>.</summary>
        public void DrawButton(GameUpdater updater, Rect button, Rect note)
        {
            var state = updater.State;
            string label = state == UpdateState.Checking ? "Checking for updates…"
                : state == UpdateState.Installing ? "Updating…"
                : state == UpdateState.Available ? $"Update to {updater.LatestTag}"
                : "Check for updates";
            bool available = state == UpdateState.Available && updater.CanInstall;
            if (DungeonUi.Button("checkUpdates", button, label, available ? AbilityCatalog.Gold : DungeonUi.Teal,
                state != UpdateState.Checking && state != UpdateState.Installing, 16))
            {
                // A found update asks again; otherwise look (again).
                if (available) updater.Ask();
                else updater.Check();
            }
            if (!string.IsNullOrEmpty(updater.Message))
                DungeonUi.Label(note, updater.Message, 14, state == UpdateState.Failed ? AbilityCatalog.Gold : DungeonUi.Muted, TextAnchor.MiddleRight);
        }

        /// <summary>Asks before installing the update just found. Escape or Not now puts it off until the next check.</summary>
        public void DrawPrompt(GameUpdater updater)
        {
            DungeonUi.Panel(new Rect(-2000, -2000, 6000, 6000), new Color(0.01f, 0.018f, 0.035f, 0.7f));
            var panel = new Rect(390, 240, 500, 260);
            DungeonUi.Panel(panel, DungeonUi.Background);
            DungeonUi.Panel(new Rect(panel.x, panel.y, panel.width, 4), AbilityCatalog.Gold);
            DungeonUi.Label(new Rect(panel.x + 24, panel.y + 20, panel.width - 48, 24), "UPDATE AVAILABLE", 13, AbilityCatalog.Gold, TextAnchor.MiddleCenter);
            DungeonUi.Label(new Rect(panel.x + 24, panel.y + 46, panel.width - 48, 50), $"Update to {updater.LatestTag}?", 26, DungeonUi.Text, TextAnchor.MiddleCenter);
            DungeonUi.Label(new Rect(panel.x + 30, panel.y + 104, panel.width - 60, 80),
                "The game closes, downloads the new version and opens again by itself. Your save is backed up first and is never reset.",
                16, DungeonUi.Muted, TextAnchor.UpperCenter);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { Event.current.Use(); updater.Decline(); return; }
            if (DungeonUi.Button("updateLater", new Rect(panel.x + 24, panel.yMax - 64, 210, 42), "Not now", DungeonUi.Muted)) updater.Decline();
            else if (DungeonUi.Button("updateNow", new Rect(panel.xMax - 234, panel.yMax - 64, 210, 42), "Update now", AbilityCatalog.Gold)) updater.Install();
        }
    }
}

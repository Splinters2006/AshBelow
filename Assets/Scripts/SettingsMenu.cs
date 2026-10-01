using UnityEngine;

namespace Slopgame
{
    /// <summary>The settings page of the main menu: UI sizes, HUD opacity and autofire above the control rebinding.</summary>
    public sealed class SettingsMenu
    {
        private readonly KeybindMenu keybinds = new KeybindMenu();

        public void Cancel() => keybinds.Cancel();

        public void Draw()
        {
            float menu = GameSettings.MenuScale, hud = GameSettings.HudScale, opacity = GameSettings.HudOpacity;
            if (DrawSlider("menuSize", new Rect(70, 176, 310, 46), "Menu size", ref menu, GameSettings.MinScale)) GameSettings.MenuScale = menu;
            if (DrawSlider("hudSize", new Rect(390, 176, 310, 46), "HUD size", ref hud, GameSettings.MinScale)) GameSettings.HudScale = hud;
            if (DrawSlider("hudOpacity", new Rect(710, 176, 310, 46), "HUD opacity", ref opacity, GameSettings.MinOpacity)) GameSettings.HudOpacity = opacity;
            bool autofire = GameSettings.Autofire;
            if (DungeonUi.Button("autofire", new Rect(1030, 176, 180, 46), autofire ? "Autofire: On" : "Autofire: Off",
                autofire ? AbilityCatalog.Gold : DungeonUi.Muted, size: 17))
                GameSettings.Autofire = !autofire;
            DungeonUi.Label(new Rect(70, 236, 400, 20), "CONTROLS", 12, DungeonUi.Muted);
            DungeonUi.Label(new Rect(480, 236, 730, 20), "AUTOFIRE LETS A FULLY CHARGED ATTACK GO AFTER 1 SECOND HELD", 12, DungeonUi.Muted, TextAnchor.UpperRight);
            keybinds.Draw();
        }

        public void ResetToDefaults()
        {
            keybinds.Cancel();
            KeyBindings.ResetToDefaults();
            GameSettings.ResetToDefaults();
        }

        private static bool DrawSlider(string id, Rect row, string label, ref float value, float min)
        {
            DungeonUi.Panel(row, DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(row.x + 18, row.y, 200, row.height), label, 17, DungeonUi.Text, TextAnchor.MiddleLeft);
            bool changed = DungeonUi.Slider(id, new Rect(row.x + 130, row.y + 8, row.width - 210, row.height - 16), ref value,
                min, GameSettings.MaxScale, 0.05f, DungeonUi.Teal);
            DungeonUi.Label(new Rect(row.xMax - 72, row.y, 56, row.height), $"{DungeonUi.SliderShown(id, value) * 100f:0}%", 17, DungeonUi.Teal, TextAnchor.MiddleRight);
            return changed;
        }
    }
}

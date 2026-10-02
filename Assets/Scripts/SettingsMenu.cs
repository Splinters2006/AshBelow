using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The settings page of the main menu (and the HUD's cog): UI sizes and HUD opacity above either the control
    /// rebinding or the autofire switches, one per hero.
    /// </summary>
    public sealed class SettingsMenu
    {
        private readonly KeybindMenu keybinds = new KeybindMenu();
        // The lower half of the page shows the autofire switches instead of the controls while this is set.
        private bool showAutofire;

        public void Cancel()
        {
            keybinds.Cancel();
            showAutofire = false;
        }

        public void Draw()
        {
            float menu = GameSettings.MenuScale, hud = GameSettings.HudScale, opacity = GameSettings.HudOpacity;
            if (DrawSlider("menuSize", new Rect(70, 176, 310, 46), "Menu size", ref menu, GameSettings.MinScale)) GameSettings.MenuScale = menu;
            if (DrawSlider("hudSize", new Rect(390, 176, 310, 46), "HUD size", ref hud, GameSettings.MinScale)) GameSettings.HudScale = hud;
            if (DrawSlider("hudOpacity", new Rect(710, 176, 310, 46), "HUD opacity", ref opacity, GameSettings.MinOpacity)) GameSettings.HudOpacity = opacity;
            if (DungeonUi.Button("autofire", new Rect(1030, 176, 180, 46), showAutofire ? "Controls" : "Autofire", showAutofire ? DungeonUi.Teal : AbilityCatalog.Gold, size: 17))
            {
                keybinds.Cancel();
                showAutofire = !showAutofire;
            }
            if (showAutofire) { DrawAutofire(); return; }
            DungeonUi.Label(new Rect(70, 236, 400, 20), "CONTROLS", 12, DungeonUi.Muted);
            DungeonUi.Label(new Rect(480, 236, 730, 20), "AUTOFIRE IS SWITCHED ON AND OFF FOR EACH HERO", 12, DungeonUi.Muted, TextAnchor.UpperRight);
            keybinds.Draw();
        }

        /// <summary>One switch per hero, in the roster's order: autofire releases a full charge the moment it is ready.</summary>
        private static void DrawAutofire()
        {
            DungeonUi.Label(new Rect(70, 236, 700, 20), "AUTOFIRE BY HERO", 12, DungeonUi.Muted);
            const int Columns = 4;
            const float Gap = 10f, Height = 48f, RowStep = 58f;
            float width = (1140f - Gap * (Columns - 1)) / Columns;
            var heroes = HeroRoster.Order;
            for (int i = 0; i < heroes.Length; i++)
            {
                var weapon = heroes[i];
                bool on = GameSettings.AutofireFor(weapon);
                var rect = new Rect(70 + i % Columns * (width + Gap), 262 + i / Columns * RowStep, width, Height);
                if (DungeonUi.Button("autofire" + weapon, rect, $"{HeroRoster.Name(weapon)}:  {(on ? "ON" : "OFF")}", on ? AbilityCatalog.Gold : DungeonUi.Muted, size: 17))
                    GameSettings.SetAutofire(weapon, !on);
            }
            float below = 262 + (heroes.Length + Columns - 1) / Columns * RowStep + 6;
            if (DungeonUi.Button("autofireAllOn", new Rect(70, below, 200, 40), "All on", AbilityCatalog.Gold, size: 16))
                foreach (var weapon in heroes) GameSettings.SetAutofire(weapon, true);
            if (DungeonUi.Button("autofireAllOff", new Rect(280, below, 200, 40), "All off", DungeonUi.Muted, size: 16))
                foreach (var weapon in heroes) GameSettings.SetAutofire(weapon, false);
            DungeonUi.Label(new Rect(70, 568, 1140, 24),
                "Holding the attack button with autofire on releases a fully charged attack the moment it is ready, then starts charging the next one.",
                14, DungeonUi.Muted);
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

using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The player's settings, saved in PlayerPrefs. The UI is laid out on a fixed canvas that already fills the screen,
    /// so a UI size shrinks it (about the centre) from full size down to <see cref="MinScale"/>.
    /// </summary>
    public static class GameSettings
    {
        public const float MinScale = 0.6f, MaxScale = 1f;
        /// <summary>The faintest the in-run HUD can be made; it never fades out entirely.</summary>
        public const float MinOpacity = 0.2f;
        // The old single autofire switch is kept only as the starting value of every hero's own switch.
        private const string MenuKey = "ui.menuScale", HudKey = "ui.hudScale", HudOpacityKey = "ui.hudOpacity", AutofireKey = "gameplay.autofire",
            HeroAutofirePrefix = "gameplay.autofire.";
        // Kept under the co-op page's old key, where the colour was first picked.
        private const string NameColorKey = "AshBelow.CoopColor";
        private static float? menuScale, hudScale, hudOpacity;
        private static int? nameColor;
        private static readonly Dictionary<WeaponType, bool> autofire = new Dictionary<WeaponType, bool>();

        /// <summary>Size of the main menu and its pages.</summary>
        public static float MenuScale
        {
            get => menuScale ??= Load(MenuKey);
            set => menuScale = Save(MenuKey, value);
        }

        /// <summary>Size of the in-run HUD.</summary>
        public static float HudScale
        {
            get => hudScale ??= Load(HudKey);
            set => hudScale = Save(HudKey, value);
        }

        /// <summary>How solid the in-run HUD is drawn over the floor, from <see cref="MinOpacity"/> to fully opaque.</summary>
        public static float HudOpacity
        {
            get => hudOpacity ??= Load(HudOpacityKey, MinOpacity);
            set => hudOpacity = Save(HudOpacityKey, value, MinOpacity);
        }

        /// <summary>The colour (<see cref="NameTag.Colors"/>) the party sees your name in.</summary>
        public static int NameColor
        {
            get
            {
                if (nameColor == null) { int.TryParse(PlayerPrefs.GetString(NameColorKey, "0"), out int saved); nameColor = NameTag.ClampColor(saved); }
                return nameColor.Value;
            }
            set
            {
                nameColor = NameTag.ClampColor(value);
                PlayerPrefs.SetString(NameColorKey, nameColor.Value.ToString());
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Autofire, switched on or off for each hero: while the attack button is held, a fully charged attack is released
        /// the moment it is ready and the next one starts charging straight away.
        /// </summary>
        public static bool AutofireFor(WeaponType weapon)
        {
            if (autofire.TryGetValue(weapon, out bool on)) return on;
            // Never set for this hero: start from the old switch every hero used to share.
            on = PlayerPrefs.GetInt(HeroAutofirePrefix + weapon, PlayerPrefs.GetInt(AutofireKey, 0)) != 0;
            autofire[weapon] = on;
            return on;
        }

        public static void SetAutofire(WeaponType weapon, bool on)
        {
            autofire[weapon] = on;
            PlayerPrefs.SetInt(HeroAutofirePrefix + weapon, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            MenuScale = MaxScale;
            HudScale = MaxScale;
            HudOpacity = 1f;
            foreach (var weapon in HeroRoster.Order) SetAutofire(weapon, false);
            PlayerPrefs.SetInt(AutofireKey, 0);
            PlayerPrefs.Save();
        }

        private static float Load(string key, float min = MinScale) => Mathf.Clamp(PlayerPrefs.GetFloat(key, MaxScale), min, MaxScale);

        private static float Save(string key, float value, float min = MinScale)
        {
            value = Mathf.Clamp(value, min, MaxScale);
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            return value;
        }
    }
}

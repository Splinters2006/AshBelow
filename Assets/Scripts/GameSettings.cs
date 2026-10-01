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
        /// <summary>How long autofire holds a fully charged attack before releasing it.</summary>
        public const float AutofireDelay = 1f;
        private const string MenuKey = "ui.menuScale", HudKey = "ui.hudScale", HudOpacityKey = "ui.hudOpacity", AutofireKey = "gameplay.autofire";
        private static float? menuScale, hudScale, hudOpacity;
        private static bool? autofire;

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

        /// <summary>Releases a fully charged attack after <see cref="AutofireDelay"/> and starts charging again while the button is held.</summary>
        public static bool Autofire
        {
            get => autofire ??= PlayerPrefs.GetInt(AutofireKey, 0) != 0;
            set
            {
                autofire = value;
                PlayerPrefs.SetInt(AutofireKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void ResetToDefaults()
        {
            MenuScale = MaxScale;
            HudScale = MaxScale;
            HudOpacity = 1f;
            Autofire = false;
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

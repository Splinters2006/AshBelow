using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// How a co-op player's name is drawn: in the colour they picked, and for a developer in their own font with
    /// <see cref="Developers.Title"/> beneath (or beside it, where a row has no room below).
    /// </summary>
    public static class NameTag
    {
        public static readonly Color[] Colors =
        {
            new Color(0.93f, 0.93f, 0.9f), new Color(1f, 0.82f, 0.36f), new Color(1f, 0.5f, 0.42f), new Color(1f, 0.52f, 0.78f),
            new Color(0.72f, 0.56f, 1f), new Color(0.45f, 0.7f, 1f), new Color(0.38f, 0.9f, 0.82f), new Color(0.6f, 0.92f, 0.44f),
        };
        private static readonly Color TitleColor = new Color(1f, 0.8f, 0.3f);
        private static readonly Dictionary<(Font, int, TextAnchor), GUIStyle> styles = new Dictionary<(Font, int, TextAnchor), GUIStyle>();

        public static int ClampColor(int index) => index >= 0 && index < Colors.Length ? index : 0;
        public static Color ColorOf(int index) => Colors[ClampColor(index)];

        /// <summary>Draws the name at the top of <paramref name="rect"/> and returns how wide it came out.</summary>
        public static float Draw(Rect rect, string name, int color, int badge, int size, TextAnchor align = TextAnchor.UpperLeft, bool titleBelow = true)
        {
            var font = Developers.FontFor(badge);
            var style = Style(font, size, align);
            style.normal.textColor = ColorOf(color);
            Rect line = new Rect(rect.x, rect.y, rect.width, size + 6);
            GUI.Label(line, name, style);
            float width = Mathf.Min(rect.width, style.CalcSize(new GUIContent(name)).x);
            if (badge <= 0) return width;
            int titleSize = Mathf.Max(9, Mathf.RoundToInt(size * 0.62f));
            if (titleBelow)
            {
                var below = Style(null, titleSize, align);
                below.normal.textColor = TitleColor;
                GUI.Label(new Rect(rect.x, line.yMax - 2, rect.width, titleSize + 6), Developers.Title, below);
            }
            else
            {
                // Beside the name, clipped to the row rather than spilling into whatever sits to its right.
                var beside = Style(null, titleSize, TextAnchor.UpperLeft);
                beside.normal.textColor = TitleColor;
                GUI.Label(new Rect(rect.x + width + 8, rect.y + (size - titleSize) * 0.6f, Mathf.Max(0f, rect.width - width - 8), titleSize + 6), Developers.Title, beside);
            }
            return width;
        }

        private static GUIStyle Style(Font font, int size, TextAnchor align)
        {
            if (!styles.TryGetValue((font, size, align), out var style))
            {
                style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = align, wordWrap = false, clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0) };
                styles.Add((font, size, align), style);
            }
            return style;
        }
    }
}

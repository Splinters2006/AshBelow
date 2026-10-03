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

        /// <summary>
        /// Draws the name at the top of <paramref name="rect"/> and returns how wide it came out. In a row (the title
        /// beside the name) a name and title too long for the row slide back and forth instead of being cut off.
        /// </summary>
        public static float Draw(Rect rect, string name, int color, int badge, int size, TextAnchor align = TextAnchor.UpperLeft, bool titleBelow = true)
        {
            var font = Developers.FontFor(badge);
            var style = Style(font, size, align);
            style.normal.textColor = ColorOf(color);
            Rect line = new Rect(rect.x, rect.y, rect.width, size + 6);
            float nameWidth = style.CalcSize(new GUIContent(name)).x;
            int titleSize = Mathf.Max(9, Mathf.RoundToInt(size * 0.62f));
            if (!titleBelow) return DrawRow(line, name, nameWidth, style, badge > 0, size, titleSize);
            GUI.Label(line, name, style);
            float width = Mathf.Min(rect.width, nameWidth);
            if (badge <= 0) return width;
            var below = Style(null, titleSize, align);
            below.normal.textColor = TitleColor;
            GUI.Label(new Rect(rect.x, line.yMax - 2, rect.width, titleSize + 6), Developers.Title, below);
            return width;
        }

        /// <summary>The name with the title beside it, gliding along the row when together they do not fit.</summary>
        private static float DrawRow(Rect line, string name, float nameWidth, GUIStyle style, bool titled, int size, int titleSize)
        {
            var beside = Style(null, titleSize, TextAnchor.UpperLeft);
            beside.normal.textColor = TitleColor;
            float titleWidth = titled ? beside.CalcSize(new GUIContent(Developers.Title)).x : 0f;
            float total = nameWidth + (titled ? 8f + titleWidth : 0f);
            float shift = DungeonUi.MarqueeOffset(total - line.width);
            // A left-aligned group so the strip can slide whatever the row's own alignment.
            var left = Style(style.font, style.fontSize, TextAnchor.UpperLeft);
            left.normal.textColor = style.normal.textColor;
            GUI.BeginGroup(line);
            GUI.Label(new Rect(-shift, 0, nameWidth + 2, line.height), name, left);
            if (titled) GUI.Label(new Rect(nameWidth + 8 - shift, (size - titleSize) * 0.6f, titleWidth + 2, titleSize + 6), Developers.Title, beside);
            GUI.EndGroup();
            return Mathf.Min(line.width, nameWidth);
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

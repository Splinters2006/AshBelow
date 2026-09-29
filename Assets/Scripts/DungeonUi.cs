using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public static class DungeonUi
    {
        public const float Width = 1280, Height = 720;
        public static readonly Color Background = new Color(0.025f, 0.035f, 0.06f, 0.97f);
        public static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.115f, 0.96f);
        public static readonly Color Muted = new Color(0.57f, 0.64f, 0.73f);
        public static readonly Color Text = new Color(0.91f, 0.93f, 0.98f);
        public static readonly Color Teal = new Color(0.42f, 0.88f, 0.79f);
        private static Texture2D rounded;
        private static GUIStyle panel, invisible;
        private static readonly Dictionary<int, GUIStyle> labels = new Dictionary<int, GUIStyle>();
        private static readonly Dictionary<string, float> hovers = new Dictionary<string, float>();
        private static readonly Dictionary<int, GUIStyle> fields = new Dictionary<int, GUIStyle>();
        private static readonly Dictionary<string, Vector2> scrolls = new Dictionary<string, Vector2>();

        public static Matrix4x4 Begin()
        {
            var previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / Width, Screen.height / Height);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) / 2f, (Screen.height - Height * scale) / 2f), Quaternion.identity, Vector3.one * scale);
            return previous;
        }

        private static void Initialize()
        {
            if (rounded != null) return;
            const int size = 32;
            rounded = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 6f);
                    float dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 6f);
                    rounded.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(10f - Mathf.Sqrt(dx * dx + dy * dy))));
                }
            rounded.Apply();
            panel = new GUIStyle { border = new RectOffset(12, 12, 12, 12) };
            panel.normal.background = rounded;
            invisible = new GUIStyle();
        }

        public static void Panel(Rect rect, Color color)
        {
            Initialize();
            Color previous = GUI.color;
            GUI.color = color;
            if (rect.width < 24f || rect.height < 24f) GUI.DrawTexture(rect, Texture2D.whiteTexture);
            else GUI.Box(rect, GUIContent.none, panel);
            GUI.color = previous;
        }

        public static void Label(Rect rect, string value, int size = 18, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var style = LabelStyle(size, align);
            style.normal.textColor = color ?? Text;
            GUI.Label(rect, value, style);
        }

        private static GUIStyle LabelStyle(int size, TextAnchor align)
        {
            int key = size * 16 + (int)align;
            if (!labels.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, alignment = align,
                    fontStyle = size >= 25 ? FontStyle.Bold : FontStyle.Normal, padding = new RectOffset(0, 0, 0, 0) };
                labels.Add(key, style);
            }
            return style;
        }

        /// <summary>
        /// Wrapped text that scrolls (mouse wheel or scrollbar) when it does not fit in <paramref name="rect"/>,
        /// with a small hint while more text lies below.
        /// </summary>
        public static void ScrollingText(string id, Rect rect, string value, int size, Color color)
        {
            var style = LabelStyle(size, TextAnchor.UpperLeft);
            const float scrollbar = 14f;
            float height = style.CalcHeight(new GUIContent(value), rect.width);
            if (height <= rect.height) { Label(rect, value, size, color); return; }
            float innerWidth = rect.width - scrollbar;
            height = style.CalcHeight(new GUIContent(value), innerWidth) + 4f;
            scrolls.TryGetValue(id, out var scroll);
            scroll = GUI.BeginScrollView(rect, scroll, new Rect(0, 0, innerWidth, height), false, true);
            Label(new Rect(0, 0, innerWidth, height), value, size, color);
            GUI.EndScrollView();
            scrolls[id] = scroll;
            if (scroll.y < height - rect.height - 2f)
                Label(new Rect(rect.x, rect.yMax, innerWidth, 16), "scroll for more  ▾", 11, Muted, TextAnchor.UpperRight);
        }

        public static bool Button(string id, Rect rect, string text, Color accent, bool enabled = true)
        {
            Initialize();
            bool hovered = enabled && rect.Contains(Event.current.mousePosition);
            hovers.TryGetValue(id, out float hover);
            if (Event.current.type == EventType.Repaint)
            {
                hover = Mathf.MoveTowards(hover, hovered ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                hovers[id] = hover;
            }
            Panel(rect, Color.Lerp(PanelColor, new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 1f), hover));
            Panel(new Rect(rect.x, rect.y + rect.height - 3, rect.width, 3), enabled ? accent : Muted * 0.5f);
            Label(rect, text, 18, enabled ? Text : Muted, TextAnchor.MiddleCenter);
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, invisible);
            GUI.enabled = previous;
            return clicked;
        }

        /// <summary>A single-line text box in the menu style.</summary>
        public static string TextField(string id, Rect rect, string value, int maxLength, int size = 20)
        {
            Initialize();
            Panel(rect, PanelColor);
            Panel(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), Muted * 0.6f);
            if (!fields.TryGetValue(size, out var style))
            {
                style = new GUIStyle(GUI.skin.textField) { fontSize = size, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 0, 0) };
                style.normal.background = style.focused.background = style.hover.background = null;
                style.normal.textColor = style.focused.textColor = style.hover.textColor = Text;
                fields.Add(size, style);
            }
            GUI.SetNextControlName(id);
            return GUI.TextField(rect, value ?? "", maxLength, style);
        }

        public static void Bar(Rect rect, float amount, Color color)
        {
            Panel(rect, new Color(0.02f, 0.03f, 0.045f, 0.95f));
            if (amount > 0f) Panel(new Rect(rect.x, rect.y, Mathf.Max(rect.height, rect.width * Mathf.Clamp01(amount)), rect.height), color);
        }

        public static string SpecialName(WeaponType weapon) => weapon == WeaponType.Bow ? "Triple shot"
            : weapon == WeaponType.Shadow ? "Nightfall" : weapon == WeaponType.Staff ? "Lightning" : weapon == WeaponType.Daggers ? "Shadowstep"
            : weapon == WeaponType.Fists ? "Empower" : weapon == WeaponType.Hammer ? "Holy Sword" : "Reflect shield";
        public static float SpecialCooldown(WeaponType weapon) => weapon == WeaponType.Bow ? 6f
            : weapon == WeaponType.Shadow ? AdminAttack.NightfallCooldown : weapon == WeaponType.Staff ? 3f : weapon == WeaponType.Daggers ? 4f
            : weapon == WeaponType.Fists ? BrawlerAttack.EmpowerCooldown : weapon == WeaponType.Hammer ? PaladinAttack.HolySwordCooldown
            : KnightShield.Cooldown;
    }
}

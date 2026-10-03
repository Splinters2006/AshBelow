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
        private static Texture2D rounded, cog, eyeOpen, eyeShut;
        private static GUIStyle panel, invisible;
        private static readonly Dictionary<int, GUIStyle> labels = new Dictionary<int, GUIStyle>();
        private static readonly Dictionary<string, float> hovers = new Dictionary<string, float>();
        private static readonly Dictionary<int, GUIStyle> fields = new Dictionary<int, GUIStyle>();
        private static readonly Dictionary<string, Vector2> scrolls = new Dictionary<string, Vector2>();
        private static readonly HashSet<string> revealed = new HashSet<string>();

        private static readonly Dictionary<string, float> drags = new Dictionary<string, float>();

        /// <summary>Fits the 1280x720 canvas to the screen, shrunk about the centre by the player's UI size (see <see cref="GameSettings"/>).</summary>
        public static Matrix4x4 Begin(float size = 1f)
        {
            var previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / Width, Screen.height / Height) * size;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) / 2f, (Screen.height - Height * scale) / 2f), Quaternion.identity, Vector3.one * scale);
            return previous;
        }

        /// <summary>Converts a screen pixel position (origin bottom-left) to canvas coordinates under <see cref="Begin"/> with the same size.</summary>
        public static Vector2 ScreenToCanvas(Vector2 screen, float size = 1f)
        {
            float scale = Mathf.Min(Screen.width / Width, Screen.height / Height) * size;
            return new Vector2(screen.x - (Screen.width - Width * scale) / 2f, Screen.height - screen.y - (Screen.height - Height * scale) / 2f) / scale;
        }

        // Anchors for the in-run HUD: each group of HUD elements is pinned to its own corner or edge of the screen, so
        // it stays out at the edges on wide screens and when the HUD is shrunk (rather than shrinking toward the centre).
        public static readonly Vector2 TopLeft = new Vector2(0f, 0f), TopCenter = new Vector2(0.5f, 0f), TopRight = new Vector2(1f, 0f),
            Center = new Vector2(0.5f, 0.5f), BottomCenter = new Vector2(0.5f, 1f);

        /// <summary>
        /// The canvas fitted to the screen like <see cref="Begin"/>, but with the canvas point at <paramref name="anchor"/>
        /// (0-1 across and down) pinned to the same point of the screen, then nudged by <paramref name="nudge"/> canvas units.
        /// The centre anchor with no nudge is exactly <see cref="Begin"/>.
        /// </summary>
        public static Matrix4x4 AnchorMatrix(float size, Vector2 anchor, Vector2 nudge = default)
        {
            float scale = Mathf.Min(Screen.width / Width, Screen.height / Height) * size;
            Vector2 offset = new Vector2(anchor.x * Screen.width, anchor.y * Screen.height) - new Vector2(anchor.x * Width, anchor.y * Height) * scale + nudge * scale;
            return Matrix4x4.TRS(new Vector3(offset.x, offset.y), Quaternion.identity, Vector3.one * scale);
        }

        /// <summary>Draws what follows in the anchored canvas (see <see cref="AnchorMatrix"/>).</summary>
        public static void Anchor(float size, Vector2 anchor, Vector2 nudge = default) => GUI.matrix = AnchorMatrix(size, anchor, nudge);

        /// <summary>Converts a screen pixel position (origin bottom-left) to coordinates in an anchored canvas.</summary>
        public static Vector2 ScreenToCanvas(Vector2 screen, float size, Vector2 anchor, Vector2 nudge = default)
            => AnchorMatrix(size, anchor, nudge).inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0f));

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
            // Keeps whatever opacity the caller is drawing under (the HUD's opacity setting).
            GUI.color = new Color(color.r, color.g, color.b, color.a * previous.a);
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
        /// How far text <paramref name="overflow"/> pixels too wide for its row has slid along this frame: it rests at the
        /// start, glides to the end, rests, and glides back.
        /// </summary>
        public static float MarqueeOffset(float overflow)
        {
            if (overflow <= 0f) return 0f;
            const float Rest = 1.4f, Speed = 28f;
            float glide = overflow / Speed, cycle = 2f * (Rest + glide);
            float t = Mathf.Repeat(Time.unscaledTime, cycle);
            if (t < Rest) return 0f;
            if (t < Rest + glide) return Mathf.SmoothStep(0f, overflow, (t - Rest) / glide);
            if (t < 2f * Rest + glide) return overflow;
            return Mathf.SmoothStep(overflow, 0f, (t - 2f * Rest - glide) / glide);
        }

        /// <summary>One line of text that slides back and forth inside <paramref name="rect"/> when it is too long to fit.</summary>
        public static void MarqueeLabel(Rect rect, string value, int size, Color color, TextAnchor align = TextAnchor.UpperLeft)
        {
            var style = LabelStyle(size, align);
            float width = style.CalcSize(new GUIContent(value)).x;
            if (width <= rect.width) { Label(rect, value, size, color, align); return; }
            GUI.BeginGroup(rect);
            Label(new Rect(-MarqueeOffset(width - rect.width), 0, width + 4, rect.height), value, size, color);
            GUI.EndGroup();
        }

        /// <summary>
        /// Wrapped text that scrolls (mouse wheel or scrollbar) when it does not fit in <paramref name="rect"/>,
        /// with a small hint while more text lies below.
        /// </summary>
        public static void ScrollingText(string id, Rect rect, string value, int size, Color color)
        {
            var style = LabelStyle(size, TextAnchor.UpperLeft);
            float height = style.CalcHeight(new GUIContent(value), rect.width);
            if (height <= rect.height) { Label(rect, value, size, color); return; }
            // Wrap inside the skin's real scrollbar width so nothing spills sideways; text only ever scrolls vertically.
            var bar = GUI.skin.verticalScrollbar;
            float innerWidth = rect.width - Mathf.Max(14f, bar.fixedWidth + bar.margin.horizontal);
            height = style.CalcHeight(new GUIContent(value), innerWidth) + 4f;
            scrolls.TryGetValue(id, out var scroll);
            scroll.x = 0f;
            scroll = GUI.BeginScrollView(rect, scroll, new Rect(0, 0, innerWidth, height), false, true, GUIStyle.none, bar);
            Label(new Rect(0, 0, innerWidth, height), value, size, color);
            GUI.EndScrollView();
            scrolls[id] = scroll;
            if (scroll.y < height - rect.height - 2f)
                Label(new Rect(rect.x, rect.yMax, innerWidth, 16), "scroll for more  ▾", 11, Muted, TextAnchor.UpperRight);
        }

        public static bool Button(string id, Rect rect, string text, Color accent, bool enabled = true, int size = 18)
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
            Label(rect, text, size, enabled ? Text : Muted, TextAnchor.MiddleCenter);
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, invisible);
            GUI.enabled = previous;
            return clicked;
        }

        /// <summary>
        /// One segment of a tab bar drawn over a shared track: the active tab is filled with its accent and cannot be
        /// clicked; the others light up on hover. Returns true when an inactive tab is clicked.
        /// </summary>
        public static bool Tab(string id, Rect rect, string text, Color accent, bool active, int size = 14)
        {
            Initialize();
            bool hovered = !active && rect.Contains(Event.current.mousePosition);
            hovers.TryGetValue(id, out float hover);
            if (Event.current.type == EventType.Repaint)
            {
                hover = Mathf.MoveTowards(hover, hovered ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                hovers[id] = hover;
            }
            if (active)
            {
                Panel(rect, new Color(accent.r * 0.24f, accent.g * 0.24f, accent.b * 0.24f, 1f));
                Panel(new Rect(rect.x + 10, rect.yMax - 3, rect.width - 20, 2), accent);
            }
            else if (hover > 0f) Panel(rect, new Color(accent.r, accent.g, accent.b, 0.1f * hover));
            Label(rect, text, size, active ? accent : Color.Lerp(Muted, Text, hover), TextAnchor.MiddleCenter);
            return !active && GUI.Button(rect, GUIContent.none, invisible);
        }

        /// <summary>A thin rule in the muted colour, for dividing sections of a panel.</summary>
        public static void Divider(Rect rect) => Panel(rect, new Color(Muted.r, Muted.g, Muted.b, 0.22f));

        /// <summary>A row of <paramref name="max"/> small squares with the first <paramref name="filled"/> lit; drawn right to left from <paramref name="right"/>.</summary>
        public static void Pips(float right, float centerY, int filled, int max, Color color, float size = 8f, float gap = 4f)
        {
            for (int i = 0; i < max; i++)
            {
                float x = right - (max - i) * (size + gap) + gap;
                Panel(new Rect(x, centerY - size / 2f, size, size), i < filled ? color : new Color(Muted.r, Muted.g, Muted.b, 0.25f));
            }
        }

        /// <summary>The settings cog wheel shown on every menu and the HUD: a square button with a gear on it.</summary>
        public static bool CogButton(string id, Rect rect, bool open = false)
        {
            bool clicked = Button(id, rect, "", open ? AbilityCatalog.Gold : Muted);
            if (Event.current.type != EventType.Repaint) return clicked;
            float size = Mathf.Min(rect.width, rect.height) - 14f;
            Color previous = GUI.color;
            GUI.color = FlameMesh.Alpha(open ? AbilityCatalog.Gold : Text, previous.a);
            GUI.DrawTexture(new Rect(rect.center.x - size / 2f, rect.center.y - size / 2f - 1f, size, size), CogTexture);
            GUI.color = previous;
            return clicked;
        }

        /// <summary>An eight-toothed gear with a hole through its hub.</summary>
        private static Texture2D CogTexture
        {
            get
            {
                if (cog != null) return cog;
                const int size = 64;
                const float center = (size - 1) / 2f;
                cog = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - center, dy = y - center, radius = Mathf.Sqrt(dx * dx + dy * dy);
                        // Teeth: the rim steps out wherever the angle falls on a tooth.
                        float tooth = Mathf.Clamp01(Mathf.Cos(Mathf.Atan2(dy, dx) * 8f) * 4f + 0.5f);
                        float outer = Mathf.Lerp(22f, 30f, tooth);
                        float alpha = Mathf.Clamp01(outer - radius) * Mathf.Clamp01(radius - 9f);
                        cog.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                cog.Apply();
                return cog;
            }
        }

        /// <summary>
        /// A horizontal slider snapped to <paramref name="step"/>. The new value only lands in <paramref name="value"/> when the
        /// drag is released (so a slider that resizes the UI doesn't move under the cursor); returns true then.
        /// </summary>
        public static bool Slider(string id, Rect rect, ref float value, float min, float max, float step, Color accent)
        {
            Initialize();
            int control = GUIUtility.GetControlID(id.GetHashCode(), FocusType.Passive, rect);
            Event e = Event.current;
            if (!drags.TryGetValue(id, out float shown)) shown = value;
            bool released = false;
            switch (e.GetTypeForControl(control))
            {
                case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition):
                    GUIUtility.hotControl = control;
                    drags[id] = shown = SliderValue(rect, e.mousePosition.x, min, max, step);
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == control:
                    drags[id] = shown = SliderValue(rect, e.mousePosition.x, min, max, step);
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == control:
                    GUIUtility.hotControl = 0;
                    drags.Remove(id);
                    released = !Mathf.Approximately(shown, value);
                    value = shown;
                    e.Use();
                    break;
            }
            float t = Mathf.InverseLerp(min, max, shown);
            float x = rect.x + 8 + t * (rect.width - 16);
            Panel(new Rect(rect.x, rect.center.y - 2, rect.width, 4), Muted * 0.4f);
            Panel(new Rect(rect.x, rect.center.y - 2, x - rect.x, 4), accent);
            Panel(new Rect(x - 6, rect.center.y - 11, 12, 22), drags.ContainsKey(id) ? Text : accent);
            return released;
        }

        /// <summary>The value a slider shows: the one being dragged, or else <paramref name="value"/>.</summary>
        public static float SliderShown(string id, float value) => drags.TryGetValue(id, out float shown) ? shown : value;

        private static float SliderValue(Rect rect, float x, float min, float max, float step)
        {
            float value = Mathf.Lerp(min, max, Mathf.Clamp01((x - rect.x - 8) / (rect.width - 16)));
            return Mathf.Clamp(Mathf.Round(value / step) * step, min, max);
        }

        /// <summary>A single-line text box in the menu style.</summary>
        public static string TextField(string id, Rect rect, string value, int maxLength, int size = 20) =>
            GUI.TextField(rect, value ?? "", maxLength, FieldStyle(id, rect, size));

        /// <summary>
        /// A text box in the menu style that shows dots instead of what is typed, with an eye at its end that shows the
        /// typing while it is open.
        /// </summary>
        public static string PasswordField(string id, Rect rect, string value, int maxLength, int size = 20)
        {
            var style = FieldStyle(id, rect, size);
            // The text stops short of the eye, so clicks on the eye never land in the box.
            var eyeRect = new Rect(rect.xMax - rect.height, rect.y, rect.height, rect.height);
            var textRect = new Rect(rect.x, rect.y, rect.width - eyeRect.width, rect.height);
            bool shown = revealed.Contains(id);
            value = shown ? GUI.TextField(textRect, value ?? "", maxLength, style) : GUI.PasswordField(textRect, value ?? "", '*', maxLength, style);
            if (EyeButton(id + "Eye", eyeRect, shown) && !revealed.Remove(id)) revealed.Add(id);
            return value;
        }

        /// <summary>Forgets which password boxes were showing their typing, so they are hidden again next time.</summary>
        public static void HidePasswords() => revealed.Clear();

        private static bool EyeButton(string id, Rect rect, bool open)
        {
            Initialize();
            bool hovered = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                float size = rect.height * 0.6f;
                Color previous = GUI.color;
                GUI.color = FlameMesh.Alpha(open ? Teal : hovered ? Text : Muted, previous.a);
                GUI.DrawTexture(new Rect(rect.center.x - size / 2f, rect.center.y - size / 2f, size, size), EyeTexture(open));
                GUI.color = previous;
            }
            return GUI.Button(rect, new GUIContent("", open ? "Hide password" : "Show password"), invisible);
        }

        /// <summary>An outlined eye with its pupil; the shut one is crossed out.</summary>
        private static Texture2D EyeTexture(bool open)
        {
            var cached = open ? eyeOpen : eyeShut;
            if (cached != null) return cached;
            const int size = 64;
            const float center = (size - 1) / 2f;
            // The lids are two arcs of circles above and below the eye: 44 pixels wide, 24 tall.
            const float halfWidth = 22f, halfHeight = 12f;
            const float offset = (halfWidth * halfWidth - halfHeight * halfHeight) / (2f * halfHeight), radius = offset + halfHeight;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center, dy = y - center;
                    float lids = Mathf.Max(Mathf.Sqrt(dx * dx + (dy + offset) * (dy + offset)), Mathf.Sqrt(dx * dx + (dy - offset) * (dy - offset))) - radius;
                    float alpha = Mathf.Max(Mathf.Clamp01(3f - Mathf.Abs(lids)), Mathf.Clamp01(7.5f - Mathf.Sqrt(dx * dx + dy * dy)));
                    if (!open && Mathf.Abs(dx) <= 24f) alpha = Mathf.Max(alpha, Mathf.Clamp01(3.5f - Mathf.Abs(dx + dy) * 0.7071f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            texture.Apply();
            if (open) eyeOpen = texture;
            else eyeShut = texture;
            return texture;
        }

        private static GUIStyle FieldStyle(string id, Rect rect, int size)
        {
            Initialize();
            Panel(rect, PanelColor);
            Panel(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), Muted * 0.6f);
            // Keyed by box height too: the padding centres one line of text in the box. (Middle alignment centres the text
            // but leaves the cursor near the top.)
            int key = size * 1000 + Mathf.RoundToInt(rect.height);
            if (!fields.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.textField) { fontSize = size, alignment = TextAnchor.UpperLeft, padding = new RectOffset(14, 14, 0, 0) };
                style.normal.background = style.focused.background = style.hover.background = null;
                style.normal.textColor = style.focused.textColor = style.hover.textColor = Text;
                int top = Mathf.Max(0, Mathf.FloorToInt((rect.height - style.CalcHeight(new GUIContent("Ag"), 1000f)) / 2f));
                style.padding = new RectOffset(14, 14, top, 0);
                fields.Add(key, style);
            }
            GUI.SetNextControlName(id);
            return style;
        }

        public static void Bar(Rect rect, float amount, Color color)
        {
            Panel(rect, new Color(0.02f, 0.03f, 0.045f, 0.95f));
            if (amount > 0f) Panel(new Rect(rect.x, rect.y, Mathf.Max(rect.height, rect.width * Mathf.Clamp01(amount)), rect.height), color);
        }

        public static string SpecialName(WeaponType weapon) => weapon == WeaponType.Bow ? "Triple shot"
            : weapon == WeaponType.Mutation ? "Guard / hook" : weapon == WeaponType.Staff ? "Zap" : weapon == WeaponType.Daggers ? "Shadowstep"
            : weapon == WeaponType.Fists ? "Empower" : weapon == WeaponType.Tail ? "Tail sweep" : weapon == WeaponType.Coins ? "Coin volley" : weapon == WeaponType.Beam ? "Plasma cannon" : weapon == WeaponType.Scythe ? "Soul skull" : weapon == WeaponType.Katana ? "Dash slash" : weapon == WeaponType.Hammer ? "Holy Sword" : "Reflect shield";
        public static float SpecialCooldown(WeaponType weapon) => weapon == WeaponType.Bow ? 6f
            : weapon == WeaponType.Mutation ? SpecimenAttack.GuardCooldown : weapon == WeaponType.Staff ? 3f : weapon == WeaponType.Daggers ? 4f
            : weapon == WeaponType.Fists ? BrawlerAttack.EmpowerCooldown : weapon == WeaponType.Tail ? DemonessAttack.SweepCooldown : weapon == WeaponType.Coins ? GamblerAttack.VolleyCooldown : weapon == WeaponType.Beam ? CyborgAttack.CannonCooldown : weapon == WeaponType.Scythe ? ReaperAttack.SkullCooldown : weapon == WeaponType.Katana ? SamuraiAttack.DashCooldown : weapon == WeaponType.Hammer ? PaladinAttack.HolySwordCooldown
            : KnightShield.Cooldown;
    }
}

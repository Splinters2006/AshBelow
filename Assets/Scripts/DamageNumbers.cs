using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Numbers that pop off whatever was hit and drift up as they fade (the shop's training dummies). Drawn by the HUD
    /// on its centred canvas, so they follow the HUD scale.
    /// </summary>
    public static class DamageNumbers
    {
        public const float Lifetime = 0.9f, Rise = 0.8f;
        public static readonly Color HitColor = new Color(1f, 0.96f, 0.86f), TickColor = new Color(1f, 0.6f, 0.45f);

        private struct Popup
        {
            public Vector2 Position;
            public int Amount;
            public Color Color;
            public float BornAt;
        }

        private static readonly List<Popup> popups = new List<Popup>();
        private static GUIStyle style;

        /// <summary>A number pops off <paramref name="position"/>, nudged sideways so a flurry of hits stays readable.</summary>
        public static void Show(Vector2 position, int amount, Color color)
        {
            if (popups.Count > 64) popups.RemoveAt(0);
            popups.Add(new Popup { Position = position + new Vector2(Random.Range(-0.25f, 0.25f), 0.55f), Amount = amount, Color = color, BornAt = Time.time });
        }

        public static void Clear() => popups.Clear();

        /// <summary>Draws every live number through <paramref name="view"/>; call from OnGUI with the centred canvas pinned.</summary>
        public static void Draw(Camera view)
        {
            popups.RemoveAll(popup => Time.time - popup.BornAt > Lifetime);
            if (view == null || popups.Count == 0 || Event.current.type != EventType.Repaint) return;
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = Color.white } };
            Color previous = GUI.color;
            foreach (var popup in popups)
            {
                float age = (Time.time - popup.BornAt) / Lifetime;
                Vector3 screen = view.WorldToScreenPoint(popup.Position + Vector2.up * Rise * age);
                if (screen.z < 0f) continue;
                Vector2 point = DungeonUi.ScreenToCanvas(screen, GameSettings.HudScale);
                // Pops in big, then settles and fades.
                style.fontSize = Mathf.RoundToInt(Mathf.Lerp(26f, 18f, Mathf.Clamp01(age * 4f)));
                float alpha = previous.a * (1f - age * age);
                var rect = new Rect(point.x - 50f, point.y - 15f, 100f, 30f);
                string text = popup.Amount.ToString();
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);
                GUI.color = new Color(popup.Color.r, popup.Color.g, popup.Color.b, alpha);
                GUI.Label(rect, text, style);
            }
            GUI.color = previous;
        }
    }
}

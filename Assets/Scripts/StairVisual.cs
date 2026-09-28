using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class StairVisual : MonoBehaviour
    {
        private readonly List<SpriteRenderer> stone = new List<SpriteRenderer>();
        private readonly List<Color> colors = new List<Color>();
        private bool unlocked;
        public static StairVisual Create(Transform parent, Vector2 position)
        {
            var root = new GameObject("Descending stairs");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var stairs = root.AddComponent<StairVisual>();
            stairs.Part("Dark stairwell", Vector2.zero, new Vector2(1.35f, 1.35f), new Color(0.025f, 0.025f, 0.03f));
            for (int i = 0; i < 5; i++)
            {
                float width = 1.06f - i * 0.13f;
                float y = -0.46f + i * 0.22f;
                Color gold = Color.Lerp(AbilityCatalog.Gold, new Color(0.34f, 0.21f, 0.07f), i / 5f);
                stairs.Part("Step " + (i + 1), new Vector2(0, y), new Vector2(width, 0.19f), gold);
                stairs.Part("Step lip " + (i + 1), new Vector2(0, y - 0.07f), new Vector2(width, 0.035f), gold * 1.25f);
            }
            stairs.Part("Left rail", new Vector2(-0.63f, 0), new Vector2(0.12f, 1.4f), AbilityCatalog.Gold);
            stairs.Part("Right rail", new Vector2(0.63f, 0), new Vector2(0.12f, 1.4f), AbilityCatalog.Gold);
            stairs.SetUnlocked(false, true);
            return stairs;
        }
        private void Part(string name, Vector2 offset, Vector2 size, Color color)
        {
            var part = DungeonVisuals.Create(name, transform, (Vector2)transform.position + offset, size, color, 1);
            stone.Add(part); colors.Add(color);
        }
        public void SetUnlocked(bool value, bool force = false)
        {
            if (!force && unlocked == value) return;
            unlocked = value;
            for (int i = 0; i < stone.Count; i++)
                stone[i].color = value ? colors[i] : Color.Lerp(colors[i], new Color(0.18f, 0.18f, 0.2f), 0.8f);
        }
    }
}

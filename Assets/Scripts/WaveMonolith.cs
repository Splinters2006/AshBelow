using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The standing stone on a wave level: nothing attacks until a hero touches it, which calls the first wave. Its
    /// runes pulse in the world's colour while it waits and go dark once the waves are under way.
    /// </summary>
    public sealed class WaveMonolith : MonoBehaviour
    {
        public const float Reach = 1.6f;
        private static readonly Color Stone = new Color(0.13f, 0.13f, 0.17f), StoneLight = new Color(0.22f, 0.22f, 0.28f);
        private readonly List<SpriteRenderer> runes = new List<SpriteRenderer>();
        private SpriteRenderer glow;
        private Color accent;
        private bool spent;

        public static WaveMonolith Create(Transform parent, Vector2 position, Color accent)
        {
            var root = new GameObject("Wave monolith");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var monolith = root.AddComponent<WaveMonolith>();
            monolith.accent = accent;
            monolith.glow = monolith.Part("Glow", new Vector2(0f, -0.55f), new Vector2(1.5f, 0.5f), FlameMesh.Alpha(accent, 0.3f));
            monolith.Part("Plinth", new Vector2(0f, -0.6f), new Vector2(1.1f, 0.24f), StoneLight);
            monolith.Part("Slab", new Vector2(0f, 0.25f), new Vector2(0.72f, 1.6f), Stone);
            monolith.Part("Slab edge", new Vector2(-0.31f, 0.25f), new Vector2(0.1f, 1.6f), StoneLight);
            monolith.Part("Cap", new Vector2(0f, 1.09f), new Vector2(0.5f, 0.12f), Stone);
            for (int i = 0; i < 4; i++)
                monolith.runes.Add(monolith.Part("Rune " + (i + 1), new Vector2(i % 2 == 0 ? 0.04f : 0.1f, -0.25f + i * 0.33f),
                    new Vector2(i % 2 == 0 ? 0.34f : 0.2f, 0.07f), accent));
            return monolith;
        }

        private SpriteRenderer Part(string name, Vector2 offset, Vector2 size, Color color)
            => DungeonVisuals.Create(name, transform, (Vector2)transform.position + offset, size, color, 2);

        public bool IsNear(DungeonPlayer hero) => hero != null && Vector2.Distance(hero.transform.position, transform.position) < Reach;

        /// <summary>The waves have been called: the stone discharges and its runes go dark.</summary>
        public void Activate()
        {
            if (spent) return;
            spent = true;
            HeroVfx.Pulse(transform.parent, transform.position, 4f, accent, 0.6f);
            HeroVfx.Sparks(transform.parent, (Vector2)transform.position + Vector2.up * 0.6f, accent, 24, 6f, 0.6f, Vector2.up, 360f);
            ScreenFx.Shake(0.25f, 0.35f);
            foreach (var rune in runes) rune.color = Color.Lerp(accent, Stone, 0.8f);
            glow.enabled = false;
        }

        private void Update()
        {
            if (spent) return;
            for (int i = 0; i < runes.Count; i++)
                runes[i].color = FlameMesh.Alpha(accent, 0.55f + 0.45f * Mathf.Sin(Time.time * 3f - i * 0.8f));
            glow.color = FlameMesh.Alpha(accent, 0.2f + 0.12f * Mathf.Sin(Time.time * 3f));
        }
    }
}

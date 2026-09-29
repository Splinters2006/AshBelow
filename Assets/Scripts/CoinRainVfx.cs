using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Windfall's shower: gold coins tumble out of the air all around the Gambler, spinning and glinting,
    /// bounce once where they land and fade away. Purely visual; Windfall adds the coins.
    /// </summary>
    public sealed class CoinRainVfx : MeshEffect
    {
        private const int Count = 22;
        private const float Spread = 1.8f, Drop = 2.6f, Fall = 0.45f;
        private static readonly Color Rim = new Color(0.7f, 0.46f, 0.1f), Shine = new Color(1f, 0.97f, 0.75f);
        private Vector2 center;
        private float seed;

        public static void Play(Transform root, Vector2 center)
        {
            var effect = Spawn<CoinRainVfx>(root, 1.4f, 10);
            if (effect == null) return;
            effect.center = center;
            effect.seed = Random.value * 100f;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            float time = t * Duration;
            for (int i = 0; i < Count; i++)
            {
                float h1 = FlameMesh.Hash(i, seed), h2 = FlameMesh.Hash(seed, i + 0.5f), h3 = FlameMesh.Hash(i * 1.7f, seed + 3f);
                float start = h3 * 0.55f, local = time - start;
                if (local < 0f) continue;
                // Spread over a disc around the Gambler, denser near him.
                Vector2 ground = center + FlameMesh.Polar(h1 * Mathf.PI * 2f, Spread * Mathf.Sqrt(h2));
                float height, alpha = 1f;
                if (local < Fall) { float u = local / Fall; height = Drop * (1f - u * u); }
                else
                {
                    float b = (local - Fall) / 0.25f;
                    height = b < 1f ? 0.25f * 4f * b * (1f - b) : 0f;
                    alpha = Mathf.Clamp01(1f - (local - Fall - 0.25f) / 0.3f);
                }
                if (alpha <= 0f) continue;
                float size = 0.11f + 0.05f * h2;
                // A soft shadow that grows as the coin nears the ground.
                float near = 1f - height / Drop;
                Mesh.Ellipse(ground, size * (0.6f + 0.6f * near), size * 0.35f, FlameMesh.Alpha(Color.black, 0.3f * near * alpha), FlameMesh.Alpha(Color.black, 0f), 12);
                Vector2 at = ground + Vector2.up * height;
                float spin = local < Fall + 0.25f ? Mathf.Cos(local * (14f + 8f * h1) + h2 * 6f) : 0.9f;
                float w = Mathf.Max(0.02f, size * Mathf.Abs(spin));
                Mesh.Ellipse(at + Vector2.down * 0.015f, w + 0.015f, size + 0.01f, FlameMesh.Alpha(Rim, alpha), FlameMesh.Alpha(Rim, alpha), 14);
                Color face = spin >= 0f ? Color.Lerp(GamblerAttack.Gold, Color.white, 0.3f) : GamblerAttack.Gold;
                Mesh.Ellipse(at, w, size, FlameMesh.Alpha(face, alpha), FlameMesh.Alpha(GamblerAttack.Gold, alpha), 14);
                // Now and then a coin catches the light.
                float glint = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Repeat(local * 2.2f + h1, 1f) - 0.5f) * 8f);
                if (glint > 0f)
                {
                    float g = glint * size * 2.2f;
                    Mesh.Bar(at - Vector2.up * g, Vector2.up, 2f * g, 0.03f, FlameMesh.Alpha(Shine, glint * alpha), FlameMesh.Alpha(Shine, glint * alpha));
                    Mesh.Bar(at - Vector2.right * g * 0.8f, Vector2.right, 1.6f * g, 0.03f, FlameMesh.Alpha(Shine, glint * alpha), FlameMesh.Alpha(Shine, glint * alpha));
                }
                // A little clink of sparks on the bounce.
                float clink = local - Fall;
                if (clink >= 0f && clink < 0.12f)
                    for (int s = -1; s <= 1; s += 2)
                        Mesh.Bar(ground, FlameMesh.Polar(Mathf.PI * 0.5f + s * 0.9f, 1f), 0.18f * (1f - clink / 0.12f), 0.025f,
                            FlameMesh.Alpha(Shine, 1f - clink / 0.12f), FlameMesh.Alpha(Shine, 0f));
            }
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// All In's coin toss: a big gold coin flips up over the Gambler's head, spins, and lands in the air before him.
    /// On a win it blazes gold with rays and a starburst; on a loss it goes dull, cracks and tumbles away. Follows the hero. Purely visual.
    /// </summary>
    public sealed class CoinFlipVfx : MeshEffect
    {
        private const float Land = 0.55f, Radius = 0.32f;
        private static readonly Color Rim = new Color(0.7f, 0.46f, 0.1f), Face = GamblerAttack.Gold, Shine = new Color(1f, 0.97f, 0.75f);
        private static readonly Color Dull = new Color(0.45f, 0.42f, 0.38f), DullRim = new Color(0.25f, 0.23f, 0.22f), Crack = new Color(0.08f, 0.06f, 0.06f);
        private Transform hero;
        private Vector2 anchor;
        private bool won;

        public static void Play(Transform root, Transform hero, bool won)
        {
            var effect = Spawn<CoinFlipVfx>(root, 1.2f, 11);
            if (effect == null) return;
            effect.hero = hero;
            effect.anchor = hero != null ? (Vector2)hero.position : Vector2.zero;
            effect.won = won;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            if (hero != null) anchor = hero.position;
            Vector2 rest = anchor + Vector2.up * 1.2f;
            if (t < Land)
            {
                // Tossed up in an arc, spinning fast, then caught at head height.
                float u = t / Land;
                Vector2 at = rest + Vector2.up * (4f * u * (1f - u) * 1.3f - (1f - u) * 0.6f);
                float spin = u * Mathf.PI * 9f;
                DrawCoin(at, Mathf.Cos(spin), Face, Rim, 1f, 0f);
                // Motion glints trailing the spinning coin.
                for (int i = 1; i <= 3; i++)
                    Mesh.Diamond(at - Vector2.up * 0.12f * i * (1f - 2f * u), 0.05f, FlameMesh.Alpha(Shine, 0.5f - 0.15f * i));
                return;
            }
            float r = (t - Land) / (1f - Land), fade = r < 0.6f ? 1f : 1f - (r - 0.6f) / 0.4f;
            if (won) DrawWin(rest, r, fade);
            else DrawLoss(rest, r, fade);
        }

        private void DrawWin(Vector2 at, float r, float fade)
        {
            float pop = 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(r / 0.25f) * Mathf.PI);
            // Golden rays turning slowly behind the coin.
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f + r * 1.5f;
                float length = (0.6f + 0.25f * (i % 2)) * EaseOut(r / 0.3f);
                Mesh.Bar(at + FlameMesh.Polar(a, Radius * 0.9f), FlameMesh.Polar(a, 1f), length, 0.09f,
                    FlameMesh.Alpha(Shine, 0.8f * fade), FlameMesh.Alpha(Face, 0f));
            }
            Mesh.Ellipse(at, 0.9f * pop, 0.9f * pop, FlameMesh.Alpha(Face, 0.35f * fade), FlameMesh.Alpha(Face, 0f), 28);
            DrawCoin(at, 1f, Face, Rim, fade, 1f, pop);
            // A starburst glint across the face.
            float glint = Mathf.Clamp01(1f - Mathf.Abs(r - 0.2f) / 0.2f);
            if (glint > 0f)
            {
                Vector2 spot = at + new Vector2(0.12f, 0.12f) * pop;
                Mesh.Bar(spot - Vector2.up * 0.35f * glint, Vector2.up, 0.7f * glint, 0.05f, FlameMesh.Alpha(Color.white, glint), FlameMesh.Alpha(Color.white, glint));
                Mesh.Bar(spot - Vector2.right * 0.35f * glint, Vector2.right, 0.7f * glint, 0.05f, FlameMesh.Alpha(Color.white, glint), FlameMesh.Alpha(Color.white, glint));
            }
        }

        private void DrawLoss(Vector2 at, float r, float fade)
        {
            // It lands, shudders, goes grey and cracks, then tumbles out of the air.
            float dull = Mathf.Clamp01(r / 0.2f);
            float drop = r < 0.3f ? 0f : (r - 0.3f) / 0.7f;
            Vector2 shake = r < 0.3f ? new Vector2(Mathf.Sin(r * 90f), 0f) * 0.04f : Vector2.zero;
            Vector2 where = at + shake + new Vector2(0.25f * drop, -1.1f * drop * drop);
            float tilt = Mathf.Cos(drop * Mathf.PI * 2.5f);
            DrawCoin(where, tilt, Color.Lerp(Face, Dull, dull), Color.Lerp(Rim, DullRim, dull), fade, 0f);
            if (dull >= 1f && Mathf.Abs(tilt) > 0.3f)
            {
                float w = Radius * Mathf.Abs(tilt);
                Color crack = FlameMesh.Alpha(Crack, fade);
                Vector2 a = where + new Vector2(-0.1f * w / Radius, Radius * 0.85f), b = where + new Vector2(0.08f * w / Radius, 0.05f),
                    c = where + new Vector2(-0.06f * w / Radius, -0.18f), d = where + new Vector2(0.12f * w / Radius, -Radius * 0.8f);
                Mesh.Bar(a, (b - a).normalized, Vector2.Distance(a, b), 0.04f, crack, crack);
                Mesh.Bar(b, (c - b).normalized, Vector2.Distance(b, c), 0.04f, crack, crack);
                Mesh.Bar(c, (d - c).normalized, Vector2.Distance(c, d), 0.035f, crack, crack);
            }
        }

        /// <summary>A coin seen edge-on as <paramref name="facing"/> goes to zero, with a darker rim for thickness.</summary>
        private void DrawCoin(Vector2 at, float facing, Color face, Color rim, float alpha, float emblem, float scale = 1f)
        {
            float r = Radius * scale, w = Mathf.Max(0.04f, r * Mathf.Abs(facing));
            Mesh.Ellipse(at + Vector2.down * 0.03f, w + 0.03f, r + 0.02f, FlameMesh.Alpha(rim, alpha), FlameMesh.Alpha(rim, alpha), 24);
            Color inner = facing >= 0f ? Color.Lerp(face, Color.white, 0.25f) : face;
            Mesh.Ellipse(at, w, r, FlameMesh.Alpha(inner, alpha), FlameMesh.Alpha(face, alpha), 24);
            Mesh.Ellipse(at, w * 0.72f, r * 0.72f, FlameMesh.Alpha(rim, 0.35f * alpha), FlameMesh.Alpha(rim, 0.35f * alpha), 20);
            Mesh.Ellipse(at, w * 0.62f, r * 0.62f, FlameMesh.Alpha(face, alpha), FlameMesh.Alpha(inner, alpha), 20);
            // A star stamped on the face, squashed with the coin.
            float star = Mathf.Lerp(0.5f, 1f, emblem) * alpha;
            Mesh.Diamond(at, r * 0.3f, FlameMesh.Alpha(emblem > 0f ? Shine : rim, star));
        }
    }
}

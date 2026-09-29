using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Jackpot's payout: a little three-reel slot window pops up over the Gambler's head, the reels spin and stop one by one
    /// on the prize (speed chevrons, a sword for damage or a cross for a heal), then that prize's symbol rises out of the
    /// window big and bright in its own colour. Follows the hero. Purely visual.
    /// </summary>
    public sealed class JackpotVfx : MeshEffect
    {
        public static readonly Color SpeedColor = new Color(0.35f, 0.9f, 1f);
        public static readonly Color DamageColor = new Color(1f, 0.3f, 0.22f);
        public static readonly Color HealColor = new Color(0.4f, 1f, 0.45f);
        private const float Reveal = 0.42f, Cell = 0.36f, Gap = 0.42f;
        private static readonly Color Frame = new Color(0.12f, 0.06f, 0.1f), Rim = GamblerAttack.Gold, Shine = new Color(1f, 0.97f, 0.75f);
        private Transform hero;
        private Vector2 anchor;
        private GamblerAttack.JackpotPrize prize;

        public static Color PrizeColor(GamblerAttack.JackpotPrize prize) => prize switch
        {
            GamblerAttack.JackpotPrize.Speed => SpeedColor,
            GamblerAttack.JackpotPrize.Damage => DamageColor,
            _ => HealColor
        };

        public static void Play(Transform root, Transform hero, GamblerAttack.JackpotPrize prize)
        {
            var effect = Spawn<JackpotVfx>(root, 1.7f, 11);
            if (effect == null) return;
            effect.hero = hero;
            effect.anchor = hero != null ? (Vector2)hero.position : Vector2.zero;
            effect.prize = prize;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            if (hero != null) anchor = hero.position;
            Color color = PrizeColor(prize);
            Vector2 window = anchor + Vector2.up * 1.3f;
            float open = EaseOut(t / 0.08f), fade = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            float won = Mathf.Clamp01((t - Reveal) / 0.08f);

            // The machine's window: a gold rim that lights up in the prize's colour once all three reels agree.
            Vector2 half = new Vector2(Gap * 1.5f + 0.06f, Cell * 0.5f + 0.06f) * open;
            if (won > 0f) Mesh.Ellipse(window, half.x * 1.5f, half.y * 2.4f, FlameMesh.Alpha(color, 0.4f * won * fade), FlameMesh.Alpha(color, 0f), 28);
            Color rim = FlameMesh.Alpha(Color.Lerp(Rim, color, won * (0.5f + 0.5f * Mathf.Sin(t * 60f))), fade);
            Mesh.Rect(window - half - Vector2.one * 0.05f, window + half + Vector2.one * 0.05f, rim);
            Mesh.Rect(window - half, window + half, FlameMesh.Alpha(Frame, 0.92f * fade));
            if (open < 1f) return;

            for (int i = 0; i < 3; i++)
            {
                Vector2 cell = window + Vector2.right * Gap * (i - 1);
                float stop = 0.14f + 0.1f * i;
                if (t < stop)
                {
                    // Blurred symbols rolling down through the cell.
                    float scroll = Age * 16f + i * 0.37f, frac = scroll - Mathf.Floor(scroll);
                    var shown = (GamblerAttack.JackpotPrize)(Mathf.FloorToInt(scroll) % 3);
                    float y = (0.5f - frac) * Cell;
                    DrawSymbol(shown, cell + Vector2.up * y, Cell * 0.4f, FlameMesh.Alpha(PrizeColor(shown), (1f - Mathf.Abs(y) / (Cell * 0.5f)) * 0.8f));
                    continue;
                }
                // Stopped on the prize with a little bounce and a flash of the cell.
                float since = (t - stop) * Duration;
                float bounce = Mathf.Exp(-since * 18f) * Mathf.Sin(since * 40f) * 0.06f;
                float flash = Mathf.Clamp01(1f - since / 0.15f);
                if (flash > 0f) Mesh.Rect(cell - Vector2.one * Cell * 0.5f, cell + Vector2.one * Cell * 0.5f, FlameMesh.Alpha(Shine, 0.5f * flash * fade));
                DrawSymbol(prize, cell + Vector2.up * bounce, Cell * 0.4f, FlameMesh.Alpha(color, fade));
            }

            if (t < Reveal) return;
            // The prize itself rises out of the machine, big, with rays turning behind it.
            float r = (t - Reveal) / (1f - Reveal), rise = EaseOut(r / 0.35f), glow = r < 0.65f ? 1f : 1f - (r - 0.65f) / 0.35f;
            float pop = 1f + 0.3f * Mathf.Sin(Mathf.Clamp01(r / 0.2f) * Mathf.PI);
            Vector2 at = window + Vector2.up * (0.35f + 0.55f * rise);
            float size = 0.34f * pop * Mathf.Lerp(0.5f, 1f, rise);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5f + r * 2f;
                Mesh.Bar(at + FlameMesh.Polar(a, size * 0.6f), FlameMesh.Polar(a, 1f), (0.45f + 0.2f * (i % 2)) * rise, 0.08f,
                    FlameMesh.Alpha(color, 0.75f * glow), FlameMesh.Alpha(color, 0f));
            }
            Mesh.Ellipse(at, size * 2f, size * 2f, FlameMesh.Alpha(color, 0.45f * glow), FlameMesh.Alpha(color, 0f), 28);
            // A dark outline under the bright symbol keeps it readable over any floor.
            DrawSymbol(prize, at + new Vector2(0.03f, -0.03f), size * 1.1f, FlameMesh.Alpha(Frame, 0.8f * glow));
            DrawSymbol(prize, at, size, FlameMesh.Alpha(Color.Lerp(color, Shine, 0.35f * (1f - rise)), glow));
        }

        /// <summary>The prize's icon, <paramref name="size"/> being about half its height.</summary>
        private void DrawSymbol(GamblerAttack.JackpotPrize symbol, Vector2 c, float size, Color color)
        {
            if (color.a <= 0.01f) return;
            float w = size * 0.3f;
            switch (symbol)
            {
                case GamblerAttack.JackpotPrize.Speed:
                    // Fast-forward chevrons.
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 tip = c + new Vector2(size * (0.05f + 0.55f * i), 0f);
                        Mesh.Bar(tip, new Vector2(-0.6f, 0.8f), size * 0.8f, w, color, color);
                        Mesh.Bar(tip, new Vector2(-0.6f, -0.8f), size * 0.8f, w, color, color);
                        Mesh.Diamond(tip, w * 0.5f, color);
                    }
                    break;
                case GamblerAttack.JackpotPrize.Damage:
                    // A sword, point up.
                    Vector2 guard = c + Vector2.down * size * 0.45f;
                    Mesh.Bar(guard, Vector2.up, size * 1.1f, w * 0.9f, color, color);
                    Vector2 tipBase = guard + Vector2.up * size * 1.1f;
                    Mesh.Triangle(tipBase + Vector2.left * w * 0.45f, tipBase + Vector2.up * size * 0.35f, tipBase + Vector2.right * w * 0.45f, color, color, color);
                    Mesh.Bar(guard + Vector2.left * size * 0.45f, Vector2.right, size * 0.9f, w * 0.8f, color, color);
                    Mesh.Bar(guard, Vector2.down, size * 0.35f, w * 0.7f, color, color);
                    Mesh.Diamond(guard + Vector2.down * size * 0.45f, w * 0.6f, color);
                    break;
                default:
                    // A healing cross.
                    Mesh.Rect(c - new Vector2(w * 0.8f, size * 0.85f), c + new Vector2(w * 0.8f, size * 0.85f), color);
                    Mesh.Rect(c - new Vector2(size * 0.85f, w * 0.8f), c + new Vector2(size * 0.85f, w * 0.8f), color);
                    break;
            }
        }
    }
}

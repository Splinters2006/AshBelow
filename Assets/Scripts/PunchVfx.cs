using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Brawler's punches, drawn as moving gloves rather than flat boxes. A jab shoots a glove out with speed lines
    /// and an impact star; a flurry keeps gloves hammering through the area in a blur; a heavy blow drives a huge
    /// glove through a cone of shockwaves that cracks the ground.
    /// </summary>
    public sealed class PunchVfx : MeshEffect
    {
        public enum Style : byte { Jab, Flurry, Heavy, Windup }
        private static readonly Color Cuff = new Color(0.96f, 0.93f, 0.86f);
        private static readonly Color Dust = new Color(0.75f, 0.68f, 0.58f);
        private readonly Vector2[] points = new Vector2[6];
        private Style style;
        private Vector2 origin, aim;
        private float length, halfWidth;
        private Color color;
        private Transform follow;
        private System.Func<Vector2> aimSource;

        public static PunchVfx Play(Transform root, Style style, Vector2 origin, Vector2 aim, float length, float halfWidth, Color color, float duration)
        {
            if (aim.sqrMagnitude < 0.0001f) return null;
            var effect = Spawn<PunchVfx>(root, duration);
            if (effect == null) return null;
            effect.style = style;
            effect.origin = origin;
            effect.aim = aim.normalized;
            effect.length = Mathf.Max(0.3f, length);
            effect.halfWidth = Mathf.Max(0.1f, halfWidth);
            effect.color = color;
            effect.Redraw();
            return effect;
        }

        /// <summary>A flurry stays on the Brawler and turns with her aim while it lasts.</summary>
        public PunchVfx Follow(Transform hero, System.Func<Vector2> aimOf)
        {
            follow = hero;
            aimSource = aimOf;
            return this;
        }

        public void Stop() => Destroy(gameObject);

        protected override void LateUpdate()
        {
            if (follow != null) origin = follow.position;
            if (aimSource != null)
            {
                Vector2 next = aimSource();
                if (next.sqrMagnitude > 0.0001f) aim = next.normalized;
            }
            base.LateUpdate();
        }

        protected override void Draw(float t)
        {
            switch (style)
            {
                case Style.Jab: DrawJab(t); break;
                case Style.Flurry: DrawFlurry(); break;
                case Style.Heavy: DrawHeavy(t); break;
                case Style.Windup: DrawWindup(t); break;
            }
        }

        private Vector2 Side => Vector2.Perpendicular(aim);

        private void DrawJab(float t)
        {
            float extend = t < 0.4f ? EaseOut(t / 0.4f) : 1f;
            float fade = t < 0.4f ? 1f : 1f - (t - 0.4f) / 0.6f;
            float fist = Mathf.Clamp(halfWidth * 0.8f, 0.2f, 0.75f);
            Area(0.12f * fade);
            Vector2 head = origin + aim * Mathf.Max(fist, length * extend - fist * 0.5f);
            SpeedLines(head, length * extend, fist, fade, 3);
            Glove(head, fist, fade);
            if (t >= 0.35f) Impact(origin + aim * length, fist * 1.3f, (t - 0.35f) / 0.65f);
        }

        private void DrawFlurry()
        {
            Area(0.1f + 0.04f * Mathf.Sin(Age * 30f));
            float fist = Mathf.Clamp(halfWidth * 0.35f, 0.18f, 0.4f);
            // Several gloves pump in and out at different lanes and rhythms, blurring into a barrage.
            for (int i = 0; i < 5; i++)
            {
                float seed = FlameMesh.Hash(i, 6.6f), cycle = Mathf.Repeat(Age * (7f + seed * 3f) + seed, 1f);
                float lane = (seed - 0.5f) * 2f * (halfWidth - fist);
                float reach = Mathf.Sin(cycle * Mathf.PI) * (length - fist);
                Vector2 head = origin + Side * lane + aim * Mathf.Max(fist, reach);
                float alpha = 0.35f + 0.65f * Mathf.Sin(cycle * Mathf.PI);
                SpeedLines(head, reach, fist, alpha * 0.7f, 2);
                Glove(head, fist, alpha);
                if (cycle > 0.45f && cycle < 0.6f) Star(origin + Side * lane + aim * length, fist * 1.2f, 1f - (cycle - 0.45f) / 0.15f);
            }
        }

        private void DrawHeavy(float t)
        {
            float extend = t < 0.25f ? EaseOut(t / 0.25f) : 1f, after = Mathf.Clamp01((t - 0.2f) / 0.8f), fade = 1f - after;
            float fist = Mathf.Clamp(halfWidth * 0.75f, 0.45f, 1.2f);
            Vector2 impact = origin + aim * length;
            // A cone of shock waves bursts out ahead of the blow.
            for (int w = 0; w < 3; w++)
            {
                float wave = Mathf.Clamp01(after * 1.4f - w * 0.15f);
                if (wave <= 0f || wave >= 1f) continue;
                Vector2 c = origin + aim * length * (0.35f + 0.75f * wave);
                float width = halfWidth * (1.1f + 0.9f * wave);
                for (int i = 0; i < points.Length; i++)
                {
                    float u = i / (float)(points.Length - 1) * 2f - 1f;
                    points[i] = c + Side * u * width - aim * u * u * width * 0.45f;
                }
                Stroke(points, points.Length, 0.18f * (1f - wave), 0.18f * (1f - wave), FlameMesh.Alpha(Color.white, 0.9f * (1f - wave)),
                    FlameMesh.Alpha(color, 0.9f * (1f - wave)));
            }
            // Cracks split the ground along the punch.
            for (int c = 0; c < 4; c++)
            {
                float lane = (FlameMesh.Hash(c, 3.4f) - 0.5f) * halfWidth * 1.4f;
                for (int i = 0; i < points.Length; i++)
                {
                    float u = i / (float)(points.Length - 1);
                    points[i] = origin + aim * length * Mathf.Min(1f, u * extend * 1.1f) + Side * (lane + (FlameMesh.Hash(c * 9 + i, 1.3f) - 0.5f) * 0.3f);
                }
                Stroke(points, points.Length, 0.12f, 0.03f, FlameMesh.Alpha(new Color(0.12f, 0.08f, 0.06f), 0.8f * fade), FlameMesh.Alpha(Dust, 0.3f * fade));
            }
            Area(0.2f * fade);
            Vector2 head = origin + aim * Mathf.Max(fist, length * extend - fist * 0.4f);
            SpeedLines(head, length * extend, fist, fade, 6);
            Glove(head, fist, Mathf.Clamp01(fade * 1.5f));
            if (t >= 0.2f)
            {
                Mesh.Disc(impact, halfWidth * (0.6f + 1.4f * EaseOut(after)), FlameMesh.Alpha(Color.white, 0.6f * fade), FlameMesh.Alpha(color, 0f), 32);
                Star(impact, fist * 2.2f, fade);
            }
        }

        /// <summary>
        /// Knuckle Sandwich's windup: the target area stretches out and brightens while the glove is drawn back and
        /// swells, with streaks of power rushing into it.
        /// </summary>
        private void DrawWindup(float t)
        {
            float reach = length * Mathf.Lerp(0.25f, 1f, t), flicker = 0.06f * Mathf.Sin(Age * 40f);
            Vector2 side = Side * halfWidth, end = origin + aim * reach;
            Color area = FlameMesh.Alpha(color, Mathf.Lerp(0.08f, 0.3f, t) + flicker);
            Mesh.Quad(origin - side, origin + side, end + side, end - side, FlameMesh.Alpha(color, 0.05f), FlameMesh.Alpha(color, 0.05f), area, area);
            Mesh.Bar(end - side, Side, halfWidth * 2f, 0.07f, FlameMesh.Alpha(Color.white, 0.3f + 0.5f * t), FlameMesh.Alpha(Color.white, 0.3f + 0.5f * t));
            float size = Mathf.Lerp(0.3f, 0.65f, t);
            Vector2 glove = origin - aim * Mathf.Lerp(0.2f, 0.55f, t) + Side * 0.15f;
            Mesh.Disc(glove, size * (1.3f + 0.6f * t), FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.5f), 0.45f * t), FlameMesh.Alpha(color, 0f), 24);
            for (int i = 0; i < 9; i++)
            {
                float seed = FlameMesh.Hash(i, 9.9f), cycle = Mathf.Repeat(Age * (2.5f + seed * 2f) + seed, 1f);
                Vector2 dir = FlameMesh.Polar(seed * Mathf.PI * 2f, 1f), p = glove + dir * (0.4f + 1.4f * (1f - cycle));
                Mesh.Bar(p, -dir, 0.35f * (1f - cycle) + 0.05f, 0.05f, FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(Color.white, 0.9f * cycle * t));
            }
            Glove(glove, size, 1f);
        }

        /// <summary>The hit area, kept faint so the attack still reads clearly.</summary>
        private void Area(float alpha)
        {
            Vector2 side = Side * halfWidth, end = origin + aim * length;
            Color c = FlameMesh.Alpha(color, alpha);
            Mesh.Quad(origin - side, origin + side, end + side, end - side, FlameMesh.Alpha(color, alpha * 0.4f), FlameMesh.Alpha(color, alpha * 0.4f), c, c);
            Mesh.Bar(end - side, Side, halfWidth * 2f, 0.05f, FlameMesh.Alpha(color, alpha * 3f), FlameMesh.Alpha(color, alpha * 3f));
        }

        private void SpeedLines(Vector2 head, float reach, float fist, float alpha, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float lane = (i - (count - 1) * 0.5f) / Mathf.Max(1, count - 1) * fist * 1.4f;
                float len = reach * (0.45f + 0.35f * FlameMesh.Hash(i, fist));
                Mesh.Bar(head - aim * fist * 0.4f + Side * lane, -aim, len, 0.045f, FlameMesh.Alpha(Color.white, 0.75f * alpha), FlameMesh.Alpha(color, 0f));
            }
        }

        /// <summary>A boxing glove seen from above, knuckles leading.</summary>
        private void Glove(Vector2 head, float size, float alpha)
        {
            Color glove = FlameMesh.Alpha(color, alpha), light = FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.45f), alpha);
            Vector2 side = Side;
            Mesh.Quad(head - aim * size * 1.1f - side * size * 0.38f, head - aim * size * 1.1f + side * size * 0.38f,
                head - aim * size * 0.6f + side * size * 0.42f, head - aim * size * 0.6f - side * size * 0.42f,
                FlameMesh.Alpha(Cuff, alpha), FlameMesh.Alpha(Cuff, alpha), FlameMesh.Alpha(Cuff, alpha), FlameMesh.Alpha(Cuff, alpha));
            Mesh.Disc(head - aim * size * 0.2f, size * 0.55f, light, glove, 20);
            for (int k = -1; k <= 1; k++)
                Mesh.Disc(head + aim * size * 0.18f + side * k * size * 0.28f, size * 0.22f, light, glove, 12);
            Mesh.Disc(head - aim * size * 0.25f + side * size * 0.48f, size * 0.2f, glove, glove, 10);
            Mesh.Diamond(head - aim * size * 0.1f - side * size * 0.2f, size * 0.12f, FlameMesh.Alpha(Color.white, 0.8f * alpha));
        }

        private void Impact(Vector2 at, float size, float t)
        {
            float fade = 1f - t;
            Mesh.Ring(at, size * (0.5f + t), 0.06f, FlameMesh.Alpha(Color.white, 0.8f * fade), 24);
            Star(at, size, fade);
        }

        /// <summary>A comic-book impact star.</summary>
        private void Star(Vector2 at, float size, float alpha)
        {
            if (alpha <= 0f) return;
            const int spikes = 8;
            Color core = FlameMesh.Alpha(Color.white, alpha), edge = FlameMesh.Alpha(Color.Lerp(color, AbilityCatalog.Gold, 0.5f), alpha * 0.8f);
            for (int i = 0; i < spikes; i++)
            {
                float a = i * Mathf.PI * 2f / spikes + FlameMesh.Hash(i, size) * 0.3f, reach = size * (0.7f + 0.5f * FlameMesh.Hash(i, 2.5f));
                Mesh.Triangle(at + FlameMesh.Polar(a - 0.25f, size * 0.25f), at + FlameMesh.Polar(a, reach), at + FlameMesh.Polar(a + 0.25f, size * 0.25f), core, edge, core);
            }
            Mesh.Disc(at, size * 0.28f, core, core, 12);
        }
    }
}

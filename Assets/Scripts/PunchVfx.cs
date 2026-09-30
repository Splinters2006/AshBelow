using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Brawler's punches, drawn as moving gloves rather than flat boxes. A jab shoots a glove out with speed lines
    /// and an impact star; a flurry keeps gloves hammering through the area in a blur; a heavy blow drives a huge
    /// glove through a cone of shockwaves that cracks the ground. An uppercut swings a glove up through its target in a
    /// crescent and bursts into a star with a column of launch lines; a thunder clap sends a wave rolling out from the
    /// Brawler through its cone; a dash leaves straight streaks of wind and dust along its path.
    /// </summary>
    public sealed class PunchVfx : MeshEffect
    {
        public enum Style : byte { Jab, Flurry, Heavy, Windup, Uppercut, Clap, Dash }
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
                case Style.Uppercut: DrawUppercut(t); break;
                case Style.Clap: DrawClap(t); break;
                case Style.Dash: DrawDash(t); break;
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

        /// <summary>The moment of an uppercut's impact, as a fraction of its effect.</summary>
        public const float UppercutImpact = 0.22f;

        /// <summary>
        /// An uppercut at <c>origin</c>: the glove scoops in low from behind the target and swings up through it
        /// in a bright crescent. At impact a flash, a big impact star, two shock rings and a column of lines shooting
        /// straight up sell the launch, with a ring of dust kicked off the floor.
        /// </summary>
        private void DrawUppercut(float t)
        {
            Vector2 at = origin, up = Vector2.up;
            float size = Mathf.Clamp(halfWidth, 0.35f, 0.8f);
            float swing = Mathf.Clamp01(t / UppercutImpact), after = Mathf.Clamp01((t - UppercutImpact) / (1f - UppercutImpact)), fade = 1f - after;
            // The swing: a curve from low and behind the target, up through it and on above.
            Vector2 low = at - aim * 0.55f + Vector2.down * 0.45f, high = at + up * 0.9f + aim * 0.1f;
            Vector2 Arc(float u) => Vector2.Lerp(Vector2.Lerp(low, at + aim * 0.15f, u), Vector2.Lerp(at + aim * 0.15f, high, u), u);
            float reached = EaseOut(swing) * (t < UppercutImpact ? 0.75f : 0.75f + 0.25f * EaseOut(after * 3f));
            for (int i = 0; i < points.Length; i++) points[i] = Arc(reached * i / (points.Length - 1));
            float trail = t < UppercutImpact ? 1f : fade;
            Stroke(points, points.Length, 0.04f, size * 0.9f, FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.6f), 0.85f * trail));
            if (t >= UppercutImpact)
            {
                // Dust thrown off the floor in a flattened ring.
                float dust = EaseOut(after);
                for (int i = 0; i < 14; i++)
                {
                    float a = i * Mathf.PI * 2f / 14f + FlameMesh.Hash(i, 7.7f) * 0.3f;
                    Vector2 p = at + Vector2.down * 0.3f + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.4f) * (0.3f + 1.2f * dust);
                    Mesh.Disc(p, 0.12f * (1f - dust * 0.5f), FlameMesh.Alpha(Dust, 0.5f * fade), FlameMesh.Alpha(Dust, 0f), 8);
                }
                // Launch lines shooting straight up out of the hit.
                for (int i = 0; i < 7; i++)
                {
                    float lane = (i - 3f) / 3f * size * 0.9f, seed = FlameMesh.Hash(i, 4.2f);
                    Vector2 bottom = at + Vector2.right * lane + up * (0.2f + 2.4f * EaseOut(after) * (0.6f + 0.4f * seed));
                    Mesh.Bar(bottom, up, 0.6f + seed * 0.7f, 0.05f, FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(Color.white, 0.8f * fade));
                }
                Mesh.Disc(at, size * (0.8f + 1.8f * EaseOut(after)), FlameMesh.Alpha(Color.white, 0.7f * fade * fade), FlameMesh.Alpha(color, 0f), 32);
                Mesh.Ring(at, size * (0.6f + 2.6f * EaseOut(after)), 0.12f * fade, FlameMesh.Alpha(Color.white, 0.9f * fade), 40);
                Mesh.Ring(at, size * (0.4f + 1.6f * EaseOut(after)), 0.08f * fade, FlameMesh.Alpha(color, 0.9f * fade), 40);
                Star(at + up * 0.1f, size * 2.4f * (1f + 0.2f * after), Mathf.Clamp01(fade * 1.6f));
            }
            // The glove itself, knuckles leading up the swing, gone soon after impact.
            Vector2 head = Arc(reached), heading = (Arc(Mathf.Min(1f, reached + 0.05f)) - Arc(Mathf.Max(0f, reached - 0.05f)));
            if (heading.sqrMagnitude > 0.0001f)
            {
                Vector2 saved = aim;
                aim = heading.normalized;
                Glove(head, size, t < UppercutImpact ? 1f : Mathf.Clamp01(1f - after * 2.5f));
                aim = saved;
            }
        }

        /// <summary>How far through a thunder clap's effect its wave front reaches the edge of the cone.</summary>
        public const float ClapTravel = 0.55f;

        /// <summary>
        /// Thunder Clap: a flash of two palms meeting at the Brawler, then a wave front that rolls out from her across
        /// the cone (reaching its edge at <see cref="ClapTravel"/>), trailed by fainter echoes and kicked-up dust.
        /// <c>length</c> is the cone's reach and <c>halfWidth</c> half its angle, in degrees.
        /// </summary>

        private void DrawClap(float t)
        {
            float half = halfWidth * Mathf.Deg2Rad, facing = Mathf.Atan2(aim.y, aim.x);
            float fade = 1f - Mathf.Clamp01((t - ClapTravel) / (1f - ClapTravel));
            // The clap: two big gloved hands swing in from either side and slam together over her, with a flash and
            // comic impact lines, then fall away as the wave rolls out.
            if (t < 0.45f) DrawClappingHands(t);
            // The wave front and two echoes behind it, each a thick arc across the cone.
            for (int w = 0; w < 3; w++)
            {
                float progress = Mathf.Clamp01((t - w * 0.07f) / ClapTravel);
                if (progress <= 0f) continue;
                float radius = length * EaseOut(progress), alpha = (w == 0 ? 1f : 0.45f / w) * (progress < 1f ? 1f : fade);
                if (alpha <= 0.01f) continue;
                float thickness = Mathf.Lerp(0.12f, 0.4f, progress) * (w == 0 ? 1f : 0.6f);
                const int Segments = 20;
                for (int i = 0; i < Segments; i++)
                {
                    float a0 = facing - half + 2f * half * i / Segments, a1 = facing - half + 2f * half * (i + 1) / Segments;
                    // Brightest in the middle of the cone, fading at its edges.
                    float edge0 = 1f - Mathf.Abs(i / (float)Segments * 2f - 1f), edge1 = 1f - Mathf.Abs((i + 1) / (float)Segments * 2f - 1f);
                    Color lead0 = FlameMesh.Alpha(Color.white, alpha * (0.3f + 0.7f * edge0)), lead1 = FlameMesh.Alpha(Color.white, alpha * (0.3f + 0.7f * edge1));
                    Color tail0 = FlameMesh.Alpha(color, 0f), tail1 = tail0;
                    Mesh.Quad(origin + FlameMesh.Polar(a0, Mathf.Max(0f, radius - thickness)), origin + FlameMesh.Polar(a0, radius),
                        origin + FlameMesh.Polar(a1, radius), origin + FlameMesh.Polar(a1, Mathf.Max(0f, radius - thickness)), tail0, lead0, lead1, tail1);
                    Mesh.Quad(origin + FlameMesh.Polar(a0, radius), origin + FlameMesh.Polar(a0, radius + 0.06f),
                        origin + FlameMesh.Polar(a1, radius + 0.06f), origin + FlameMesh.Polar(a1, radius), lead0, FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0f), lead1);
                }
                if (w != 0 || progress >= 1f) continue;
                // Dust kicked up just behind the front.
                for (int i = 0; i < 6; i++)
                {
                    float a = facing + (FlameMesh.Hash(i, 3.3f) * 2f - 1f) * half;
                    Mesh.Disc(origin + FlameMesh.Polar(a, radius * 0.85f), 0.1f + 0.1f * progress, FlameMesh.Alpha(Dust, 0.45f * (1f - progress)), FlameMesh.Alpha(Dust, 0f), 8);
                }
            }
        }

        /// <summary>
        /// Two open gloved hands seen from the front, palms facing, fingers up and thumbs raised: they swing in from the
        /// sides and meet over the Brawler, then a flash, a ring of impact lines and a shake as they part and fade.
        /// </summary>
        private void DrawClappingHands(float t)
        {
            const float Meet = 0.12f;
            Vector2 center = origin + Vector2.up * 0.75f;
            float swing = EaseOut(Mathf.Clamp01(t / Meet)), after = Mathf.Clamp01((t - Meet) / (0.45f - Meet));
            float alpha = t < Meet ? Mathf.Clamp01(t / 0.04f) : 1f - after;
            // Wide apart and tilted back, then together; they rebound a touch after the smack.
            float gap = Mathf.Lerp(0.75f, 0.1f, swing) + (t >= Meet ? 0.1f * Mathf.Sin(after * Mathf.PI) : 0f);
            float tilt = Mathf.Lerp(35f, 0f, swing);
            var glove = BrawlerAttack.Glove;
            for (int side = -1; side <= 1; side += 2) Hand(center + Vector2.right * side * gap, side, tilt, glove, alpha);
            if (t < Meet) return;
            float pop = EaseOut(after), fade = 1f - after;
            Mesh.Disc(center, 0.25f + 0.6f * pop, FlameMesh.Alpha(Color.white, 0.9f * fade * fade), FlameMesh.Alpha(color, 0f), 24);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5f + 0.15f;
                Vector2 dir = FlameMesh.Polar(a, 1f);
                Mesh.Bar(center + dir * (0.3f + 0.5f * pop), dir, 0.25f * fade + 0.05f, 0.06f * fade, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(color, 0f));
            }
        }

        /// <summary>One gloved hand, palm toward the middle (<paramref name="side"/> -1 is the left hand), tipped outward by <paramref name="tilt"/> degrees.</summary>
        private void Hand(Vector2 wrist, int side, float tilt, Color glove, float alpha)
        {
            if (alpha <= 0f) return;
            // Tipped outward: the left hand leans left, the right hand right. "Across" points toward the other hand.
            var lean = Quaternion.Euler(0f, 0f, -side * tilt);
            Vector2 up = lean * Vector2.up, across = -side * (Vector2)(lean * Vector2.right);
            Color c = FlameMesh.Alpha(glove, alpha), light = FlameMesh.Alpha(Color.Lerp(glove, Color.white, 0.4f), alpha), cuff = FlameMesh.Alpha(Cuff, alpha);
            // Cuff at the wrist, then the palm.
            Mesh.Bar(wrist - up * 0.12f, up, 0.1f, 0.2f, cuff, cuff);
            Vector2 palm = wrist + up * 0.1f;
            Mesh.Disc(palm, 0.13f, light, c, 16);
            // Four fingers fanning up, and a thumb raised toward the other hand.
            for (int f = 0; f < 4; f++)
            {
                float spread = (f - 1.5f) * 0.045f;
                Vector2 root = palm + up * 0.08f + across * spread * -1f;
                Vector2 dir = (up + across * -spread * 0.8f).normalized;
                Mesh.Bar(root, dir, 0.17f - Mathf.Abs(f - 1.5f) * 0.025f, 0.055f, c, light);
                Mesh.Disc(root + dir * (0.17f - Mathf.Abs(f - 1.5f) * 0.025f), 0.028f, light, light, 8);
            }
            Vector2 thumb = palm + across * 0.08f;
            Mesh.Bar(thumb, (across + up * 0.8f).normalized, 0.13f, 0.06f, c, light);
        }

        /// <summary>A dash: straight streaks of wind along the path from <c>origin</c>, fading from the start, and dust at take-off.</summary>
        private void DrawDash(float t)
        {
            float fade = 1f - t;
            Vector2 end = origin + aim * length;
            Mesh.Quad(origin - Side * halfWidth * 0.3f, origin + Side * halfWidth * 0.3f, end + Side * halfWidth, end - Side * halfWidth,
                FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0.18f * fade), FlameMesh.Alpha(color, 0.18f * fade));
            for (int i = 0; i < 5; i++)
            {
                float lane = (i - 2f) / 2f * halfWidth * 0.9f, seed = FlameMesh.Hash(i, length);
                float start = Mathf.Clamp01(t * 1.3f + seed * 0.2f);
                float len = length * (1f - start) * (0.7f + 0.3f * seed);
                if (len <= 0.02f) continue;
                Mesh.Bar(end - aim * (0.2f + seed * 0.3f) + Side * lane, -aim, len, 0.05f, FlameMesh.Alpha(Color.white, 0.8f * fade), FlameMesh.Alpha(color, 0f));
            }
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Atan2(-aim.y, -aim.x) + (FlameMesh.Hash(i, 5.5f) - 0.5f) * 1.6f;
                Mesh.Disc(origin + FlameMesh.Polar(a, 0.2f + 0.6f * EaseOut(t)), 0.12f + 0.1f * t, FlameMesh.Alpha(Dust, 0.5f * fade), FlameMesh.Alpha(Dust, 0f), 8);
            }
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

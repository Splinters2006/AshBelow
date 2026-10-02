using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Bloodscent: the Samurai breathes the scent in (<see cref="Sniff"/>), and a twisting thread of it runs from her
    /// to every bleeding enemy (<see cref="Play"/>). As the thread arrives, the blood the enemy has spilt is drawn
    /// back up off the floor into it, and then the wound is torn open afresh: a crossed gash, a ring of blood and
    /// new drops running down. Purely visual.
    /// </summary>
    public sealed class BloodscentVfx : MeshEffect
    {
        private const float Arrive = 0.4f, TrailLength = 0.45f;
        private const int TrailPoints = 14;
        private static readonly Vector2[] points = new Vector2[TrailPoints];
        private Vector2 from, at;
        private float radius, seed;
        private bool sniff;

        /// <param name="radius">The enemy's hit radius: the reopened wound scales with it.</param>
        public static void Play(Transform root, Vector2 from, Vector2 at, float radius)
        {
            var effect = Spawn<BloodscentVfx>(root, 0.85f, 10);
            if (effect == null) return;
            effect.from = from;
            effect.at = at;
            effect.radius = Mathf.Max(0.3f, radius);
            effect.seed = at.x * 3.7f + at.y * 9.1f;
            effect.Redraw();
        }

        /// <summary>The breath she takes as she casts it: the scent is pulled in to her from all round.</summary>
        public static void Sniff(Transform root, Vector2 hero)
        {
            var effect = Spawn<BloodscentVfx>(root, 0.5f, 10);
            if (effect == null) return;
            effect.at = hero;
            effect.sniff = true;
            effect.seed = hero.x * 5.3f + hero.y * 2.9f;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            Color blood = SamuraiAttack.Blood, dark = new Color(blood.r * 0.35f, 0f, blood.b * 0.25f);
            if (sniff) { DrawSniff(t, blood); return; }
            DrawThread(t, blood);
            DrawReturning(t, blood, dark);
            if (t >= Arrive) DrawWound((t - Arrive) / (1f - Arrive), blood, dark);
        }

        private void DrawSniff(float t, Color blood)
        {
            float rest = 1f - t, pull = EaseOut(t);
            Vector2 face = at + Vector2.up * 0.2f;
            Mesh.Disc(face, 0.9f * rest + 0.2f, FlameMesh.Alpha(blood, 0.35f * rest), FlameMesh.Alpha(blood, 0f), 24);
            Mesh.Ring(face, 0.25f + 1.5f * (1f - pull), 0.1f * rest + 0.01f, FlameMesh.Alpha(blood, 0.8f * Mathf.Sin(t * Mathf.PI)), 40);
            for (int i = 0; i < 12; i++)
            {
                // Wisps that curl as they are drawn in.
                float angle = (i + FlameMesh.Hash(i, seed)) / 12f * Mathf.PI * 2f + (1f - pull) * 0.9f;
                float far = Mathf.Lerp(1.9f + 0.6f * FlameMesh.Hash(i, seed + 2.2f), 0.25f, pull);
                Vector2 dir = FlameMesh.Polar(angle, 1f);
                Mesh.Bar(face + dir * (far + 0.5f), -dir, 0.5f, 0.06f, FlameMesh.Alpha(blood, 0f), FlameMesh.Alpha(i % 4 == 0 ? Color.white : blood, 0.9f * rest));
            }
        }

        /// <summary>Two strands of scent winding round each other from her to the enemy, head first, thinning to nothing behind.</summary>
        private void DrawThread(float t, Color blood)
        {
            float head = EaseOut(t / Arrive), tail = Mathf.Clamp01(t / Arrive * 1.5f - TrailLength);
            if (tail >= head) return;
            Vector2 delta = at - from;
            if (delta.sqrMagnitude < 0.0001f) return;
            Vector2 across = Vector2.Perpendicular(delta.normalized);
            float waves = Mathf.Max(1f, delta.magnitude * 0.6f);
            for (int strand = -1; strand <= 1; strand += 2)
            {
                for (int i = 0; i < TrailPoints; i++)
                {
                    float u = Mathf.Lerp(tail, head, i / (float)(TrailPoints - 1));
                    // No sway at either end, so the thread leaves her and meets the wound cleanly.
                    float sway = Mathf.Sin(u * waves * Mathf.PI * 2f + seed + t * 9f) * 0.28f * Mathf.Sin(u * Mathf.PI);
                    points[i] = from + delta * u + across * sway * strand;
                }
                Stroke(points, TrailPoints, 0.02f, 0.16f, FlameMesh.Alpha(blood, 0f), FlameMesh.Alpha(blood, 0.9f));
                if (strand > 0) Stroke(points, TrailPoints, 0.01f, 0.05f, FlameMesh.Alpha(Color.white, 0f), FlameMesh.Alpha(Color.white, 0.8f));
            }
            Mesh.Disc(points[TrailPoints - 1], 0.14f, FlameMesh.Alpha(Color.white, 0.9f), FlameMesh.Alpha(blood, 0f), 12);
        }

        /// <summary>The blood it had already lost: blotches on the floor that shrink as drops lift off them, back up into the wound.</summary>
        private void DrawReturning(float t, Color blood, Color dark)
        {
            float pull = Mathf.Clamp01(t / (Arrive + 0.1f));
            if (pull >= 1f) return;
            float rest = 1f - pull, rise = pull * pull;
            for (int i = 0; i < 9; i++)
            {
                float angle = (i + FlameMesh.Hash(i, seed + 1.3f)) / 9f * Mathf.PI * 2f, far = radius * (1.1f + 1.3f * FlameMesh.Hash(i, seed + 4.2f));
                Vector2 pool = at + new Vector2(Mathf.Cos(angle) * far, Mathf.Sin(angle) * far * 0.55f - radius * 0.5f);
                float size = radius * (0.2f + 0.2f * FlameMesh.Hash(i, seed + 6.6f)) * rest;
                Mesh.Ellipse(pool, size * 1.3f, size * 0.7f, FlameMesh.Alpha(dark, 0.7f * rest), FlameMesh.Alpha(dark, 0.4f * rest), 12);
                Vector2 drop = Vector2.Lerp(pool, at, rise) + Vector2.up * Mathf.Sin(pull * Mathf.PI) * 0.35f;
                Mesh.Bar(drop, (pool - at).normalized, 0.28f * pull, 0.07f, FlameMesh.Alpha(blood, 0.9f), FlameMesh.Alpha(blood, 0f));
                Mesh.Disc(drop, 0.06f, FlameMesh.Alpha(blood, 0.95f), FlameMesh.Alpha(blood, 0.95f), 8);
            }
        }

        /// <summary>The wound torn open again: a crossed gash, a ring thrown off it and fresh blood running down.</summary>
        private void DrawWound(float t, Color blood, Color dark)
        {
            float fade = 1f - t, open = EaseOut(t / 0.25f);
            float flash = Mathf.Clamp01(1f - t / 0.2f);
            if (flash > 0f) Mesh.Disc(at, radius * (0.7f + 0.8f * (1f - flash)), FlameMesh.Alpha(Color.white, 0.8f * flash), FlameMesh.Alpha(blood, 0f), 24);
            Mesh.Ring(at, radius * (0.6f + 1.3f * EaseOut(t)), 0.12f * fade + 0.01f, FlameMesh.Alpha(blood, fade), 40);
            for (int cut = 0; cut < 2; cut++)
            {
                Vector2 dir = FlameMesh.Polar((cut == 0 ? 0.9f : 2.3f) + (FlameMesh.Hash(cut, seed) - 0.5f) * 0.5f, 1f), across = Vector2.Perpendicular(dir);
                float half = radius * 1.25f * open, width = radius * 0.22f * (0.4f + 0.6f * fade);
                // A lens: widest in the middle, a point at each end, with the pale flesh of the cut down its centre.
                Mesh.Quad(at - dir * half, at + across * width, at + dir * half, at - across * width,
                    FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(dark, fade), FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(dark, fade));
                Mesh.Quad(at - dir * half * 0.85f, at + across * width * 0.3f, at + dir * half * 0.85f, at - across * width * 0.3f,
                    FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(Color.white, fade));
            }
            // Fresh drops, flicked out of the cut and then running down.
            for (int i = 0; i < 8; i++)
            {
                float side = (FlameMesh.Hash(i, seed + 8.1f) - 0.5f) * 2f, speed = 0.6f + 0.8f * FlameMesh.Hash(i, seed + 5.5f);
                Vector2 drop = at + new Vector2(side * radius * (0.4f + 1.1f * t), radius * 0.2f + speed * (0.9f * t - 2.2f * t * t));
                float size = 0.05f + 0.04f * FlameMesh.Hash(i, seed + 3.3f);
                Mesh.Bar(drop, Vector2.up, size * 3f, size, FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(blood, 0f));
                Mesh.Disc(drop, size, FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(blood, fade), 8);
            }
        }
    }
}

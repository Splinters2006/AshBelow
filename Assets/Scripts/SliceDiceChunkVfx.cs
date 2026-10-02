using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Slice Dice Chunk's three slashes, each drawn over the cone it hits and never past its reach. Slice is one
    /// razor line ruled across the cone that parts and bleeds. Dice is a lattice of crossing cuts whose cells tumble
    /// loose as little dice. Chunk is a cleave that floods the cone with blood, cracks the ground and throws chunks.
    /// Purely visual.
    /// </summary>
    public sealed class SliceDiceChunkVfx : MeshEffect
    {
        private static readonly float[] Durations = { 0.36f, 0.5f, 0.6f };
        private int stage;
        private Vector2 origin, aim, side;
        private float reach, cone, heading;

        public static void Play(Transform root, Vector2 origin, Vector2 aim, float reach, float coneAngle, int stage)
        {
            if (aim.sqrMagnitude < 0.0001f) return;
            stage = Mathf.Clamp(stage, 0, 2);
            var effect = Spawn<SliceDiceChunkVfx>(root, Durations[stage], 10);
            if (effect == null) return;
            effect.stage = stage;
            effect.origin = origin;
            effect.aim = aim.normalized;
            effect.side = Vector2.Perpendicular(effect.aim);
            effect.reach = Mathf.Max(0.5f, reach);
            effect.cone = Mathf.Clamp(coneAngle, 10f, 360f) * Mathf.Deg2Rad;
            effect.heading = Mathf.Atan2(effect.aim.y, effect.aim.x);
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            if (stage == 0) DrawSlice(t);
            else if (stage == 1) DrawDice(t);
            else DrawChunk(t);
        }

        /// <summary>A point in the cone: <paramref name="across"/> runs -0.5..0.5 from one edge to the other, <paramref name="out"/> 0..1 to the reach.</summary>
        private Vector2 At(float across, float @out) => origin + FlameMesh.Polar(heading + cone * across, reach * @out);

        /// <summary>A cut that snaps from <paramref name="from"/> to <paramref name="to"/> as <paramref name="snap"/> runs 0..1: white over a bloody glow, with pointed ends.</summary>
        private void Cutline(Vector2 from, Vector2 to, float snap, float half, float alpha)
        {
            Vector2 head = Vector2.Lerp(from, to, EaseOut(snap)), middle = (from + head) * 0.5f;
            if ((head - from).sqrMagnitude < 0.0001f) return;
            Vector2 across = Vector2.Perpendicular((head - from).normalized);
            Color blood = FlameMesh.Alpha(SamuraiAttack.Blood, 0.8f * alpha), clear = FlameMesh.Alpha(SamuraiAttack.Blood, 0f), white = FlameMesh.Alpha(Color.white, alpha);
            Mesh.Triangle(from, middle + across * half * 3.5f, middle - across * half * 3.5f, clear, blood, blood);
            Mesh.Triangle(head, middle - across * half * 3.5f, middle + across * half * 3.5f, clear, blood, blood);
            Mesh.Triangle(from, middle + across * half, middle - across * half, white, white, white);
            Mesh.Triangle(head, middle - across * half, middle + across * half, white, white, white);
        }

        private void DrawSlice(float t)
        {
            // One line ruled from edge to edge of the cone. Its ends sit on the cone's rim, so the whole chord is inside the cut.
            Vector2 from = At(-0.5f, 0.92f), to = At(0.5f, 0.92f), along = (to - from).normalized;
            float length = Vector2.Distance(from, to);
            Color blood = SamuraiAttack.Blood;
            if (t < 0.3f)
            {
                float u = t / 0.3f;
                Cutline(from, to, u / 0.35f, Mathf.Lerp(0.12f, 0.035f, u), 1f);
                Vector2 head = Vector2.Lerp(from, to, EaseOut(u / 0.35f));
                Color white = FlameMesh.Alpha(Color.white, 1f - u), clear = FlameMesh.Alpha(Color.white, 0f);
                Mesh.Bar(head, aim, 0.5f * (1f - u), 0.06f, white, clear);
                Mesh.Bar(head, -aim, 0.5f * (1f - u), 0.06f, white, clear);
                Mesh.Diamond(head, 0.16f * (1f - u), white);
                return;
            }
            // Then everything this side of the line slips: the near half slides along the cut and the gap bleeds.
            float part = (t - 0.3f) / 0.7f, fade = 1f - part, open = EaseOut(part);
            Vector2 slip = along * 0.3f * open - aim * 0.1f * open;
            Mesh.Triangle(origin + slip, from + slip, to + slip, FlameMesh.Alpha(SamuraiAttack.Steel, 0.02f * fade), FlameMesh.Alpha(SamuraiAttack.Steel, 0.22f * fade), FlameMesh.Alpha(SamuraiAttack.Steel, 0.22f * fade));
            Mesh.Bar(from + slip, along, length, 0.05f * fade + 0.01f, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade));
            Mesh.Bar(from, along, length, 0.05f * fade + 0.01f, FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(blood, fade));
            for (int i = 0; i < 9; i++)
            {
                float where = FlameMesh.Hash(i, length + 2.3f), drop = (0.15f + 0.45f * FlameMesh.Hash(i, 7.7f)) * open;
                Mesh.Bar(from + along * length * where, -aim, drop, 0.045f, FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(blood, 0f));
            }
        }

        private void DrawDice(float t)
        {
            // Two sets of three cuts crossing at right angles, diagonal to her aim: a lattice with four cells.
            Vector2 d1 = (aim + side).normalized, d2 = (aim - side).normalized, center = origin + aim * reach * 0.55f;
            float half = reach * 0.38f, spacing = reach * 0.22f;
            Color blood = SamuraiAttack.Blood;
            float fade = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
            for (int i = 0; i < 6; i++)
            {
                // The cuts land one after another, alternating direction, in the first 40% of the effect.
                float born = i * 0.065f;
                if (t < born) continue;
                Vector2 along = i % 2 == 0 ? d1 : d2, across = i % 2 == 0 ? d2 : d1;
                Vector2 middle = center + across * spacing * (i / 2 - 1), from = middle - along * half, to = middle + along * half;
                float age = (t - born) / 0.12f;
                if (i % 4 >= 2) { var swap = from; from = to; to = swap; }
                Cutline(from, to, age, Mathf.Lerp(0.09f, 0.03f, Mathf.Clamp01(age)), fade);
            }
            // Once the last cut lands, the cells come loose: four dice tumbling outward, each with its pip.
            if (t < 0.4f) return;
            float loose = (t - 0.4f) / 0.6f, drift = EaseOut(loose);
            for (int i = 0; i < 4; i++)
            {
                Vector2 cell = d1 * spacing * (i % 2 - 0.5f) + d2 * spacing * (i / 2 - 0.5f);
                Vector2 at = center + cell * (1f + 0.55f * drift);
                float spin = (FlameMesh.Hash(i, 5.1f) - 0.5f) * 3f * drift, size = spacing * 0.36f * (1f - 0.4f * loose);
                Vector2 x = (Vector2)(Quaternion.Euler(0f, 0f, spin * Mathf.Rad2Deg) * d1) * size, y = Vector2.Perpendicular(x);
                Color rim = FlameMesh.Alpha(blood, 1f - loose), face = FlameMesh.Alpha(SamuraiAttack.Steel, 0.9f * (1f - loose));
                Mesh.Quad(at - x * 1.25f - y * 1.25f, at - x * 1.25f + y * 1.25f, at + x * 1.25f + y * 1.25f, at + x * 1.25f - y * 1.25f, rim, rim, rim, rim);
                Mesh.Quad(at - x - y, at - x + y, at + x + y, at + x - y, face, face, face, face);
                Mesh.Diamond(at, size * 0.4f, rim);
            }
        }

        private void DrawChunk(float t)
        {
            Color blood = SamuraiAttack.Blood, dark = new Color(blood.r * 0.35f, 0f, blood.b * 0.25f);
            float fade = 1f - t, slam = EaseOut(t / 0.22f);
            const int Segments = 28;
            // The whole cone floods with blood as the blade comes down, then drains.
            Color flood = FlameMesh.Alpha(blood, 0.5f * fade * fade), core = FlameMesh.Alpha(dark, 0.25f * fade * fade);
            for (int i = 0; i < Segments; i++)
                Mesh.Triangle(origin, At(-0.5f + i / (float)Segments, slam), At(-0.5f + (i + 1) / (float)Segments, slam), core, flood, flood);
            // The front of the cleave: a heavy band that rolls out to the reach and thins there, white along its leading edge.
            float front = Mathf.Lerp(0.45f, 1f, slam), thick = Mathf.Lerp(0.34f, 0.05f, t);
            for (int i = 0; i < Segments; i++)
            {
                float a0 = -0.5f + i / (float)Segments, a1 = -0.5f + (i + 1) / (float)Segments;
                // Fattest dead ahead, tapering to nothing at the cone's edges.
                float w0 = thick * Mathf.Sin((a0 + 0.5f) * Mathf.PI), w1 = thick * Mathf.Sin((a1 + 0.5f) * Mathf.PI);
                Mesh.Quad(At(a0, front - w0), At(a0, front), At(a1, front), At(a1, front - w1),
                    FlameMesh.Alpha(blood, 0f), FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(blood, fade), FlameMesh.Alpha(blood, 0f));
                Mesh.Quad(At(a0, front - w0 * 0.25f), At(a0, front), At(a1, front), At(a1, front - w1 * 0.25f),
                    FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade));
            }
            // The blade's own line, straight down the middle, burning white for the first beat.
            float flash = Mathf.Clamp01(1f - t / 0.35f);
            if (flash > 0f)
            {
                Vector2 tip = origin + aim * reach * slam;
                Mesh.Triangle(origin - side * 0.22f * flash, tip, origin + side * 0.22f * flash, FlameMesh.Alpha(blood, 0.3f * flash), FlameMesh.Alpha(blood, flash), FlameMesh.Alpha(blood, 0.3f * flash));
                Mesh.Triangle(origin - side * 0.08f * flash, tip, origin + side * 0.08f * flash, FlameMesh.Alpha(Color.white, 0.5f * flash), FlameMesh.Alpha(Color.white, flash), FlameMesh.Alpha(Color.white, 0.5f * flash));
            }
            // Cracks split the ground outward from where it landed: each a jagged run of three strokes.
            for (int i = 0; i < 7; i++)
            {
                float across = (i + 0.5f) / 7f - 0.5f + (FlameMesh.Hash(i, 3.3f) - 0.5f) * 0.06f, length = (0.55f + 0.4f * FlameMesh.Hash(i, 8.8f)) * slam;
                Vector2 previous = At(across, 0.18f);
                for (int step = 1; step <= 3; step++)
                {
                    float jag = (FlameMesh.Hash(i * 3 + step, 1.9f) - 0.5f) * 0.07f * (step < 3 ? 1f : 0f);
                    Vector2 next = At(Mathf.Clamp(across + jag, -0.5f, 0.5f), 0.18f + (length - 0.18f) * step / 3f);
                    if ((next - previous).sqrMagnitude > 0.0001f)
                        Mesh.Bar(previous, (next - previous).normalized, Vector2.Distance(previous, next), 0.07f * (1f - step * 0.2f) * (0.4f + 0.6f * fade),
                            FlameMesh.Alpha(dark, 0.9f * fade), FlameMesh.Alpha(dark, 0.9f * fade));
                    previous = next;
                }
            }
            // Chunks torn loose and thrown toward the rim, tumbling and shrinking as they go.
            for (int i = 0; i < 9; i++)
            {
                float across = FlameMesh.Hash(i, 6.6f) - 0.5f, far = Mathf.Lerp(0.3f, 0.55f + 0.4f * FlameMesh.Hash(i, 2.4f), EaseOut(t));
                Vector2 at = At(across * 0.9f, far);
                float size = (0.1f + 0.12f * FlameMesh.Hash(i, 9.2f)) * (1f - 0.6f * t), spin = FlameMesh.Hash(i, 4.1f) * 6f + t * (4f + 6f * FlameMesh.Hash(i, 1.1f));
                Color chunk = FlameMesh.Alpha(i % 3 == 0 ? blood : dark, fade);
                Mesh.Triangle(at + FlameMesh.Polar(spin, size), at + FlameMesh.Polar(spin + 2.2f, size), at + FlameMesh.Polar(spin + 4.1f, size * 1.3f), chunk, chunk, chunk);
            }
        }
    }
}

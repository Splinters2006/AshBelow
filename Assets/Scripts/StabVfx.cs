using UnityEngine;

namespace Slopgame
{
    /// <summary>The Assassin's stab: a dagger thrusts straight out to full reach with speed lines, flashes at the tip and snaps back.</summary>
    public sealed class StabVfx : MeshEffect
    {
        private Vector2 origin, aim;
        private float reach;
        private Color color;

        public static void Play(Transform root, Vector2 origin, Vector2 aim, float reach, Color color)
        {
            if (aim.sqrMagnitude < 0.0001f) return;
            var effect = Spawn<StabVfx>(root, 0.2f);
            if (effect == null) return;
            effect.origin = origin;
            effect.aim = aim.normalized;
            effect.reach = Mathf.Max(0.5f, reach);
            effect.color = color;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            // Snap out in the first 35%, then ease back a little while fading.
            float extend = t < 0.35f ? EaseOut(t / 0.35f) : 1f - 0.25f * (t - 0.35f) / 0.65f;
            float fade = t < 0.35f ? 1f : 1f - (t - 0.35f) / 0.65f;
            Vector2 side = Vector2.Perpendicular(aim);
            Vector2 tip = origin + aim * reach * extend, hilt = tip - aim * Mathf.Min(0.75f, reach * 0.4f);
            Vector2 start = origin + aim * 0.25f;
            // The thrust's wake: a narrow glowing lane behind the blade.
            Mesh.Quad(start - side * 0.04f, start + side * 0.04f, tip + side * 0.16f, tip - side * 0.16f,
                FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0.45f * fade), FlameMesh.Alpha(color, 0.45f * fade));
            for (int i = -1; i <= 1; i += 2)
            {
                float seed = FlameMesh.Hash(i, reach);
                Vector2 lineEnd = tip - aim * (0.25f + seed * 0.2f) + side * i * (0.2f + seed * 0.08f);
                Mesh.Bar(lineEnd, -aim, reach * extend * 0.55f, 0.035f, FlameMesh.Alpha(Color.white, 0.7f * fade), FlameMesh.Alpha(color, 0f));
            }
            // The dagger itself: a pale blade, a violet guard and a dark grip.
            Color steel = new Color(0.9f, 0.9f, 1f, fade);
            Mesh.Triangle(hilt - side * 0.07f, tip, hilt + side * 0.07f, steel, FlameMesh.Alpha(Color.white, fade), steel);
            Mesh.Bar(hilt - side * 0.15f, side, 0.3f, 0.06f, FlameMesh.Alpha(color, fade), FlameMesh.Alpha(color, fade));
            Mesh.Bar(hilt, -aim, 0.22f, 0.07f, FlameMesh.Alpha(ShadowstepVfx.Smoke, fade), FlameMesh.Alpha(ShadowstepVfx.Smoke, fade));
            // A glint as the point strikes home.
            float glint = Mathf.Clamp01(1f - Mathf.Abs(t - 0.35f) / 0.2f);
            if (glint > 0f)
            {
                Mesh.Bar(tip - aim * 0.25f * glint, aim, 0.5f * glint, 0.05f, FlameMesh.Alpha(Color.white, glint), FlameMesh.Alpha(Color.white, glint));
                Mesh.Bar(tip - side * 0.2f * glint, side, 0.4f * glint, 0.05f, FlameMesh.Alpha(Color.white, glint), FlameMesh.Alpha(Color.white, glint));
            }
        }
    }
}

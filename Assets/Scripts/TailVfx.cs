using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Demoness's tail: a black, violet-rimmed tail with a spade tip that curls up from behind her. A stab thrusts
    /// it straight out and snaps back; a sweep swings it across a cone behind a crescent wake.
    /// </summary>
    public sealed class TailVfx : MeshEffect
    {
        private const int Points = 14;
        private readonly Vector2[] points = new Vector2[Points];
        private Vector2 origin, aim;
        private float reach, cone;
        private Color color;
        private bool sweep;

        public static void Stab(Transform root, Vector2 origin, Vector2 aim, float reach, Color color)
            => Create(root, origin, aim, reach, 0f, color, false, 0.22f);

        public static void Sweep(Transform root, Vector2 origin, Vector2 aim, float reach, float cone, Color color)
        {
            if (Create(root, origin, aim, reach, cone, color, true, 0.3f)) HeroVfx.Slash(root, origin, aim, reach, cone, color, 0.26f);
        }

        private static bool Create(Transform root, Vector2 origin, Vector2 aim, float reach, float cone, Color color, bool sweep, float duration)
        {
            if (aim.sqrMagnitude < 0.0001f) return false;
            var effect = Spawn<TailVfx>(root, duration);
            if (effect == null) return false;
            effect.origin = origin;
            effect.aim = aim.normalized;
            effect.reach = Mathf.Max(0.5f, reach);
            effect.cone = cone;
            effect.color = color;
            effect.sweep = sweep;
            effect.Redraw();
            return true;
        }

        protected override void Draw(float t)
        {
            float fade = t < 0.45f ? 1f : 1f - (t - 0.45f) / 0.55f;
            if (sweep)
            {
                // Swings from one edge of the cone to the other in the first 45%, then fades out at the far edge.
                // Same direction as HeroVfx.Slash (counter-clockwise) so the tail leads its crescent wake.
                float swing = EaseOut(t / 0.45f);
                float angle = Mathf.Atan2(aim.y, aim.x) + cone * Mathf.Deg2Rad * (swing - 0.5f);
                DrawTail(FlameMesh.Polar(angle, 1f), reach, fade, 0f);
                return;
            }
            float extend = t < 0.35f ? EaseOut(t / 0.35f) : 1f - 0.3f * (t - 0.35f) / 0.65f;
            DrawTail(aim, Mathf.Lerp(0.4f, reach, extend), fade, Mathf.Clamp01(1f - Mathf.Abs(t - 0.35f) / 0.2f));
        }

        private void DrawTail(Vector2 direction, float length, float fade, float glint)
        {
            Vector2 side = Vector2.Perpendicular(direction);
            // From behind her hip, arcing up over the shoulder and down onto the target.
            Vector2 start = origin - direction * 0.25f - side * 0.2f;
            Vector2 tip = origin + direction * length;
            Vector2 bend = origin + direction * length * 0.35f + side * (0.35f + length * 0.15f);
            for (int i = 0; i < Points; i++)
            {
                float u = i / (float)(Points - 1);
                points[i] = (1 - u) * (1 - u) * start + 2 * (1 - u) * u * bend + u * u * tip;
            }
            Stroke(points, Points, 0.28f, 0.14f, FlameMesh.Alpha(color, 0.35f * fade), FlameMesh.Alpha(color, 0.6f * fade));
            Stroke(points, Points, 0.15f, 0.07f, FlameMesh.Alpha(DemonessAttack.Abyss, fade), FlameMesh.Alpha(DemonessAttack.Abyss, fade));
            // The spade tip, pointing along the last stretch of tail.
            Vector2 end = (tip - points[Points - 2]).normalized, normal = Vector2.Perpendicular(end);
            Vector2 back = tip - end * 0.3f;
            Color dark = FlameMesh.Alpha(DemonessAttack.Abyss, fade), rim = FlameMesh.Alpha(color, fade);
            Mesh.Triangle(back + normal * 0.2f, tip + end * 0.14f, back - normal * 0.2f, rim, FlameMesh.Alpha(DemonessAttack.Pale, fade), rim);
            Mesh.Triangle(back + normal * 0.14f, tip + end * 0.04f, back - normal * 0.14f, dark, dark, dark);
            Mesh.Triangle(back + normal * 0.2f, back - end * 0.1f, back - normal * 0.2f, rim, dark, rim);
            if (glint <= 0f) return;
            Color white = FlameMesh.Alpha(Color.white, glint * fade);
            Mesh.Bar(tip - end * 0.25f * glint, end, 0.5f * glint, 0.05f, white, white);
            Mesh.Bar(tip - normal * 0.2f * glint, normal, 0.4f * glint, 0.05f, white, white);
        }
    }
}

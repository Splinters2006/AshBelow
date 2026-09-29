using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// HEEEELP: a portal rips open in the air above the target while a violet warning circle fills on the ground,
    /// red eyes glare out of it, then the Demoness's giant pet shoves a red-furred, clawed paw through and slams it down. Purely visual; she applies the damage.
    /// </summary>
    public sealed class DemonPawVfx : MonoBehaviour
    {
        private const float SlamTime = 0.5f, PortalHeight = 2.6f;
        /// <summary>Where the paw lands; the caster keeps it on the target until the paw commits.</summary>
        public Vector2 Center { get; set; }
        private FlameMesh mesh;
        private float radius, windup, age;
        private static readonly Color Blood = new Color(0.62f, 0.04f, 0.06f);
        private static readonly Color BloodDark = new Color(0.22f, 0.01f, 0.03f);
        private static readonly Color Hellfire = new Color(1f, 0.16f, 0.08f);
        private static readonly Color ClawBlack = new Color(0.05f, 0.01f, 0.02f);

        public static DemonPawVfx Play(Transform root, Vector2 center, float radius, float windup)
        {
            if (root == null) return null;
            var effect = new GameObject("Demon paw").AddComponent<DemonPawVfx>();
            effect.transform.SetParent(root, false);
            effect.Center = center;
            effect.radius = radius;
            effect.windup = Mathf.Max(0.05f, windup);
            effect.mesh = new FlameMesh(effect.gameObject, 10);
            return effect;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= windup + SlamTime) { Destroy(gameObject); return; }
            mesh.Begin();
            float size = radius * 0.62f;
            if (age < windup)
            {
                float t = age / windup;
                DrawWarning(t);
                float open = EaseOut(Mathf.Clamp01(t / 0.4f));
                DrawPortal(Center + Vector2.up * PortalHeight, open, 1f);
                DrawEyes(Center + Vector2.up * PortalHeight, open * Mathf.Clamp01((0.4f - t) * 8f));
                // The paw pushes through the portal, then plunges.
                float drop = Mathf.Clamp01((t - 0.35f) / 0.65f);
                drop *= drop;
                if (t > 0.35f) Paw(Center + Vector2.up * Mathf.Lerp(PortalHeight, size * 0.35f, drop), size, Mathf.Clamp01((t - 0.35f) * 6f));
            }
            else
            {
                float t = (age - windup) / SlamTime, fade = 1f - t;
                DrawPortal(Center + Vector2.up * PortalHeight, 1f - EaseOut(t), fade);
                mesh.Ring(Center, radius * (1f + 0.4f * t), 0.14f * fade, FlameMesh.Alpha(Hellfire, 0.85f * fade), 56);
                DrawCracks(fade);
                // The paw lifts slightly as it withdraws.
                Paw(Center + Vector2.up * (size * 0.35f + t * t * 1.2f), size, fade);
            }
            mesh.Commit();
        }

        private void DrawWarning(float t)
        {
            Color violet = DemonessAttack.Violet;
            mesh.Disc(Center, radius * t, FlameMesh.Alpha(violet, 0.08f + 0.18f * t), FlameMesh.Alpha(violet, 0.3f * t), 40);
            mesh.Ring(Center, radius, 0.06f, FlameMesh.Alpha(violet, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(age * 14f))), 56);
            // The paw's shadow grows as it closes in.
            mesh.Ellipse(Center, radius * 0.7f * t, radius * 0.35f * t, FlameMesh.Alpha(DemonessAttack.Abyss, 0.5f * t), FlameMesh.Alpha(DemonessAttack.Abyss, 0f));
        }

        private void DrawPortal(Vector2 at, float open, float alpha)
        {
            if (open <= 0.01f || alpha <= 0f) return;
            float rx = radius * 0.95f * open, ry = radius * 0.42f * open;
            mesh.Ellipse(at, rx * 1.25f, ry * 1.4f, FlameMesh.Alpha(DemonessAttack.Violet, 0.5f * alpha), FlameMesh.Alpha(DemonessAttack.Violet, 0f));
            mesh.Ellipse(at, rx, ry, FlameMesh.Alpha(Color.black, 0.95f * alpha), FlameMesh.Alpha(DemonessAttack.Abyss, 0.9f * alpha));
            // Swirling motes on the rim.
            for (int i = 0; i < 8; i++)
            {
                float a = age * 3f + i * Mathf.PI / 4f;
                mesh.Diamond(at + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry), 0.07f, FlameMesh.Alpha(DemonessAttack.Pale, 0.8f * alpha));
            }
        }

        /// <summary>
        /// A huge paw matted with blood-red fur: jagged tufts bristle along the forearm and knuckles, embers glow in
        /// the cracks of its hide and long hooked black claws rake toward the ground.
        /// </summary>
        private void Paw(Vector2 at, float size, float alpha)
        {
            if (alpha <= 0f) return;
            Color fur = FlameMesh.Alpha(Blood, alpha), furDark = FlameMesh.Alpha(BloodDark, alpha);
            Color rim = FlameMesh.Alpha(Hellfire, 0.55f * alpha), clawBase = FlameMesh.Alpha(ClawBlack, alpha);
            float breathe = 0.75f + 0.25f * Mathf.Sin(age * 9f);
            // The forearm reaching down out of the portal, bristling with fur along both edges.
            float armWidth = size * 1.3f;
            mesh.Bar(at, Vector2.up, PortalHeight, armWidth + size * 0.12f, FlameMesh.Alpha(Hellfire, 0.4f * alpha), FlameMesh.Alpha(Hellfire, 0f));
            mesh.Bar(at, Vector2.up, PortalHeight, armWidth, fur, FlameMesh.Alpha(BloodDark, 0.3f * alpha));
            for (int i = 0; i < 9; i++)
            {
                float y = size * 0.3f + i * PortalHeight / 10f, fade = alpha * (1f - i / 10f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float spike = size * (0.22f + 0.2f * FlameMesh.Hash(i, side * 3.1f)) * (0.9f + 0.1f * Mathf.Sin(age * 12f + i));
                    Vector2 root = at + new Vector2(side * armWidth * 0.5f, y);
                    mesh.Triangle(root + Vector2.down * size * 0.12f, root + new Vector2(side * spike, spike * 0.7f), root + Vector2.up * size * 0.14f,
                        FlameMesh.Alpha(Blood, fade), FlameMesh.Alpha(BloodDark, fade), FlameMesh.Alpha(Blood, fade));
                }
            }
            // The paw itself: a hellish glow, then dark-edged red fur.
            mesh.Ellipse(at, size * 1.22f, size * 0.95f, FlameMesh.Alpha(Hellfire, 0.5f * alpha * breathe), FlameMesh.Alpha(Hellfire, 0f));
            mesh.Ellipse(at, size * 1.06f, size * 0.8f, fur, furDark);
            // Ragged tufts along the top and sides of the paw.
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(0.15f, Mathf.PI - 0.15f, i / 6f);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.78f);
                Vector2 root = at + dir * size;
                Vector2 tangent = Vector2.Perpendicular(dir).normalized * size * 0.16f;
                float length = size * (0.25f + 0.18f * FlameMesh.Hash(i, 8.2f));
                mesh.Triangle(root + tangent, root + dir.normalized * length + tangent * 0.8f, root - tangent, fur, furDark, fur);
            }
            // Embers smouldering in cracks across the back of the paw.
            for (int i = 0; i < 4; i++)
            {
                float a = -0.9f + i * 0.6f + FlameMesh.Hash(i, 2.4f) * 0.3f;
                Vector2 from = at + new Vector2(Mathf.Cos(a + Mathf.PI * 0.5f) * size * 0.15f, size * 0.25f);
                Vector2 dir = FlameMesh.Polar(a - Mathf.PI * 0.5f, 1f);
                mesh.Bar(from, dir, size * (0.35f + 0.2f * FlameMesh.Hash(i, 6.6f)), size * 0.07f,
                    FlameMesh.Alpha(FlameMesh.Yellow, 0.9f * alpha * breathe), FlameMesh.Alpha(Hellfire, 0.2f * alpha));
            }
            for (int i = 0; i < 4; i++)
            {
                float offset = i - 1.5f;
                Vector2 toe = at + new Vector2(offset * size * 0.52f, -size * 0.72f + Mathf.Abs(offset) * size * 0.16f);
                mesh.Ellipse(toe, size * 0.3f, size * 0.33f, FlameMesh.Alpha(Hellfire, 0.5f * alpha), FlameMesh.Alpha(Hellfire, 0f));
                mesh.Ellipse(toe, size * 0.23f, size * 0.27f, fur, furDark);
                // Long hooked claws: they bow outward, then curl back in to a needle point.
                float flare = offset * size * 0.1f;
                Vector2 baseL = toe + new Vector2(-size * 0.11f, -size * 0.12f), baseR = toe + new Vector2(size * 0.11f, -size * 0.12f);
                Vector2 bend = toe + new Vector2(flare * 1.6f, -size * 0.52f);
                Vector2 tip = toe + new Vector2(flare * 0.4f - Mathf.Sign(offset) * size * 0.08f, -size * 0.9f);
                mesh.Triangle(baseL, bend + Vector2.left * size * 0.07f, baseR, clawBase, clawBase, clawBase);
                mesh.Triangle(baseR, bend + Vector2.left * size * 0.07f, bend + Vector2.right * size * 0.07f, clawBase, clawBase, clawBase);
                mesh.Triangle(bend + Vector2.left * size * 0.07f, tip, bend + Vector2.right * size * 0.07f, clawBase, FlameMesh.Alpha(Hellfire, alpha), clawBase);
                // A red glint along the edge of each claw.
                mesh.Bar(baseR, (bend - baseR).normalized, (bend - baseR).magnitude, size * 0.03f, rim, FlameMesh.Alpha(Hellfire, 0.9f * alpha));
            }
        }

        /// <summary>Two slit eyes glaring out of the dark before the paw comes through.</summary>
        private void DrawEyes(Vector2 at, float alpha)
        {
            if (alpha <= 0f) return;
            float gap = radius * 0.24f, w = radius * 0.16f, h = radius * 0.05f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = at + new Vector2(side * gap, 0f);
                mesh.Ellipse(eye, w * 1.8f, h * 3f, FlameMesh.Alpha(Hellfire, 0.45f * alpha), FlameMesh.Alpha(Hellfire, 0f));
                // Slanted inward into a scowl.
                mesh.Triangle(eye + new Vector2(-side * w, h * 1.4f), eye + new Vector2(side * w, -h * 0.2f), eye + new Vector2(-side * w * 0.2f, -h * 1.2f),
                    FlameMesh.Alpha(FlameMesh.Yellow, alpha), FlameMesh.Alpha(Hellfire, alpha), FlameMesh.Alpha(Hellfire, alpha));
                mesh.Ellipse(eye + new Vector2(-side * w * 0.2f, h * 0.1f), h * 0.35f, h * 1.2f, FlameMesh.Alpha(Color.black, alpha), FlameMesh.Alpha(Color.black, alpha), 10);
            }
        }

        private void DrawCracks(float fade)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + FlameMesh.Hash(i, 5.3f);
                float length = radius * (0.7f + FlameMesh.Hash(i, 1.7f) * 0.6f);
                mesh.Bar(Center, FlameMesh.Polar(a, 1f), length, 0.1f, FlameMesh.Alpha(FlameMesh.Yellow, 0.9f * fade), FlameMesh.Alpha(Hellfire, 0f));
            }
        }

        private static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

        private void OnDestroy() { mesh?.Release(); }
    }
}

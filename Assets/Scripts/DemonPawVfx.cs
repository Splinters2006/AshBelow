using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// HEEEELP: a portal rips open in the air above the target while a violet warning circle fills on the ground,
    /// then the Demoness's giant pet shoves a clawed paw through and slams it down. Purely visual; she applies the damage.
    /// </summary>
    public sealed class DemonPawVfx : MonoBehaviour
    {
        private const float SlamTime = 0.5f, PortalHeight = 2.6f;
        /// <summary>Where the paw lands; the caster keeps it on the target until the paw commits.</summary>
        public Vector2 Center { get; set; }
        private FlameMesh mesh;
        private float radius, windup, age;

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
                // The paw pushes through the portal, then plunges.
                float drop = Mathf.Clamp01((t - 0.35f) / 0.65f);
                drop *= drop;
                if (t > 0.35f) Paw(Center + Vector2.up * Mathf.Lerp(PortalHeight, size * 0.35f, drop), size, Mathf.Clamp01((t - 0.35f) * 6f));
            }
            else
            {
                float t = (age - windup) / SlamTime, fade = 1f - t;
                DrawPortal(Center + Vector2.up * PortalHeight, 1f - EaseOut(t), fade);
                mesh.Ring(Center, radius * (1f + 0.4f * t), 0.14f * fade, FlameMesh.Alpha(DemonessAttack.Pale, 0.8f * fade), 56);
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

        /// <summary>A huge dark paw, pad down, with pale claws reaching toward the ground.</summary>
        private void Paw(Vector2 at, float size, float alpha)
        {
            if (alpha <= 0f) return;
            Color fur = FlameMesh.Alpha(DemonessAttack.Abyss, alpha), rim = FlameMesh.Alpha(DemonessAttack.Violet, 0.75f * alpha);
            Color claw = FlameMesh.Alpha(DemonessAttack.Pale, alpha);
            // The forearm reaching down out of the portal.
            mesh.Bar(at, Vector2.up, PortalHeight, size * 1.3f, FlameMesh.Alpha(DemonessAttack.Abyss, 0.9f * alpha), FlameMesh.Alpha(DemonessAttack.Abyss, 0.2f * alpha));
            mesh.Ellipse(at, size * 1.12f, size * 0.86f, rim, rim);
            mesh.Ellipse(at, size, size * 0.74f, fur, fur);
            for (int i = 0; i < 4; i++)
            {
                float offset = i - 1.5f;
                Vector2 toe = at + new Vector2(offset * size * 0.5f, -size * 0.72f + Mathf.Abs(offset) * size * 0.14f);
                mesh.Ellipse(toe, size * 0.26f, size * 0.3f, rim, rim);
                mesh.Ellipse(toe, size * 0.21f, size * 0.25f, fur, fur);
                mesh.Triangle(toe + new Vector2(-size * 0.09f, -size * 0.14f), toe + new Vector2(offset * size * 0.06f, -size * 0.62f),
                    toe + new Vector2(size * 0.09f, -size * 0.14f), claw, FlameMesh.Alpha(Color.white, alpha), claw);
            }
            mesh.Ellipse(at + Vector2.down * size * 0.08f, size * 0.38f, size * 0.26f, FlameMesh.Alpha(DemonessAttack.Violet, 0.6f * alpha),
                FlameMesh.Alpha(DemonessAttack.Violet, 0.1f * alpha));
        }

        private void DrawCracks(float fade)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + FlameMesh.Hash(i, 5.3f);
                float length = radius * (0.7f + FlameMesh.Hash(i, 1.7f) * 0.6f);
                mesh.Bar(Center, FlameMesh.Polar(a, 1f), length, 0.08f, FlameMesh.Alpha(DemonessAttack.Violet, 0.9f * fade), FlameMesh.Alpha(DemonessAttack.Abyss, 0f));
            }
        }

        private static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

        private void OnDestroy() { mesh?.Release(); }
    }
}

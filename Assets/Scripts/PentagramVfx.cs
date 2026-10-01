using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Demon Curse's sigil: a violet circle and five-pointed star, ringed by a band of burning runes, trace themselves onto the ground during the windup,
    /// then flare with dark flames as the curse takes hold. Purely visual; the Demoness applies the curse.
    /// </summary>
    public sealed class PentagramVfx : MonoBehaviour
    {
        private const float FlareTime = 0.6f;
        private const int RuneCount = 18;
        // Runes as line segments (x0, y0, x1, y1, ...) in a unit cell; x runs along the rim, y points outward.
        private static readonly float[][] Runes =
        {
            new[] { 0f, -0.5f, 0f, 0.5f, 0f, 0.5f, 0.4f, 0.25f, 0f, 0.15f, 0.4f, -0.1f },
            new[] { -0.3f, -0.5f, -0.3f, 0.5f, -0.3f, 0.5f, 0.3f, 0.2f, 0.3f, 0.2f, 0.3f, -0.5f },
            new[] { 0f, -0.5f, 0f, 0.5f, 0f, 0.25f, 0.35f, 0f, 0.35f, 0f, 0f, -0.25f },
            new[] { 0f, -0.5f, 0f, 0.5f, 0f, 0.5f, 0.35f, 0.25f, 0.35f, 0.25f, 0f, 0f, 0f, 0f, 0.35f, -0.5f },
            new[] { 0.3f, 0.5f, -0.2f, 0f, -0.2f, 0f, 0.3f, -0.5f },
            new[] { -0.35f, 0.5f, 0.35f, -0.5f, 0.35f, 0.5f, -0.35f, -0.5f },
            new[] { -0.3f, -0.5f, -0.3f, 0.5f, 0.3f, -0.5f, 0.3f, 0.5f, -0.3f, 0.2f, 0.3f, -0.2f },
            new[] { 0f, -0.5f, 0f, 0.5f, 0f, 0.1f, -0.35f, 0.5f, 0f, 0.1f, 0.35f, 0.5f },
            new[] { 0f, -0.5f, 0f, 0.5f, 0f, 0.5f, -0.35f, 0.15f, 0f, 0.5f, 0.35f, 0.15f },
            new[] { 0f, 0.5f, 0.3f, 0.1f, 0.3f, 0.1f, 0f, -0.3f, 0f, -0.3f, -0.3f, 0.1f, -0.3f, 0.1f, 0f, 0.5f, 0f, -0.3f, -0.3f, -0.5f, 0f, -0.3f, 0.3f, -0.5f },
        };
        private FlameMesh mesh;
        private Vector2 center;
        private float radius, windup, age;
        // The rune band's outer ring sits 1.2 circles out; the circle is drawn smaller so that ring lands on the cursed area's edge.
        private float SigilRadius => radius / 1.2f;

        public static PentagramVfx Play(Transform root, Vector2 center, float radius, float windup)
        {
            if (root == null) return null;
            var effect = new GameObject("Pentagram").AddComponent<PentagramVfx>();
            effect.transform.SetParent(root, false);
            effect.center = center;
            effect.radius = radius;
            effect.windup = Mathf.Max(0.05f, windup);
            effect.mesh = new FlameMesh(effect.gameObject, 8);
            return effect;
        }

        private void LateUpdate()
        {
            float previous = age;
            age += Time.deltaTime;
            if (previous < windup && age >= windup) Flare();
            if (age >= windup + FlareTime) { Destroy(gameObject); return; }
            mesh.Begin();
            if (age < windup) DrawSigil(age / windup, 1f, 0f);
            else DrawFlare((age - windup) / FlareTime);
            mesh.Commit();
        }

        /// <summary>The circle and star, traced up to <paramref name="progress"/> and faded by <paramref name="alpha"/>.</summary>
        private void DrawSigil(float progress, float alpha, float flare)
        {
            Color violet = DemonessAttack.Violet, pale = DemonessAttack.Pale;
            float r = SigilRadius;
            mesh.Disc(center, radius, FlameMesh.Alpha(DemonessAttack.Abyss, 0.45f * alpha), FlameMesh.Alpha(violet, (0.12f + 0.3f * flare) * alpha), 48);
            mesh.Ring(center, r, 0.09f, FlameMesh.Alpha(violet, (0.4f + 0.6f * progress) * alpha), 64);
            mesh.Ring(center, r * 0.86f, 0.04f, FlameMesh.Alpha(pale, 0.6f * progress * alpha), 56);
            float spin = age * 0.6f + Mathf.PI / 2f;
            var tips = new Vector2[5];
            for (int i = 0; i < 5; i++) tips[i] = center + FlameMesh.Polar(spin + i * Mathf.PI * 2f / 5f, r * 0.86f);
            // Each of the five strokes joins every second point; they draw in one after another.
            for (int i = 0; i < 5; i++)
            {
                float line = Mathf.Clamp01(progress * 5f - i);
                if (line <= 0f) break;
                Vector2 from = tips[i], to = tips[(i + 2) % 5];
                Vector2 direction = (to - from).normalized;
                float length = Vector2.Distance(from, to) * line;
                mesh.Bar(from, direction, length, 0.16f, FlameMesh.Alpha(violet, 0.55f * alpha), FlameMesh.Alpha(violet, 0.55f * alpha));
                mesh.Bar(from, direction, length, 0.06f, FlameMesh.Alpha(pale, alpha), FlameMesh.Alpha(pale, alpha));
            }
            for (int i = 0; i < 5; i++) mesh.Diamond(tips[i], 0.12f + 0.08f * flare, FlameMesh.Alpha(pale, progress * alpha));
            DrawRunes(progress, alpha, flare);
        }

        /// <summary>A band of glowing runes around the outside of the circle, burning in one by one and turning against the star.</summary>
        private void DrawRunes(float progress, float alpha, float flare)
        {
            Color violet = DemonessAttack.Violet, pale = DemonessAttack.Pale;
            float r = SigilRadius;
            float band = r * 1.1f, height = r * 0.13f, width = 0.035f + 0.03f * flare;
            mesh.Ring(center, r * 1.2f, 0.05f, FlameMesh.Alpha(violet, 0.7f * progress * alpha), 72);
            mesh.Ring(center, band, height * 1.6f, FlameMesh.Alpha(DemonessAttack.Abyss, 0.35f * alpha), FlameMesh.Alpha(violet, 0.08f * alpha), 72);
            float spin = -age * 0.35f;
            for (int i = 0; i < RuneCount; i++)
            {
                float show = Mathf.Clamp01(progress * RuneCount * 1.5f - i);
                if (show <= 0f) break;
                float a = spin + i * Mathf.PI * 2f / RuneCount;
                Vector2 up = FlameMesh.Polar(a, 1f), along = -Vector2.Perpendicular(up);
                Vector2 at = center + up * band;
                float glow = show * alpha * (0.7f + 0.3f * Mathf.Sin(age * 9f + i * 2.3f));
                float[] rune = Runes[(int)(FlameMesh.Hash(i, 7.3f) * Runes.Length) % Runes.Length];
                for (int k = 0; k + 3 < rune.Length; k += 4)
                {
                    Vector2 from = at + (along * rune[k] * 0.75f + up * rune[k + 1]) * height;
                    Vector2 to = at + (along * rune[k + 2] * 0.75f + up * rune[k + 3]) * height;
                    Vector2 direction = (to - from).normalized;
                    float length = Vector2.Distance(from, to);
                    mesh.Bar(from, direction, length, width * 3f, FlameMesh.Alpha(violet, 0.45f * glow), FlameMesh.Alpha(violet, 0.45f * glow));
                    mesh.Bar(from, direction, length, width, FlameMesh.Alpha(pale, glow), FlameMesh.Alpha(pale, glow));
                }
                // A tick on the outer ring between each rune.
                Vector2 tick = FlameMesh.Polar(a + Mathf.PI / RuneCount, 1f);
                mesh.Bar(center + tick * r * 1.16f, tick, r * 0.08f, 0.04f, FlameMesh.Alpha(pale, 0.8f * glow), FlameMesh.Alpha(violet, 0f));
            }
        }

        private void DrawFlare(float t)
        {
            float fade = 1f - t;
            DrawSigil(1f, fade, 1f - t);
            // Dark flames lick up from the rim of the circle.
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5f + 0.3f;
                Vector2 foot = center + FlameMesh.Polar(a, radius * 0.95f);
                float height = (0.7f + FlameMesh.Hash(i, 3.1f) * 0.6f) * (1f - 0.5f * t);
                mesh.Bar(foot, Vector2.up, height, 0.24f, FlameMesh.Alpha(DemonessAttack.Violet, 0.6f * fade), FlameMesh.Alpha(DemonessAttack.Violet, 0f));
                mesh.Bar(foot, Vector2.up, height * 0.7f, 0.1f, FlameMesh.Alpha(DemonessAttack.Abyss, 0.9f * fade), FlameMesh.Alpha(DemonessAttack.Abyss, 0f));
            }
            mesh.Disc(center, radius * (1f + 0.2f * t), FlameMesh.Alpha(DemonessAttack.Pale, 0.35f * fade * fade), FlameMesh.Alpha(DemonessAttack.Violet, 0f), 48);
        }

        private void Flare()
        {
            var root = transform.parent;
            HeroVfx.Pulse(root, center, radius * 1.15f, DemonessAttack.Violet, 0.4f);
            HeroVfx.Sparks(root, center, DemonessAttack.Pale, 18, 4.5f, 0.45f, Vector2.up, 200f, 1.1f);
            CombatVfx.Ring(root, center, radius, DemonessAttack.Pale, 0.35f);
            ScreenFx.Shake(0.12f, 0.18f);
        }

        private void OnDestroy() { mesh?.Release(); }
    }
}

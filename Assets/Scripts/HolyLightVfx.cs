using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Judgment's holy light: shafts of light gather over a golden seal on the ground during the windup, then a
    /// blinding pillar smites the area and radiates rays. Purely visual; the Paladin applies the damage.
    /// </summary>
    public sealed class HolyLightVfx : MonoBehaviour
    {
        private const float SmiteTime = 0.55f;
        private FlameMesh mesh;
        private Vector2 center;
        private float radius, windup, age;

        public static HolyLightVfx Play(Transform root, Vector2 center, float radius, float windup)
        {
            if (root == null) return null;
            var effect = new GameObject("Holy light").AddComponent<HolyLightVfx>();
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
            if (previous < windup && age >= windup) Smite();
            if (age >= windup + SmiteTime) { Destroy(gameObject); return; }
            mesh.Begin();
            if (age < windup) DrawGathering(age / windup);
            else DrawSmite((age - windup) / SmiteTime);
            mesh.Commit();
        }

        private void DrawGathering(float t)
        {
            Color gold = AbilityCatalog.Gold, white = new Color(1f, 0.98f, 0.88f);
            float spin = age * 1.5f;
            // The seal fills in as the light gathers.
            mesh.Disc(center, radius * t, FlameMesh.Alpha(white, 0.1f + 0.15f * t), FlameMesh.Alpha(gold, 0.25f * t), 48);
            mesh.Ring(center, radius, 0.08f, FlameMesh.Alpha(gold, 0.5f + 0.5f * t), 64);
            mesh.Ring(center, radius * 0.62f, 0.05f, FlameMesh.Alpha(gold, 0.6f * t), 48);
            for (int i = 0; i < 8; i++)
            {
                float a = spin + i * Mathf.PI / 4f;
                mesh.Bar(center + FlameMesh.Polar(a, radius * 0.62f), FlameMesh.Polar(a, 1f), radius * 0.38f, 0.05f,
                    FlameMesh.Alpha(gold, 0.7f * t), FlameMesh.Alpha(gold, 0.1f));
            }
            // Shafts of light descend from above and converge on the seal.
            for (int i = 0; i < 9; i++)
            {
                float seed = FlameMesh.Hash(i, 2.7f);
                Vector2 foot = center + new Vector2((seed - 0.5f) * radius * 1.6f * (1f - t * 0.7f), (FlameMesh.Hash(i, 9.1f) - 0.5f) * radius * 0.6f);
                float height = 7f, drop = Mathf.Lerp(height, 0f, Mathf.Clamp01(t * 1.3f - seed * 0.3f));
                float width = 0.18f + seed * 0.25f;
                mesh.Bar(foot + Vector2.up * drop, Vector2.up, height, width * 2.2f, FlameMesh.Alpha(gold, 0.3f * t), FlameMesh.Alpha(gold, 0f));
                mesh.Bar(foot + Vector2.up * drop, Vector2.up, height, width, FlameMesh.Alpha(white, 0.85f * t), FlameMesh.Alpha(gold, 0f));
            }
        }

        private void DrawSmite(float t)
        {
            Color gold = AbilityCatalog.Gold, white = new Color(1f, 0.98f, 0.9f);
            float fade = 1f - t;
            float pillar = radius * (0.9f - 0.5f * t);
            mesh.Bar(center + Vector2.down * 0.3f, Vector2.up, 9f, pillar * 1.5f, FlameMesh.Alpha(gold, 0.45f * fade), FlameMesh.Alpha(gold, 0f));
            mesh.Bar(center + Vector2.down * 0.3f, Vector2.up, 9f, pillar * 0.6f, FlameMesh.Alpha(white, fade), FlameMesh.Alpha(white, 0f));
            mesh.Disc(center, radius * (1f + 0.25f * t), FlameMesh.Alpha(white, 0.55f * fade), FlameMesh.Alpha(gold, 0.15f * fade), 48);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f + 0.1f;
                mesh.Bar(center + FlameMesh.Polar(a, radius * 0.4f), FlameMesh.Polar(a, 1f), radius * (0.8f + 0.9f * t), 0.12f,
                    FlameMesh.Alpha(white, 0.8f * fade), FlameMesh.Alpha(gold, 0f));
            }
        }

        private void Smite()
        {
            var root = transform.parent;
            HeroVfx.Pulse(root, center, radius * 1.2f, AbilityCatalog.Gold, 0.45f);
            HeroVfx.Sparks(root, center, new Color(1f, 0.95f, 0.75f), 24, 6f, 0.5f, null, 360f, 1.3f);
            CombatVfx.Ring(root, center, radius, Color.white, 0.4f);
            ScreenFx.Shake(0.2f, 0.25f);
        }

        private void OnDestroy() { mesh?.Release(); }
    }
}

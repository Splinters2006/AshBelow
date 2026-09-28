using UnityEngine;

namespace Slopgame
{
    public sealed class CombatVfx : MonoBehaviour
    {
        private LineRenderer line;
        private Material material;
        private float duration, elapsed;
        private Color tint;
        private float width;

        private static CombatVfx Create(Transform parent, Color color, float duration, float width)
        {
            var effect = new GameObject("Combat effect").AddComponent<CombatVfx>();
            effect.transform.SetParent(parent, false);
            effect.duration = duration;
            effect.tint = color;
            effect.width = width;
            effect.line = effect.gameObject.AddComponent<LineRenderer>();
            effect.material = new Material(Shader.Find("Sprites/Default"));
            effect.line.sharedMaterial = effect.material;
            effect.line.useWorldSpace = true;
            effect.line.sortingOrder = 9;
            effect.line.numCapVertices = 4;
            effect.line.widthMultiplier = width;
            effect.line.startColor = effect.line.endColor = color;
            return effect;
        }

        private static Material trailMaterial;

        /// <summary>Adds a short fading trail to a moving projectile. The trail dies with its object.</summary>
        public static TrailRenderer Trail(GameObject target, Color color, float width = 0.12f, float time = 0.14f)
        {
            if (trailMaterial == null)
                trailMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Projectile trails (shared)", hideFlags = HideFlags.HideAndDontSave };
            // A GameObject may hold only one Renderer, and projectiles already have a SpriteRenderer.
            var holder = new GameObject("Trail");
            holder.transform.SetParent(target.transform, false);
            var trail = holder.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = time;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.numCapVertices = 2;
            trail.sortingOrder = 5;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.5f), 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            return trail;
        }

        public static void Bolt(Transform parent, Vector2 from, Vector2 to, Color color) => Bolt(parent, from, to, color, 0.09f, 0.22f);

        /// <summary>Lightning with a wide soft glow under a thin white-hot core.</summary>
        public static void GlowBolt(Transform parent, Vector2 from, Vector2 to, Color color)
        {
            var glow = color;
            glow.a *= 0.35f;
            Bolt(parent, from, to, glow, 0.24f, 0.3f);
            Bolt(parent, from, to, Color.Lerp(color, Color.white, 0.75f), 0.05f, 0.2f);
        }

        public static void Bolt(Transform parent, Vector2 from, Vector2 to, Color color, float width, float duration)
        {
            var effect = Create(parent, color, duration, width);
            const int count = 9;
            effect.line.positionCount = count;
            Vector2 normal = Vector2.Perpendicular((to - from).normalized);
            for (int i = 0; i < count; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)(count - 1));
                if (i > 0 && i < count - 1) point += normal * Random.Range(-0.22f, 0.22f);
                effect.line.SetPosition(i, point);
            }
        }

        public static void Ring(Transform parent, Vector2 center, float radius, Color color, float duration = 0.4f)
        {
            var effect = Create(parent, color, duration, 0.075f);
            effect.line.loop = true;
            effect.line.positionCount = 64;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64;
                effect.line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float fade = Mathf.Clamp01(1f - elapsed / duration);
            var color = tint;
            color.a *= fade;
            line.startColor = line.endColor = color;
            line.widthMultiplier = width * (0.5f + fade);
            if (elapsed >= duration) Destroy(gameObject);
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}

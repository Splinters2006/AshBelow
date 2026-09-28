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

        public static void Bolt(Transform parent, Vector2 from, Vector2 to, Color color)
        {
            var effect = Create(parent, color, 0.22f, 0.09f);
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

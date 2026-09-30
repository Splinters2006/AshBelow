using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A fallen hero's or enemy's last moment: a copy of its sprites recoils, topples over, darkens and fades away,
    /// so the live object can be hidden or destroyed at once.
    /// </summary>
    public sealed class DeathAnimation : MonoBehaviour
    {
        public const float EnemyDuration = 0.6f;
        public const float BossDuration = 1.2f;
        /// <summary>The hero's fall runs on unscaled time: a solo death freezes the game underneath it.</summary>
        public const float HeroDuration = 1.3f;

        private static readonly Color HurtTint = new Color(1f, 0.35f, 0.3f);
        private static readonly Color DeadTint = new Color(0.25f, 0.22f, 0.24f);

        private SpriteRenderer[] sprites;
        private Color[] startColors;
        private Vector3 startPosition, startScale;
        private float duration, age, tipAngle;
        private bool unscaled;

        /// <summary>
        /// Copies the body sprites of <paramref name="source"/> under <paramref name="parent"/> and plays the fall.
        /// Children far outside the body (attack telegraphs, auras) are left out.
        /// </summary>
        public static DeathAnimation Play(Transform source, Transform parent, float duration, bool unscaled = false)
        {
            if (source == null) return null;
            var body = source.GetComponent<SpriteRenderer>();
            var root = new GameObject(source.name + " death");
            root.transform.SetParent(parent, false);
            root.transform.position = source.position;
            Bounds bodyBounds = body != null ? body.bounds : new Bounds(source.position, Vector3.one);
            bodyBounds.Expand(Mathf.Max(bodyBounds.size.x, bodyBounds.size.y));
            foreach (var original in source.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!original.enabled || original.sprite == null || original.color.a <= 0.01f) continue;
                if (original != body && !bodyBounds.Contains(original.bounds.center)) continue;
                var copy = new GameObject(original.name).AddComponent<SpriteRenderer>();
                copy.transform.SetParent(root.transform, false);
                copy.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                copy.transform.localScale = original.transform.lossyScale;
                copy.sprite = original.sprite;
                copy.color = original.color;
                copy.flipX = original.flipX;
                copy.flipY = original.flipY;
                copy.sharedMaterial = original.sharedMaterial;
                copy.sortingLayerID = original.sortingLayerID;
                copy.sortingOrder = original.sortingOrder;
            }
            var animation = root.AddComponent<DeathAnimation>();
            animation.duration = Mathf.Max(0.05f, duration);
            animation.unscaled = unscaled;
            // Topple away from the way the body faced.
            animation.tipAngle = body != null && body.flipX ? -90f : 90f;
            return animation;
        }

        private void Start()
        {
            sprites = GetComponentsInChildren<SpriteRenderer>();
            startColors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) startColors[i] = sprites[i].color;
            startPosition = transform.localPosition;
            startScale = transform.localScale;
        }

        private void Update()
        {
            age += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            // Recoil: a quick squash and a red flash.
            float recoil = 1f - Mathf.Clamp01(t / 0.2f);
            transform.localScale = Vector3.Scale(startScale, new Vector3(1f + 0.25f * recoil, 1f - 0.2f * recoil, 1f));
            // Topple: a small hop while tipping over onto the side, easing out.
            float fall = Mathf.Clamp01((t - 0.08f) / 0.42f);
            float eased = 1f - (1f - fall) * (1f - fall);
            transform.localRotation = Quaternion.Euler(0f, 0f, tipAngle * eased);
            float size = Mathf.Max(0.2f, startScale.y);
            transform.localPosition = startPosition + Vector3.up * (Mathf.Sin(fall * Mathf.PI) * 0.18f * size - eased * 0.12f * size);
            // Darken as it falls, then fade out.
            float fade = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color tint = Color.Lerp(Color.Lerp(Color.white, DeadTint, eased), HurtTint, recoil);
                Color color = startColors[i] * tint;
                color.a = startColors[i].a * fade;
                sprites[i].color = color;
            }
            if (age >= duration) Destroy(gameObject);
        }
    }
}

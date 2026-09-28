using UnityEngine;

namespace Slopgame
{
    /// <summary>Fades every sprite under this object to transparent, then destroys it.</summary>
    public sealed class FadingSprite : MonoBehaviour
    {
        public float Duration { get; set; } = 0.25f;
        private SpriteRenderer[] sprites;
        private float[] startAlpha;
        private float age;

        private void Start()
        {
            sprites = GetComponentsInChildren<SpriteRenderer>();
            startAlpha = new float[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) startAlpha[i] = sprites[i].color.a;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float fade = Mathf.Clamp01(1f - age / Mathf.Max(0.01f, Duration));
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                var color = sprites[i].color;
                color.a = startAlpha[i] * fade;
                sprites[i].color = color;
            }
            if (age >= Duration) Destroy(gameObject);
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Chunks thrown up off the floor (rubble from a landing, shards from a body): each flies out, arcs up through a
    /// pretend height, bounces once, settles and fades. One component drives the whole burst. Purely cosmetic.
    /// </summary>
    public sealed class Debris : MonoBehaviour
    {
        private const float Gravity = 22f;

        private Transform[] chunks;
        private SpriteRenderer[] sprites;
        private Vector2[] ground, velocity;
        private float[] height, lift, spin, startAlpha;
        private float age, life;
        private bool unscaled;

        /// <summary>
        /// Throws <paramref name="count"/> square chunks out of <paramref name="center"/>. <paramref name="lift"/> is how
        /// hard they are thrown upward; <paramref name="unscaled"/> keeps them moving while the game is frozen.
        /// </summary>
        public static Debris Burst(Transform parent, Vector2 center, Color color, int count, float speed, float lift, float size,
            float life, int order = 8, bool unscaled = false)
        {
            if (parent == null || count <= 0) return null;
            var debris = new GameObject("Debris").AddComponent<Debris>();
            debris.transform.SetParent(parent, false);
            debris.transform.position = center;
            debris.life = Mathf.Max(0.1f, life);
            debris.unscaled = unscaled;
            debris.chunks = new Transform[count];
            debris.sprites = new SpriteRenderer[count];
            debris.ground = new Vector2[count];
            debris.velocity = new Vector2[count];
            debris.height = new float[count];
            debris.lift = new float[count];
            debris.spin = new float[count];
            debris.startAlpha = new float[count];
            for (int i = 0; i < count; i++)
            {
                float chunk = size * Random.Range(0.55f, 1.25f);
                // A little variety in the shade, so a heap of rubble does not read as one flat colour.
                Color shade = Color.Lerp(color, Color.black, Random.Range(0f, 0.3f));
                shade.a = color.a;
                var sprite = DungeonVisuals.Create("Chunk", debris.transform, center, new Vector2(chunk, chunk * Random.Range(0.6f, 1f)), shade, order);
                debris.chunks[i] = sprite.transform;
                debris.sprites[i] = sprite;
                debris.ground[i] = center;
                float angle = (i + Random.value) / count * Mathf.PI * 2f;
                debris.velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f) * speed * Random.Range(0.45f, 1.1f);
                debris.lift[i] = lift * Random.Range(0.6f, 1.2f);
                debris.spin[i] = Random.Range(-720f, 720f);
                debris.startAlpha[i] = shade.a;
                sprite.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }
            return debris;
        }

        private void Update()
        {
            float dt = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            float fade = 1f - Mathf.Clamp01((age - life * 0.6f) / (life * 0.4f));
            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i] == null) continue;
                bool airborne = height[i] > 0f || lift[i] > 0f;
                if (airborne)
                {
                    height[i] += lift[i] * dt;
                    lift[i] -= Gravity * dt;
                    if (height[i] <= 0f)
                    {
                        // One bounce, then it lies where it fell.
                        height[i] = 0f;
                        lift[i] = lift[i] < -3f ? -lift[i] * 0.3f : 0f;
                        velocity[i] *= 0.45f;
                        spin[i] *= 0.4f;
                    }
                }
                else velocity[i] *= Mathf.Max(0f, 1f - 10f * dt);
                ground[i] += velocity[i] * dt;
                chunks[i].position = ground[i] + Vector2.up * height[i];
                chunks[i].Rotate(0f, 0f, spin[i] * dt);
                var color = sprites[i].color;
                color.a = startAlpha[i] * fade;
                sprites[i].color = color;
            }
            if (age >= life) Destroy(gameObject);
        }
    }
}

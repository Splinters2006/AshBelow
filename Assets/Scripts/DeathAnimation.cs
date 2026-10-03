using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A fallen hero's or enemy's last moment, played on a copy of its sprites so the live object can be hidden or
    /// destroyed at once: a jolt and a flash as the blow lands, then the body is thrown back spinning, thuds down in a
    /// puff of dust, and breaks apart into shards (a hero's soul rising out of it).
    /// </summary>
    public sealed class DeathAnimation : MonoBehaviour
    {
        public const float EnemyDuration = 0.75f;
        public const float BossDuration = 1.6f;
        /// <summary>The hero's fall runs on unscaled time: a solo death freezes the game underneath it.</summary>
        public const float HeroDuration = 1.3f;

        // Fractions of the duration: the jolt, the flight, and the moment it starts breaking apart.
        private const float JoltEnd = 0.12f, LandAt = 0.5f, BreakAt = 0.62f;
        private static readonly Color HurtTint = new Color(1f, 0.35f, 0.3f);
        private static readonly Color DeadTint = new Color(0.3f, 0.26f, 0.28f);
        private static readonly Color Dust = new Color(0.55f, 0.52f, 0.48f, 0.8f);

        private Transform body;
        private SpriteRenderer[] sprites;
        private Color[] startColors;
        private Color bodyColor;
        private Vector3 startScale;
        private Vector2 knock;
        private float duration, age, tipAngle, size;
        private bool unscaled, soul, landed, broken;
        private SpriteRenderer flash, wisp;

        /// <summary>
        /// Copies the body sprites of <paramref name="source"/> under <paramref name="parent"/> and plays the death.
        /// <paramref name="unscaled"/> keeps it playing while the game is frozen; <paramref name="soul"/> lets a hero's soul go.
        /// </summary>
        public static DeathAnimation Play(Transform source, Transform parent, float duration, bool unscaled = false, bool soul = false)
        {
            if (source == null || parent == null) return null;
            var root = new GameObject(source.name + " death");
            root.transform.SetParent(parent, false);
            root.transform.position = source.position;
            var copy = CopyBody(source, root.transform, " body");
            var body = source.GetComponent<SpriteRenderer>();
            var animation = root.AddComponent<DeathAnimation>();
            animation.body = copy.transform;
            animation.duration = Mathf.Max(0.05f, duration);
            animation.unscaled = unscaled;
            animation.soul = soul;
            // Thrown back the way it was facing from, and toppling that way.
            bool facingLeft = body != null && body.flipX;
            animation.tipAngle = facingLeft ? -100f : 100f;
            animation.knock = new Vector2(facingLeft ? 1f : -1f, 0.15f);
            animation.size = body != null ? Mathf.Max(0.4f, Mathf.Max(body.bounds.size.x, body.bounds.size.y)) : 1f;
            return animation;
        }

        /// <summary>
        /// A loose copy of the body sprites of <paramref name="source"/> under <paramref name="parent"/>, standing where it
        /// stands (for animations that must outlive or stand in for it). Children far outside the body are left out.
        /// </summary>
        public static GameObject CopyBody(Transform source, Transform parent, string suffix)
        {
            if (source == null) return null;
            var body = source.GetComponent<SpriteRenderer>();
            var root = new GameObject(source.name + suffix);
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
            return root;
        }

        private void Start()
        {
            sprites = body.GetComponentsInChildren<SpriteRenderer>();
            startColors = new Color[sprites.Length];
            bodyColor = Color.clear;
            int order = int.MaxValue;
            for (int i = 0; i < sprites.Length; i++)
            {
                startColors[i] = sprites[i].color;
                bodyColor += startColors[i];
                order = Mathf.Min(order, sprites[i].sortingOrder);
            }
            bodyColor = sprites.Length > 0 ? bodyColor / sprites.Length : Color.gray;
            if (sprites.Length == 0) order = 5;
            bodyColor.a = 1f;
            startScale = body.localScale;
            // The flash of the killing blow, behind the body.
            flash = DungeonVisuals.Create("Death flash", transform, transform.position, Vector2.one * size * 2.2f, new Color(1f, 0.95f, 0.85f, 0.9f), order - 1);
            flash.sprite = DungeonVisuals.GlowSprite;
        }

        private void Update()
        {
            age += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            Jolt(t);
            Fly(t);
            Break(t);
            if (age >= duration) Destroy(gameObject);
        }

        /// <summary>The blow lands: the body shudders, swells red and a white flash bursts behind it.</summary>
        private void Jolt(float t)
        {
            float jolt = 1f - Mathf.Clamp01(t / JoltEnd);
            if (flash != null)
            {
                flash.transform.localScale = Vector3.one * size * Mathf.Lerp(2.2f, 3.4f, 1f - jolt);
                flash.color = new Color(1f, 0.95f, 0.85f, 0.9f * jolt * jolt);
                if (jolt <= 0f) { Destroy(flash.gameObject); flash = null; }
            }
            float fade = 1f - Mathf.Clamp01((t - BreakAt) / (1f - BreakAt));
            float dark = Mathf.Clamp01((t - JoltEnd) / (LandAt - JoltEnd));
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color tint = Color.Lerp(Color.Lerp(Color.white, DeadTint, dark), HurtTint, jolt);
                Color color = startColors[i] * tint;
                color.a = startColors[i].a * fade;
                sprites[i].color = color;
            }
        }

        /// <summary>Thrown back in an arc while spinning over, then a thud and a bounce where it lands.</summary>
        private void Fly(float t)
        {
            Vector3 shake = t < JoltEnd ? (Vector3)(Random.insideUnitCircle * 0.06f * size) : Vector3.zero;
            float flight = Mathf.Clamp01((t - JoltEnd) / (LandAt - JoltEnd));
            float eased = 1f - (1f - flight) * (1f - flight);
            float arc = Mathf.Sin(flight * Mathf.PI) * 0.45f * size;
            // A small rebound after the thud.
            float rebound = Mathf.Clamp01((t - LandAt) / 0.12f);
            arc += Mathf.Sin(rebound * Mathf.PI) * 0.08f * size;
            body.localPosition = (Vector3)(knock * 0.7f * size * eased) + Vector3.up * arc + shake;
            // Spins past flat, then settles back onto its side.
            float angle = t < LandAt ? tipAngle * eased : Mathf.Lerp(tipAngle, tipAngle * 0.9f, rebound);
            body.localRotation = Quaternion.Euler(0f, 0f, angle);
            float squash = t < JoltEnd ? 1f - t / JoltEnd : 0f;
            float flatten = Mathf.Clamp01((t - BreakAt) / (1f - BreakAt));
            body.localScale = Vector3.Scale(startScale, new Vector3((1f + 0.3f * squash) * (1f + 0.3f * flatten), (1f - 0.2f * squash) * (1f - 0.6f * flatten), 1f));
            if (t >= LandAt && !landed)
            {
                landed = true;
                Debris.Burst(transform.parent, body.position + Vector3.down * 0.3f * size, Dust, Mathf.RoundToInt(6 + 4 * size), 2.2f * size, 2f, 0.07f * size,
                    0.6f, 7, unscaled);
            }
        }

        /// <summary>The body comes apart into shards of its own colour; a hero's soul drifts up out of it.</summary>
        private void Break(float t)
        {
            if (t >= BreakAt && !broken)
            {
                broken = true;
                Debris.Burst(transform.parent, body.position, bodyColor, Mathf.RoundToInt(10 + 6 * size), 3.5f * size, 5f, 0.1f * Mathf.Sqrt(size),
                    duration * (1f - BreakAt) + 0.5f, 9, unscaled);
                if (soul)
                {
                    wisp = DungeonVisuals.Create("Soul", transform, body.position, Vector2.one * 0.6f, Color.clear, 40);
                    wisp.sprite = DungeonVisuals.GlowSprite;
                }
            }
            if (wisp == null) return;
            float rise = Mathf.Clamp01((t - BreakAt) / (1f - BreakAt));
            wisp.transform.position = body.position + Vector3.up * (rise * rise * 2.4f) + Vector3.right * (Mathf.Sin(rise * 9f) * 0.15f);
            wisp.transform.localScale = new Vector3(0.5f + 0.3f * rise, 0.7f + 0.9f * rise, 1f);
            Color glow = Color.Lerp(bodyColor, Color.white, 0.6f);
            glow.a = 0.85f * Mathf.Sin(rise * Mathf.PI);
            wisp.color = glow;
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A Shadow Clone made visible: a full copy of the Assassin's silhouette in living shadow, rimmed in violet with
    /// glowing eyes. It rises out of smoke a step behind the victim, lunges in and stabs, holds the pose a moment and
    /// then dissolves back into smoke.
    /// </summary>
    public sealed class ShadowCloneVfx : MonoBehaviour
    {
        public const float LingerTime = 0.3f, FadeTime = 0.25f;
        private static readonly Color Body = new Color(0.07f, 0.03f, 0.13f, 0.95f), Rim = new Color(0.62f, 0.3f, 1f, 0.55f), Eye = new Color(0.85f, 0.6f, 1f);
        private Transform root;
        private Vector2 from, target;
        private float lungeTime, age;
        private bool struck;
        private SpriteRenderer[] parts;
        private float[] alphas;

        /// <summary>Copies <paramref name="source"/>'s visible sprites into a shadow that lunges from <paramref name="from"/> at <paramref name="target"/>.</summary>
        public static void Play(Transform root, Transform source, Vector2 from, Vector2 target, float lungeTime)
        {
            if (root == null || source == null) return;
            var clone = new GameObject("Shadow clone").AddComponent<ShadowCloneVfx>();
            clone.transform.SetParent(root, false);
            clone.transform.position = from;
            clone.root = root;
            clone.from = from;
            clone.target = target;
            clone.lungeTime = Mathf.Max(0.05f, lungeTime);
            bool facingLeft = target.x < from.x;
            // Only what is actually showing, like the dodge afterimage; the hero's ground shadow stays behind.
            var shown = new System.Collections.Generic.List<SpriteRenderer>();
            int lowest = int.MaxValue, highest = int.MinValue;
            foreach (var part in source.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!part.enabled || part.sprite == null || part.color.a < 0.05f || part.name.StartsWith("Shadow")) continue;
                shown.Add(part);
                lowest = Mathf.Min(lowest, part.sortingOrder);
                highest = Mathf.Max(highest, part.sortingOrder);
            }
            if (shown.Count == 0) { lowest = highest = 5; }
            foreach (var part in shown)
            {
                Vector2 offset = part.transform.position - source.position;
                if (facingLeft != part.flipX) offset.x = -offset.x;
                // A violet rim: the same sprite a touch larger, behind the whole body.
                clone.Copy(part, offset, 1.14f, Rim, lowest - 1, facingLeft);
                clone.Copy(part, offset, 1f, Body, part.sortingOrder, facingLeft);
            }
            // Two slits of light where its eyes would be.
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = DungeonVisuals.Create("Clone eye", clone.transform, from, new Vector2(0.09f, 0.04f), Eye, highest + 1);
                eye.transform.localPosition = new Vector2(side * 0.1f + (facingLeft ? -0.04f : 0.04f), 0.16f);
            }
            clone.parts = clone.GetComponentsInChildren<SpriteRenderer>();
            clone.alphas = new float[clone.parts.Length];
            for (int i = 0; i < clone.parts.Length; i++) clone.alphas[i] = clone.parts[i].color.a;
            clone.SetAlpha(0f);
            ShadowstepVfx.Puff(root, from);
        }

        private void Copy(SpriteRenderer part, Vector2 offset, float scale, Color color, int order, bool facingLeft)
        {
            var copy = new GameObject(part.name).AddComponent<SpriteRenderer>();
            copy.transform.SetParent(transform, false);
            copy.transform.localPosition = offset;
            copy.transform.localScale = part.transform.lossyScale * scale;
            copy.sprite = part.sprite;
            copy.flipX = facingLeft;
            copy.color = color;
            copy.sortingOrder = order;
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                var color = parts[i].color;
                color.a = alphas[i] * alpha;
                parts[i].color = color;
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            Vector2 toward = target - from;
            Vector2 aim = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector2.right;
            if (age < lungeTime)
            {
                // Rises out of the smoke, coils back, then darts in.
                float t = age / lungeTime;
                SetAlpha(Mathf.Clamp01(t * 2.5f));
                float lunge = t < 0.5f ? -0.12f * Mathf.Sin(t * Mathf.PI * 2f) : Mathf.SmoothStep(0f, 0.35f, (t - 0.5f) * 2f);
                transform.position = from + aim * lunge;
                if (Time.frameCount % 3 == 0) HeroVfx.Sparks(root, (Vector2)transform.position + Vector2.down * 0.3f, ShadowstepVfx.Smoke, 2, 1f, 0.35f, Vector2.up, 80f, 0.8f);
                return;
            }
            if (!struck)
            {
                struck = true;
                StabVfx.Play(root, transform.position, aim, Mathf.Max(0.5f, toward.magnitude), ShadowstepVfx.Violet);
                HeroVfx.Sparks(root, target, ShadowstepVfx.Violet, 8, 3.5f, 0.25f, aim, 80f, 0.9f);
            }
            float after = age - lungeTime;
            if (after < LingerTime) return;
            float fade = 1f - (after - LingerTime) / FadeTime;
            // Dissolves upward into smoke.
            SetAlpha(Mathf.Clamp01(fade));
            transform.position = (Vector2)transform.position + Vector2.up * Time.deltaTime * 0.4f;
            if (Time.frameCount % 2 == 0) HeroVfx.Sparks(root, transform.position, ShadowstepVfx.Smoke, 3, 1.4f, 0.4f, Vector2.up, 120f, 0.9f);
            if (fade <= 0f) { ShadowstepVfx.Puff(root, transform.position); Destroy(gameObject); }
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Base for short-lived effects drawn into one <see cref="FlameMesh"/> each frame. Subclasses set up their shape,
    /// then draw it from <see cref="Draw"/> using the normalised age.
    /// </summary>
    public abstract class MeshEffect : MonoBehaviour
    {
        protected FlameMesh Mesh { get; private set; }
        protected float Age { get; private set; }
        protected float Duration { get; private set; }

        protected static T Spawn<T>(Transform root, float duration, int sortingOrder = 9) where T : MeshEffect
        {
            if (root == null) return null;
            var effect = new GameObject(typeof(T).Name).AddComponent<T>();
            effect.transform.SetParent(root, false);
            effect.Mesh = new FlameMesh(effect.gameObject, sortingOrder);
            effect.Duration = Mathf.Max(0.05f, duration);
            return effect;
        }

        /// <summary>Draws the first frame right away so the effect is visible on the frame it was cast.</summary>
        protected void Redraw()
        {
            Mesh.Begin();
            Draw(Mathf.Clamp01(Age / Duration));
            Mesh.Commit();
        }

        protected virtual void LateUpdate()
        {
            Age += Time.deltaTime;
            if (Age >= Duration) { Destroy(gameObject); return; }
            Redraw();
        }

        protected abstract void Draw(float t);

        /// <summary>A tapering stroke through <paramref name="points"/>, fading between two colours.</summary>
        protected void Stroke(Vector2[] points, int count, float startWidth, float endWidth, Color start, Color end)
        {
            for (int i = 0; i < count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1], delta = b - a;
                if (delta.sqrMagnitude < 0.000001f) continue;
                float u0 = i / (float)(count - 1), u1 = (i + 1) / (float)(count - 1);
                Vector2 n = Vector2.Perpendicular(delta.normalized);
                Vector2 n0 = n * Mathf.Lerp(startWidth, endWidth, u0) * 0.5f, n1 = n * Mathf.Lerp(startWidth, endWidth, u1) * 0.5f;
                Color c0 = Color.Lerp(start, end, u0), c1 = Color.Lerp(start, end, u1);
                Mesh.Quad(a - n0, a + n0, b + n1, b - n1, c0, c0, c1, c1);
            }
        }

        protected static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

        protected virtual void OnDestroy() { Mesh?.Release(); }
    }
}

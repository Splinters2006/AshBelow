using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Draws an Infernal Court hex bolt as pixel art in place of its sprite: a round, flickering orb that stays on the
    /// <see cref="FlameMesh.Pixel"/> grid whichever way the bolt flies, trailing pixel embers that cool from violet to
    /// crimson. It takes the sprite's colour, so a reflected bolt turns the Knight's blue. Purely visual.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PixelHexBolt : MonoBehaviour
    {
        private static readonly Color Outline = new Color(0.3f, 0.02f, 0.4f), Body = new Color(0.75f, 0.25f, 1f);
        private static readonly Color Rim = new Color(0.95f, 0.7f, 1f), Core = new Color(1f, 0.95f, 1f), Tail = new Color(1f, 0.35f, 0.5f);
        private const int TrailLength = 6;
        private const float TrailSpacing = 0.12f;
        private readonly Vector2[] trail = new Vector2[TrailLength];
        private int trailCount;
        private SpriteRenderer sprite;
        private GameObject owner;
        private FlameMesh mesh;
        private float seed;

        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            sprite.enabled = false;
            seed = Random.value * 10f;
            // The mesh lives beside the bolt rather than on it, so the bolt's rotation never tilts the pixel grid.
            owner = new GameObject("Hex bolt pixels");
            owner.transform.SetParent(transform.parent, false);
            mesh = new FlameMesh(owner, sprite.sortingOrder) { Pixelated = true };
        }

        private void LateUpdate()
        {
            Vector2 head = transform.position;
            // A new ember drops behind the bolt each time it has flown one spacing.
            if (trailCount == 0 || Vector2.Distance(trail[0], head) >= TrailSpacing)
            {
                for (int i = Mathf.Min(trailCount, TrailLength - 1); i > 0; i--) trail[i] = trail[i - 1];
                trail[0] = head;
                trailCount = Mathf.Min(trailCount + 1, TrailLength);
            }
            Color tint = sprite.color;
            mesh.Begin();
            for (int i = trailCount - 1; i >= 1; i--)
            {
                float u = i / (float)TrailLength;
                mesh.Diamond(trail[i], u < 0.4f ? 0.12f : 0.06f, Tinted(Color.Lerp(Body, Tail, u), 1f - u, tint));
            }
            // The orb throbs a pixel bigger and smaller a few times a second.
            bool swell = Mathf.Repeat(Mathf.Floor(Time.time * 10f) + seed, 2f) >= 1f;
            mesh.Disc(head, swell ? 0.26f : 0.22f, Tinted(Outline, 0.9f, tint), Tinted(Outline, 0.9f, tint));
            mesh.Disc(head, 0.18f, Tinted(Body, 1f, tint), Tinted(Body, 1f, tint));
            mesh.Disc(head, 0.1f, Tinted(Rim, 1f, tint), Tinted(Rim, 1f, tint));
            mesh.Rect(head - Vector2.one * FlameMesh.Pixel * 0.5f, head + Vector2.one * FlameMesh.Pixel * 0.5f, Tinted(Core, 1f, tint));
            mesh.Commit();
        }

        private static Color Tinted(Color color, float alpha, Color tint) => FlameMesh.Alpha(color * tint, alpha);

        private void OnDisable() { if (owner != null) owner.SetActive(false); }

        private void OnDestroy()
        {
            mesh?.Release();
            if (owner != null) Destroy(owner);
        }
    }
}

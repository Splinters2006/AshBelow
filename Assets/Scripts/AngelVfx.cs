using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Heavenly Host's angel: a haloed, white-robed angel swoops down out of the light over the ally being healed or
    /// raised, hovers there beating its wings in a golden glow, then rises back up and fades. Purely visual.
    /// </summary>
    public sealed class AngelVfx : MonoBehaviour
    {
        private const float Descend = 0.35f, Hover = 1.25f, Lifetime = 1.7f, PixelsPerUnit = 16f, Scale = 1.5f;
        private static readonly Color Glow = new Color(1f, 0.93f, 0.62f);

        // Pixel art drawn as left halves and mirrored. O/o halo, N outline, Y golden hair, S/s skin, E eye,
        // W/w robe and its shade, G gold trim.
        private static readonly string[] BodyHalf =
        {
            "..OOOOO", ".Oo....", "..OOOOO", "...NNNN", "..NYYYY", "..NYSSS", "..NYSEs", "..NYSSS", "...NSss", "...NGGG",
            "..NWWWW", ".NWWWWG", ".NwWWWG", ".NwWWWG", "NwWWWWG", "NwWWWWG", "NwWWWWG", "NwwWWWG", "NGGGGGG", ".NNNNNN",
        };
        // The left wing, attached at its top right corner. F feathers, f feather shade.
        private static readonly string[] Wing =
        {
            "......NNNN", "....NNFFFF", "...NFFFFFF", "..NFFFFFFf", ".NFFFFFFff", ".NFFFfFFf.",
            "NFFfFFFf..", "NFfFFf....", "NFfFf.....", "NfFf......", "Nff.......", ".N........",
        };
        private static Sprite bodySprite, wingSprite;
        private static Sprite BodySprite => bodySprite != null ? bodySprite : bodySprite = Build("Angel", BodyHalf, true, new Vector2(0.5f, 0f));
        private static Sprite WingSprite => wingSprite != null ? wingSprite : wingSprite = Build("Angel wing", Wing, false, new Vector2(1f, 1f));

        private FlameMesh mesh;
        private SpriteRenderer body, leftWing, rightWing;
        private Vector2 target;
        private float age;

        public static AngelVfx Play(Transform root, Vector2 target)
        {
            if (root == null) return null;
            var angel = new GameObject("Angel").AddComponent<AngelVfx>();
            angel.transform.SetParent(root, false);
            angel.target = target;
            angel.mesh = new FlameMesh(angel.gameObject, 11);
            angel.leftWing = angel.CreateLayer("Left wing", WingSprite, 12);
            angel.rightWing = angel.CreateLayer("Right wing", WingSprite, 12);
            angel.body = angel.CreateLayer("Body", BodySprite, 13);
            return angel;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            float down = 1f - Mathf.Clamp01(age / Descend), up = Mathf.Clamp01((age - Hover) / (Lifetime - Hover));
            float fade = Mathf.Clamp01(age / 0.12f) * (1f - up);
            // Swoop down with an ease, bob gently while hovering, then rise away.
            float height = 1.2f + 3.5f * down * down + 3f * up * up + 0.08f * Mathf.Sin(age * 5f);
            Vector2 feet = target + Vector2.up * height;
            float px = Scale / PixelsPerUnit;
            Vector2 shoulder = feet + Vector2.up * 10f * px;

            mesh.Begin();
            // A column of light it rides down, and a warm glow around it.
            mesh.Quad(feet + new Vector2(-0.55f, 0f), feet + new Vector2(-0.3f, 6f), feet + new Vector2(0.3f, 6f), feet + new Vector2(0.55f, 0f),
                FlameMesh.Alpha(Glow, 0.3f * fade), FlameMesh.Alpha(Glow, 0f), FlameMesh.Alpha(Glow, 0f), FlameMesh.Alpha(Glow, 0.3f * fade));
            mesh.Ellipse(shoulder, 1.6f, 1.4f, FlameMesh.Alpha(Glow, 0.4f * fade), FlameMesh.Alpha(Glow, 0f), 32);
            mesh.Ellipse(feet + Vector2.up * 19f * px, 0.45f, 0.18f, FlameMesh.Alpha(Color.white, 0.7f * fade), FlameMesh.Alpha(Glow, 0f), 20);
            // Light falling from it onto the ally below.
            mesh.Ellipse(target, 0.9f, 0.35f, FlameMesh.Alpha(Glow, 0.45f * fade * (1f - down)), FlameMesh.Alpha(Glow, 0f), 24);
            mesh.Commit();

            var tint = new Color(1f, 1f, 1f, fade);
            Place(body, feet, Scale, 0f, tint);
            // Wings beat fast on the way down and slow while hovering.
            float flap = Mathf.Sin(age * (down > 0f ? 22f : 9f));
            Place(leftWing, shoulder + Vector2.left * 1f * px, Scale * 1.5f, -10f + 25f * flap, tint);
            Place(rightWing, shoulder + Vector2.right * 1f * px, Scale * 1.5f, 10f - 25f * flap, tint, true);
        }

        private static void Place(SpriteRenderer layer, Vector2 at, float scale, float angle, Color tint, bool mirror = false)
        {
            layer.enabled = tint.a > 0f;
            layer.transform.localPosition = at;
            layer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            layer.transform.localScale = new Vector3(mirror ? -scale : scale, scale, 1f);
            layer.color = tint;
        }

        private SpriteRenderer CreateLayer(string name, Sprite sprite, int order)
        {
            var layer = new GameObject(name).AddComponent<SpriteRenderer>();
            layer.transform.SetParent(transform, false);
            layer.sprite = sprite;
            layer.sortingOrder = order;
            layer.enabled = false;
            return layer;
        }

        private static Sprite Build(string name, string[] rows, bool mirrored, Vector2 pivot)
        {
            int half = rows[0].Length, width = mirrored ? half * 2 : half, height = rows.Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[(height - y - 1) * width + x] = PixelColor(rows[y][x < half ? x : width - 1 - x]);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit);
        }

        private static Color PixelColor(char c)
        {
            switch (c)
            {
                case 'O': return new Color(1f, 0.88f, 0.35f);
                case 'o': return new Color(0.85f, 0.62f, 0.15f);
                case 'N': return new Color(0.35f, 0.28f, 0.2f);
                case 'Y': return new Color(1f, 0.8f, 0.35f);
                case 'S': return new Color(1f, 0.86f, 0.72f);
                case 's': return new Color(0.88f, 0.68f, 0.56f);
                case 'E': return new Color(0.25f, 0.45f, 0.85f);
                case 'W': return new Color(1f, 1f, 0.97f);
                case 'w': return new Color(0.8f, 0.82f, 0.9f);
                case 'G': return AbilityCatalog.Gold;
                case 'F': return new Color(1f, 1f, 1f);
                case 'f': return new Color(0.78f, 0.82f, 0.92f);
                default: return Color.clear;
            }
        }

        private void OnDestroy() { mesh?.Release(); }
    }
}

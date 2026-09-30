using UnityEngine;

namespace Slopgame
{
    /// <summary>Builds guardian sprites from mirrored half grids: a tintable shaded body layer and a fixed-colour detail layer.</summary>
    public static class BossArt
    {
        /// <summary>
        /// One unit across. Body keys (L highlight, W base, M shade, D outline) are greys for the boss's tint; with
        /// <paramref name="details"/> only the other keys are drawn, in the colours <paramref name="detailColor"/> gives them.
        /// </summary>
        public static Sprite Build(string name, string[] halfRows, bool details, System.Func<char, Color> detailColor)
        {
            int half = halfRows[0].Length, width = half * 2, height = halfRows.Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    char c = halfRows[y][x < half ? x : width - 1 - x];
                    Color? body = c == 'L' ? new Color(1f, 1f, 1f) : c == 'W' ? new Color(0.82f, 0.82f, 0.82f)
                        : c == 'M' ? new Color(0.55f, 0.55f, 0.55f) : c == 'D' ? new Color(0.08f, 0.08f, 0.08f) : (Color?)null;
                    pixels[(height - y - 1) * width + x] = details ? (body.HasValue ? Color.clear : detailColor(c)) : body ?? Color.clear;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, width);
        }

        /// <summary>Adds the fixed-colour detail layer just above a boss's body.</summary>
        public static SpriteRenderer AddDetails(Transform boss, string name, Sprite sprite)
        {
            var body = boss.GetComponent<SpriteRenderer>();
            var details = new GameObject(name).AddComponent<SpriteRenderer>();
            details.transform.SetParent(boss, false);
            details.sprite = sprite;
            details.sortingOrder = body.sortingOrder + 1;
            return details;
        }
    }
}

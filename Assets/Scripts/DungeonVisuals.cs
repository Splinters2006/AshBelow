using UnityEngine;

namespace Slopgame
{
    public static class DungeonVisuals
    {
        private static Sprite square;
        private static Sprite emberBolt;

        public static SpriteRenderer CreateEmberBolt(Transform parent, Vector2 position)
        {
            if (emberBolt == null)
            {
                const int width = 32, height = 16;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Ember bolt sprite",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        // The hot head sits at the collision position; the tail trails behind it.
                        float dx = x + 0.5f - 23f;
                        float dy = Mathf.Abs(y + 0.5f - 8f);
                        float head = Mathf.Sqrt(dx * dx + dy * dy);
                        float tail = Mathf.Clamp01((x + 0.5f - 2f) / 21f);
                        Color color = Color.clear;
                        if (head < 7f)
                            color = new Color(1f, 0.28f, 0.04f, 0.28f * (1f - head / 7f));
                        if (dx < 0f && dy < tail * 4f)
                            color = new Color(1f, 0.32f, 0.04f, tail * 0.75f);
                        if (head < 4.5f)
                            color = new Color(1f, 0.42f, 0.06f);
                        if (dx < 0f && dy < tail * 1.8f)
                            color = new Color(1f, 0.66f, 0.12f, tail);
                        if (head < 3f)
                            color = new Color(1f, 0.8f, 0.3f);
                        if (head < 1.8f)
                            color = new Color(1f, 0.97f, 0.78f);
                        pixels[y * width + x] = color;
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                emberBolt = Sprite.Create(texture, new Rect(0, 0, width, height),
                    new Vector2(23f / width, 0.5f), 40f);
            }
            var renderer = Create("Ember bolt", parent, position, Vector2.one, Color.white, 6);
            renderer.sprite = emberBolt;
            return renderer;
        }

        public static SpriteRenderer Create(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order)
        {
            if (square == null)
            {
                var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                square = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            }
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = size;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        public static void DrawMap(DungeonMap map, Transform root)
        {
            for (int x = 0; x < DungeonMap.Width; x++)
                for (int y = 0; y < DungeonMap.Height; y++)
                {
                    bool walkable = map.IsFloor(x, y);
                    if (!walkable && !map.IsFloor(x - 1, y) && !map.IsFloor(x + 1, y)
                        && !map.IsFloor(x, y - 1) && !map.IsFloor(x, y + 1)) continue;
                    Color color = walkable
                        ? ((x + y) % 2 == 0 ? new Color(0.12f, 0.17f, 0.21f) : new Color(0.14f, 0.19f, 0.23f))
                        : new Color(0.29f, 0.38f, 0.43f);
                    Create(walkable ? "Floor" : "Wall", root, new Vector2(x, y), Vector2.one * 0.97f, color, 0);
                }
        }
    }
}

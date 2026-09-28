using UnityEngine;

namespace Slopgame
{
    public static class DungeonVisuals
    {
        private static Sprite square;
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

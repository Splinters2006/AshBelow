using System;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonMap
    {
        public const int Width = 55;
        public const int Height = 39;
        private readonly bool[,] floor = new bool[Width, Height];
        public List<Vector2Int> Centers { get; } = new List<Vector2Int>();

        public DungeonMap(int seed, bool bossArena = false)
        {
            if (bossArena)
            {
                for (int x = 14; x <= 40; x++)
                    for (int y = 8; y <= 30; y++) floor[x, y] = true;
                Centers.Add(new Vector2Int(27, 14));
                Centers.Add(new Vector2Int(27, 22));
                return;
            }
            var random = new System.Random(seed);
            var rooms = new List<RectInt>();
            for (int attempt = 0; attempt < 160 && rooms.Count < 9; attempt++)
            {
                int w = random.Next(6, 11), h = random.Next(5, 9);
                var room = new RectInt(random.Next(2, Width - w - 2), random.Next(2, Height - h - 2), w, h);
                var padding = new RectInt(room.x - 2, room.y - 2, w + 4, h + 4);
                if (rooms.Exists(other => padding.Overlaps(other))) continue;
                for (int x = room.xMin; x < room.xMax; x++)
                    for (int y = room.yMin; y < room.yMax; y++) floor[x, y] = true;
                var center = new Vector2Int(room.x + w / 2, room.y + h / 2);
                if (Centers.Count > 0)
                {
                    var cursor = Centers[Centers.Count - 1];
                    while (cursor.x != center.x) { Carve(cursor); cursor.x += Math.Sign(center.x - cursor.x); }
                    while (cursor.y != center.y) { Carve(cursor); cursor.y += Math.Sign(center.y - cursor.y); }
                    Carve(cursor);
                }
                rooms.Add(room);
                Centers.Add(center);
            }
        }

        private void Carve(Vector2Int p)
        {
            floor[p.x, p.y] = true;
            floor[p.x + 1, p.y] = true;
        }

        public bool IsFloor(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && floor[x, y];

        public bool CanStand(Vector2 p, float radius = 0.28f)
        {
            return IsFloor(Mathf.RoundToInt(p.x - radius), Mathf.RoundToInt(p.y - radius))
                && IsFloor(Mathf.RoundToInt(p.x + radius), Mathf.RoundToInt(p.y - radius))
                && IsFloor(Mathf.RoundToInt(p.x - radius), Mathf.RoundToInt(p.y + radius))
                && IsFloor(Mathf.RoundToInt(p.x + radius), Mathf.RoundToInt(p.y + radius));
        }

        public Vector2 Move(Vector2 position, Vector2 delta, float radius = 0.28f)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.15f));
            delta /= steps;
            for (int i = 0; i < steps; i++)
            {
                if (CanStand(position + new Vector2(delta.x, 0), radius)) position.x += delta.x;
                if (CanStand(position + new Vector2(0, delta.y), radius)) position.y += delta.y;
            }
            return position;
        }
    }
}

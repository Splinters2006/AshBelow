using System;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>How a world carves its floors; each world in <see cref="WorldCatalog"/> picks one.</summary>
    public enum MapLayout { Dungeon, CityBlocks, Caverns, Chambers, Alleys, Clearings }

    public sealed class DungeonMap
    {
        public const int Width = 55;
        public const int Height = 39;
        private readonly bool[,] floor = new bool[Width, Height];
        public List<Vector2Int> Centers { get; } = new List<Vector2Int>();
        /// <summary>The walkable cells of a boss floor.</summary>
        public static readonly RectInt Arena = new RectInt(14, 8, 27, 23);

        /// <summary>Keeps a point inside the boss arena, at least <paramref name="margin"/> away from its walls.</summary>
        public static Vector2 ClampToArena(Vector2 point, float margin) => new Vector2(
            Mathf.Clamp(point.x, Arena.xMin - 0.5f + margin, Arena.xMax - 0.5f - margin),
            Mathf.Clamp(point.y, Arena.yMin - 0.5f + margin, Arena.yMax - 0.5f - margin));

        /// <summary>The walkable cells of the crystal shop visited before each boss.</summary>
        public static readonly RectInt ShopRoom = new RectInt(19, 13, 17, 11);
        /// <summary>The merchant's counter: solid cells along the shop's back wall.</summary>
        public static readonly RectInt ShopCounter = new RectInt(25, 22, 5, 2);

        /// <summary>The crystal shop: one room, the hero entering on the left and the stairs down to the guardian on the right.</summary>
        public static DungeonMap Shop()
        {
            var map = new DungeonMap();
            for (int x = ShopRoom.xMin; x < ShopRoom.xMax; x++)
                for (int y = ShopRoom.yMin; y < ShopRoom.yMax; y++) map.floor[x, y] = !ShopCounter.Contains(new Vector2Int(x, y));
            map.Centers.Add(new Vector2Int(ShopRoom.xMin + 2, 17));
            map.Centers.Add(new Vector2Int(ShopRoom.xMax - 3, 17));
            return map;
        }

        private DungeonMap() { }

        public DungeonMap(int seed, bool bossArena = false, MapLayout layout = MapLayout.Dungeon)
        {
            if (bossArena)
            {
                for (int x = Arena.xMin; x < Arena.xMax; x++)
                    for (int y = Arena.yMin; y < Arena.yMax; y++) floor[x, y] = true;
                Centers.Add(new Vector2Int(27, 14));
                Centers.Add(new Vector2Int(27, 22));
                return;
            }
            var random = new System.Random(seed);
            switch (layout)
            {
                case MapLayout.CityBlocks: GenerateCityBlocks(random); break;
                case MapLayout.Caverns: GenerateCaverns(random); break;
                case MapLayout.Chambers: GenerateChambers(random); break;
                case MapLayout.Alleys: GenerateAlleys(random); break;
                case MapLayout.Clearings: GenerateClearings(random); break;
                default: GenerateDungeon(random); break;
            }
            Finish();
        }

        /// <summary>The Ash Below: square rooms joined by narrow corridors.</summary>
        private void GenerateDungeon(System.Random random)
        {
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

        /// <summary>The Neon Arcology: one open district of streets and plazas broken up by solid tower blocks.</summary>
        private void GenerateCityBlocks(System.Random random)
        {
            FillRect(new RectInt(2, 2, Width - 4, Height - 4));
            PickCenters(random, 8, 9f, 4);
            var blocks = new List<RectInt>();
            for (int attempt = 0; attempt < 900 && blocks.Count < 40; attempt++)
            {
                int w = random.Next(3, 8), h = random.Next(3, 7);
                var block = new RectInt(random.Next(4, Width - w - 4), random.Next(4, Height - h - 4), w, h);
                // Two-wide streets between towers, and every room keeps an open plaza around its centre.
                var street = new RectInt(block.x - 2, block.y - 2, w + 4, h + 4);
                if (blocks.Exists(other => street.Overlaps(other)) || Centers.Exists(c => NearRect(block, c, 3))) continue;
                blocks.Add(block);
                FillRect(block, false);
            }
        }

        /// <summary>The Infernal Court: organic caves grown by a cellular automaton.</summary>
        private void GenerateCaverns(System.Random random)
        {
            for (int x = 2; x < Width - 2; x++)
                for (int y = 2; y < Height - 2; y++) floor[x, y] = random.NextDouble() > 0.46;
            var next = new bool[Width, Height];
            for (int pass = 0; pass < 4; pass++)
            {
                for (int x = 2; x < Width - 2; x++)
                    for (int y = 2; y < Height - 2; y++)
                    {
                        // Rock wins where most of the 3x3 block around a cell is already rock.
                        int walls = 0;
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++) if (!floor[x + dx, y + dy]) walls++;
                        next[x, y] = walls < 5;
                    }
                Array.Copy(next, floor, next.Length);
            }
            PickCenters(random, 8, 9f, 2, requireFloor: true);
            foreach (var center in Centers) FillCircle(center, 2.6f);
            ChainCenters(random, winding: true);
        }

        /// <summary>The Arcane Spire: round chambers linked by straight diagonal passages.</summary>
        private void GenerateChambers(System.Random random)
        {
            var radii = new List<float>();
            for (int attempt = 0; attempt < 300 && Centers.Count < 8; attempt++)
            {
                float radius = random.Next(3, 6) + 0.4f;
                int r = Mathf.CeilToInt(radius);
                var center = new Vector2Int(random.Next(2 + r, Width - 2 - r), random.Next(2 + r, Height - 2 - r));
                bool overlaps = false;
                for (int i = 0; i < Centers.Count; i++)
                    if (Vector2Int.Distance(center, Centers[i]) < radius + radii[i] + 3f) { overlaps = true; break; }
                if (overlaps) continue;
                Centers.Add(center);
                radii.Add(radius);
                FillCircle(center, radius);
                // Some chambers keep a ring of arcane pillars.
                if (radius > 4f && random.Next(2) == 0)
                    for (int k = 0; k < 6; k++)
                    {
                        float angle = k * Mathf.PI / 3f;
                        floor[center.x + Mathf.RoundToInt(Mathf.Cos(angle) * (radius - 1.6f)), center.y + Mathf.RoundToInt(Mathf.Sin(angle) * (radius - 1.6f))] = false;
                    }
            }
            for (int i = 1; i < Centers.Count; i++) Line(Centers[i - 1], Centers[i], 1);
            // One extra passage makes a loop through the spire.
            if (Centers.Count > 3) Line(Centers[0], Centers[random.Next(2, Centers.Count)], 1);
        }

        /// <summary>The Shadow Market: small squares tied together by narrow, winding alleys.</summary>
        private void GenerateAlleys(System.Random random)
        {
            PickCenters(random, 9, 8f, 4);
            foreach (var center in Centers)
                FillRect(new RectInt(center.x - random.Next(2, 4), center.y - random.Next(2, 3), random.Next(5, 8), random.Next(5, 7)));
            ChainCenters(random, winding: true);
            // Dead-end side alleys for the cutpurses to lurk in.
            for (int i = 0; i < 6; i++)
            {
                var cursor = Centers[random.Next(Centers.Count)];
                for (int step = 0; step < 30; step++)
                {
                    cursor += Steps[random.Next(4)];
                    cursor.x = Mathf.Clamp(cursor.x, 3, Width - 4);
                    cursor.y = Mathf.Clamp(cursor.y, 3, Height - 4);
                    floor[cursor.x, cursor.y] = true;
                }
            }
        }

        /// <summary>The Savage Wilds: ragged clearings joined by wide game trails, with trees growing in the open.</summary>
        private void GenerateClearings(System.Random random)
        {
            PickCenters(random, 8, 10f, 6);
            foreach (var center in Centers)
            {
                float radius = random.Next(4, 6);
                for (int x = center.x - 7; x <= center.x + 7; x++)
                    for (int y = center.y - 7; y <= center.y + 7; y++)
                    {
                        if (!Interior(x, y)) continue;
                        float ragged = radius + (float)random.NextDouble() * 1.6f - 0.8f;
                        if (Vector2.Distance(new Vector2(x, y), center) <= ragged) floor[x, y] = true;
                    }
            }
            ChainCenters(random, winding: true, wide: true);
            for (int i = 0; i < 40; i++)
            {
                int x = random.Next(3, Width - 3), y = random.Next(3, Height - 3);
                if (floor[x, y] && !Centers.Exists(c => Mathf.Abs(c.x - x) <= 2 && Mathf.Abs(c.y - y) <= 2)
                    && floor[x - 1, y] && floor[x + 1, y] && floor[x, y - 1] && floor[x, y + 1]) floor[x, y] = false;
            }
        }

        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        private static bool Interior(int x, int y) => x >= 2 && y >= 2 && x < Width - 2 && y < Height - 2;

        private static bool NearRect(RectInt rect, Vector2Int point, int margin) =>
            point.x >= rect.xMin - margin && point.x < rect.xMax + margin && point.y >= rect.yMin - margin && point.y < rect.yMax + margin;

        private void FillRect(RectInt rect, bool value = true)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
                for (int y = rect.yMin; y < rect.yMax; y++)
                    if (Interior(x, y)) floor[x, y] = value;
        }

        private void FillCircle(Vector2Int center, float radius)
        {
            int r = Mathf.CeilToInt(radius);
            for (int x = center.x - r; x <= center.x + r; x++)
                for (int y = center.y - r; y <= center.y + r; y++)
                    if (Interior(x, y) && (x - center.x) * (x - center.x) + (y - center.y) * (y - center.y) <= radius * radius) floor[x, y] = true;
        }

        /// <summary>A straight passage, <paramref name="halfWidth"/> cells either side of the line.</summary>
        private void Line(Vector2Int from, Vector2Int to, int halfWidth)
        {
            int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2Int.RoundToInt(Vector2.Lerp(from, to, steps == 0 ? 0f : i / (float)steps));
                FillRect(new RectInt(p.x - halfWidth + 1, p.y - halfWidth + 1, halfWidth * 2, halfWidth * 2));
            }
        }

        /// <summary>Joins each room to the next with a trail that wanders but always closes in on its goal.</summary>
        private void ChainCenters(System.Random random, bool winding, bool wide = false)
        {
            for (int i = 1; i < Centers.Count; i++)
            {
                var cursor = Centers[i - 1];
                var goal = Centers[i];
                for (int guard = 0; cursor != goal && guard < 400; guard++)
                {
                    FillRect(new RectInt(cursor.x, cursor.y, wide ? 3 : 2, wide ? 3 : 2));
                    if (winding && random.Next(3) == 0) cursor += Steps[random.Next(4)];
                    else if (cursor.x != goal.x && (cursor.y == goal.y || random.Next(2) == 0)) cursor.x += Math.Sign(goal.x - cursor.x);
                    else cursor.y += Math.Sign(goal.y - cursor.y);
                    cursor.x = Mathf.Clamp(cursor.x, 2, Width - 3);
                    cursor.y = Mathf.Clamp(cursor.y, 2, Height - 3);
                }
            }
        }

        /// <summary>
        /// Spreads <paramref name="count"/> room centres at least <paramref name="spacing"/> apart. The first sits on the
        /// left of the map and the last on the right, so the stairs are always a trek away.
        /// </summary>
        private void PickCenters(System.Random random, int count, float spacing, int margin, bool requireFloor = false)
        {
            var picked = new List<Vector2Int>();
            for (int attempt = 0; attempt < 2000 && picked.Count < count; attempt++)
            {
                var point = new Vector2Int(random.Next(margin, Width - margin - 1), random.Next(margin, Height - margin - 1));
                if (picked.Count == 0) point.x = random.Next(margin, margin + 6);
                else if (picked.Count == count - 1) point.x = random.Next(Width - margin - 7, Width - margin - 1);
                if (requireFloor && attempt < 1500 && !floor[point.x, point.y]) continue;
                if (picked.Exists(other => Vector2Int.Distance(other, point) < spacing)) continue;
                picked.Add(point);
            }
            Centers.AddRange(picked);
        }

        /// <summary>
        /// Shared clean-up for every layout: clears a spawn pad at each room centre, links any room the layout left cut off,
        /// and walls in stray pockets no hero could reach.
        /// </summary>
        private void Finish()
        {
            foreach (var center in Centers) FillRect(new RectInt(center.x - 1, center.y - 1, 4, 4));
            var reached = Flood(Centers[0]);
            for (int guard = 0; guard < Centers.Count; guard++)
            {
                int lost = Centers.FindIndex(c => !reached[c.x, c.y]);
                if (lost < 0) break;
                // Tunnel from the nearest reachable room.
                var from = Centers[0];
                foreach (var center in Centers)
                    if (reached[center.x, center.y] && Vector2Int.Distance(center, Centers[lost]) < Vector2Int.Distance(from, Centers[lost])) from = center;
                var cursor = from;
                var goal = Centers[lost];
                while (cursor.x != goal.x) { Carve(cursor); cursor.x += Math.Sign(goal.x - cursor.x); }
                while (cursor.y != goal.y) { Carve(cursor); cursor.y += Math.Sign(goal.y - cursor.y); }
                Carve(cursor);
                reached = Flood(Centers[0]);
            }
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (!reached[x, y]) floor[x, y] = false;
        }

        private bool[,] Flood(Vector2Int start)
        {
            var reached = new bool[Width, Height];
            var queue = new Queue<Vector2Int>();
            reached[start.x, start.y] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var step in Steps)
                {
                    var next = cell + step;
                    if (IsFloor(next.x, next.y) && !reached[next.x, next.y]) { reached[next.x, next.y] = true; queue.Enqueue(next); }
                }
            }
            return reached;
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

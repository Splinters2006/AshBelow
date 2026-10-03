using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The floor's minimap. Cells are revealed as heroes see them, and an enemy appears (at its live position) once any
    /// hero has had line of sight to it. Every co-op machine knows where all heroes are, so the whole party shares one
    /// map without extra messages. Everything resets when a new floor is built.
    /// </summary>
    public sealed class FloorMinimap : MonoBehaviour
    {
        public const float SightRadius = 8f, SpotRadius = 11f, ScanInterval = 0.12f;
        private static readonly Color Lava = new Color(0.9f, 0.35f, 0.08f, 0.95f);
        private static readonly Color Unknown = Color.clear, Floor = new Color(0.2f, 0.27f, 0.32f, 0.95f), Wall = new Color(0.45f, 0.56f, 0.62f, 0.95f);
        public DungeonRun Run { get; set; }
        private readonly bool[,] explored = new bool[DungeonMap.Width, DungeonMap.Height];
        private readonly HashSet<DungeonEnemy> spotted = new HashSet<DungeonEnemy>();
        private readonly List<Vector2> heroes = new List<Vector2>();
        private DungeonMap map;
        private Texture2D texture;
        private Color[] pixels;
        private bool dirty;
        private float nextScan;

        public bool IsExplored(Vector2Int cell) => map != null && cell.x >= 0 && cell.y >= 0 && cell.x < DungeonMap.Width && cell.y < DungeonMap.Height && explored[cell.x, cell.y];
        public bool IsSpotted(DungeonEnemy enemy) => enemy != null && spotted.Contains(enemy);

        private void Update()
        {
            if (Run == null || Run.Map == null || Run.Player == null || Run.IsInMainMenu) return;
            if (Run.Map != map) Reset(Run.Map);
            if (!Run.IsPlaying || Time.time < nextScan) return;
            nextScan = Time.time + ScanInterval;
            Scan();
        }

        private void Reset(DungeonMap next)
        {
            map = next;
            System.Array.Clear(explored, 0, explored.Length);
            spotted.Clear();
            dirty = true;
            nextScan = 0f;
        }

        /// <summary>Reveals what every living hero can see right now. Public so tests can force a scan.</summary>
        public void Scan()
        {
            if (Run.Map != map) Reset(Run.Map);
            heroes.Clear();
            if (Run.Player.Health > 0) heroes.Add(Run.Player.transform.position);
            if (Run.IsNetworked)
                foreach (var hero in Run.Coop.RemoteHeroes)
                    if (hero != null && hero.IsAlive) heroes.Add(hero.transform.position);
            foreach (var hero in heroes)
            {
                var center = Vector2Int.RoundToInt(hero);
                int reach = Mathf.CeilToInt(SightRadius);
                for (int x = center.x - reach; x <= center.x + reach; x++)
                    for (int y = center.y - reach; y <= center.y + reach; y++)
                    {
                        if (x < 0 || y < 0 || x >= DungeonMap.Width || y >= DungeonMap.Height || explored[x, y] || !map.IsFloor(x, y)) continue;
                        var cell = new Vector2(x, y);
                        if (Vector2.Distance(cell, hero) > SightRadius || !Run.HasLineOfSight(hero, cell)) continue;
                        explored[x, y] = true;
                        dirty = true;
                    }
                foreach (var enemy in Run.Enemies)
                {
                    if (enemy == null || spotted.Contains(enemy)) continue;
                    Vector2 position = enemy.transform.position;
                    if (Vector2.Distance(position, hero) <= SpotRadius && Run.HasLineOfSight(hero, position)) spotted.Add(enemy);
                }
            }
            spotted.RemoveWhere(enemy => enemy == null);
        }

        /// <summary>Draws the map into a HUD rect (inside the scaled IMGUI space).</summary>
        public void Draw(Rect rect)
        {
            if (map == null) return;
            UpdateTexture();
            DungeonUi.Panel(new Rect(rect.x - 6, rect.y - 6, rect.width + 12, rect.height + 12), new Color(0.02f, 0.03f, 0.05f, 0.82f));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            float cellWidth = rect.width / DungeonMap.Width, cellHeight = rect.height / DungeonMap.Height;
            Vector2 ToMap(Vector2 world) => new Vector2(rect.x + (world.x + 0.5f) * cellWidth, rect.yMax - (world.y + 0.5f) * cellHeight);
            void Dot(Vector2 world, float size, Color color)
            {
                Vector2 point = ToMap(world);
                DungeonUi.Panel(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), color);
            }
            var stairs = map.Centers[map.Centers.Count - 1];
            if (IsExplored(stairs) && !Run.IsTesting)
                Dot(stairs, 7f, Run.HostileCount == 0 && Run.Artifact == null && !Run.WavesPending ? AbilityCatalog.Gold : new Color(0.55f, 0.45f, 0.25f));
            if (Run.Artifact != null) Dot(Run.Artifact.transform.position, 7f, AbilityCatalog.Gold);
            float blink = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var enemy in Run.Enemies)
            {
                if (!IsSpotted(enemy) || enemy.Health <= 0) continue;
                var color = enemy.Boss != null ? new Color(1f, 0.25f, 0.55f) : enemy.IsTank ? new Color(0.75f, 0.8f, 0.9f)
                    : enemy.IsRanged ? new Color(1f, 0.65f, 0.2f) : new Color(1f, 0.3f, 0.35f);
                color.a = blink;
                Dot(enemy.Boss != null ? enemy.Boss.Behaviour.GroundPosition : (Vector2)enemy.transform.position, enemy.Boss != null ? 9f : 5f, color);
            }
            if (Run.IsNetworked)
                foreach (var hero in Run.Coop.RemoteHeroes)
                    if (hero != null) Dot(hero.transform.position, 6f, hero.IsAlive ? hero.Character.Color : DungeonUi.Muted);
            Dot(Run.Player.transform.position, 7f, Color.white);
            Dot(Run.Player.transform.position, 4f, Run.SelectedCharacter.Color);
        }

        private void UpdateTexture()
        {
            if (texture == null)
            {
                texture = new Texture2D(DungeonMap.Width, DungeonMap.Height, TextureFormat.RGBA32, false)
                { name = "Minimap", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                pixels = new Color[DungeonMap.Width * DungeonMap.Height];
                dirty = true;
            }
            if (!dirty) return;
            dirty = false;
            for (int x = 0; x < DungeonMap.Width; x++)
                for (int y = 0; y < DungeonMap.Height; y++)
                    pixels[y * DungeonMap.Width + x] = explored[x, y] ? (map.IsLava(x, y) ? Lava : Floor) : BordersExplored(x, y) ? Wall : Unknown;
            texture.SetPixels(pixels);
            texture.Apply(false);
        }

        // Walls show once a floor cell next to them has been seen.
        private bool BordersExplored(int x, int y)
        {
            if (map.IsFloor(x, y)) return false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (IsExplored(new Vector2Int(x + dx, y + dy))) return true;
            return false;
        }

        private void OnDestroy() { if (texture != null) Destroy(texture); }
    }
}

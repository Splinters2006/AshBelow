using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Infernal Court's lava pools: glowing, churning tiles that burn a hero standing in them once a second (a
    /// dodge roll crosses safely). The court's fire-born enemies wade through unharmed. Each co-op machine judges only
    /// its own hero; the pools come from the seeded map, so everyone sees the same ones.
    /// </summary>
    public sealed class LavaField : MonoBehaviour
    {
        public static readonly Color Deep = new Color(0.55f, 0.08f, 0.02f), Hot = new Color(1f, 0.45f, 0.08f), Crust = new Color(0.3f, 0.06f, 0.04f);
        private const float BubbleInterval = 0.12f, BubbleRange = 11f;
        private DungeonRun run;
        private DungeonMap map;
        private readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();
        private readonly List<Vector2Int> cells = new List<Vector2Int>();
        private readonly List<float> phases = new List<float>();
        private readonly List<bool> edges = new List<bool>();
        private float nextBubble;

        public static LavaField Create(DungeonRun run, Transform parent, DungeonMap map)
        {
            var field = new GameObject("Lava").AddComponent<LavaField>();
            field.transform.SetParent(parent, false);
            field.run = run;
            field.map = map;
            for (int x = 0; x < DungeonMap.Width; x++)
                for (int y = 0; y < DungeonMap.Height; y++)
                {
                    if (!map.IsLava(x, y)) continue;
                    field.cells.Add(new Vector2Int(x, y));
                    field.tiles.Add(DungeonVisuals.Create("Lava tile", field.transform, new Vector2(x, y), Vector2.one, Deep, 1));
                    field.phases.Add(FlameMesh.Hash(x, y) * Mathf.PI * 2f);
                    // Cells on the pool's rim cool into a darker crust.
                    field.edges.Add(!map.IsLava(x + 1, y) || !map.IsLava(x - 1, y) || !map.IsLava(x, y + 1) || !map.IsLava(x, y - 1));
                }
            return field;
        }

        private void Update()
        {
            float time = Time.time;
            for (int i = 0; i < tiles.Count; i++)
            {
                // Slow waves of heat roll across the surface.
                var cell = cells[i];
                float heat = 0.5f + 0.5f * Mathf.Sin(time * 1.6f + cell.x * 0.7f + cell.y * 0.45f + phases[i]);
                var color = Color.Lerp(Deep, Hot, heat * (edges[i] ? 0.45f : 1f));
                tiles[i].color = edges[i] ? Color.Lerp(color, Crust, 0.35f) : color;
            }
            if (run == null || !run.IsPlaying || run.Player == null) return;
            var hero = run.Player;
            Vector2 position = hero.transform.position;
            if (time >= nextBubble && cells.Count > 0)
            {
                nextBubble = time + BubbleInterval;
                var cell = cells[Random.Range(0, cells.Count)];
                if (Vector2.Distance(cell, position) <= BubbleRange)
                    HeroVfx.Sparks(run.ProjectileRoot, (Vector2)cell + Random.insideUnitCircle * 0.3f, Hot, 3, 1.4f, 0.4f, Vector2.up, 50f, 0.8f);
            }
            if (hero.Health <= 0 || !map.IsLava(position)) return;
            int before = hero.Health;
            hero.Burn();
            if (hero.Health != before) HeroVfx.Sparks(run.ProjectileRoot, position, Hot, 12, 4f, 0.4f, Vector2.up, 120f);
        }
    }
}

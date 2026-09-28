using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonRun : MonoBehaviour
    {
        public DungeonMap Map { get; private set; }
        public DungeonPlayer Player { get; private set; }
        public List<DungeonEnemy> Enemies { get; } = new List<DungeonEnemy>();
        public bool IsPlaying { get; private set; }
        public bool ChoosingUpgrade { get; private set; }
        public int Floor { get; private set; }
        public int Kills { get; set; }
        public int Seed { get; private set; }
        public Camera View => view;
        private Transform level;
        private Camera view;
        private Vector2 exit;
        private SpriteRenderer stairs;
        private readonly int[,] distances = new int[DungeonMap.Width, DungeonMap.Height];
        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private Vector2Int lastPlayerCell = new Vector2Int(-1, -1);

        private void Start()
        {
            view = Camera.main;
            if (view == null) view = new GameObject("Dungeon Camera", typeof(Camera)).GetComponent<Camera>();
            view.orthographic = true;
            view.orthographicSize = 8;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.035f, 0.055f, 0.08f);
            gameObject.AddComponent<DungeonHud>().Run = this;
            Restart();
        }

        public void Restart()
        {
            if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); }
            Seed = UnityEngine.Random.Range(0, 1000000);
            Floor = 0;
            Kills = 0;
            Player = DungeonVisuals.Create("Delver", transform, Vector2.zero, Vector2.one * 0.65f,
                new Color(0.35f, 0.95f, 0.8f), 4).gameObject.AddComponent<DungeonPlayer>();
            Player.Run = this;
            NextFloor();
        }

        private void NextFloor()
        {
            if (level != null) { level.gameObject.SetActive(false); Destroy(level.gameObject); }
            Enemies.Clear();
            Floor++;
            Map = new DungeonMap(Seed + Floor * 7919);
            level = new GameObject("Floor " + Floor).transform;
            level.SetParent(transform);
            DungeonVisuals.DrawMap(Map, level);
            Player.transform.position = (Vector2)Map.Centers[0];
            exit = Map.Centers[Map.Centers.Count - 1];
            stairs = DungeonVisuals.Create("Stairs", level, exit, Vector2.one * 0.85f, Color.gray, 1);
            for (int room = 1; room < Map.Centers.Count; room++)
            {
                int count = Mathf.Min(4, 1 + Floor);
                for (int i = 0; i < count; i++)
                {
                    Vector2 position = (Vector2)Map.Centers[room] + new Vector2(i % 2, i / 2);
                    var enemy = DungeonVisuals.Create("Ashling", level, position, Vector2.one * 0.6f,
                        new Color(1f, 0.35f, 0.4f), 3).gameObject.AddComponent<DungeonEnemy>();
                    enemy.Run = this;
                    enemy.Health = 2 + (Floor - 1) / 2;
                    enemy.Speed = Mathf.Min(3.6f, 1.8f + Floor * 0.12f);
                    if (i == 1) enemy.gameObject.AddComponent<EnemyShooter>();
                    Enemies.Add(enemy);
                }
            }
            lastPlayerCell = new Vector2Int(-1, -1);
            ChoosingUpgrade = false;
            IsPlaying = true;
            view.transform.position = new Vector3(Player.transform.position.x, Player.transform.position.y, -10);
            UpdatePaths();
        }

        private void Update()
        {
            if (!IsPlaying) return;
            UpdatePaths();
            stairs.color = Enemies.Count == 0 ? new Color(1f, 0.8f, 0.25f) : new Color(0.4f, 0.4f, 0.4f);
            if (Enemies.Count == 0 && Vector2.Distance(Player.transform.position, exit) < 1.2f && PlayerInput.Interact)
            {
                IsPlaying = false;
                ChoosingUpgrade = true;
            }
        }

        private void LateUpdate()
        {
            if (Player == null) return;
            var target = new Vector3(Player.transform.position.x, Player.transform.position.y, -10);
            view.transform.position = Vector3.Lerp(view.transform.position, target, 1 - Mathf.Exp(-10 * Time.deltaTime));
        }

        public void EndRun() { IsPlaying = false; }
        public void ChooseUpgrade(int choice) { Player.Upgrade(choice); NextFloor(); }

        public bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) * 5));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, (float)i / steps);
                if (!Map.IsFloor(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y))) return false;
            }
            return true;
        }

        private void UpdatePaths()
        {
            Vector2Int cell = Vector2Int.RoundToInt(Player.transform.position);
            if (cell == lastPlayerCell) return;
            lastPlayerCell = cell;
            for (int x = 0; x < DungeonMap.Width; x++)
                for (int y = 0; y < DungeonMap.Height; y++) distances[x, y] = int.MaxValue;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(cell);
            distances[cell.x, cell.y] = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var step in Steps)
                {
                    var next = current + step;
                    if (!Map.IsFloor(next.x, next.y) || distances[next.x, next.y] != int.MaxValue) continue;
                    distances[next.x, next.y] = distances[current.x, current.y] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        public Vector2 DirectionToPlayer(Vector2 position)
        {
            var cell = Vector2Int.RoundToInt(position);
            var best = cell;
            foreach (var step in Steps)
            {
                var next = cell + step;
                if (Map.IsFloor(next.x, next.y) && distances[next.x, next.y] < distances[best.x, best.y]) best = next;
            }
            return ((Vector2)best - position).normalized;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    public static class DungeonProjectSetup
    {
        private static double smokeStarted;
        private static bool checkedProgression;
        private static bool smokeFailed;
        private static int combatStage;
        private static float combatResumeAt;
        private static int combatHealth;

        [InitializeOnLoadMethod]
        private static void ResumeSmokeTest()
        {
            if (!SessionState.GetBool("SlopgameSmoke", false)) return;
            smokeStarted = EditorApplication.timeSinceStartup;
            Application.logMessageReceived += CaptureError;
            EditorApplication.update += CheckPlayMode;
        }

        public static void SmokeTest()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            smokeStarted = EditorApplication.timeSinceStartup;
            checkedProgression = false;
            smokeFailed = false;
            combatStage = 0;
            SessionState.SetBool("SlopgameSmoke", true);
            Application.logMessageReceived += CaptureError;
            EditorApplication.update += CheckPlayMode;
            EditorApplication.EnterPlaymode();
        }

        private static void CaptureError(string message, string stack, LogType type)
        {
            // Unity 6000.6 can throw while initializing its search index in batch mode.
            // Keep the original error in the log, but do not count editor search as gameplay.
            if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) smokeFailed = true;
        }

        private static void CheckPlayMode()
        {
            if (EditorApplication.timeSinceStartup - smokeStarted > 90)
            {
                Debug.LogError("Play mode smoke test timed out.");
                SessionState.SetBool("SlopgameSmoke", false);
                EditorApplication.Exit(1);
                return;
            }
            if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 2) return;
            try
            {
                var run = UnityEngine.Object.FindFirstObjectByType<DungeonRun>();
                if (run == null || run.Player == null || run.Enemies.Count == 0) throw new Exception("Run failed to initialize.");
                if (!checkedProgression)
                {
                    if (!CheckCombat(run)) return;
                    while (run.Enemies.Count > 0) run.Enemies[0].Hit(1000);
                    if (run.Kills == 0) throw new Exception("Kills were not recorded.");
                    run.ChooseUpgrade(1);
                    if (run.Floor != 2 || run.Player.MaxHealth != 8 || run.Enemies.Count == 0) throw new Exception("Floor progression failed.");
                    run.EndRun();
                    if (run.IsPlaying) throw new Exception("Death did not stop gameplay.");
                    run.Restart();
                    checkedProgression = true;
                    return;
                }
                if (run.Floor != 1 || run.Kills != 0 || run.Player.MaxHealth != 6) throw new Exception("Restart failed to reset the run.");
                if (UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length != 0)
                    throw new Exception("Projectiles survived a floor change/restart.");
                Debug.Log(smokeFailed ? "SLOPGAME_SMOKE_FAILED" : "SLOPGAME_SMOKE_OK: Cone hits, ranged contact immunity, projectile damage/walls, dodge immunity/expiry, progression, and restart passed.");
                EditorApplication.update -= CheckPlayMode;
                SessionState.SetBool("SlopgameSmoke", false);
                Application.logMessageReceived -= CaptureError;
                EditorApplication.Exit(smokeFailed ? 1 : 0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool("SlopgameSmoke", false);
                EditorApplication.Exit(1);
            }
        }

        private static bool CheckCombat(DungeonRun run)
        {
            var player = run.Player;
            if (combatStage == 0)
            {
                Vector2 origin = run.Map.Centers[0];
                player.transform.position = origin;
                var offsets = new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.right * 2f };
                if (run.Enemies.Count < offsets.Length) throw new Exception("Insufficient combat test targets.");
                foreach (var enemy in run.Enemies)
                {
                    enemy.enabled = false;
                    var shooter = enemy.GetComponent<EnemyShooter>();
                    if (shooter != null) shooter.enabled = false;
                }
                for (int i = 0; i < offsets.Length; i++)
                {
                    run.Enemies[i].transform.position = origin + offsets[i];
                    run.Enemies[i].Health = 10;
                }
                var sword = player.GetComponent<SwordAttack>();
                if (!sword.TryAttack(Vector2.right)) throw new Exception("Sword did not attack.");
                for (int i = 0; i < offsets.Length; i++)
                    if (run.Enemies[i].Health != (i == 0 ? 9 : 10)) throw new Exception("Sword cone hit an invalid target or missed its forward target.");
                if (!SwordAttack.ContainsTarget(new Vector2(1, 1), Vector2.right)
                    || SwordAttack.ContainsTarget(new Vector2(0.5f, 1), Vector2.right)) throw new Exception("Cone boundary mismatch.");
                var caster = run.Enemies.Find(enemy => enemy.IsRanged);
                if (caster == null) throw new Exception("No ranged enemies spawned.");
                caster.transform.position = origin;
                caster.enabled = true;
                combatHealth = player.Health;
                combatStage = 1;
                return false;
            }
            if (combatStage == 1)
            {
                if (player.Health != combatHealth) throw new Exception("Ranged enemy dealt contact damage.");
                foreach (var enemy in run.Enemies) enemy.enabled = false;
                if (!player.TryRoll(Vector2.right) || !player.IsInvulnerable) throw new Exception("Dodge did not start.");
                var bolt = EnemyProjectile.Spawn(run, run.Enemies[0].transform.parent,
                    (Vector2)player.transform.position + Vector2.right * 0.3f, Vector2.left);
                bolt.Advance(0.01f);
                if (!bolt.IsSpent || player.Health != combatHealth) throw new Exception("Dodge did not block a projectile.");
                if (player.GetComponent<SwordAttack>().TryAttack(Vector2.right)) throw new Exception("Player attacked during dodge.");
                combatResumeAt = Time.time + 0.3f;
                combatStage = 2;
                return false;
            }
            if (Time.time < combatResumeAt) return false;
            if (player.IsInvulnerable || player.IsRolling) throw new Exception("Dodge immunity did not expire.");
            if (player.TryRoll(Vector2.right)) throw new Exception("Dodge cooldown was bypassed.");
            player.transform.position = (Vector2)run.Map.Centers[0];
            var hit = EnemyProjectile.Spawn(run, run.Enemies[0].transform.parent,
                (Vector2)player.transform.position + Vector2.right * 0.3f, Vector2.left);
            hit.Advance(0.01f);
            if (!hit.IsSpent || player.Health != combatHealth - 1) throw new Exception("Projectile did not damage player after dodge.");
            var wall = EnemyProjectile.Spawn(run, run.Enemies[0].transform.parent, new Vector2(-1, -1), Vector2.right);
            wall.Advance(0.01f);
            if (!wall.IsSpent) throw new Exception("Projectile passed through a wall.");
            EnemyProjectile.Spawn(run, run.Enemies[0].transform.parent, run.Map.Centers[1], Vector2.right);
            return true;
        }

        [MenuItem("Slopgame/Create playable dungeon scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Dungeon Run").AddComponent<DungeonRun>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Dungeon.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Dungeon.unity", true) };
            ValidateMaps();
            Debug.Log("SLOPGAME_SETUP_OK: Dungeon scene created; 500 dungeon seeds validated.");
        }

        [MenuItem("Slopgame/Validate generated dungeons")]
        public static void ValidateMaps()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var map = new DungeonMap(seed);
                if (map.Centers.Count < 2) throw new Exception("Too few rooms: " + seed);
                var visited = new HashSet<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(map.Centers[0]);
                visited.Add(map.Centers[0]);
                var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var direction in directions)
                    {
                        var next = cell + direction;
                        if (map.IsFloor(next.x, next.y) && visited.Add(next)) queue.Enqueue(next);
                    }
                }
                foreach (var center in map.Centers)
                {
                    if (!visited.Contains(center) || !map.CanStand(center)) throw new Exception("Unreachable room: " + seed);
                    for (int i = 0; i < 4; i++)
                        if (!map.CanStand((Vector2)center + new Vector2(i % 2, i / 2))) throw new Exception("Invalid spawn: " + seed);
                }
                for (int x = 0; x < DungeonMap.Width; x++)
                    for (int y = 0; y < DungeonMap.Height; y++)
                        if (map.IsFloor(x, y) && !visited.Contains(new Vector2Int(x, y))) throw new Exception("Disconnected floor: " + seed);
                if (map.CanStand(new Vector2(-1, -1))) throw new Exception("Bounds check failed");
            }
        }
    }
}

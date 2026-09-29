using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    /// <summary>
    /// Play-mode check of the boss roster and the debug skip-room button: the Duelist dashes, the Archdemon's Cataclysm
    /// burns heroes outside the safe circle while he is untouchable, and he can be killed once he crashes down.
    /// Run with <c>-batchmode -executeMethod Slopgame.Editor.BossRosterTests.Run</c>; it exits the editor when done.
    /// </summary>
    public static class BossRosterTests
    {
        private static double started;
        private static int stage;
        private static float waitUntil;
        private static bool failed, sawDash;
        private static int healthBefore;
        private static HellfireZone inferno;

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("BossRosterSmoke", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += CaptureError;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            SessionState.SetBool("BossRosterSmoke", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += CaptureError;
            EditorApplication.EnterPlaymode();
        }

        private static void CaptureError(string message, string stack, LogType type)
        {
            if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
        }

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        private static void SkipTo(DungeonRun run, int floor)
        {
            while (run.Floor < floor)
            {
                int before = run.Floor;
                run.DebugSkipRoom();
                Require(run.Floor == before + 1, "Skip room did not advance the floor.");
            }
        }

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timed out at stage " + stage); return; }
            if (!EditorApplication.isPlaying) return;
            var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null || run.Characters.Count == 0) return;
            try
            {
                if (Time.time < waitUntil) { if (run.Boss != null && run.Boss.NetState == 2) sawDash = true; return; }
                switch (stage)
                {
                    case 0:
                        run.Restart();
                        DebugMode.Set(false);
                        Require(!run.CanSkipRoom, "Skip room was offered outside debug mode.");
                        run.DebugSkipRoom();
                        Require(run.Floor == 1, "Skip room worked outside debug mode.");
                        DebugMode.Set(true);
                        Require(run.CanSkipRoom, "Skip room was not offered in solo debug mode.");
                        SkipTo(run, 5);
                        Require(run.Boss != null && run.Boss.Kind == BossKind.AshWarden, "Floor five is not the Ash Warden.");
                        SkipTo(run, 10);
                        Require(run.Boss != null && run.Boss.Behaviour is DuelistBoss, "Floor ten is not the Duelist.");
                        Require(run.Boss.MaxHealth < DungeonRun.ScaleHealth(24 + 10 * 3, 1), "The Duelist is not squishier than the Warden.");
                        waitUntil = Time.time + 5f;
                        stage = 1;
                        break;
                    case 1:
                        Require(sawDash, "The Duelist never dashed.");
                        run.Boss.Enemy.Hit(100000);
                        Require(run.Artifact != null && run.Enemies.Count == 0, "The Duelist dropped no artifact.");
                        SkipTo(run, 15);
                        Require(run.Boss != null && run.Boss.Behaviour is ArchdemonBoss, "Floor fifteen is not the Archdemon.");
                        // Bloodied, he opens with Cataclysm.
                        run.Boss.Enemy.Health = run.Boss.MaxHealth / 2;
                        foreach (HazardShape shape in Enum.GetValues(typeof(HazardShape)))
                            if (shape != HazardShape.Inferno)
                                HellfireZone.Spawn(run, new HazardSpec { Shape = shape, Center = new Vector2(20f, 12f), Direction = Vector2.right, Radius = 5f, Width = 1f, Telegraph = 0.3f, Duration = 1f });
                        stage = 2;
                        break;
                    case 2:
                        if (!run.Boss.IsInvulnerable) return;
                        int health = run.Boss.Enemy.Health;
                        run.Boss.Enemy.Hit(100000);
                        Require(run.Boss.Enemy.Health == health && run.Enemies.Count == 1, "The airborne Archdemon took damage.");
                        foreach (var zone in run.ProjectileRoot.GetComponentsInChildren<HellfireZone>())
                            if (zone.name.EndsWith(HazardShape.Inferno.ToString())) inferno = zone;
                        Require(inferno != null, "Cataclysm did not cover the arena in hellfire.");
                        stage = 3;
                        break;
                    case 3:
                        if (!inferno.IsBurning) return;
                        Vector2 exposed = Vector2.zero;
                        foreach (var corner in new[] { new Vector2(15f, 9f), new Vector2(39f, 9f), new Vector2(15f, 29f), new Vector2(39f, 29f) })
                            if (inferno.Contains(corner)) { exposed = corner; break; }
                        Require(exposed != Vector2.zero, "Every corner was safe from the inferno.");
                        DebugMode.Set(false);
                        run.Player.transform.position = exposed;
                        healthBefore = run.Player.Health;
                        waitUntil = Time.time + 0.3f;
                        stage = 4;
                        break;
                    case 4:
                        Require(run.Player.Health < healthBefore, "Standing in the hellfire did no damage.");
                        DebugMode.Set(true);
                        stage = 5;
                        break;
                    case 5:
                        if (run.Boss.IsInvulnerable || run.Boss.Tell.IndexOf("STAGGERED", StringComparison.Ordinal) < 0) return;
                        run.Boss.Enemy.Hit(100000);
                        Require(run.Artifact != null && run.Enemies.Count == 0, "The Archdemon dropped no artifact.");
                        Require(run.ProjectileRoot.GetComponentsInChildren<HellfireZone>().Length == 0, "Hellfire outlived the Archdemon.");
                        Finish(!failed, "Warden/Duelist/Archdemon rotation, Duelist dashes, Cataclysm inferno and invulnerable flight, stagger kill, debug skip room");
                        break;
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool("BossRosterSmoke", false);
            EditorApplication.update -= Check;
            Application.logMessageReceived -= CaptureError;
            DebugMode.Set(false);
            Time.timeScale = 1f;
            Debug.Log((success ? "BOSS_ROSTER_SMOKE_OK: " : "BOSS_ROSTER_SMOKE_FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}

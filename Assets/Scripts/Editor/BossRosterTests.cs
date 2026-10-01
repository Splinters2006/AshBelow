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
        private static int neonIndex, attackMask;
        private static bool sawMinion;
        private static bool sawNeonHazard, sawNeonBolt;
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
                bool shop = !run.InShop && DungeonRun.IsShopNext(before);
                run.DebugSkipRoom();
                Require(shop ? run.InShop && run.Floor == before : run.Floor == before + 1, "Skip room did not advance the floor.");
            }
        }

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 420) { Finish(false, "Timed out at stage " + stage); return; }
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
                        TestMinimap(run);
                        DebugMode.Set(false);
                        Require(!run.CanSkipRoom, "Skip room was offered outside debug mode.");
                        run.DebugSkipRoom();
                        Require(run.Floor == 1, "Skip room worked outside debug mode.");
                        DebugMode.Set(true);
                        Require(run.CanSkipRoom, "Skip room was not offered in solo debug mode.");
                        SkipTo(run, 5);
                        Require(run.Boss != null && run.Boss.Kind == BossKind.AshWarden, "Floor five is not the Rime Warden.");
                        SkipTo(run, 10);
                        Require(run.Boss != null && run.Boss.Behaviour is DuelistBoss, "Floor ten is not the Duelist.");
                        Require(run.Boss.MaxHealth < DungeonBoss.ScaledHealth(24 + 10 * 3, 1), "The Duelist is not squishier than the Warden.");
                        Require(run.Boss.MaxHealth == DungeonBoss.ScaledHealth(12 + 10 * 2, run.PartySize), "The Duelist's health is not boss-scaled.");
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
                        stage = 6;
                        break;
                    case 6:
                        SkipTo(run, 20 + neonIndex * 5);
                        // Three Arcology machines, then the Infernal Court's three guardians and the Arcane Spire's three mages.
                        Require(neonIndex < 3 ? run.Boss.Behaviour is NeonBossBehaviour
                            : neonIndex < 6 ? run.Boss.Behaviour is InfernalBossBehaviour : run.Boss.Behaviour is ArcaneBossBehaviour,
                            "World reused an ash guardian at floor " + run.Floor);
                        Require((int)run.Boss.Kind == 3 + neonIndex, "Wrong guardian at floor " + run.Floor);
                        sawMinion = false;
                        run.Boss.Enemy.Health = run.Boss.MaxHealth / 2;
                        attackMask = 0; sawNeonHazard = false; sawNeonBolt = false;
                        stage = 7;
                        break;
                    case 7:
                        byte state = run.Boss.NetState;
                        var court = run.Boss.Behaviour as CourtBossBehaviour;
                        int attacks = court != null ? court.AttackCount : 3;
                        byte spent = court != null ? CourtBossBehaviour.Spent : (byte)4;
                        if (state >= 1 && state <= attacks) attackMask |= 1 << (state - 1);
                        sawNeonHazard |= run.ProjectileRoot.GetComponentsInChildren<HellfireZone>().Length > 0;
                        sawNeonBolt |= run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length > 0;
                        sawMinion |= run.Enemies.Exists(enemy => enemy.IsMinion);
                        if (attackMask != (1 << attacks) - 1 || state != spent || (neonIndex >= 3 && !sawMinion)) return;
                        Require(sawNeonHazard, "Arcology guardian created no hazards.");
                        if (neonIndex == 2) Require(sawNeonBolt, "Core did not fire its spiral/satellite bolts.");
                        var behaviour = run.Boss.Behaviour;
                        // The hound fights with fire and its pack alone: neither it nor its brutes and imps shoot.
                        if (neonIndex >= 3 && !(behaviour is BrimstoneHoundBoss)) Require(sawNeonBolt, "Court guardian fired no bolts.");
                        for (byte value = 0; value <= (court != null ? CourtBossBehaviour.Summoning : 4); value++)
                        {
                            if (court != null && value > attacks && value < CourtBossBehaviour.Spent) continue;
                            behaviour.ApplyNetState((value >= 1 && value <= attacks) || (court != null && value == CourtBossBehaviour.Summoning), value);
                            Require(behaviour.NetState == value && !string.IsNullOrEmpty(behaviour.Tell), "Arcology snapshot state failed.");
                            behaviour.VisualTick();
                        }
                        run.Boss.Enemy.Hit(100000);
                        Require(run.Artifact != null && run.Enemies.Count == 0, "Guardian dropped no artifact or its minions outlived it.");
                        Require(run.ProjectileRoot.GetComponentsInChildren<HellfireZone>().Length == 0, "Arcology hazards outlived their boss.");
                        Require(run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length == 0, "Arcology bolts outlived their boss.");
                        if (++neonIndex < 9) { stage = 6; break; }
                        Finish(!failed, "All twelve guardians, the Arcology's, Infernal Court's and Arcane Spire's attack patterns, summoned minions, hazards/projectiles, replicated states, artifact drops and cleanup; ash roster regressions");
                        break;
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        }

        private static void TestMinimap(DungeonRun run)
        {
            var minimap = run.Minimap;
            minimap.Scan();
            Require(minimap.IsExplored(Vector2Int.RoundToInt(run.Player.transform.position)), "The minimap did not reveal the hero's surroundings.");
            var far = run.Enemies.Find(enemy => Vector2.Distance(enemy.transform.position, run.Player.transform.position) > FloorMinimap.SpotRadius + 1f);
            Require(far != null && !minimap.IsSpotted(far), "A distant enemy was marked before anyone saw it.");
            Vector2 start = run.Player.transform.position;
            run.Player.transform.position = far.transform.position + Vector3.left * 0.4f;
            minimap.Scan();
            Require(minimap.IsSpotted(far), "An enemy in plain sight was not marked on the minimap.");
            run.Player.transform.position = start;
            minimap.Scan();
            Require(minimap.IsSpotted(far), "A spotted enemy vanished from the minimap.");
            DebugMode.Set(true);
            run.DebugSkipRoom();
            DebugMode.Set(false);
            minimap.Scan();
            // On a fresh floor only what the hero can see right now is revealed.
            for (int x = 0; x < DungeonMap.Width; x++)
                for (int y = 0; y < DungeonMap.Height; y++)
                    if (minimap.IsExplored(new Vector2Int(x, y)))
                        Require(Vector2.Distance(new Vector2(x, y), run.Player.transform.position) <= FloorMinimap.SightRadius, "The minimap carried over to the next floor.");
            run.Restart();
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

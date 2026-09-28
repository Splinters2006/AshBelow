using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    public static class AdminPreviewCapture
    {
        private static double started, nextAt;
        private static DateTime captureStarted;
        private static int stage;
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("AdminPreview", false)) return;
            started = EditorApplication.timeSinceStartup;
            captureStarted = DateTime.UtcNow;
            nextAt = started + 2f;
            EditorApplication.update += Check;
        }
        public static void Capture()
        {
            var gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.position = new Rect(40, 40, 1280, 760);
            gameView.Show(); gameView.Focus();
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            SessionState.SetBool("AdminPreview", true);
            started = EditorApplication.timeSinceStartup;
            captureStarted = DateTime.UtcNow;
            nextAt = started + 2f;
            EditorApplication.update += Check;
            EditorApplication.EnterPlaymode();
        }
        private static string PathFor(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "admin-" + name + ".png"));
        private static void Save(string name)
        {
            ScreenCapture.CaptureScreenshot(PathFor(name));
            nextAt = EditorApplication.timeSinceStartup + 1f;
        }
        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 90f) { Finish(1); return; }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextAt) return;
            var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null) return;
            try
            {
                if (stage == 0)
                {
                    foreach (var hero in run.Characters) if (hero.Weapon == WeaponType.Shadow) run.SelectCharacter(hero);
                    typeof(MainMenu).GetField("selecting", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(run.GetComponent<MainMenu>(), true);
                    stage++; nextAt = EditorApplication.timeSinceStartup + 0.5f; return;
                }
                if (stage == 1) { Save("selection"); stage++; return; }
                if (stage == 2) { run.Restart(); stage++; nextAt = EditorApplication.timeSinceStartup + 0.5f; return; }
                if (run.Player == null || run.Player.Weapon == null) return;
                if (stage == 3)
                {
                    run.Player.enabled = false;
                    while (run.Floor < 5)
                    {
                        while (run.Enemies.Count > 0) run.Enemies[0].Hit(100000);
                        run.BeginUpgradeChoice(); run.ChooseUpgrade(0);
                    }
                    run.Player.transform.position = new Vector2(27, 19);
                    run.View.transform.position = new Vector3(27, 19, -10);
                    run.Player.Protect(100f);
                    run.Boss.enabled = false;
                    run.Boss.Enemy.enabled = false;
                    run.Player.Abilities.Claim(AbilityType.Eclipse, 0);
                    run.Player.Abilities.Claim(AbilityType.ShadowReign, 1);
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 8;
                        var enemy = DungeonVisuals.Create("Preview ashling", run.ProjectileRoot,
                            (Vector2)run.Player.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 4.5f,
                            Vector2.one * 0.65f, new Color(0.9f, 0.35f, 0.55f), 4).gameObject.AddComponent<DungeonEnemy>();
                        enemy.Run = run; enemy.Health = 20; enemy.Speed = 0;
                        run.Enemies.Add(enemy);
                    }
                    stage++; nextAt = EditorApplication.timeSinceStartup + 0.5f; return;
                }
                if (stage == 4)
                {
                    run.Player.Weapon.TryHeavyAttack(Vector2.up);
                    stage++; nextAt = EditorApplication.timeSinceStartup + 0.18f; return;
                }
                if (stage == 5) { Time.timeScale = 0f; Save("nightfall"); stage++; return; }
                if (stage == 6) { run.BeginArtifactChoice(); stage++; nextAt = EditorApplication.timeSinceStartup + 0.3f; return; }
                if (stage == 7) { Save("artifacts"); stage++; return; }
                bool valid = true;
                foreach (string name in new[] { "selection", "nightfall", "artifacts" })
                    valid &= File.Exists(PathFor(name)) && File.GetLastWriteTimeUtc(PathFor(name)) >= captureStarted;
                Debug.Log(valid ? "ADMIN_PREVIEW_OK" : "ADMIN_PREVIEW_FAILED: No fresh screenshots");
                Finish(valid ? 0 : 1);
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void Finish(int code)
        {
            SessionState.SetBool("AdminPreview", false);
            EditorApplication.update -= Check;
            Time.timeScale = 1f;
            EditorApplication.Exit(code);
        }
    }
}

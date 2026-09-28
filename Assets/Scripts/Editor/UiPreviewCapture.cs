using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    public static class UiPreviewCapture
    {
        private static int stage;
        private static double nextAt, started;
        private static System.DateTime captureStartedAt;
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("SlopgamePreview", false)) return;
            started = EditorApplication.timeSinceStartup;
            captureStartedAt = System.DateTime.UtcNow;
            nextAt = started + 3f;
            EditorApplication.update += Check;
        }

        public static void Capture()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null)
            {
                var gameView = EditorWindow.GetWindow(gameViewType);
                gameView.position = new Rect(40, 40, 1280, 760);
                gameView.Show();
                gameView.Focus();
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            SessionState.SetBool("SlopgamePreview", true);
            started = EditorApplication.timeSinceStartup;
            nextAt = started + 3f;
            EditorApplication.update += Check;
            EditorApplication.EnterPlaymode();
        }

        private static void Save(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "..", name + ".png")));
            nextAt = EditorApplication.timeSinceStartup + 1.5f;
        }

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 90) { Finish(1); return; }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextAt) return;
            var run = Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null) return;
            if (stage == 0) { Save("preview-menu"); stage++; return; }
            if (stage == 1)
            {
                foreach (var hero in run.Characters) if (hero.Weapon == WeaponType.Staff) run.SelectCharacter(hero);
                typeof(MainMenu).GetField("selecting", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(run.GetComponent<MainMenu>(), true);
                stage++; nextAt = EditorApplication.timeSinceStartup + 0.5f; return;
            }
            if (stage == 2) { Save("preview-classes"); stage++; return; }
            if (stage == 3) { run.Restart(); stage++; nextAt = EditorApplication.timeSinceStartup + 0.5f; return; }
            if (run.Player == null || run.Player.Weapon == null) return;
            if (stage == 4)
            {
                run.Player.enabled = false;
                while (run.Floor < 5)
                {
                    while (run.Enemies.Count > 0) run.Enemies[0].Hit(100000);
                    run.BeginUpgradeChoice(); run.ChooseUpgrade(0);
                }
                run.Player.transform.position = new Vector2(27, 19);
                run.View.transform.position = new Vector3(27, 19, -10);
                run.Player.Abilities.Claim(AbilityType.Fireball, 0);
                run.Player.Abilities.Claim(AbilityType.FrostNova, 1);
                run.Player.Protect(100f);
                nextAt = EditorApplication.timeSinceStartup + 0.7f;
                stage++; return;
            }
            if (stage == 5) { Save("preview-arena"); stage++; return; }
            if (stage == 6)
            {
                run.Boss.Enemy.Hit(100000);
                run.BeginArtifactChoice();
                nextAt = EditorApplication.timeSinceStartup + 0.6f;
                stage++; return;
            }
            if (stage == 7) { Save("preview-artifacts"); stage++; return; }
            if (stage == 8)
            {
                run.FinishArtifactChoice(); run.BeginUpgradeChoice();
                while (run.Enemies.Count > 0) run.Enemies[0].Hit(100000);
                run.BeginUpgradeChoice();
                nextAt = EditorApplication.timeSinceStartup + 0.6f;
                stage++; return;
            }
            if (stage == 9) { Save("preview-talents"); stage++; return; }
            bool captured = true;
            foreach (string name in new[] { "menu", "classes", "arena", "artifacts", "talents" })
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "preview-" + name + ".png"));
                captured &= File.Exists(path) && File.GetLastWriteTimeUtc(path) >= captureStartedAt;
            }
            Debug.Log(captured ? "UI_PREVIEWS_OK" : "UI_PREVIEWS_FAILED: Game view did not produce screenshots.");
            Finish(captured ? 0 : 1);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool("SlopgamePreview", false);
            EditorApplication.update -= Check;
            Time.timeScale = 1f;
            EditorApplication.Exit(code);
        }
    }
}

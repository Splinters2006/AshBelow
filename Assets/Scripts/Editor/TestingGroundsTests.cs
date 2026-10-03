using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    /// <summary>
    /// Play-mode smoke test for the testing grounds: the Samurai is put in the arena, five training dummies stand in a
    /// pentagon, they take hits without falling or locking anything, and only discovered talents can be granted.
    /// </summary>
    public static class TestingGroundsTests
    {
        private static double started;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            EditorApplication.EnterPlaymode();
        }

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying) return;
            var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null) return;
            try
            {
                foreach (var character in run.Characters) if (character.Weapon == WeaponType.Katana) run.SelectCharacter(character);
                run.BeginTesting();
                Require(run.IsTesting && run.IsPlaying && run.Player != null && run.Player.ClassWeapon == WeaponType.Katana, "The testing grounds did not open.");
                Require(run.Enemies.Count == DungeonRun.TestingDummies && run.Enemies.TrueForAll(enemy => enemy.IsTrainingDummy) && run.HostileCount == 0,
                    "The testing grounds did not hold exactly five training dummies.");
                // A pentagon: every dummy the same distance from the middle, and every neighbour the same distance apart.
                Vector2 middle = Vector2.zero;
                foreach (var enemy in run.Enemies) middle += (Vector2)enemy.transform.position;
                middle /= run.Enemies.Count;
                float side = Vector2.Distance(run.Enemies[0].transform.position, run.Enemies[1].transform.position);
                for (int i = 0; i < run.Enemies.Count; i++)
                {
                    Vector2 at = run.Enemies[i].transform.position, next = run.Enemies[(i + 1) % run.Enemies.Count].transform.position;
                    Require(Mathf.Abs(Vector2.Distance(at, middle) - DungeonRun.TestingDummyRing) < 0.01f && Mathf.Abs(Vector2.Distance(at, next) - side) < 0.01f,
                        "The training dummies are not in a pentagon.");
                    Require(run.Map.CanStand(at), "A training dummy stands in a wall.");
                }
                Require(run.Map.CanStand(run.Player.transform.position), "The hero starts in a wall.");
                var dummy = run.Enemies[0];
                dummy.Hit(TrainingDummy.DummyHealth + 10, run.Player.transform.position, 3f);
                Require(dummy.Health > 0 && run.Enemies.Contains(dummy), "A training dummy fell.");

                // Only talents the encyclopedia has found are offered; granting one adds a rank.
                var talent = PowerupType.BledDry;
                run.Progress.Discover(Encyclopedia.TalentId(talent));
                Require(run.Player.Powerups.CanTake(talent) && run.Player.GrantPowerup(talent) && run.Player.Powerups.Count(talent) == 1, "Could not grant a talent.");

                run.ShowMainMenu();
                Require(run.IsInMainMenu && !run.IsTesting && run.Enemies.Count == 0, "Leaving the testing grounds did not clear them.");
                Finish(true, "Testing grounds, pentagon of dummies, talent grant, leaving");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(false, exception.Message);
            }
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Check;
            Debug.Log((success ? "TESTING_GROUNDS_OK: " : "TESTING_GROUNDS_FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}

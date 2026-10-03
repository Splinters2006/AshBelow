using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    /// <summary>
    /// Saving and continuing a descent: a built-up Reaper is saved, the menu shown, and the descent continued; the hero,
    /// the floor and the ash already paid for the floor must all come back. Run with -batchmode -executeMethod
    /// Slopgame.Editor.RunSaveTests.Run.
    /// </summary>
    public static class RunSaveTests
    {
        private const string SessionKey = "RunSaveSmoke";
        private static double started;
        private static int stage;
        private static bool failed;
        private static RunSnapshot saved;
        private static int maxHealth, damage, ashBefore;

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (SessionState.GetBool(SessionKey, false)) Subscribe();
        }

        private static void Subscribe()
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += Log;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            SessionState.SetBool(SessionKey, true);
            Subscribe();
            EditorApplication.EnterPlaymode();
        }

        private static void Log(string message, string stack, LogType type)
        {
            if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
        }

        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timeout"); return; }
            if (!EditorApplication.isPlaying) return;
            var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null || run.Characters.Count == 0) return;
            try
            {
                if (stage == 0)
                {
                    Require(RunSnapshot.FromJson("{\"version\":99,\"floor\":3,\"weapon\":\"Scythe\"}") == null, "A save from a newer version was read.");
                    Require(RunSnapshot.FromJson("not json") == null, "A damaged save was read.");
                    run.SelectCharacter(Find(run, WeaponType.Scythe));
                    run.Restart();
                    stage = 1;
                }
                else if (stage == 1)
                {
                    // A frame on, the class weapon is in place: build the hero up, then save the descent.
                    var player = run.Player;
                    var reaper = player.Weapon as ReaperAttack;
                    Require(reaper != null, "The Reaper has no scythe.");
                    Require(player.GrantPowerup(PowerupType.Vitality) && player.GrantPowerup(PowerupType.Damage), "Talents were not granted.");
                    var ability = AbilityCatalog.PoolFor(WeaponType.Scythe, run.Progress)[0];
                    Require(player.Abilities.Learn(ability.Type) && player.Abilities.Learn(ability.Type), "The ability was not learned.");
                    player.Crystals.Add(37);
                    reaper.AddSouls(12);
                    player.Hit();
                    maxHealth = player.MaxHealth;
                    damage = player.BaseDamage;
                    var snapshot = new RunSnapshot
                    {
                        weapon = WeaponType.Scythe.ToString(), seed = run.Seed, floor = run.Floor, kills = 4, runAsh = 30, guardians = 0,
                        floorAshPaid = 11, savedAt = DateTime.UtcNow.Ticks, hero = player.CaptureRun(),
                    };
                    saved = RunSnapshot.FromJson(snapshot.ToJson());
                    Require(saved != null && saved.hero.talents.Count == 2 && saved.hero.Extra("souls", -1) == reaper.Souls, "The save did not survive JSON.");
                    // Accounts cannot sign in here, so the save is handed over as if the cloud had sent it.
                    typeof(CloudRunSave).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(run.RunSave, saved);
                    run.ShowMainMenu();
                    Require(run.IsInMainMenu && run.SavedRunHero != null && run.SavedRunHero.Weapon == WeaponType.Scythe, "The menu does not offer the saved descent.");
                    // The menu's pick must not matter: the saved hero comes back.
                    run.SelectCharacter(Find(run, WeaponType.Sword));
                    Require(run.ResumeRun(), "The saved descent did not continue.");
                    stage = 2;
                }
                else if (stage == 2)
                {
                    var player = run.Player;
                    var hero = saved.hero;
                    var reaper = player.Weapon as ReaperAttack;
                    Require(run.IsPlaying && run.Floor == saved.floor && run.Seed == saved.seed && !run.InShop, "The descent came back on the wrong floor.");
                    Require(player.ClassWeapon == WeaponType.Scythe && reaper != null, "The wrong hero came back.");
                    Require(player.MaxHealth == maxHealth && player.Health == hero.health && player.BaseDamage == damage, "The hero's stats changed.");
                    Require(player.Powerups.Count(PowerupType.Vitality) == 1 && player.Powerups.Count(PowerupType.Damage) == 1, "Talents were lost.");
                    var ability = AbilityCatalog.PoolFor(WeaponType.Scythe, run.Progress)[0];
                    Require(player.Abilities.Rank(ability.Type) == 2 && player.Abilities.Equipped(0) == ability.Type, "The ability or its key was lost.");
                    Require(player.Crystals.Crystals == hero.crystals && reaper.Souls == hero.Extra("souls", -1), "Crystals or souls were lost.");
                    Require(run.Kills == 4 && run.RunAshEarned == 30, "The descent's tally was lost.");

                    // The floor's first 11 ash were paid before the save: only what goes past them is paid again.
                    ashBefore = run.Progress.Ash;
                    int enemies = run.Enemies.Count;
                    Require(enemies >= 2, "The resumed floor has too few enemies to test its ash.");
                    run.EnemyDefeated(run.Enemies[0]);
                    Require(run.Progress.Ash == ashBefore && run.RunAshEarned == 31, "A kill paid ash twice after the resume.");
                    while (run.Enemies.Count > 0) run.EnemyDefeated(run.Enemies[0]);
                    int earned = enemies + 10;
                    Require(run.Progress.Ash == ashBefore + earned - 11 && run.RunAshEarned == 30 + earned,
                        $"Clearing the resumed floor paid {run.Progress.Ash - ashBefore} ash, not {earned - 11}.");
                    Finish(!failed, failed ? "An error was logged." : "Saved descents continue.");
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(false, error.Message);
            }
        }

        private static CharacterDefinition Find(DungeonRun run, WeaponType weapon)
        {
            foreach (var character in run.Characters) if (character.Weapon == weapon) return character;
            throw new Exception("No hero uses " + weapon);
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Check;
            Application.logMessageReceived -= Log;
            Time.timeScale = 1f;
            Debug.Log((success ? "RUN_SAVE_OK: " : "RUN_SAVE_FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}

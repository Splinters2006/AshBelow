using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    public static class ProgressionTests
    {
        private static double started;
        private static int stage, hero;
        private static bool failed;
        private static readonly WeaponType[] Classes = (WeaponType[])Enum.GetValues(typeof(WeaponType));

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("AshProgressSmoke", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += Log;
        }
        public static void Run()
        {
            try { TestStorage(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); return; }
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            SessionState.SetBool("AshProgressSmoke", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += Log;
            EditorApplication.EnterPlaymode();
        }
        private static void Log(string message, string stack, LogType type)
        {
            if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) failed = true;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void TestStorage()
        {
            string root = Path.Combine(Path.GetTempPath(), "ash-save-test-" + Guid.NewGuid().ToString("N"));
            var save = new PermanentProgress(root);
            Require(save.Ash == 0 && !save.TryPurchase("health"), "Empty wallet could buy an upgrade.");
            save.AwardAsh(20000);
            foreach (var upgrade in PermanentUpgradeCatalog.All)
            {
                int before = save.Ash;
                Require(save.TryPurchase(upgrade.Id) && save.Ash == before - upgrade.Cost(0), "Wrong purchase price or missing upgrade.");
                Require(save.Rank(upgrade.Id) == 1, "Purchase rank missing.");
            }
            int healthRanks = PermanentUpgradeCatalog.Get("health").MaxRank;
            while (save.Rank("health") < healthRanks) Require(save.TryPurchase("health"), "Rank purchase failed.");
            int balance = save.Ash;
            Require(!save.TryPurchase("health") && !save.TryPurchase("unknown") && save.Ash == balance, "Invalid purchase consumed Ash.");
            var loaded = new PermanentProgress(root);
            Require(loaded.Ash == save.Ash && loaded.Rank("health") == healthRanks, "Purchases failed to survive reload.");
            save.AwardAsh(1);
            File.WriteAllText(save.SavePath, "invalid json");
            var recovered = new PermanentProgress(root);
            Require(!recovered.IsReadOnly && recovered.Ash == balance && recovered.Rank("health") == healthRanks, "Backup recovery failed.");
            recovered.AwardAsh(5);
            Require(new PermanentProgress(root).Ash == balance + 5, "Recovered save could not be written safely.");
            File.WriteAllText(save.SavePath, "{\"version\":99,\"ash\":987,\"upgrades\":[]}");
            var future = new PermanentProgress(root);
            future.AwardAsh(1);
            Require(future.IsReadOnly && !future.TryPurchase("speed") && File.ReadAllText(save.SavePath).Contains("987"), "Old game overwrote a newer save.");
            string blockedRoot = Path.Combine(root, "not-a-directory");
            File.WriteAllText(blockedRoot, "block writes");
            var blocked = new PermanentProgress(blockedRoot);
            blocked.AwardAsh(100);
            Require(blocked.Ash == 100 && blocked.HasUnsavedChanges && !blocked.TryPurchase("health") && blocked.Rank("health") == 0,
                "Failed write lost earned Ash or granted an unsaved purchase.");
            Debug.Log("ASH_STORAGE_OK: prices/caps, all upgrades, reload, backup recovery, future schema, and failed writes");
        }
        private static void StartHero(DungeonRun run)
        {
            run.ShowMainMenu();
            foreach (var character in run.Characters) if (character.Weapon == Classes[hero]) run.SelectCharacter(character);
            run.Restart();
        }
        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying) return;
            var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null) return;
            try
            {
                if (stage == 0) { run.Restart(); stage = 1; return; }
                if (run.Player == null || run.Player.Weapon == null) return;
                run.Player.enabled = false;
                foreach (var enemy in run.Enemies)
                {
                    enemy.enabled = false;
                    var shooter = enemy.GetComponent<EnemyShooter>();
                    if (shooter != null) shooter.enabled = false;
                }
                if (stage == 1)
                {
                    Require(run.ProjectileRoot.GetComponentsInChildren<StairVisual>().Length == 1, "Missing stairwell visual.");
                    int before = run.Progress.Ash;
                    int enemies = run.Enemies.Count;
                    var first = run.Enemies[0];
                    first.Hit(100000);
                    Require(run.Progress.Ash == before + 1, "Ordinary enemy reward is not one Ash.");
                    first.Hit(100000);
                    Require(run.Progress.Ash == before + 1, "Dead enemy paid twice.");
                    while (run.Enemies.Count > 0) run.Enemies[0].Hit(100000);
                    Require(run.Progress.Ash == before + enemies + 10, "Floor-clear reward incorrect.");
                    int clearBalance = run.Progress.Ash;
                    run.BeginUpgradeChoice(); run.ChooseUpgrade(0);
                    Require(run.Progress.Ash == clearBalance, "Descending paid floor-clear reward twice.");
                    while (run.Floor < 5)
                    {
                        while (run.Enemies.Count > 0) run.Enemies[0].Hit(100000);
                        run.BeginUpgradeChoice(); run.ChooseUpgrade(0);
                    }
                    before = run.Progress.Ash;
                    var boss = run.Boss.Enemy;
                    boss.Hit(100000);
                    Require(run.Progress.Ash == before + 60, "Boss plus floor reward must be 60 Ash.");
                    boss.Hit(100000);
                    Require(run.Progress.Ash == before + 60, "Boss reward duplicated.");
                    Require(!run.TryBuyUpgrade("health"), "Shop purchases allowed during a run.");
                    run.EndRun();
                    run.ShowMainMenu();
                    Require(new PermanentProgress(Path.GetDirectoryName(run.Progress.SavePath)).Ash == run.Progress.Ash, "Death/menu lost saved Ash.");
                    run.Progress.AwardAsh(10000);
                    foreach (var upgrade in PermanentUpgradeCatalog.All) Require(run.TryBuyUpgrade(upgrade.Id), "Shop failed to buy " + upgrade.Id);
                    StartHero(run);
                    stage = 2;
                    return;
                }
                if (stage == 2)
                {
                    var player = run.Player;
                    var character = run.SelectedCharacter;
                    int expectedHealth = character.StartingHealth + 1 + (Classes[hero] == WeaponType.Sword || Classes[hero] == WeaponType.Hammer ? 1 : 0);
                    Require(player.MaxHealth == expectedHealth && player.Health == expectedHealth, "Permanent HP did not apply or leaked between classes.");
                    Require(player.BaseDamage == character.StartingDamage + 1 + (Classes[hero] == WeaponType.Bow ? 1 : 0), "Permanent damage did not apply or leaked.");
                    Require(Mathf.Abs(player.Powerups.AttackIntervalMultiplier - 1f / 1.05f) < 0.001f, "Permanent attack speed missing.");
                    Require(Mathf.Abs(player.Powerups.DodgeCooldownMultiplier - 0.97f) < 0.001f, "Permanent dodge reduction missing.");
                    switch (Classes[hero])
                    {
                        case WeaponType.Sword: Require(player.Powerups.ReflectionDamage == 3, "Knight reflection upgrade missing."); break;
                        case WeaponType.Bow: Require(Mathf.Abs(player.Charge.Duration - 0.95f / 1.05f) < 0.001f, "Archer draw upgrade missing."); break;
                        case WeaponType.Staff: Require(player.Permanent.LightningDamage == 1 && Mathf.Abs(player.Powerups.ElementalEffectChance - 0.08f) < 0.001f, "Wizard upgrades missing."); break;
                        case WeaponType.Daggers: Require(Mathf.Abs(player.Powerups.PhysicalCritChance - 0.18f) < 0.001f && Mathf.Abs(player.Speed - character.MoveSpeed - 0.35f) < 0.001f, "Assassin upgrades missing."); break;
                        case WeaponType.Hammer:
                            Require(player.Weapon.TryAttack(Vector2.right, 1f) && player.Blessing.Remaining > 8.9f, "Paladin permanent blessing upgrade missing."); break;
                    }
                    hero++;
                    if (hero < Classes.Length) { StartHero(run); return; }
                    Finish(!failed, "Ash rewards, floor/boss deduplication, purchases, persistent upgrades across all classes, stairs, and save survival");
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        }
        private static void Finish(bool success, string message)
        {
            SessionState.SetBool("AshProgressSmoke", false);
            EditorApplication.update -= Check;
            Application.logMessageReceived -= Log;
            Time.timeScale = 1f;
            Debug.Log((success ? "ASH_PROGRESSION_OK: " : "ASH_PROGRESSION_FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}

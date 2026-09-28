using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    public static class BossArtifactTests
    {
        private static double started;
        private static int stage;
        private static float waitUntil;
        private static bool failed;
        private static readonly List<WeaponType> classes = new List<WeaponType> { WeaponType.Sword, WeaponType.Bow, WeaponType.Staff, WeaponType.Daggers, WeaponType.Hammer };
        private static int classIndex;
        private static int abilityIndex;
        private static DungeonEnemy burnTarget;

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("BossArtifactSmoke", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += CaptureError;
        }

        public static void Run()
        {
            DungeonProjectSetup.ValidateMaps();
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            SessionState.SetBool("BossArtifactSmoke", true);
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

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying) return;
            var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null) return;
            try
            {
                if (stage == 0)
                {
                    Require(run.Characters.Count == 5, "Five class assets must load.");
                    StartClass(run, WeaponType.Staff);
                    stage = 1;
                    return;
                }
                if (run.Player == null || run.Player.Weapon == null) return;
                run.Player.enabled = false;
                if (stage == 1)
                {
                    FreezeEnemies(run);
                    TestWizard(run);
                    while (run.Floor < 5) ClearFloor(run);
                    Require(run.IsBossFloor && run.Boss != null && run.Enemies.Count == 1, "Floor five did not create a dedicated boss arena.");
                    Require(run.Map.CanStand(new Vector2(15, 9)) && !run.Map.CanStand(new Vector2(12, 9)), "Arena bounds are wrong.");
                    run.Player.Protect(30f);
                    waitUntil = Time.time + 3.05f;
                    stage = 2;
                    return;
                }
                if (stage == 2)
                {
                    if (Time.time < waitUntil) return;
                    Require(run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length > 0, "Boss never fired its telegraphed fan.");
                    run.Boss.Enemy.Hit(100000);
                    Require(run.Artifact != null && run.Enemies.Count == 0, "Boss failed to drop an artifact.");
                    Require(run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length == 0, "Boss bolts survived its death.");
                    run.BeginUpgradeChoice();
                    Require(run.Floor == 5, "Unclaimed artifact was bypassed by stairs.");
                    run.BeginArtifactChoice();
                    Require(run.ChoosingArtifact && !run.IsPlaying && Time.timeScale == 0f, "Artifact menu did not pause combat.");
                    Require(!run.ChooseArtifact(AbilityType.ShieldRush, 0), "Wizard accepted a knight artifact.");
                    Require(run.ChooseArtifact(AbilityType.Fireball, 0), "Wizard failed to bind Q.");
                    Require(run.Player.Powerups.CanTake(PowerupType.FireballRadius), "Fireball talent did not unlock.");
                    Require(!run.Player.Powerups.CanTake(PowerupType.FrostDuration), "Locked ice talent was available.");
                    Require(run.Artifact == null && run.IsPlaying && Time.timeScale == 1f, "Artifact choice did not resume play.");
                    run.BeginUpgradeChoice();
                    Require(run.Floor == 6 && !run.ChoosingUpgrade, "Boss floor gave a passive reward or failed to descend.");
                    while (run.Floor < 10) ClearFloor(run);
                    Require(run.IsBossFloor && run.Boss != null, "Floor ten has no boss.");
                    run.Boss.Enemy.Hit(100000);
                    run.BeginArtifactChoice();
                    Require(run.ChooseArtifact(AbilityType.FrostNova, 1), "Second artifact did not fill F.");
                    TestSlots(run);
                    StartClass(run, classes[0]);
                    stage = 3;
                    return;
                }
                if (stage == 3)
                {
                    FreezeEnemies(run);
                    TestClass(run, classes[classIndex]);
                    classIndex++;
                    if (classIndex < classes.Count) { StartClass(run, classes[classIndex]); return; }
                    StartClass(run, AbilityCatalog.All[0].ClassWeapon);
                    stage = 4;
                    return;
                }
                if (stage == 4)
                {
                    FreezeEnemies(run);
                    TestActive(run, AbilityCatalog.All[abilityIndex]);
                    abilityIndex++;
                    if (abilityIndex < AbilityCatalog.All.Length)
                    { StartClass(run, AbilityCatalog.All[abilityIndex].ClassWeapon); return; }
                    burnTarget = run.Enemies[0];
                    burnTarget.enabled = true;
                    burnTarget.Speed = 0f;
                    burnTarget.transform.position = (Vector2)run.Map.Centers[run.Map.Centers.Count - 1];
                    burnTarget.Health = 100;
                    burnTarget.Burn(1, 5);
                    waitUntil = Time.time + 1.1f;
                    stage = 5;
                    return;
                }
                if (stage == 5)
                {
                    if (Time.time < waitUntil) return;
                    Require(burnTarget.Health == 95, "First burn did not tick exactly once.");
                    burnTarget.Burn(1, 1);
                    waitUntil = Time.time + 1.1f;
                    stage = 6;
                    return;
                }
                if (stage == 6)
                {
                    if (Time.time < waitUntil) return;
                    Require(burnTarget.Health == 94, "New burn inherited expired burn damage.");
                    run.ShowMainMenu(); run.Restart();
                    Require(run.Player.Abilities.EmptySlot == 0 && run.Player.Powerups.Count(PowerupType.CriticalHits) == 0, "New run retained abilities or talents.");
                    Finish(!failed, "Boss floors 5/10, artifact gates/Q/F/replacement, five classes, all 15 active abilities, lightning, elemental damage/burn expiration, talent gates, cooldowns, and reset");
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        }

        private static void StartClass(DungeonRun run, WeaponType type)
        {
            run.ShowMainMenu();
            foreach (var hero in run.Characters) if (hero.Weapon == type) run.SelectCharacter(hero);
            run.Restart();
        }

        private static void FreezeEnemies(DungeonRun run)
        {
            foreach (var enemy in run.Enemies)
            {
                enemy.enabled = false;
                var shooter = enemy.GetComponent<EnemyShooter>();
                if (shooter != null) shooter.enabled = false;
                enemy.transform.position = (Vector2)run.Map.Centers[run.Map.Centers.Count - 1];
            }
        }

        private static void ClearFloor(DungeonRun run)
        {
            while (run.Enemies.Count > 0) run.Enemies[0].Hit(100000);
            run.BeginUpgradeChoice();
            Require(run.ChoosingUpgrade, "Regular floor did not offer talents.");
            run.ChooseUpgrade(0);
        }

        private static void TestWizard(DungeonRun run)
        {
            var wizard = run.Player.Weapon as WizardAttack;
            Require(wizard != null && run.Player.Health == 4, "Wizard failed to initialize.");
            Require(Mathf.Abs(run.Player.Powerups.CritChance - 0.05f) < 0.001f, "Baseline proc chance must be 5%.");
            Require(!run.Player.Powerups.CanTake(PowerupType.FireballRadius), "Artifact talent appeared before unlock.");
            Vector2 origin = run.Player.transform.position;
            var offsets = new[] { Vector2.right, new Vector2(1, 1), Vector2.up };
            for (int i = 0; i < 3; i++) { run.Enemies[i].Health = 100; run.Enemies[i].transform.position = origin + offsets[i]; }
            Require(wizard.TryHeavyAttack(Vector2.right), "Lightning did not cast.");
            for (int i = 0; i < 3; i++) Require(run.Enemies[i].Health == 98, "Lightning missed a chain target or critically hit.");
            Require(!wizard.TryHeavyAttack(Vector2.right), "Lightning bypassed cooldown.");
            float oldRange = wizard.LightningRange;
            run.Player.Upgrade((int)PowerupType.LightningRange);
            Require(wizard.LightningRange == oldRange + 1f, "Lightning range talent has no effect.");
            var target = run.Enemies[0];
            for (int i = 0; i < 5; i++) run.Player.Upgrade((int)PowerupType.CriticalHits);
            target.Health = 100;
            CombatDamage.Apply(run.Player, target, 5, DamageElement.Fire, origin);
            Require(target.Health == 95, "Elemental damage incorrectly used physical critical multiplier.");
        }

        private static void TestSlots(DungeonRun run)
        {
            var skills = run.Player.Abilities;
            Require(skills.Equipped(0) == AbilityType.Fireball && skills.Equipped(1) == AbilityType.FrostNova, "Q/F ordering failed.");
            Require(skills.Claim(AbilityType.Blink, 0) && skills.Equipped(1) == AbilityType.FrostNova, "Replacing Q overwrote F.");
            Require(!run.Player.Powerups.CanTake(PowerupType.FireballRadius), "Unequipped ability talent was offered.");
            Require(skills.Claim(AbilityType.Blink, 0) && skills.Claim(AbilityType.Blink, 0) && !skills.Claim(AbilityType.Blink, 0), "Artifact rank cap failed.");
            Require(!skills.Claim(AbilityType.Aegis, 1), "Cross-class ability was accepted.");
        }

        private static void TestClass(DungeonRun run, WeaponType type)
        {
            Require(run.Player.ClassWeapon == type, "Wrong class selected.");
            Require(run.Player.Abilities.EmptySlot == 0, "Class swap leaked artifacts.");
            int slot = 0;
            foreach (var ability in AbilityCatalog.All)
            {
                if (ability.ClassWeapon != type || slot >= 2) continue;
                Require(run.Player.Abilities.Claim(ability.Type, slot), "Class artifact failed to equip.");
                slot++;
            }
            Require(slot == 2 && run.Player.Abilities.TryUse(0, Vector2.right), "Class Q ability failed to cast.");
            Require(!run.Player.Abilities.TryUse(0, Vector2.right), "Active ability bypassed cooldown.");
            if (type == WeaponType.Daggers)
            {
                Require(Mathf.Abs(run.Player.Powerups.PhysicalCritChance - 0.15f) < 0.001f, "Assassin bonus crit missing.");
                var enemy = run.Enemies[0];
                enemy.Health = 100;
                CombatDamage.Apply(run.Player, enemy, 3, DamageElement.Physical, (Vector2)enemy.transform.position - enemy.Facing.Direction);
                Require(enemy.Health == 94 || enemy.Health == 88, "Assassin backstab damage missing.");
            }
            if (type == WeaponType.Hammer) Require(run.Player.Shield != null && run.Player.MaxHealth == 7, "Paladin shield/stats missing.");
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool("BossArtifactSmoke", false);
            EditorApplication.update -= Check;
            Application.logMessageReceived -= CaptureError;
            Time.timeScale = 1f;
            Debug.Log((success ? "BOSS_ARTIFACT_SMOKE_OK: " : "BOSS_ARTIFACT_SMOKE_FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static void TestActive(DungeonRun run, AbilityDefinition ability)
        {
            var player = run.Player;
            Require(player.Abilities.Claim(ability.Type, 0), "Ability failed to equip: " + ability.Name);
            var target = run.Enemies[0];
            Vector2 origin = player.transform.position;
            target.transform.position = origin + Vector2.right;
            target.Health = 100;
            if (ability.Type == AbilityType.HealingLight) player.Hit();
            Require(player.Abilities.TryUse(0, Vector2.right), "Ability failed to cast: " + ability.Name);
            foreach (var shot in run.ProjectileRoot.GetComponentsInChildren<SpellProjectile>()) shot.Advance(0.15f);
            foreach (var arrow in run.ProjectileRoot.GetComponentsInChildren<PlayerProjectile>()) arrow.Advance(0.15f);
            switch (ability.Type)
            {
                case AbilityType.FrostNova:
                    Require(target.Health < 100 && target.MoveMultiplier < 1f, "Frost Nova did not damage and chill."); break;
                case AbilityType.Aegis:
                case AbilityType.ShadowVeil:
                case AbilityType.Sanctuary:
                    Require(player.IsInvulnerable, "Protection ability did not protect: " + ability.Name); break;
                case AbilityType.HealingLight:
                    Require(player.Health == player.MaxHealth, "Healing Light did not heal."); break;
                case AbilityType.Blink:
                case AbilityType.Windstep:
                    Require(Vector2.Distance(origin, player.transform.position) > 0.5f && run.Map.CanStand(player.transform.position), "Dash failed or passed through walls."); break;
                default:
                    Require(target.Health < 100, "Offensive ability did not damage its target: " + ability.Name); break;
            }
            foreach (var talent in PowerupCatalog.All)
                if (talent.RequiredAbility == ability.Type)
                {
                    Require(player.Powerups.CanTake(talent.Type), "Matching active talent did not unlock.");
                    for (int i = 0; i < talent.MaxStacks + 1; i++) player.Powerups.Add(talent.Type);
                    Require(player.Powerups.Count(talent.Type) == talent.MaxStacks && !player.Powerups.CanTake(talent.Type), "Active talent cap failed.");
                }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    public static class AdminTests
    {
        private static double started;
        private static int stage, cleanupFrame;
        private static float waitUntil;
        private static bool failed;
        private static readonly List<Mesh> retiredMeshes = new List<Mesh>();
        private static readonly Vector2[] Directions = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("AdminSmoke", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += Log;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run AdminTests in a disposable batch-mode project so rewards use a temporary wallet.");
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            SessionState.SetBool("AdminSmoke", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += Log;
            EditorApplication.EnterPlaymode();
        }

        private static void Log(string message, string stack, LogType type)
        {
            if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
        }

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool DamageMatches(int before, DungeonEnemy target, int damage) =>
            before - target.Health == damage || before - target.Health == damage * 2;

        private static void Freeze(DungeonRun run)
        {
            run.Player.enabled = false;
            foreach (var enemy in run.Enemies)
            {
                enemy.enabled = false;
                var shooter = enemy.GetComponent<EnemyShooter>();
                if (shooter != null) shooter.enabled = false;
                if (enemy.Boss != null) enemy.Boss.enabled = false;
            }
        }

        private static DungeonEnemy Target(DungeonRun run, Vector2 position, int health = 10000)
        {
            var enemy = DungeonVisuals.Create("Admin test target", run.ProjectileRoot, position,
                Vector2.one * 0.6f, Color.red, 3).gameObject.AddComponent<DungeonEnemy>();
            enemy.Run = run;
            enemy.Health = health;
            enemy.enabled = false;
            run.Enemies.Add(enemy);
            return enemy;
        }

        private static void ClearNormalFloor(DungeonRun run)
        {
            while (run.Enemies.Count > 0) run.Enemies[0].Hit(100000);
            run.BeginUpgradeChoice();
            Require(run.ChoosingUpgrade, "Regular floor did not offer a boon.");
            run.ChooseUpgrade(0);
        }

        private static void TestRift(DungeonRun run, AdminAttack admin)
        {
            var player = run.Player;
            Require(run.Characters.Count == 7 && player.ClassWeapon == WeaponType.Shadow && ReferenceEquals(player.Weapon, admin),
                "The sixth class did not initialize the Admin weapon.");
            Require(player.MaxHealth == 12 && player.BaseDamage == 32 && Mathf.Abs(player.Speed - 6.4f) < 0.001f,
                "Admin starting stats are incorrect.");
            Require(player.Charge.Damage(1f) == 96 && player.Charge.Damage(5f) == 96 && player.Charge.Damage(-1f) == 32,
                "Admin charge damage is not capped at three times base damage.");
            Require(admin.RiftRange(-1f) == 8f && admin.RiftRange(1f) == 12f && admin.RiftRange(5f) == 12f,
                "Rift range did not respect its charge cap.");
            Require(!player.Powerups.CanTake(PowerupType.EclipseRadius) && !player.Powerups.CanTake(PowerupType.SoulRendPower)
                && !player.Powerups.CanTake(PowerupType.ReignDuration), "Locked relic talents were offered.");
            Require(!player.Abilities.Claim(AbilityType.Fireball, 0), "Admin accepted a Wizard relic.");
            Vector2 origin = player.transform.position;
            Vector2 aim = Vector2.zero;
            foreach (var direction in Directions)
                if (run.Map.CanStand(origin + direction * 3f) && run.HasLineOfSight(origin, origin + direction * 3f))
                { aim = direction; break; }
            Require(aim != Vector2.zero, "Starting room lacks a three-unit test lane.");
            foreach (var enemy in run.Enemies) enemy.transform.position = origin - aim * 5f;
            var first = Target(run, origin + aim);
            var second = Target(run, origin + aim * 2.8f);
            var missed = Target(run, origin + aim * 1.8f + Vector2.Perpendicular(aim) * 2f);
            Require(admin.TryAttack(aim), "Rift failed to fire.");
            Require(DamageMatches(10000, first, 32) && DamageMatches(10000, second, 32) && missed.Health == 10000,
                "Rift failed to pierce two targets exactly once or hit an enemy outside its width.");
            Require(!admin.TryAttack(aim), "Basic rift ignored its attack cooldown.");
            Require(player.Powerups.Add(PowerupType.RiftReach) && player.Powerups.Add(PowerupType.AbyssalPower),
                "Admin base talents were unavailable.");
            Require(admin.RiftRange(1f) == 13.5f && admin.RiftDamage(0f) == 48 && admin.RiftDamage(1f) == 144,
                "Admin base talents did not affect range and charged damage.");
            player.Powerups.Add(PowerupType.AttackSpeed);
            Require(Mathf.Abs(player.Charge.Duration - 1f) < 0.001f, "Attack speed did not accelerate Admin charging.");
        }

        private static void TestWalls(DungeonRun run, AdminAttack admin)
        {
            for (int x = 1; x < DungeonMap.Width - 1; x++)
                for (int y = 1; y < DungeonMap.Height - 1; y++)
                {
                    Vector2 from = new Vector2(x, y);
                    if (!run.Map.CanStand(from)) continue;
                    foreach (var direction in Directions)
                        for (int distance = 2; distance <= 10; distance++)
                        {
                            Vector2 to = from + direction * distance;
                            if (!run.Map.CanStand(to) || run.HasLineOfSight(from, to)) continue;
                            run.Player.transform.position = from;
                            var hidden = Target(run, to);
                            Require(admin.TryAttack(direction, 1f) && hidden.Health == 10000,
                                "Charged rift damaged a target through a wall.");
                            return;
                        }
                }
            throw new Exception("Generated dungeon lacks a wall-separated test pair.");
        }

        private static void ValidateMeshes(DungeonRun run)
        {
            var effects = run.ProjectileRoot.GetComponentsInChildren<ShadowVfx>();
            Require(effects.Length > 0 && effects.Length <= 32, "Transient shadow effects exceeded their 32-effect bound.");
            foreach (var effect in effects)
            {
                var mesh = effect.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh != null && mesh.vertexCount > 0, "A shadow effect had no geometry.");
                foreach (var vertex in mesh.vertices)
                    Require(Finite(vertex.x) && Finite(vertex.y) && Finite(vertex.z), "Shadow geometry contains NaN or infinity.");
                foreach (var color in mesh.colors)
                    Require(Finite(color.r) && Finite(color.g) && Finite(color.b) && Finite(color.a), "Shadow colors contain invalid values.");
                foreach (int index in mesh.triangles) Require(index >= 0 && index < mesh.vertexCount, "Invalid shadow triangle index.");
                var bounds = mesh.bounds;
                Require(Finite(bounds.center.x) && Finite(bounds.center.y) && Finite(bounds.size.x) && Finite(bounds.size.y),
                    "Invalid shadow mesh bounds.");
            }
        }

        private static void RememberMeshes(Transform root)
        {
            foreach (var effect in root.GetComponentsInChildren<ShadowVfx>(true))
                retiredMeshes.Add(effect.GetComponent<MeshFilter>().sharedMesh);
        }

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying) return;
            var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null) return;
            try
            {
                if (stage == 0)
                {
                    foreach (var character in run.Characters) if (character.Weapon == WeaponType.Shadow) run.SelectCharacter(character);
                    run.Restart(); stage = 1; return;
                }
                if (stage == 10)
                {
                    if (Time.frameCount < cleanupFrame) return;
                    Require(UnityEngine.Object.FindObjectsByType<ShadowVfx>().Length == 0, "Shadow effects survived main-menu cleanup.");
                    foreach (var mesh in retiredMeshes) Require(mesh == null, "Shadow meshes leaked after their owners were destroyed.");
                    Finish(!failed, "Sixth class, capped piercing rifts, walls, dodge/pause/cooldowns, boss reward once, three relics, talents, finite bounded VFX, and cleanup");
                    return;
                }
                if (run.Player == null || run.Player.Weapon == null || Time.time < waitUntil) return;
                Freeze(run);
                var player = run.Player;
                var admin = player.GetComponent<AdminAttack>();
                Require(admin != null, "AdminAttack component was not created.");
                if (stage == 1)
                {
                    TestRift(run, admin); waitUntil = Time.time + 0.3f; stage = 2;
                }
                else if (stage == 2)
                {
                    TestWalls(run, admin); waitUntil = Time.time + 0.3f; stage = 3;
                }
                else if (stage == 3)
                {
                    Require(player.TryRoll(Vector2.right), "Admin could not dodge.");
                    Require(!admin.TryAttack(Vector2.right) && !admin.TryHeavyAttack(Vector2.right)
                        && !admin.CastRelic(AbilityType.ShadowReign, Vector2.right, 1), "Admin attacked during a dodge.");
                    waitUntil = Time.time + 0.3f; stage = 4;
                }
                else if (stage == 4)
                {
                    while (run.Floor < 5) ClearNormalFloor(run);
                    Freeze(run);
                    Require(run.IsBossFloor && run.Boss != null, "Floor five did not contain a boss.");
                    player.transform.position = new Vector2(27, 14);
                    var boss = run.Boss.Enemy;
                    boss.transform.position = new Vector2(27, 18);
                    int ash = run.Progress.Ash;
                    Require(admin.TryHeavyAttack(Vector2.up), "Nightfall failed to cast.");
                    Require(boss.Health <= 0 && run.Enemies.Count == 0 && run.Artifact != null && run.Progress.Ash == ash + 60,
                        "Nightfall failed to execute the guardian, drop its artifact, or grant 50+10 Ash.");
                    boss.Hit(100000);
                    Require(run.Progress.Ash == ash + 60 && !admin.TryHeavyAttack(Vector2.up), "Nightfall repeated its reward or bypassed cooldown.");
                    run.BeginArtifactChoice();
                    Require(!admin.TryAttack(Vector2.up) && !admin.TryHeavyAttack(Vector2.up)
                        && !admin.CastRelic(AbilityType.Eclipse, Vector2.up, 1), "Shadow attacks fired while rewards paused combat.");
                    Require(run.ChooseArtifact(AbilityType.Eclipse, 1) && player.Abilities.Equipped(0) == AbilityType.None,
                        "Admin's first artifact could not bind to E.");
                    Require(player.Powerups.CanTake(PowerupType.EclipseRadius) && !player.Powerups.CanTake(PowerupType.SoulRendPower),
                        "Equipped Admin relic did not gate its matching talent.");
                    waitUntil = Time.time + 0.25f; stage = 5;
                }
                else if (stage == 5)
                {
                    Require(player.Abilities.Claim(AbilityType.Eclipse, 1) && player.Powerups.Add(PowerupType.EclipseRadius),
                        "Eclipse rank or talent upgrade failed.");
                    var near = Target(run, new Vector2(39, 14));
                    var far = Target(run, new Vector2(39, 22));
                    Require(player.Abilities.TryUse(1, Vector2.right) && near.Health <= 0 && far.Health == 10000,
                        "Eclipse failed to execute within its upgraded radius or exceeded it.");
                    Require(!player.Abilities.TryUse(1, Vector2.right), "Eclipse ignored its cooldown.");
                    waitUntil = Time.time + 0.25f; stage = 6;
                }
                else if (stage == 6)
                {
                    Require(player.Abilities.Claim(AbilityType.SoulRend, 0) && player.Powerups.Add(PowerupType.SoulRendPower),
                        "Soul Rend or its talent failed to equip.");
                    var target = Target(run, (Vector2)player.transform.position + Vector2.right * 4f);
                    int damage = admin.RiftDamage(1f) * 4;
                    Require(player.Abilities.TryUse(0, Vector2.right) && DamageMatches(10000, target, damage),
                        "Soul Rend's fully charged damage and matching talent did not apply.");
                    Require(!player.Abilities.TryUse(0, Vector2.right), "Soul Rend ignored its cooldown.");
                    Require(player.Abilities.Claim(AbilityType.ShadowReign, 1) && player.Powerups.Add(PowerupType.ReignDuration)
                        && !player.Powerups.CanTake(PowerupType.EclipseRadius), "Replacing a relic did not update talent eligibility.");
                    Require(!player.Abilities.TryUse(1, Vector2.right) && player.Abilities.CooldownRemaining(1) > 0,
                        "Replacing Eclipse erased its slot cooldown.");
                    waitUntil = Time.time + player.Abilities.CooldownRemaining(1) + 0.05f; stage = 7;
                }
                else if (stage == 7)
                {
                    int before = admin.RiftDamage(1f);
                    Require(player.Abilities.TryUse(1, Vector2.right) && admin.IsReigning && player.IsInvulnerable
                        && admin.RiftDamage(1f) == before * 2, "Shadow Reign did not grant protection and doubled rift damage.");
                    Require(!player.Abilities.TryUse(1, Vector2.right), "Shadow Reign ignored its cooldown.");
                    waitUntil = Time.time + 3.1f; stage = 8;
                }
                else if (stage == 8)
                {
                    Require(admin.IsReigning, "Eternal Reign talent did not extend the three-second base duration.");
                    for (int i = 0; i < 48; i++)
                    {
                        Vector2 center = player.transform.position;
                        switch (i % 4)
                        {
                            case 0: ShadowVfx.Rift(run.ProjectileRoot, center, center, 0f); break;
                            case 1: ShadowVfx.Death(run.ProjectileRoot, center); break;
                            case 2: ShadowVfx.Execution(run.ProjectileRoot, center, 0f); break;
                            default: ShadowVfx.Singularity(run.ProjectileRoot, center, 0f); break;
                        }
                    }
                    ValidateMeshes(run);
                    RememberMeshes(run.ProjectileRoot);
                    while (run.Enemies.Count > 0) run.Enemies[0].Hit(100000);
                    run.BeginUpgradeChoice();
                    Require(run.Floor == 6 && !admin.IsReigning, "Shadow Reign or boss progression leaked across floors.");
                    cleanupFrame = Time.frameCount + 3; stage = 9;
                }
                else if (stage == 9)
                {
                    if (Time.frameCount < cleanupFrame) return;
                    foreach (var mesh in retiredMeshes) Require(mesh == null, "Floor transition leaked shadow meshes.");
                    Require(run.ProjectileRoot.GetComponentsInChildren<ShadowVfx>().Length == 0, "Transient shadows survived changing floors.");
                    Require(player.GetComponentsInChildren<ShadowVfx>().Length == 1, "Admin lost or duplicated its persistent aura.");
                    ShadowVfx.Execution(run.ProjectileRoot, player.transform.position, 9f);
                    RememberMeshes(run.ProjectileRoot);
                    RememberMeshes(player.transform);
                    run.ShowMainMenu(); cleanupFrame = Time.frameCount + 3; stage = 10;
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool("AdminSmoke", false);
            EditorApplication.update -= Check;
            Application.logMessageReceived -= Log;
            Time.timeScale = 1f;
            Debug.Log((success ? "ADMIN_SMOKE_OK: " : "ADMIN_SMOKE_FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}

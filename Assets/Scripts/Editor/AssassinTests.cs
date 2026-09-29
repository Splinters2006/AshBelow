using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    public static class AssassinTests
    {
        private static double started;
        private static bool begun, failed;

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("AssassinSmoke", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += Log;
        }

        public static void Run()
        {
            try { TestLandings(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); return; }
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            SessionState.SetBool("AssassinSmoke", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += Log;
            EditorApplication.EnterPlaymode();
        }

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Log(string message, string stack, LogType type)
        {
            if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
        }

        private static void TestLandings()
        {
            var arena = new DungeonMap(1, true);
            Require(!PlayerAbilities.FindShadowstepLanding(arena, new Vector2(40.2f, 15), Vector2.right, 3f, out _),
                "Blink into the outer wall should fail when no landing exists.");
            Require(!PlayerAbilities.FindShadowstepLanding(arena, new Vector2(25, 15), Vector2.zero, 3f, out _),
                "Zero-direction blink succeeded.");
            Require(PlayerAbilities.FindShadowstepLanding(arena, new Vector2(39, 15), Vector2.right, 3f, out Vector2 edge)
                && arena.CanStand(edge) && edge.x <= 40.22f && edge.x > 39.1f, "Blocked endpoint did not fall back to safe ground.");
            bool crossedWall = false;
            for (int seed = 1; seed <= 12 && !crossedWall; seed++)
            {
                var map = new DungeonMap(seed);
                for (int x = 1; x < DungeonMap.Width - 1 && !crossedWall; x++)
                    for (int y = 1; y < DungeonMap.Height - 1 && !crossedWall; y++)
                    {
                        Vector2 from = new Vector2(x, y);
                        if (!map.CanStand(from)) continue;
                        foreach (var aim in new[] { Vector2.right, Vector2.up, new Vector2(1, 1), new Vector2(1, -1) })
                        {
                            if (!PlayerAbilities.FindShadowstepLanding(map, from, aim, 3f, out Vector2 to)) continue;
                            Require(map.CanStand(to) && Vector2.Distance(from, to) <= 3.001f, "Blink landed outside its safe range.");
                            for (int step = 1; step < 30; step++)
                                if (!map.CanStand(Vector2.Lerp(from, to, step / 30f))) { crossedWall = true; break; }
                        }
                    }
            }
            Require(crossedWall, "No blink crossed a generated wall or obstacle.");
        }

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 60) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying) return;
            var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null) return;
            try
            {
                if (!begun)
                {
                    foreach (var character in run.Characters)
                        if (character.Weapon == WeaponType.Daggers) run.SelectCharacter(character);
                    run.Restart();
                    begun = true;
                    return;
                }
                if (run.Player == null || run.Player.Weapon == null) return;
                var player = run.Player;
                player.enabled = false;
                foreach (var enemy in run.Enemies)
                {
                    enemy.enabled = false;
                    var shooter = enemy.GetComponent<EnemyShooter>();
                    if (shooter != null) shooter.enabled = false;
                }
                Require(player.ClassWeapon == WeaponType.Daggers, "Assassin did not load.");
                Require(Mathf.Abs(player.Charge.Duration - 0.8f) < 0.001f, "Assassin charge is not 50% faster.");
                Require(player.Charge.Damage(1f) == player.Damage * 5 && player.Charge.Damage(3f) == player.Charge.Damage(1f)
                    && player.Charge.Damage(-1f) == player.Damage, "Charge damage or cap is wrong.");
                Require(player.Sword.ChargedCone(0f) == SwordAttack.StabAngle && player.Sword.ChargedCone(1f) == 22f
                    && player.Sword.ChargedCone(0.5f) < SwordAttack.StabAngle, "Assassin charge did not narrow its stab.");
                Vector2 side = new Vector2(Mathf.Cos(16f * Mathf.Deg2Rad), Mathf.Sin(16f * Mathf.Deg2Rad));
                Require(SwordAttack.ContainsTarget(side, Vector2.right, SwordAttack.Reach, player.Sword.ChargedCone(0f))
                    && !SwordAttack.ContainsTarget(side, Vector2.right, SwordAttack.Reach, player.Sword.ChargedCone(1f)),
                    "Narrow cone did not exclude a target that the uncharged slash hits.");
                int fullBackstab = player.Charge.Damage(1f) * 2;
                Require(CombatDamage.ShadowstepDamageForRoll(player, 0.25f) == fullBackstab * 2
                    && CombatDamage.ShadowstepDamageForRoll(player, 0.31f) == fullBackstab,
                    "Shadowstep did not double the normal Assassin crit chance.");

                Vector2 origin = player.transform.position;
                Vector2 aim = Vector2.right;
                foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                    if (run.Map.CanStand(origin + direction * PlayerAbilities.ShadowstepDistance)) { aim = direction; break; }
                Require(run.Map.CanStand(origin + aim * PlayerAbilities.ShadowstepDistance), "No room for sweep test.");
                foreach (var enemy in run.Enemies) enemy.transform.position = origin - aim * 4f;
                var first = run.Enemies[0];
                var second = run.Enemies[1];
                var missed = run.Enemies[2];
                first.transform.position = origin + aim;
                second.transform.position = origin + aim * 2f;
                missed.transform.position = origin + aim * 1.5f + Vector2.Perpendicular(aim) * 1.5f;
                first.Health = second.Health = missed.Health = 1000;
                Require(player.Weapon.TryHeavyAttack(aim * PlayerAbilities.ShadowstepDistance), "RMB failed to shadowstep.");
                Require(Vector2.Distance(player.transform.position, origin + aim * PlayerAbilities.ShadowstepDistance) < 0.01f && player.IsInvulnerable,
                    "Shadowstep failed to travel or grant brief protection.");
                Require(first.Health == 1000 - fullBackstab || first.Health == 1000 - fullBackstab * 2,
                    "First swept enemy did not take exactly one fully charged backstab.");
                Require(second.Health == 1000 - fullBackstab || second.Health == 1000 - fullBackstab * 2,
                    "Second swept enemy did not take exactly one fully charged backstab.");
                Require(first.LastHitRegion == EnemyHitRegion.Back && second.LastHitRegion == EnemyHitRegion.Back
                    && missed.Health == 1000, "Shadowstep hit region or swept area is incorrect.");
                Require(!player.Weapon.TryHeavyAttack(aim), "Shadowstep bypassed its cooldown.");
                player.Powerups.Add(PowerupType.AttackSpeed);
                Require(Mathf.Abs(player.Charge.Duration - 0.8f / 1.2f) < 0.001f, "Attack speed stopped affecting Assassin charge.");
                for (int i = 0; i < 5; i++) { player.Powerups.Add(PowerupType.CriticalHits); player.Powerups.Add(PowerupType.AssassinCrit); }
                Require(CombatDamage.ShadowstepDamageForRoll(player, 0.999f) == fullBackstab * 2,
                    "Doubled crit chance failed to cap at 100%.");
                Finish(!failed, "Safe wall-crossing landings, capped narrow charge, swept backstabs, doubled crit chance, and cooldown");
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool("AssassinSmoke", false);
            EditorApplication.update -= Check;
            Application.logMessageReceived -= Log;
            Time.timeScale = 1f;
            Debug.Log((success ? "ASSASSIN_SMOKE_OK: " : "ASSASSIN_SMOKE_FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}

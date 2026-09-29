using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    public static class DungeonProjectSetup
    {
        private static double smokeStarted;
        private static bool checkedProgression;
        private static bool smokeFailed;
        private static int combatStage;
        private static float combatResumeAt;
        private static int combatHealth;
        private static int heavyStage;
        private static float heavyStarted;
        private static bool menuChecked;
        private static bool switchedToArcher;
        private static int archerStage;
        private static float archerStarted;

        [InitializeOnLoadMethod]
        private static void ResumeSmokeTest()
        {
            if (!SessionState.GetBool("SlopgameSmoke", false)) return;
            smokeStarted = EditorApplication.timeSinceStartup;
            Application.logMessageReceived += CaptureError;
            EditorApplication.update += CheckPlayMode;
        }

        public static void SmokeTest()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            smokeStarted = EditorApplication.timeSinceStartup;
            checkedProgression = false;
            smokeFailed = false;
            combatStage = 0;
            heavyStage = 0;
            universalStage = 0;
            menuChecked = false;
            switchedToArcher = false;
            archerStage = 0;
            SessionState.SetBool("SlopgameSmoke", true);
            Application.logMessageReceived += CaptureError;
            EditorApplication.update += CheckPlayMode;
            EditorApplication.EnterPlaymode();
        }

        private static void CaptureError(string message, string stack, LogType type)
        {
            // Unity 6000.6 can throw while initializing its search index in batch mode.
            // Keep the original error in the log, but do not count editor search as gameplay.
            if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) smokeFailed = true;
        }

        private static void CheckPlayMode()
        {
            if (EditorApplication.timeSinceStartup - smokeStarted > 90)
            {
                Debug.LogError("Play mode smoke test timed out.");
                SessionState.SetBool("SlopgameSmoke", false);
                EditorApplication.Exit(1);
                return;
            }
            if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < 2) return;
            try
            {
                var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
                if (run == null) throw new Exception("Run controller missing.");
                if (!menuChecked)
                {
                    if (!run.IsInMainMenu || run.IsPlaying || run.Player != null || run.Enemies.Count != 0)
                        throw new Exception("Game did not start at a peaceful main menu.");
                    if (run.Characters.Count < 2 || run.SelectedCharacter.DisplayName != "Knight")
                        throw new Exception("Knight character selection missing.");
                    run.SelectCharacter(run.SelectedCharacter);
                    run.Restart();
                    menuChecked = true;
                    return;
                }
                if (run.Player != null && run.Player.Weapon == null) return;
                if (run == null || run.Player == null || run.Enemies.Count == 0) throw new Exception("Run failed to initialize.");
                if (!checkedProgression)
                {
                    if (!CheckCombat(run)) return;
                    if (!CheckHeavyAttack(run)) return;
                    while (run.Enemies.Count > 0) run.Enemies[0].Hit(1000);
                    if (run.Kills == 0) throw new Exception("Kills were not recorded.");
                    run.Player.Upgrade((int)PowerupType.Vitality);
                    run.BeginUpgradeChoice();
                    run.ChooseUpgrade(0);
                    if (run.Floor != 2 || run.Player.MaxHealth < 8 || run.Enemies.Count == 0) throw new Exception("Floor progression failed.");
                    run.EndRun();
                    if (run.IsPlaying) throw new Exception("Death did not stop gameplay.");
                    run.Restart();
                    checkedProgression = true;
                    return;
                }
                if (!switchedToArcher)
                {
                    if (run.Floor != 1 || run.Kills != 0 || run.Player.MaxHealth != 6) throw new Exception("Restart failed to reset the run.");
                    if (UnityEngine.Object.FindObjectsByType<EnemyProjectile>().Length != 0)
                        throw new Exception("Projectiles survived a floor change/restart.");
                    run.ShowMainMenu();
                    foreach (var character in run.Characters)
                        if (character.Weapon == WeaponType.Bow) run.SelectCharacter(character);
                    if (run.SelectedCharacter.DisplayName != "Archer") throw new Exception("Archer selection missing.");
                    run.Restart();
                    switchedToArcher = true;
                    return;
                }
                if (universalStage == 0)
                {
                    if (!CheckArcher(run)) return;
                    CheckPowerupsAndFacing(run);
                }
                if (!CheckUniversalPowerups(run)) return;
                run.ShowMainMenu();
                if (!run.IsInMainMenu || run.IsPlaying || run.Player != null || run.Enemies.Count != 0)
                    throw new Exception("Returning to menu did not clear the run.");
                run.Restart();
                if (run.IsInMainMenu || run.Player.Health != run.SelectedCharacter.StartingHealth || run.Floor != 1)
                    throw new Exception("Starting again from the menu failed.");
                Debug.Log(smokeFailed ? "SLOPGAME_SMOKE_FAILED" : "SLOPGAME_SMOKE_OK: Knight/Archer combat, powerup effects/caps/offers/reset, universal boons, enemy facing/turning/hit regions, menu, and progression passed.");
                EditorApplication.update -= CheckPlayMode;
                SessionState.SetBool("SlopgameSmoke", false);
                Application.logMessageReceived -= CaptureError;
                EditorApplication.Exit(smokeFailed ? 1 : 0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool("SlopgameSmoke", false);
                EditorApplication.Exit(1);
            }
        }

        private static bool CheckCombat(DungeonRun run)
        {
            if (combatStage == 3) return true;
            var player = run.Player;
            if (combatStage == 0)
            {
                Vector2 origin = run.Map.Centers[0];
                player.transform.position = origin;
                var offsets = new[] { Vector2.right * 2f, Vector2.left, Vector2.up, Vector2.right * (SwordAttack.Reach + 0.2f) };
                if (run.Enemies.Count < offsets.Length) throw new Exception("Insufficient combat test targets.");
                foreach (var enemy in run.Enemies)
                {
                    enemy.enabled = false;
                    var shooter = enemy.GetComponent<EnemyShooter>();
                    if (shooter != null) shooter.enabled = false;
                }
                for (int i = 0; i < offsets.Length; i++)
                {
                    run.Enemies[i].transform.position = origin + offsets[i];
                    run.Enemies[i].Health = 10;
                }
                var sword = player.GetComponent<SwordAttack>();
                if (!sword.TryAttack(Vector2.right)) throw new Exception("Sword did not attack.");
                for (int i = 0; i < offsets.Length; i++)
                    if (i == 0 ? run.Enemies[i].Health != 9 && run.Enemies[i].Health != 8 : run.Enemies[i].Health != 10)
                        throw new Exception("Sword cone hit an invalid target or missed its forward target.");
                if (!SwordAttack.ContainsTarget(new Vector2(1, 0.5f), Vector2.right)
                    || SwordAttack.ContainsTarget(new Vector2(1, 1), Vector2.right)
                    || !SwordAttack.ContainsTarget(new Vector2(1, 1), Vector2.right, SwordAttack.Reach, 120f))
                    throw new Exception("Light/heavy cone boundary mismatch.");
                var caster = run.Enemies.Find(enemy => enemy.IsRanged);
                if (caster == null) throw new Exception("No ranged enemies spawned.");
                caster.transform.position = origin;
                caster.enabled = true;
                combatHealth = player.Health;
                combatStage = 1;
                return false;
            }
            if (combatStage == 1)
            {
                if (player.Health != combatHealth) throw new Exception("Ranged enemy dealt contact damage.");
                foreach (var enemy in run.Enemies) enemy.enabled = false;
                if (!player.TryRoll(Vector2.right) || !player.IsInvulnerable) throw new Exception("Dodge did not start.");
                var bolt = EnemyProjectile.Spawn(run, run.Enemies[0].transform.parent,
                    (Vector2)player.transform.position + Vector2.right * 0.3f, Vector2.left);
                bolt.Advance(0.01f);
                if (!bolt.IsSpent || player.Health != combatHealth) throw new Exception("Dodge did not block a projectile.");
                if (player.GetComponent<SwordAttack>().TryAttack(Vector2.right)) throw new Exception("Player attacked during dodge.");
                combatResumeAt = Time.time + 0.3f;
                combatStage = 2;
                return false;
            }
            if (Time.time < combatResumeAt) return false;
            if (player.IsInvulnerable || player.IsRolling) throw new Exception("Dodge immunity did not expire.");
            if (player.TryRoll(Vector2.right)) throw new Exception("Dodge cooldown was bypassed.");
            player.transform.position = (Vector2)run.Map.Centers[0];
            var hit = EnemyProjectile.Spawn(run, run.Enemies[0].transform.parent,
                (Vector2)player.transform.position + Vector2.right * 0.3f, Vector2.left);
            hit.Advance(0.01f);
            if (!hit.IsSpent || player.Health != combatHealth - 1) throw new Exception("Projectile did not damage player after dodge.");
            var wall = EnemyProjectile.Spawn(run, run.Enemies[0].transform.parent, new Vector2(-1, -1), Vector2.right);
            wall.Advance(0.01f);
            if (!wall.IsSpent) throw new Exception("Projectile passed through a wall.");
            EnemyProjectile.Spawn(run, run.Enemies[0].transform.parent, run.Map.Centers[1], Vector2.right);
            combatStage = 3;
            return true;
        }

        private static bool CheckHeavyAttack(DungeonRun run)
        {
            var player = run.Player;
            var sword = player.Sword;
            if (heavyStage == 0)
            {
                if (Time.timeSinceLevelLoad < 3.2f || player.IsInvulnerable) return false;
                Vector2 origin = run.Map.Centers[0];
                player.transform.position = origin;
                foreach (var enemy in run.Enemies) enemy.transform.position = (Vector2)run.Map.Centers[run.Map.Centers.Count - 1];
                run.Enemies[0].transform.position = origin + Vector2.right * 1.8f;
                run.Enemies[1].transform.position = origin + new Vector2(1f, 1f);
                run.Enemies[0].Health = run.Enemies[1].Health = 20;
                if (player.Charge.Damage(-1f) != 1 || player.Charge.Damage(0.5f) != 2 || player.Charge.Damage(10f) != 3)
                    throw new Exception("Charge damage cap failed.");
                if (!sword.TryAttack(Vector2.right, 1f) || (run.Enemies[0].Health != 17 && run.Enemies[0].Health != 14)
                    || (run.Enemies[1].Health != 17 && run.Enemies[1].Health != 14))
                    throw new Exception("Charged slash damage or widened cone failed.");
                if (!sword.TryHeavyAttack(Vector2.right)) throw new Exception("Shield did not raise.");
                if (sword.TryAttack(Vector2.right)) throw new Exception("Attacked through raised shield.");
                var bolt = EnemyProjectile.Spawn(run, run.ProjectileRoot, origin + Vector2.right * 0.8f, Vector2.left);
                bolt.Advance(0.01f);
                if (!bolt.IsReflected || bolt.IsSpent) throw new Exception("Shield did not reflect frontal bolt.");
                int reflectedTargetHealth = run.Enemies[0].Health;
                bolt.Advance(0.3f);
                int reflectedLoss = reflectedTargetHealth - run.Enemies[0].Health;
                if (!bolt.IsSpent || (reflectedLoss != 2 && reflectedLoss != 4)) throw new Exception("Reflected bolt did not damage enemy exactly once.");
                int health = player.Health;
                var rear = EnemyProjectile.Spawn(run, run.ProjectileRoot, origin - Vector2.right * 0.3f, Vector2.right);
                rear.Advance(0.01f);
                if (rear.IsReflected || !rear.IsSpent || player.Health != health - 1)
                    throw new Exception("Shield incorrectly protected player's rear.");
                heavyStarted = Time.time;
                heavyStage = 1;
                return false;
            }
            if (heavyStage == 1)
            {
                if (Time.time - heavyStarted < 0.9f) return false;
                if (sword.IsHeavyAttacking || sword.TryHeavyAttack(Vector2.right))
                    throw new Exception("Shield duration or cooldown failed.");
                if (Mathf.Abs(sword.ChargedCone(10f) - 120f) > 0.01f) throw new Exception("Charged cone cap failed.");
                player.Upgrade((int)PowerupType.SweepingEdge);
                player.Upgrade((int)PowerupType.SweepingEdge);
                player.Upgrade((int)PowerupType.SweepingEdge);
                if (sword.ChargedCone(1f) != 150f || player.Powerups.CanTake(PowerupType.SweepingEdge))
                    throw new Exception("Knight cone talent/cap failed.");
                if (player.Powerups.Add(PowerupType.QuickDraw)) throw new Exception("Knight accepted archer talent.");
                heavyStage = 2;
            }
            if (heavyStage == 2)
            {
                if (Time.time - heavyStarted < KnightShield.Cooldown + 0.05f) return false;
                if (!sword.TryHeavyAttack(Vector2.right)) throw new Exception("Shield cooldown did not expire.");
                if (!player.TryRoll(Vector2.left) || sword.IsHeavyAttacking) throw new Exception("Dodge did not cancel shield.");
                if (sword.HeavyCooldownRemaining < KnightShield.Cooldown - 0.1f) throw new Exception("Cancel refunded shield cooldown.");
                heavyStarted = Time.time;
                heavyStage = 3;
                return false;
            }
            if (Time.time - heavyStarted < 1.5f) return false;
            player.Charge.Tick(true, true);
            if (!player.Charge.IsCharging) throw new Exception("Held attack did not begin charging.");
            if (!player.TryRoll(Vector2.left) || player.Charge.IsCharging) throw new Exception("Dodge did not cancel charge.");
            player.Charge.Tick(false, false);
            return true;
        }
        private static bool CheckArcher(DungeonRun run)
        {
            var bow = run.Player.Weapon as BowAttack;
            if (bow == null || run.Player.Sword != null || run.Player.MaxHealth != 5)
                throw new Exception("Archer did not initialize with bow and five HP.");
            if (archerStage == 0)
            {
                foreach (var enemy in run.Enemies)
                {
                    enemy.enabled = false;
                    var shooter = enemy.GetComponent<EnemyShooter>();
                    if (shooter != null) shooter.enabled = false;
                    enemy.transform.position = (Vector2)run.Map.Centers[run.Map.Centers.Count - 1];
                }
                run.Player.transform.position = (Vector2)run.Map.Centers[0];
                var target = run.Enemies[0];
                target.Health = 20;
                target.transform.position = run.Player.transform.position + Vector3.right;
                if (!bow.TryAttack(Vector2.right, 1f)) throw new Exception("Archer charged attack failed.");
                var arrows = UnityEngine.Object.FindObjectsByType<PlayerProjectile>();
                if (arrows.Length != 1) throw new Exception("Base attack must fire one arrow.");
                if (Mathf.Abs(arrows[0].RemainingRange - 6f) > 0.001f) throw new Exception("Charged basic arrow did not gain range.");
                arrows[0].Advance(0.12f);
                if ((target.Health != 17 && target.Health != 14) || !arrows[0].IsSpent || run.Player.Health != 5)
                    throw new Exception("Arrow collision/damage failed.");
                var wall = PlayerProjectile.Spawn(run, new Vector2(-1, -1), Vector2.right, 1);
                wall.Advance(0.1f);
                if (!wall.IsSpent) throw new Exception("Arrow passed through a wall.");
                CheckArrowRange(run);
                archerStarted = Time.time;
                archerStage = 1;
                return false;
            }
            if (archerStage == 1)
            {
                if (Time.time - archerStarted < 0.4f) return false;
                if (!bow.TryHeavyAttack(Vector2.right)) throw new Exception("Triple shot failed.");
                var arrows = UnityEngine.Object.FindObjectsByType<PlayerProjectile>();
                if (arrows.Length != 3) throw new Exception("Heavy attack must fire exactly three arrows.");
                var angles = new List<float>();
                foreach (var arrow in arrows)
                {
                    if (Mathf.Abs(arrow.RemainingRange - BowAttack.HeavyRange) > 0.001f) throw new Exception("Triple shot range is incorrect.");
                    angles.Add(Vector2.SignedAngle(Vector2.right, arrow.Direction));
                }
                angles.Sort();
                if (Mathf.Abs(angles[0] + BowAttack.SpreadAngle) > 0.01f || Mathf.Abs(angles[1]) > 0.01f || Mathf.Abs(angles[2] - BowAttack.SpreadAngle) > 0.01f)
                    throw new Exception("Triple arrow cone spread is incorrect.");
                if (Mathf.Abs(bow.HeavyCooldownRemaining - 6f) > 0.02f) throw new Exception("Archer cooldown must be six seconds.");
                archerStarted = Time.time;
                archerStage = 2;
                return false;
            }
            if (archerStage == 2)
            {
                if (Time.time - archerStarted < 0.5f) return false;
                if (bow.TryHeavyAttack(Vector2.right)) throw new Exception("Archer bypassed heavy cooldown.");
                if (!bow.TryAttack(Vector2.right)) throw new Exception("Heavy cooldown blocked the base attack.");
                run.Player.TryRoll(Vector2.left);
                if (bow.TryAttack(Vector2.right) || bow.TryHeavyAttack(Vector2.right)) throw new Exception("Archer fired during dodge.");
                archerStage = 3;
            }
            if (archerStage == 3)
            {
                if (Time.time - archerStarted < 6.1f) return false;
                if (!bow.TryHeavyAttack(Vector2.right)) throw new Exception("Archer heavy cooldown did not expire.");
                run.Player.Upgrade((int)PowerupType.Damage);
                while (run.Enemies.Count > 0) run.Enemies[0].Hit(1000);
                run.BeginUpgradeChoice();
                run.ChooseUpgrade(0);
                archerStage = 4;
                return false;
            }
            if (UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length != 0)
                throw new Exception("Arrows survived a floor change.");
            if (run.Player.Damage < 2) throw new Exception("Archer damage upgrade failed.");
            return true;
        }

        private static void CheckPowerupsAndFacing(DungeonRun run)
        {
            run.Restart();
            var player = run.Player;
            var powers = player.Powerups;
            if (powers.Add(PowerupType.Riposte)) throw new Exception("Archer accepted knight talent.");
            for (int i = 0; i < 3; i++)
            {
                powers.Add(PowerupType.QuickDraw);
                powers.Add(PowerupType.Bodkin);
            }
            if (Mathf.Abs(player.Charge.Duration - 0.7f) > 0.001f || player.Charge.Damage(10f) != 4
                || powers.CanTake(PowerupType.Bodkin) || powers.CanTake(PowerupType.QuickDraw))
                throw new Exception("Archer talents or charge cap failed.");
            for (int floor = 1; floor <= 8; floor++)
                if (DungeonRun.EnemyHealthForFloor(floor) != (floor <= 3 ? 2 : floor - 1))
                    throw new Exception("Enemy HP scaling after floor three failed.");
            foreach (var type in new[] { PowerupType.AttackSpeed, PowerupType.CriticalHits, PowerupType.Armor, PowerupType.DodgeRecovery })
            {
                int cap = PowerupCatalog.Get(type).MaxStacks;
                for (int i = 0; i < cap + 1; i++) player.Upgrade((int)type);
                if (powers.Count(type) != cap || powers.CanTake(type)) throw new Exception("Powerup stacking cap failed.");
            }
            if (Mathf.Abs(powers.AttackIntervalMultiplier - 0.5f) > 0.001f
                || Mathf.Abs(powers.DodgeCooldownMultiplier - 0.7f) > 0.001f)
                throw new Exception("Attack/dodge powerup scaling failed.");
            if (Mathf.Abs(player.Charge.Duration - 0.35f) > 0.001f)
                throw new Exception("Attack speed did not stack with Quick Draw.");
            if (powers.DamageForRoll(3, 0.54f) != 6 || powers.DamageForRoll(3, 0.55f) != 3)
                throw new Exception("Critical damage calculation failed.");
            int health = player.Health;
            player.Hit();
            if (player.Health != health || powers.ArmorCharges != 2) throw new Exception("Ward did not absorb damage.");
            powers.BeginFloor();
            if (powers.ArmorCharges != 3) throw new Exception("Ward did not refresh on descent.");
            while (powers.AbsorbHit()) { }
            player.Upgrade((int)PowerupType.LifeSteal);
            // Use a separate fresh player to verify kill healing without the ward hit's immunity timer.
            run.Restart();
            player = run.Player;
            powers = player.Powerups;
            if (powers.Count(PowerupType.Armor) != 0 || Mathf.Abs(powers.CritChance - 0.05f) > 0.001f) throw new Exception("Powerups leaked into a new run.");
            player.Upgrade((int)PowerupType.LifeSteal);
            player.Hit();
            health = player.Health;
            for (int i = 0; i < 4; i++) powers.OnKill(player, null);
            if (player.Health != health) throw new Exception("Soul Harvest healed early.");
            powers.OnKill(player, null);
            if (player.Health != health + 1) throw new Exception("Soul Harvest failed to heal.");
            var enemy = run.Enemies[0];
            Vector2 origin = enemy.transform.position;
            var facing = enemy.Facing;
            Vector2 before = facing.Direction;
            facing.TurnToward(-before * 4f, 0.1f);
            if (Vector2.Angle(before, facing.Direction) > 18.01f || Vector2.Angle(before, facing.Direction) < 17.99f)
                throw new Exception("Enemy turn speed is not limited to 180 degrees per second.");
            before = facing.Direction;
            facing.TurnToward(-before * 0.5f, 0.1f);
            if (Mathf.Abs(Vector2.Angle(before, facing.Direction) - 6.3f) > 0.02f)
                throw new Exception("Enemy close-range turning did not slow down.");
            Vector2 front = facing.Direction;
            if (!facing.IsInFront(origin + front) || !facing.IsBehind(origin - front)
                || facing.RegionFrom(origin + new Vector2(-front.y, front.x)) != EnemyHitRegion.Side)
                throw new Exception("Enemy front/back/side classification failed.");
            enemy.Health = 10;
            enemy.Hit(1, origin - front);
            if (enemy.LastHitRegion != EnemyHitRegion.Back || enemy.Health != 9) throw new Exception("Rear hit was not recorded correctly.");
            while (run.Enemies.Count > 0) run.Enemies[0].Hit(1000);
            for (int i = 0; i < 5; i++) player.Upgrade((int)PowerupType.CriticalHits);
            run.BeginUpgradeChoice();
            var seen = new HashSet<PowerupType>();
            if (run.UpgradeChoices.Count != 3) throw new Exception("Missing powerup offers.");
            foreach (var choice in run.UpgradeChoices)
                if (!seen.Add(choice.Type) || !powers.CanTake(choice.Type)) throw new Exception("Duplicate or capped boon offered.");
            if (!seen.Contains(PowerupType.QuickDraw) && !seen.Contains(PowerupType.Bodkin))
                throw new Exception("Upgrade offers missing available class talent.");
            var selected = run.UpgradeChoices[0].Type;
            int previous = powers.Count(selected);
            run.ChooseUpgrade(0);
            if (run.Floor != 2 || powers.Count(selected) != previous + 1) throw new Exception("Chosen powerup was not applied.");
            run.ChooseUpgrade(0);
            if (run.Floor != 2) throw new Exception("Upgrade accepted outside the selection screen.");
        }

        private static int universalStage;

        /// <summary>
        /// The universal boons: catalog order, kill-streak and paralysis resets, cooldown cuts, Kindling, Pyre Burst,
        /// Prospector and the crystal shop's Haggler prices and Merchant's Favor reroll. Each check starts a fresh run,
        /// since every kill in one editor frame lands inside the same one-second streak; the class-skill checks wait a
        /// frame for the new hero's weapon. Returns true once every stage has passed.
        /// </summary>
        private static bool CheckUniversalPowerups(DungeonRun run)
        {
            DungeonPlayer player;
            switch (universalStage)
            {
                case 0:
                    foreach (var powerup in PowerupCatalog.All)
                        if (PowerupCatalog.Get(powerup.Type) != powerup) throw new Exception("Powerup catalog order does not match PowerupType.");

                    run.Restart();
                    player = run.Player;
                    player.Upgrade((int)PowerupType.Momentum);
                    if (!player.TryRoll(Vector2.right)) throw new Exception("Could not roll for the Momentum check.");
                    player.Powerups.OnKill(player, null);
                    if (player.DodgeCooldownRemaining <= 0f) throw new Exception("Momentum reset the dodge after one kill.");
                    player.Powerups.OnKill(player, null);
                    if (player.DodgeCooldownRemaining > 0f) throw new Exception("Momentum did not reset the dodge after two quick kills.");

                    run.Restart();
                    player = run.Player;
                    player.Upgrade((int)PowerupType.Bloodrush);
                    player.TryRoll(Vector2.right);
                    float dodge = player.DodgeCooldownRemaining;
                    player.Powerups.OnKill(player, null);
                    if (Mathf.Abs(dodge - player.DodgeCooldownRemaining - PlayerPowerups.KillCooldownCut) > 0.001f)
                        throw new Exception("Bloodrush did not take half a second off the dodge.");

                    run.Restart();
                    player = run.Player;
                    if (player.Powerups.RollExtraCrystals()) throw new Exception("Extra crystals dropped without Prospector.");
                    player.Upgrade((int)PowerupType.Prospector);
                    player.Upgrade((int)PowerupType.Prospector);
                    if (!player.Powerups.RollExtraCrystals()) throw new Exception("Prospector rank two did not always drop extra crystals.");

                    var shop = CrystalShop.Create(run, new GameObject("Test shop").transform);
                    var hone = Array.Find(CrystalShop.Offers, offer => offer.Ware == CrystalShop.Ware.EmberHone);
                    if (shop.Cost(hone) != 60 || shop.RerollsLeft != 0 || shop.Reroll()) throw new Exception("Shop prices or rerolls changed without boons.");
                    player.Upgrade((int)PowerupType.Haggler);
                    player.Upgrade((int)PowerupType.MerchantsFavor);
                    if (shop.Cost(hone) != 45) throw new Exception("Haggler did not take 25% off shop prices.");
                    if (!shop.Reroll() || shop.RerollsLeft != 0 || shop.Stock.Count != 5) throw new Exception("Merchant's Favor reroll failed.");
                    UnityEngine.Object.Destroy(shop.transform.parent.gameObject);

                    run.Restart();
                    universalStage = 1;
                    return false;
                case 1:
                    player = run.Player;
                    player.Upgrade((int)PowerupType.Massacre);
                    if (!player.Weapon.TryHeavyAttack(Vector2.right)) throw new Exception("Could not use the class skill for the Massacre check.");
                    for (int i = 0; i < PlayerPowerups.MassacreKills - 1; i++) player.Powerups.OnKill(player, null);
                    if (player.Weapon.HeavyCooldownRemaining <= 0f) throw new Exception("Massacre reset the class skill too early.");
                    player.Powerups.OnKill(player, null);
                    if (player.Weapon.HeavyCooldownRemaining > 0f) throw new Exception("Massacre did not reset the class skill.");
                    run.Restart();
                    universalStage = 2;
                    return false;
                default:
                    player = run.Player;
                    var enemy = run.Enemies[0];
                    var neighbour = run.Enemies[1];
                    enemy.Health = neighbour.Health = 50;
                    CombatDamage.ApplyEffect(player, enemy, DamageElement.Ice, 2);
                    if (enemy.IsBurning || !enemy.IsFrozen) throw new Exception("Ice set an enemy burning without Kindling.");
                    player.Upgrade((int)PowerupType.Kindling);
                    player.Upgrade((int)PowerupType.NerveSnap);
                    player.Upgrade((int)PowerupType.PyreBurst);
                    CombatDamage.ApplyEffect(player, enemy, DamageElement.Ice, 2);
                    if (!enemy.IsBurning) throw new Exception("Kindling did not set a frozen enemy burning.");
                    if (!player.Weapon.TryHeavyAttack(Vector2.right)) throw new Exception("Could not use the class skill for the Nerve Snap check.");
                    neighbour.transform.position = enemy.transform.position + Vector3.right * 0.8f;
                    enemy.Hit(1000);
                    if (player.Weapon.HeavyCooldownRemaining > 0f) throw new Exception("Nerve Snap did not reset the class skill on a frozen kill.");
                    if (neighbour.Health >= 50) throw new Exception("Pyre Burst did not hurt an enemy beside a burning kill.");
                    run.Restart();
                    return true;
            }
        }

        private static void CheckArrowRange(DungeonRun run)
        {
            for (int y = 1; y < DungeonMap.Height - 1; y++)
                for (int x = 1; x < DungeonMap.Width - 7; x++)
                {
                    var origin = new Vector2(x, y);
                    bool clear = true;
                    for (int step = 0; step <= 60; step++)
                        if (!run.Map.CanStand(origin + Vector2.right * (step * 0.1f), 0.08f)) { clear = false; break; }
                    if (!clear) continue;
                    var positions = new List<Vector3>();
                    foreach (var enemy in run.Enemies)
                    {
                        positions.Add(enemy.transform.position);
                        enemy.transform.position = new Vector3(-10, -10, 0);
                    }
                    foreach (float charge in new[] { -1f, 0f, 0.5f, 1f, 2f })
                    {
                        float expectedRange = charge <= 0f ? 5f : charge < 1f ? 5.5f : 6f;
                        var arrow = PlayerProjectile.Spawn(run, origin, Vector2.right, 1, BowAttack.RangeForCharge(charge));
                        arrow.Advance(0.4f);
                        if (arrow.IsSpent || Mathf.Abs(Vector2.Distance(origin, arrow.transform.position) - 4.8f) > 0.01f)
                            throw new Exception("Arrow expired early or changed speed.");
                        arrow.Advance(0.2f);
                        if (!arrow.IsSpent || Mathf.Abs(Vector2.Distance(origin, arrow.transform.position) - expectedRange) > 0.01f)
                            throw new Exception("Arrow charge range scaling or cap failed.");
                    }
                    for (int i = 0; i < positions.Count; i++) run.Enemies[i].transform.position = positions[i];
                    return;
                }
            throw new Exception("No clear corridor found for arrow range test.");
        }

        public static void EnsureCharacterAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Characters")) AssetDatabase.CreateFolder("Assets/Resources", "Characters");
            const string path = "Assets/Resources/Characters/Knight.asset";
            if (AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CharacterDefinition>(), path);
            const string archerPath = "Assets/Resources/Characters/Archer.asset";
            if (AssetDatabase.LoadAssetAtPath<CharacterDefinition>(archerPath) == null)
            {
                var archer = ScriptableObject.CreateInstance<CharacterDefinition>();
                var serialized = new SerializedObject(archer);
                serialized.FindProperty("displayName").stringValue = "Archer";
                serialized.FindProperty("description").stringValue = "A nimble archer who fights up close. Charge arrows for up to triple damage and a little extra range, or loose a tight three-arrow spread.";
                serialized.FindProperty("startingHealth").intValue = 5;
                serialized.FindProperty("color").colorValue = new Color(0.65f, 0.85f, 0.35f);
                serialized.FindProperty("weapon").enumValueIndex = (int)WeaponType.Bow;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(archer, archerPath);
            }
            EnsureHero("Wizard", WeaponType.Staff, 4, 4.8f, new Color(0.65f, 0.45f, 1f),
                "A fire-and-storm caster. Charge fireballs that burst on impact, and cast lightning that can chain from foe to foe.");
            EnsureHero("Assassin", WeaponType.Daggers, 4, 5.6f, new Color(0.78f, 0.4f, 0.65f),
                "A shadow-cloaked killer. Charge into a narrow, deadly stab, hit from behind for double damage, and Shadowstep through walls to backstab everything in your path.");
            EnsureHero("Paladin", WeaponType.Hammer, 7, 4.5f, new Color(0.95f, 0.78f, 0.4f),
                "A holy warrior who would die for his allies. Hold to bless nearby friends with extra damage, and call down holy swords on the enemies around you.");
            EnsureHero("Admin", WeaponType.Shadow, 12, 6.4f, new Color(0.58f, 0.25f, 0.95f),
                "An unbound shadow sovereign. Tear through enemies with shadow rifts, while Nightfall executes every nearby enemy, even guardians. Intentionally overpowered.", 32);
            EnsureHero("Brawler", WeaponType.Fists, 6, 5.3f, new Color(0.98f, 0.62f, 0.42f), BrawlerDescription);
            EnsureHero("Demoness", WeaponType.Tail, 5, 5.2f, new Color(0.6f, 0.36f, 0.9f), DemonessDescription);
            EnsureHero("Gambler", WeaponType.Coins, 5, 5.1f, new Color(0.35f, 0.72f, 0.45f), GamblerDescription);
            AssetDatabase.SaveAssets();
            Debug.Log("CHARACTER_ASSETS_OK: Nine classes ready.");
        }

        private const string DemonessDescription = "The pale, ram-horned daughter of the Demon Lord. Stab with her pointed tail, charge to strike the vitals and paralyse, then sweep her tail through paralysed foes for double damage.";
        private const string GamblerDescription = "A travelling merchant hopelessly addicted to gambling. Throw coins and scoop up the gold every fallen foe drops, fling a volley of every coin you carry, and open your purse to spend or bet it all.";
        private const string BrawlerDescription = "A scrappy puppygirl pit-fighter in big red gloves. Jab fast, charge a hammering barrage of punches, and Empower to hit harder, faster and wider.";

        private static void EnsureHero(string name, WeaponType weapon, int health, float speed, Color color, string description, int damage = 1)
        {
            string path = "Assets/Resources/Characters/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path) != null) return;
            var hero = ScriptableObject.CreateInstance<CharacterDefinition>();
            var serialized = new SerializedObject(hero);
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("startingHealth").intValue = health;
            serialized.FindProperty("startingDamage").intValue = damage;
            serialized.FindProperty("moveSpeed").floatValue = speed;
            serialized.FindProperty("color").colorValue = color;
            serialized.FindProperty("weapon").enumValueIndex = (int)weapon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(hero, path);
        }

        [MenuItem("Slopgame/Create playable dungeon scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureCharacterAssets();
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Dungeon Run").AddComponent<DungeonRun>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Dungeon.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Dungeon.unity", true) };
            ValidateMaps();
            Debug.Log("SLOPGAME_SETUP_OK: Dungeon scene created; 500 dungeon seeds validated.");
        }

        [MenuItem("Slopgame/Validate generated dungeons")]
        public static void ValidateMaps()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var map = new DungeonMap(seed);
                if (map.Centers.Count < 2) throw new Exception("Too few rooms: " + seed);
                var visited = new HashSet<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(map.Centers[0]);
                visited.Add(map.Centers[0]);
                var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var direction in directions)
                    {
                        var next = cell + direction;
                        if (map.IsFloor(next.x, next.y) && visited.Add(next)) queue.Enqueue(next);
                    }
                }
                foreach (var center in map.Centers)
                {
                    if (!visited.Contains(center) || !map.CanStand(center)) throw new Exception("Unreachable room: " + seed);
                    for (int i = 0; i < 4; i++)
                        if (!map.CanStand((Vector2)center + new Vector2(i % 2, i / 2))) throw new Exception("Invalid spawn: " + seed);
                }
                for (int x = 0; x < DungeonMap.Width; x++)
                    for (int y = 0; y < DungeonMap.Height; y++)
                        if (map.IsFloor(x, y) && !visited.Contains(new Vector2Int(x, y))) throw new Exception("Disconnected floor: " + seed);
                if (map.CanStand(new Vector2(-1, -1))) throw new Exception("Bounds check failed");
            }
        }
    }
}

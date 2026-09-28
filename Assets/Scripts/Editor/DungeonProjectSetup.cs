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
                var run = UnityEngine.Object.FindFirstObjectByType<DungeonRun>();
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
                    if (UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length != 0)
                        throw new Exception("Projectiles survived a floor change/restart.");
                    run.ShowMainMenu();
                    foreach (var character in run.Characters)
                        if (character.Weapon == WeaponType.Bow) run.SelectCharacter(character);
                    if (run.SelectedCharacter.DisplayName != "Archer") throw new Exception("Archer selection missing.");
                    run.Restart();
                    switchedToArcher = true;
                    return;
                }
                if (!CheckArcher(run)) return;
                CheckPowerupsAndFacing(run);
                run.ShowMainMenu();
                if (!run.IsInMainMenu || run.IsPlaying || run.Player != null || run.Enemies.Count != 0)
                    throw new Exception("Returning to menu did not clear the run.");
                run.Restart();
                if (run.IsInMainMenu || run.Player.Health != run.SelectedCharacter.StartingHealth || run.Floor != 1)
                    throw new Exception("Starting again from the menu failed.");
                Debug.Log(smokeFailed ? "SLOPGAME_SMOKE_FAILED" : "SLOPGAME_SMOKE_OK: Knight/Archer combat, powerup effects/caps/offers/reset, enemy facing/turning/hit regions, menu, and progression passed.");
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
                    if (run.Enemies[i].Health != (i == 0 ? 9 : 10)) throw new Exception("Sword cone hit an invalid target or missed its forward target.");
                if (!SwordAttack.ContainsTarget(new Vector2(1, 0.5f), Vector2.right)
                    || SwordAttack.ContainsTarget(new Vector2(1, 1), Vector2.right)
                    || !SwordAttack.ContainsTarget(new Vector2(1, 1), Vector2.right, SwordAttack.HeavyReach, SwordAttack.HeavyConeAngle))
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
            var sword = run.Player.Sword;
            if (heavyStage == 0)
            {
                if (Time.timeSinceLevelLoad < 3.2f) return false;
                Vector2 origin = (Vector2)run.Map.Centers[0] - Vector2.right;
                run.Player.transform.position = origin;
                foreach (var enemy in run.Enemies) enemy.transform.position = (Vector2)run.Map.Centers[run.Map.Centers.Count - 1];
                run.Enemies[0].transform.position = origin + Vector2.right * (SwordAttack.HeavyReach - 0.3f);
                run.Enemies[1].transform.position = origin - Vector2.right;
                run.Enemies[0].Health = run.Enemies[1].Health = 20;
                if (!sword.TryHeavyAttack(Vector2.right)) throw new Exception("Heavy attack did not start.");
                if (Mathf.Abs(sword.HeavyCooldownRemaining - 5f) > 0.02f) throw new Exception("Heavy cooldown is not five seconds.");
                if (sword.TryAttack(Vector2.right)) throw new Exception("Light attack interrupted heavy attack.");
                heavyStarted = Time.time;
                heavyStage = 1;
                return false;
            }
            float elapsed = Time.time - heavyStarted;
            if (heavyStage == 1)
            {
                if (elapsed < 0.2f) return false;
                if (run.Enemies[0].Health != 20) throw new Exception("Heavy attack damaged during windup.");
                heavyStage = 2;
            }
            if (heavyStage == 2)
            {
                if (elapsed < 1.1f) return false;
                if (run.Enemies[0].Health != 17 || run.Enemies[1].Health != 20)
                    throw new Exception("Heavy range, damage, direction, or single-hit behavior failed.");
                if (sword.TryHeavyAttack(Vector2.right)) throw new Exception("Heavy cooldown was bypassed.");
                heavyStage = 3;
            }
            if (heavyStage == 3)
            {
                if (elapsed < 5.05f) return false;
                if (!sword.TryHeavyAttack(Vector2.right)) throw new Exception("Heavy cooldown did not expire.");
                if (!run.Player.TryRoll(Vector2.left) || sword.IsHeavyAttacking) throw new Exception("Dodge did not cancel heavy attack.");
                if (sword.HeavyCooldownRemaining < 4.9f) throw new Exception("Cancelled heavy refunded cooldown.");
                heavyStarted = Time.time;
                heavyStage = 4;
                return false;
            }
            if (Time.time - heavyStarted < 1f) return false;
            if (run.Enemies[0].Health != 17) throw new Exception("Cancelled heavy dealt delayed damage.");
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
                if (!bow.TryAttack(Vector2.right)) throw new Exception("Archer base attack failed.");
                var arrows = UnityEngine.Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None);
                if (arrows.Length != 1) throw new Exception("Base attack must fire one arrow.");
                arrows[0].Advance(0.12f);
                if (target.Health != 19 || !arrows[0].IsSpent || run.Player.Health != 5)
                    throw new Exception("Arrow collision/damage failed.");
                var wall = PlayerProjectile.Spawn(run, new Vector2(-1, -1), Vector2.right, 1);
                wall.Advance(0.1f);
                if (!wall.IsSpent) throw new Exception("Arrow passed through a wall.");
                archerStarted = Time.time;
                archerStage = 1;
                return false;
            }
            if (archerStage == 1)
            {
                if (Time.time - archerStarted < 0.4f) return false;
                if (!bow.TryHeavyAttack(Vector2.right)) throw new Exception("Triple shot failed.");
                var arrows = UnityEngine.Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None);
                if (arrows.Length != 3) throw new Exception("Heavy attack must fire exactly three arrows.");
                var angles = new List<float>();
                foreach (var arrow in arrows) angles.Add(Vector2.SignedAngle(Vector2.right, arrow.Direction));
                angles.Sort();
                if (Mathf.Abs(angles[0] + 15) > 0.01f || Mathf.Abs(angles[1]) > 0.01f || Mathf.Abs(angles[2] - 15) > 0.01f)
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
            if (UnityEngine.Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None).Length != 0)
                throw new Exception("Arrows survived a floor change.");
            if (run.Player.Damage < 2) throw new Exception("Archer damage upgrade failed.");
            return true;
        }

        private static void CheckPowerupsAndFacing(DungeonRun run)
        {
            run.Restart();
            var player = run.Player;
            var powers = player.Powerups;
            foreach (var type in new[] { PowerupType.AttackSpeed, PowerupType.CriticalHits, PowerupType.Armor, PowerupType.DodgeRecovery })
            {
                int cap = PowerupCatalog.Get(type).MaxStacks;
                for (int i = 0; i < cap + 1; i++) player.Upgrade((int)type);
                if (powers.Count(type) != cap || powers.CanTake(type)) throw new Exception("Powerup stacking cap failed.");
            }
            if (Mathf.Abs(powers.AttackIntervalMultiplier - 0.5f) > 0.001f
                || Mathf.Abs(powers.DodgeCooldownMultiplier - 0.4f) > 0.001f)
                throw new Exception("Attack/dodge powerup scaling failed.");
            if (powers.DamageForRoll(3, 0.49f) != 6 || powers.DamageForRoll(3, 0.5f) != 3)
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
            if (powers.Count(PowerupType.Armor) != 0 || powers.CritChance != 0) throw new Exception("Powerups leaked into a new run.");
            player.Upgrade((int)PowerupType.LifeSteal);
            player.Hit();
            health = player.Health;
            for (int i = 0; i < 4; i++) powers.OnKill(player);
            if (player.Health != health) throw new Exception("Soul Harvest healed early.");
            powers.OnKill(player);
            if (player.Health != health + 1) throw new Exception("Soul Harvest failed to heal.");
            var enemy = run.Enemies[0];
            Vector2 origin = enemy.transform.position;
            var facing = enemy.Facing;
            Vector2 before = facing.Direction;
            facing.TurnToward(-before, 0.1f);
            if (Vector2.Angle(before, facing.Direction) > 18.01f || Vector2.Angle(before, facing.Direction) < 17.99f)
                throw new Exception("Enemy turn speed is not limited to 180 degrees per second.");
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
            var selected = run.UpgradeChoices[0].Type;
            int previous = powers.Count(selected);
            run.ChooseUpgrade(0);
            if (run.Floor != 2 || powers.Count(selected) != previous + 1) throw new Exception("Chosen powerup was not applied.");
            run.ChooseUpgrade(0);
            if (run.Floor != 2) throw new Exception("Upgrade accepted outside the selection screen.");
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
                serialized.FindProperty("description").stringValue = "A nimble ranged delver with 5 HP. Fire precise arrows or unleash a three-arrow cone spread every six seconds.";
                serialized.FindProperty("startingHealth").intValue = 5;
                serialized.FindProperty("color").colorValue = new Color(0.65f, 0.85f, 0.35f);
                serialized.FindProperty("weapon").enumValueIndex = (int)WeaponType.Bow;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(archer, archerPath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CHARACTER_ASSETS_OK: Knight and Archer ready.");
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

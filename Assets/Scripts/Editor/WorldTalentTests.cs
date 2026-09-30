using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    /// <summary>Run with -batchmode -executeMethod Slopgame.Editor.WorldTalentTests.Run.</summary>
    public static class WorldTalentTests
    {
        private static double started;
        private static int stage;
        private static bool failed;
        private const string SessionKey = "WorldTalentSmoke";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

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
        private static void Near(float value, float expected, string message) => Require(Mathf.Abs(value - expected) < 0.01f, message);
        private static void SetTime(DungeonEnemy enemy, float value) => typeof(DungeonEnemy)
            .GetField("<ActionTime>k__BackingField", PrivateInstance).SetValue(enemy, value);
        private static void Invoke(object value, string method) => value.GetType().GetMethod(method, PrivateInstance).Invoke(value, null);
        private static T Field<T>(object value, string field) => (T)value.GetType().GetField(field, PrivateInstance).GetValue(value);

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
                    TestCatalog();
                    run.Restart();
                    DebugMode.Set(true);
                    while (run.Floor < 3) run.DebugSkipRoom();
                    DebugMode.Set(false);
                    stage++;
                    return;
                }
                run.Player.enabled = false;
                foreach (var enemy in run.Enemies)
                {
                    enemy.enabled = false;
                    var shooter = enemy.GetComponent<EnemyShooter>();
                    if (shooter != null) shooter.enabled = false;
                }
                if (stage == 1)
                {
                    TestAsh(run);
                    TestBreakables(run);
                    TestUniversalEffects(run);
                    DebugMode.Set(true);
                    while (run.Floor < 15) run.DebugSkipRoom();
                    DebugMode.Set(false);
                    TestWorldComplete(run);
                    stage++;
                    return;
                }
                TestNeon(run);
                Finish(!failed, "26 talent caps/class gates; crit, burn, freeze, shock, ward effects; world-cleared screen; both world spawns; breakables; spread fire, freeze/pause, charge/recovery/walls");
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        }

        private static void TestCatalog()
        {
            Require(PowerupCatalog.All.Count == Enum.GetValues(typeof(PowerupType)).Length, "Catalog/enum size mismatch");
            for (int i = 0; i < PowerupCatalog.All.Count; i++) Require((int)PowerupCatalog.All[i].Type == i, "Catalog IDs shifted");
            Require((int)PowerupType.DarkHorizon - (int)PowerupType.ExtendedBattery == 26, "Expected 26 new talents");
            foreach (WeaponType weapon in Enum.GetValues(typeof(WeaponType)))
            {
                var obj = new GameObject("Talent test");
                var powers = obj.AddComponent<PlayerPowerups>();
                powers.ClassWeapon = weapon;
                foreach (var talent in PowerupCatalog.All)
                {
                    if (talent.Type <= PowerupType.ExtendedBattery) continue;
                    bool eligible = !talent.ClassWeapon.HasValue || talent.ClassWeapon == weapon;
                    Require(powers.CanTake(talent.Type) == eligible, "Class gate: " + talent.Name);
                    if (!eligible) { Require(!powers.Add(talent.Type), "Wrong class acquired talent"); continue; }
                    for (int rank = 0; rank < talent.MaxStacks; rank++) Require(powers.Add(talent.Type), "Missing rank: " + talent.Name);
                    Require(!powers.Add(talent.Type), "Exceeded stack cap: " + talent.Name);
                }
                Near(powers.SkillCooldownMultiplier, 0.7f, "Class cooldown talent missing for " + weapon);
                Near(powers.RelicCooldownMultiplier, 0.76f, "Relic cooldown talent missing");
                Require(powers.DamageForRoll(4, 0f) == 11 && powers.DamageForRoll(4, 1f) == 4, "Critical damage scaling wrong");
                if (weapon == WeaponType.Staff) Near(powers.ElementalEffectChance, 0.3f, "Stormcraft missing");
                UnityEngine.Object.Destroy(obj);
            }
        }

        private static void TestBreakables(DungeonRun run)
        {
            Require(Breakable.Active.Count > 0, "No breakables on a combat floor");
            Vector2 hero = run.Map.Centers[0];
            run.Player.transform.position = hero;
            int crystals = run.ProjectileRoot.GetComponentsInChildren<Crystal>().Length;
            var loaded = Breakable.Create(run, run.ProjectileRoot, hero + Vector2.right, run.World, 2, true);
            var behind = Breakable.Create(run, run.ProjectileRoot, hero + Vector2.left * 1.5f, run.World, 0, false);
            Breakable.SmashInArc(run.Player, Vector2.right, Breakable.SwingReach);
            Require(!Breakable.Active.Contains(loaded), "Swing did not smash the urn in front");
            Require(Breakable.Active.Contains(behind), "Swing smashed an urn behind the hero");
            Require(run.ProjectileRoot.GetComponentsInChildren<Crystal>().Length == crystals + 1, "Urn dropped no crystals");
            Require(run.ProjectileRoot.GetComponentsInChildren<HealthPickup>().Length > 0, "Urn dropped no heart");
            Require(Breakable.SmashAt(run, behind.transform.position, 0.1f) && !Breakable.Active.Contains(behind), "Touch did not smash the urn");
            Require(WorldCatalog.All[0].Layout == MapLayout.Dungeon && WorldCatalog.All[1].Layout != MapLayout.Dungeon, "World layouts wrong");
        }

        private static void TestAsh(DungeonRun run)
        {
            var enemy = run.Enemies.Find(e => e.Variant is EmberFanatic);
            Require(enemy != null && enemy.name == "Ember fanatic" && enemy.IsRanged, "Missing Ash specialist or overwritten name");
            Require(!run.Enemies.Exists(e => e.Variant is NeonLancer), "Neon enemy leaked into Ash world");
            Require(enemy.Variant.Sprite != null && enemy.Variant.CrystalValue == 2, "Missing fanatic visual/reward");
            Vector2 center = run.Map.Centers[1];
            enemy.transform.position = center;
            run.Player.transform.position = center + Vector2.right * 2f;
            enemy.Facing.Face(Vector2.right);
            var shooter = enemy.GetComponent<EnemyShooter>();
            SetTime(enemy, 5f);
            Invoke(shooter, "Update");
            Require(shooter.IsCharging, "Fanatic did not wind up");
            int before = run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length;
            typeof(DungeonRun).GetField("<IsPlaying>k__BackingField", PrivateInstance).SetValue(run, false);
            SetTime(enemy, 6f);
            Invoke(shooter, "Update");
            Require(run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length == before, "Paused fanatic fired");
            typeof(DungeonRun).GetField("<IsPlaying>k__BackingField", PrivateInstance).SetValue(run, true);
            SetTime(enemy, 5.5f);
            Invoke(shooter, "Update");
            Require(run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length == before, "Fanatic fired before telegraph ended");
            enemy.Freeze(1f);
            SetTime(enemy, 6f);
            Invoke(shooter, "Update");
            Require(run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length == before, "Frozen fanatic fired");
            typeof(DungeonEnemy).GetField("frozenUntil", PrivateInstance).SetValue(enemy, 0f);
            Invoke(shooter, "Update");
            var bolts = run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>();
            Require(bolts.Length == before + 3 && !shooter.IsCharging, "Fanatic did not fire exactly three bolts");
            Near(Vector2.Angle(bolts[before].Direction, bolts[before + 2].Direction), 36f, "Fan spread wrong");
        }

        private static void TestUniversalEffects(DungeonRun run)
        {
            var player = run.Player;
            var powers = player.Powerups;
            var enemy = run.Enemies[0];
            enemy.Health = 1000;
            powers.Add(PowerupType.SlowBurn);
            powers.Add(PowerupType.Permafrost);
            CombatDamage.ApplyEffect(player, enemy, DamageElement.Fire, 4);
            Require(Field<int>(enemy, "burnTicks") == 4, "Slow Burn did not extend burning");
            CombatDamage.ApplyEffect(player, enemy, DamageElement.Ice, 4);
            Near(Field<float>(enemy, "frozenUntil") - Time.time, 1.8f, "Permafrost did not extend freezing");
            powers.Add(PowerupType.SoulShield);
            int wards = powers.ArmorCharges;
            for (int i = 0; i < 7; i++) powers.OnKill(player, null);
            Require(powers.ArmorCharges == wards, "Soul Shield paid early");
            powers.OnKill(player, null);
            Require(powers.ArmorCharges == wards + 1, "Soul Shield did not grant ward");
            for (int i = 0; i < 80; i++) powers.OnKill(player, null);
            Require(powers.ArmorCharges == 3, "Soul Shield bypassed ward cap");
            Vector2 center = run.Map.Centers[1];
            var target = run.Enemies[1];
            enemy.transform.position = center + Vector2.left;
            target.transform.position = (Vector2)enemy.transform.position + Vector2.right * 2.65f;
            target.Health = 1000;
            CombatDamage.Shock(player, enemy.transform.position, enemy, 4);
            Require(target.Health == 1000, "Shock baseline reached too far");
            powers.Add(PowerupType.StaticField);
            CombatDamage.Shock(player, enemy.transform.position, enemy, 4);
            Require(target.Health == 999, "Static Field did not expand shock damage");
        }

        /// <summary>The first world's third guardian: its stairs open the world-cleared screen, and Next world carries on into world 2.</summary>
        private static void TestWorldComplete(DungeonRun run)
        {
            int lastFloor = WorldCatalog.All.Length * WorldCatalog.FloorsPerWorld;
            Require(WorldCatalog.CompletesWorld(15) && WorldCatalog.CompletesWorld(30) && WorldCatalog.CompletesWorld(lastFloor) && !WorldCatalog.CompletesWorld(5)
                && !WorldCatalog.CompletesWorld(10) && !WorldCatalog.CompletesWorld(lastFloor + WorldCatalog.FloorsPerWorld), "Wrong world-clearing floors");
            Require(WorldCatalog.HasWorldAfter(15) && WorldCatalog.HasWorldAfter(30) && !WorldCatalog.HasWorldAfter(lastFloor), "Wrong next-world check");
            TestWorldCatalog();
            Require(run.Floor == 15 && run.IsBossFloor && !run.InShop && run.World.Index == 0, "Did not reach the first world's last guardian");
            while (run.Enemies.Count > 0) run.Enemies[0].Die(true);
            Require(run.Artifact != null, "The third guardian dropped no artifact");
            run.BeginUpgradeChoice();
            Require(!run.WorldComplete && run.IsPlaying, "The world ended before the artifact was dealt with");
            run.BeginArtifactChoice();
            Require(run.LeaveArtifact() && run.IsPlaying && run.Artifact == null, "Could not leave the artifact");
            run.BeginUpgradeChoice();
            Require(run.WorldComplete && !run.IsPlaying && !run.ChoosingUpgrade && run.Floor == 15 && run.HasNextWorld,
                "The third guardian's stairs did not open the world-cleared screen");
            run.BeginUpgradeChoice();
            Require(run.WorldComplete && run.Floor == 15, "The stairs moved on while the world-cleared screen was open");
            run.TravelToWorld(WorldCatalog.All.Length);
            Require(run.WorldComplete && run.Floor == 15, "Travelled to a world that does not exist");
            run.ContinueFromWorldComplete();
            Require(!run.WorldComplete && run.IsPlaying && run.Floor == 16 && run.World.Index == 1 && Time.timeScale == 1f,
                "Next world did not carry on into the second world");
            run.TravelToWorld(3);
            Require(run.Floor == 16, "Travelled between worlds without clearing one");
        }

        /// <summary>The travel map's data: one world per hero but the Knight and Archer, the Augment's second, all on the map.</summary>
        private static void TestWorldCatalog()
        {
            var worlds = WorldCatalog.All;
            var heroes = new System.Collections.Generic.HashSet<WeaponType>();
            var names = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < worlds.Length; i++)
            {
                var world = worlds[i];
                Require(world.Index == i && names.Add(world.Name), "World index or name clash: " + world.Name);
                Require(world.MapPosition.x >= 0f && world.MapPosition.x <= 1f && world.MapPosition.y >= 0f && world.MapPosition.y <= 1f,
                    "World off the travel map: " + world.Name);
                Require(i == 0 ? !world.Hero.HasValue : world.Hero.HasValue && heroes.Add(world.Hero.Value), "Hero world missing or doubled: " + world.Name);
                Require(!world.Hero.HasValue || (world.Hero != WeaponType.Sword && world.Hero != WeaponType.Bow && world.Hero != WeaponType.Shadow),
                    "A starting hero got a world: " + world.Name);
                Require(WorldCatalog.FirstFloor(i) == i * WorldCatalog.FloorsPerWorld + 1 && WorldCatalog.IndexForFloor(WorldCatalog.FirstFloor(i)) == i,
                    "Wrong first floor for " + world.Name);
            }
            Require(worlds[1].Hero == WeaponType.Beam && !worlds[1].IsPlaceholder && !worlds[0].IsPlaceholder, "The Arcology is not the Augment's finished world");
        }

        private static void TestNeon(DungeonRun run)
        {
            var enemy = run.Enemies.Find(e => e.Variant is NeonLancer);
            Require(enemy != null && !run.Enemies.Exists(e => e.Variant is EmberFanatic), "Wrong Neon specialist spawn");
            var lancer = (NeonLancer)enemy.Variant;
            Require(lancer.Sprite != null && lancer.CrystalValue == 2, "Missing lancer visual/reward");
            Vector2 origin = run.Map.Centers[1];
            enemy.transform.position = origin;
            run.Player.transform.position = origin + Vector2.up * 3f;
            SetTime(enemy, 5f);
            Require(lancer.Move(enemy, run.Player.transform.position, true) && lancer.IsWindingUp, "Lancer skipped windup");
            Near(Vector2.Distance(origin, enemy.transform.position), 0f, "Lancer moved during warning");
            SetTime(enemy, 5.9f);
            lancer.Move(enemy, run.Player.transform.position, true);
            Require(lancer.IsDashing, "Lancer never dashed");
            // Keep the dash phase active while crossing a wall: collision must stop it.
            for (int i = 0; i < 200 && lancer.IsDashing; i++) lancer.Move(enemy, run.Player.transform.position, true);
            Require(run.Map.CanStand(enemy.transform.position, enemy.MoveRadius), "Lancer crossed a wall");
            SetTime(enemy, 7f);
            Vector2 stopped = enemy.transform.position;
            Require(lancer.Move(enemy, run.Player.transform.position, true) && !lancer.IsDashing, "No lancer recovery");
            Near(Vector2.Distance(stopped, enemy.transform.position), 0f, "Lancer moved during recovery");
            SetTime(enemy, 10f);
            Require(!lancer.Move(enemy, run.Player.transform.position, true), "Lancer never recovered");
            enemy.Freeze(1f);
            stopped = enemy.transform.position;
            Invoke(enemy, "Update");
            Near(Vector2.Distance(stopped, enemy.transform.position), 0f, "Frozen lancer moved");
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Check;
            Application.logMessageReceived -= Log;
            Time.timeScale = 1f;
            Debug.Log((success ? "WORLD_TALENTS_OK: " : "WORLD_TALENTS_FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}

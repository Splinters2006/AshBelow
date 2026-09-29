using System;
using System.Collections.Generic;
using System.Linq;
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
        private static bool variantsTested;
        private static bool failed, checkedChilledBoss;
        private static readonly List<WeaponType> classes = new List<WeaponType> { WeaponType.Sword, WeaponType.Bow, WeaponType.Staff, WeaponType.Daggers, WeaponType.Hammer, WeaponType.Shadow, WeaponType.Fists, WeaponType.Tail, WeaponType.Coins };
        private static int classIndex;
        private static int abilityIndex;
        private static bool activeCast;
        private static DungeonEnemy activeTarget;
        private static Vector2 activeOrigin;

        private static bool IsDelayed(AbilityType type) => type == AbilityType.ShieldRush || type == AbilityType.Earthshatter
            || type == AbilityType.Judgment || type == AbilityType.KnuckleSandwich || type == AbilityType.FrostNova
            || type == AbilityType.VenomVial || type == AbilityType.DemonPaw || type == AbilityType.DemonCurse;

        private static void VerifyDelayed(DungeonRun run, AbilityDefinition ability)
        {
            if (!IsDelayed(ability.Type)) return;
            // Demon Curse deals no damage, so it has landed once the target is cursed.
            Require(activeTarget != null && (ability.Type == AbilityType.DemonCurse ? activeTarget.IsCursed : activeTarget.Health < 100),
                ability.Name + " never landed after its windup.");
            Require(!run.Player.IsBusy, ability.Name + " left the hero stuck.");
            if (ability.Type == AbilityType.FrostNova)
                Require(activeTarget.IsFrozen && activeTarget.ActionSpeedMultiplier == 0f, "Frost Nova did not freeze.");
            if (ability.Type == AbilityType.DemonCurse)
                Require(activeTarget.IsCursed && activeTarget.IsParalyzed && activeTarget.ActionSpeedMultiplier == 0f && activeTarget.Health == 100,
                    "Demon Curse did not paralyse and curse the enemy on its pentagram, or it dealt damage.");
            if (ability.Type == AbilityType.VenomVial)
                Require(activeTarget.IsBurning, "Venom Vial's pool did not poison the enemy standing in it.");
            if (ability.Type == AbilityType.ShieldRush)
                Require(Vector2.Distance(activeOrigin, run.Player.transform.position) > 0.5f && run.Map.CanStand(run.Player.transform.position),
                    "Shield Rush did not carry the Knight forward.");
        }
        private static DungeonEnemy burnTarget;
        private static DungeonPlayer nearbyAlly, distantAlly, otherRunAlly;

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
                    Require(run.Characters.Count == 9, "Nine class assets must load.");
                    StartClass(run, WeaponType.Staff);
                    stage = 1;
                    return;
                }
                if (run.Player == null || run.Player.Weapon == null) return;
                run.Player.enabled = false;
                if (stage == 1 && !variantsTested)
                {
                    TestEnemies(run);
                    FreezeEnemies(run);
                    TestWizard(run);
                    TestCrystalDrops(run);
                    TestEnemyVariants(run);
                    variantsTested = true;
                    // The cinder burst hit the hero; wait out the hit protection before the shop wounds them again.
                    waitUntil = Time.time + 1.1f;
                    return;
                }
                if (stage == 1)
                {
                    if (Time.time < waitUntil) return;
                    run.Player.Heal(run.Player.MaxHealth);
                    bool skittersSeen = false, husksSeen = false;
                    while (run.Floor < 4)
                    {
                        ClearFloor(run);
                        Require(run.Floor < 2 || run.Enemies.TrueForAll(enemy => run.Floor >= 3 || !(enemy.Variant is CinderHusk)),
                            "A cinder husk spawned before floor 3.");
                        skittersSeen |= run.Enemies.Exists(enemy => enemy.Variant is AshSkitter);
                        husksSeen |= run.Enemies.Exists(enemy => enemy.Variant is CinderHusk);
                    }
                    Require(skittersSeen && husksSeen, "Floors 2 to 4 spawned no ash skitters or cinder husks.");
                    ClearFloor(run);
                    TestShop(run);
                    Require(run.IsBossFloor && run.Boss != null && run.Enemies.Count == 1, "Floor five did not create a dedicated boss arena.");
                    Require(run.Map.CanStand(new Vector2(15, 9)) && !run.Map.CanStand(new Vector2(12, 9)), "Arena bounds are wrong.");
                    run.Player.Protect(30f);
                    run.Boss.Enemy.Chill(20f);
                    waitUntil = Time.time + 3.05f;
                    stage = 2;
                    return;
                }
                if (stage == 2)
                {
                    if (Time.time < waitUntil) return;
                    if (!checkedChilledBoss)
                    {
                        Require(run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length == 0,
                            "Chilled boss attacked at normal speed.");
                        Require(Mathf.Abs(run.Boss.Enemy.ActionTime - 1.525f) < 0.15f,
                            "Chill did not halve the boss action clock.");
                        checkedChilledBoss = true;
                        waitUntil = Time.time + 3f;
                        return;
                    }
                    Require(run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>().Length > 0, "Chilled boss never fired its delayed fan.");
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
                    Require(run.Player.Crystals.BonusDamage == 0 && run.Player.Crystals.SpeedMultiplier == 1f, "Crystal shop boons outlasted the boss arena.");
                    while (run.Floor < 10) ClearFloor(run);
                    Require(run.IsBossFloor && run.Boss != null, "Floor ten has no boss.");
                    Require(run.Boss.Kind == BossKind.Duelist && run.Boss.Behaviour is DuelistBoss, "Floor ten did not summon the Ashen Duelist.");
                    run.Boss.Enemy.Hit(100000);
                    run.BeginArtifactChoice();
                    Require(run.ChooseArtifact(AbilityType.FrostNova, 1), "Second artifact did not fill E.");
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
                    var ability = AbilityCatalog.All[abilityIndex];
                    if (!activeCast)
                    {
                        FreezeEnemies(run);
                        TestActive(run, ability);
                        activeCast = true;
                        // Wound-up abilities land a little later; check them once they have.
                        if (IsDelayed(ability.Type)) { waitUntil = Time.time + 1.2f; return; }
                    }
                    else if (Time.time < waitUntil) return;
                    VerifyDelayed(run, ability);
                    activeCast = false;
                    abilityIndex++;
                    if (abilityIndex < AbilityCatalog.All.Length)
                    { StartClass(run, AbilityCatalog.All[abilityIndex].ClassWeapon); return; }
                    // Not the last ability's target: it may still be paralysed by Demon Curse.
                    burnTarget = run.Enemies.Find(enemy => enemy != activeTarget) ?? run.Enemies[0];
                    burnTarget.enabled = true;
                    burnTarget.Speed = 0f;
                    burnTarget.transform.position = (Vector2)run.Map.Centers[run.Map.Centers.Count - 1];
                    burnTarget.Health = 100;
                    burnTarget.Chill(0.5f);
                    Require(burnTarget.ActionSpeedMultiplier == 0.5f && burnTarget.MoveMultiplier == 0.5f, "Ice did not apply a 50% action slow.");
                    burnTarget.Burn(1, 5);
                    Require(burnTarget.IsBurning && burnTarget.transform.Find("Burn indicator").gameObject.activeSelf, "Burn indicator did not appear.");
                    waitUntil = Time.time + 1.1f;
                    stage = 5;
                    return;
                }
                if (stage == 5)
                {
                    if (Time.time < waitUntil) return;
                    Require(!burnTarget.IsBurning && !burnTarget.transform.Find("Burn indicator").gameObject.activeSelf, "Expired burn indicator remained visible.");
                    Require(!burnTarget.IsChilled && burnTarget.ActionSpeedMultiplier == 1f, "Expired chill did not restore action speed.");
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
                    StartClass(run, WeaponType.Hammer);
                    stage = 7;
                    return;
                }
                if (stage == 7)
                {
                    FreezeEnemies(run);
                    Require(run.Player.Weapon is PaladinAttack && Mathf.Approximately(run.Player.Charge.Duration, 2f), "Paladin weapon/two-second blessing charge missing.");
                    var target = run.Enemies[0];
                    target.transform.position = run.Player.transform.position + Vector3.right;
                    target.Health = 100;
                    Require(run.Player.Weapon.TryAttack(Vector2.right, 0.5f), "Paladin early release did not swipe.");
                    Require((target.Health == 99 || target.Health == 98) && run.Player.Blessing.BonusDamage == 0,
                        "Partial charge gained heavy damage or granted a blessing.");
                    Require(run.Player.Weapon.TryHeavyAttack(Vector2.right) && run.Player.Weapon.HeavyCooldownRemaining > PaladinAttack.HolySwordCooldown - 0.1f
                        && run.ProjectileRoot.GetComponentInChildren<HolySwordVfx>() != null, "Holy Sword was not called down on a nearby enemy.");
                    Require(!run.Player.Weapon.TryHeavyAttack(Vector2.right), "Holy Sword bypassed its cooldown.");
                    nearbyAlly = MakeAlly(run, 2f);
                    distantAlly = MakeAlly(run, 5f);
                    otherRunAlly = MakeAlly(run, 1f);
                    otherRunAlly.Run = null;
                    waitUntil = Time.time + 0.5f;
                    stage = 8;
                    return;
                }
                if (stage == 8)
                {
                    if (Time.time < waitUntil) return;
                    run.Player.Charge.Tick(true, true);
                    Require(run.Player.Charge.IsCharging && run.Player.TryRoll(Vector2.left) && !run.Player.Charge.IsCharging,
                        "Dodge did not cancel blessing charge.");
                    run.Player.Charge.Tick(false, false);
                    Require(run.Player.Blessing.BonusDamage == 0, "Cancelled blessing granted damage.");
                    waitUntil = Time.time + 0.4f;
                    stage = 9;
                    return;
                }
                if (stage == 9)
                {
                    if (Time.time < waitUntil) return;
                    run.Player.Charge.Tick(true, true);
                    Require(run.Player.Charge.IsCharging, "Blessing charge did not start.");
                    waitUntil = Time.time + PaladinAttack.ChargeDuration + 0.05f;
                    stage = 10;
                    return;
                }
                if (stage == 10)
                {
                    if (Time.time < waitUntil) return;
                    int targetHealth = run.Enemies[0].Health;
                    run.Player.Charge.Tick(false, true);
                    Require(run.Player.Damage == run.Player.BaseDamage + 2 && nearbyAlly.Damage == nearbyAlly.BaseDamage + 2,
                        "Blessing did not increase self and nearby ally damage.");
                    Require(distantAlly.Blessing.BonusDamage == 0 && otherRunAlly.Blessing.BonusDamage == 0,
                        "Blessing reached a distant ally or another run.");
                    Require(run.Enemies[0].Health == targetHealth, "Full blessing charge still performed a damaging attack.");
                    Require(run.Player.GetComponentInChildren<BlessingSparkles>() != null && nearbyAlly.GetComponentInChildren<BlessingSparkles>() != null,
                        "Blessed heroes have no golden sparkles.");
                    run.Player.Upgrade((int)PowerupType.Damage);
                    waitUntil = Time.time + 0.7f;
                    stage = 11;
                    return;
                }
                if (stage == 11)
                {
                    if (Time.time < waitUntil) return;
                    Require(run.Player.Weapon.TryAttack(Vector2.right, 1f), "Blessing could not be refreshed.");
                    Require(run.Player.Damage == run.Player.BaseDamage + 2 && run.Player.Blessing.Remaining > 7.9f,
                        "Blessing stacked damage or failed to refresh duration.");
                    waitUntil = Time.time + 8.1f;
                    stage = 12;
                    return;
                }
                if (stage == 12)
                {
                    if (Time.time < waitUntil) return;
                    Require(run.Player.Damage == run.Player.BaseDamage && nearbyAlly.Damage == nearbyAlly.BaseDamage,
                        "Expired blessing retained damage or lost permanent upgrades.");
                    UnityEngine.Object.Destroy(nearbyAlly.gameObject);
                    UnityEngine.Object.Destroy(distantAlly.gameObject);
                    UnityEngine.Object.Destroy(otherRunAlly.gameObject);
                    run.Player.Blessing.Apply(2, 8f);
                    run.Restart();
                    Require(run.Player.Blessing.BonusDamage == 0, "New run retained blessing.");
                    Finish(!failed, "Bosses, artifacts, eight classes, Brawler jab/empower/rage/leap, Demoness stab/sweep/paralysis/curse, rear-hit markers, active abilities, status effects, Paladin swipe/charge/allies/range/refresh/expiration/reset");
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        }

        private static DungeonPlayer MakeAlly(DungeonRun run, float distance)
        {
            var ally = DungeonVisuals.Create("Test ally", run.transform,
                run.Player.transform.position + Vector3.up * distance, Vector2.one, Color.white, 3)
                .gameObject.AddComponent<DungeonPlayer>();
            ally.Run = run;
            ally.Initialize(run.SelectedCharacter);
            ally.enabled = false;
            return ally;
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
            // The crystal shop before each boss has no enemies: its stairs lead straight on.
            if (run.InShop) { run.BeginUpgradeChoice(); return; }
            while (run.Enemies.Count > 0) run.Enemies[0].Hit(100000);
            run.BeginUpgradeChoice();
            Require(run.ChoosingUpgrade, "Regular floor did not offer talents.");
            run.ChooseUpgrade(0);
        }

        private static void TestCrystalDrops(DungeonRun run)
        {
            var tank = run.Enemies.Find(enemy => enemy.IsTank);
            var normal = run.Enemies.Find(enemy => !enemy.IsTank);
            Require(Crystal.ValueFor(tank) == 3 && Crystal.ValueFor(normal) == 1, "Crystal values are wrong.");
            int before = run.ProjectileRoot.GetComponentsInChildren<Crystal>().Length;
            normal.Hit(100000);
            var dropped = run.ProjectileRoot.GetComponentsInChildren<Crystal>();
            Require(dropped.Length == before + 1 && Array.Exists(dropped, crystal => crystal.Value == 1), "A slain enemy dropped no crystal.");
        }

        private static void TestEnemyVariants(DungeonRun run)
        {
            var player = run.Player;
            Require(run.Floor == 1 && !run.Enemies.Exists(enemy => enemy.Variant != null), "Special enemies spawned on floor 1.");
            var plain = run.Enemies.FindAll(enemy => !enemy.IsTank && !enemy.IsRanged && enemy.Variant == null);
            Require(plain.Count >= 2, "Floor 1 has too few plain ashlings to test variants.");
            DungeonEnemy huskEnemy = plain[0], skitterEnemy = plain[1];
            int health = skitterEnemy.Health;
            float speed = skitterEnemy.Speed;
            skitterEnemy.gameObject.AddComponent<AshSkitter>().Configure(skitterEnemy);
            Require(skitterEnemy.Variant is AshSkitter && skitterEnemy.Speed > speed && skitterEnemy.Health == Mathf.Max(1, health / 2)
                && Crystal.ValueFor(skitterEnemy) == 1, "The ash skitter is not a fast, fragile ashling.");
            health = huskEnemy.Health;
            var husk = huskEnemy.gameObject.AddComponent<CinderHusk>();
            husk.Configure(huskEnemy);
            Require(huskEnemy.Health == health + 2 && Crystal.ValueFor(huskEnemy) == 2, "The cinder husk's health or crystals are wrong.");

            // Killing the husk leaves a fuse; the burst then hurts the hero and nearby enemies.
            Vector2 at = player.transform.position;
            huskEnemy.transform.position = at + Vector2.right * 0.8f;
            skitterEnemy.transform.position = at + Vector2.right * 1.6f;
            Require(!player.IsInvulnerable, "The hero was still protected before the cinder burst test.");
            int hearts = player.Health, wards = player.Powerups.ArmorCharges;
            huskEnemy.Hit(100000);
            var fuses = run.GetComponentsInChildren<CinderBurst>();
            Require(fuses.Length == 1 && player.Health == hearts && run.Enemies.Contains(skitterEnemy), "The cinder husk burst without a fuse.");
            fuses[0].Detonate();
            Require(player.Health == hearts - 1 || player.Powerups.ArmorCharges == wards - 1, "The cinder burst did not hurt a hero in range.");
            Require(!run.Enemies.Contains(skitterEnemy), "The cinder burst did not hurt a nearby enemy.");
        }

        private static void TestShop(DungeonRun run)
        {
            var player = run.Player;
            var pouch = player.Crystals;
            Require(run.InShop && run.Floor == 4 && run.Enemies.Count == 0 && run.Shop != null && !run.IsBossFloor, "The floor before the boss did not lead into the crystal shop.");
            Require(run.Map.CanStand(run.Map.Centers[0]) && run.Map.CanStand(run.Exit) && !run.Map.IsFloor(27, 22), "The shop room layout is wrong.");
            var shop = run.Shop;
            CrystalShop.Offer Offer(CrystalShop.Ware ware) => Array.Find(CrystalShop.Offers, offer => offer.Ware == ware);
            int Stocked(CrystalShop.Category category) => shop.Stock.Count(offer => offer.Category == category);
            Require(Stocked(CrystalShop.Category.Healing) == CrystalShop.HealingStock && Stocked(CrystalShop.Category.Arena) == CrystalShop.ArenaStock
                && Stocked(CrystalShop.Category.Relic) == CrystalShop.RelicStock && shop.Stock.Distinct().Count() == shop.Stock.Count,
                "The shop did not stock one healing ware, two arena boons and two distinct relics.");
            var stocked = shop.Stock.ToList();
            shop.Restock(run.Seed + run.Floor * 104729);
            Require(shop.Stock.SequenceEqual(stocked), "The same run and floor stocked a different shop.");
            var assortments = new HashSet<string>();
            for (int seed = 0; seed < 40; seed++)
            {
                shop.Restock(seed);
                assortments.Add(string.Join(",", shop.Stock.Select(offer => offer.Ware)));
            }
            Require(assortments.Count > 5, "Shop stock was not randomised.");
            var missing = Array.Find(CrystalShop.Offers, offer => !shop.Stock.Contains(offer));
            pouch.Add(1000 - pouch.Crystals);
            Require(!shop.CanBuy(missing) && !shop.Buy(missing), "The shop sold a ware it did not stock.");
            shop.SetStock(CrystalShop.Offers);
            var hone = Offer(CrystalShop.Ware.EmberHone);
            int baseDamage = player.Damage;
            Require(shop.Buy(hone) && player.Powerups.Count(PowerupType.Damage) > 0 && player.Damage == baseDamage + 1, "The Ember Hone did not raise damage for the descent.");
            var feather = Offer(CrystalShop.Ware.PhoenixFeather);
            while (shop.Buy(feather)) pouch.Add(1000 - pouch.Crystals);
            Require(player.Powerups.Count(PowerupType.DodgeRecovery) == PowerupCatalog.Get(PowerupType.DodgeRecovery).MaxStacks && !shop.CanBuy(feather),
                "A relic was sold past its boon's highest rank.");
            pouch.Add(1000 - pouch.Crystals);
            Require(!shop.CanBuy(Offer(CrystalShop.Ware.Draught)) && !shop.Buy(Offer(CrystalShop.Ware.Elixir)), "Healing was sold to a hero at full health.");
            Require(player.Hit() && player.Health == player.MaxHealth - 1, "The shop test could not wound the hero.");
            var draught = Offer(CrystalShop.Ware.Draught);
            Require(shop.Buy(draught) && player.Health == player.MaxHealth && pouch.Crystals == 1000 - draught.BaseCost, "The Healing Draught did not heal for its price.");
            Require(shop.Cost(draught) == draught.BaseCost + draught.BaseCost / 2, "Wares did not grow dearer after a purchase.");
            int maxHealth = player.MaxHealth;
            Require(shop.Buy(Offer(CrystalShop.Ware.HeartCrystal)) && player.MaxHealth == maxHealth + 1 && player.Health == player.MaxHealth, "The Heart Crystal did not raise max HP.");
            int damage = player.Damage;
            Require(shop.Buy(Offer(CrystalShop.Ware.Whetstone)) && shop.Buy(Offer(CrystalShop.Ware.Stoneskin)) && shop.Buy(Offer(CrystalShop.Ware.Quicksilver)),
                "Arena boons could not be bought.");
            Require(player.Damage == damage && pouch.SpeedMultiplier == 1f && pouch.PendingDamage == 1 && pouch.PendingWards == CrystalShop.StoneskinWards,
                "Arena boons applied in the shop instead of waiting for the guardian.");
            pouch.Spend(pouch.Crystals);
            Require(!shop.CanBuy(Offer(CrystalShop.Ware.Whetstone)), "The shop sold a ware the hero could not afford.");
            player.transform.position = (Vector2)run.Map.Centers[0];
            Require(!shop.IsNear(player), "The merchant can be reached from the shop entrance.");
            player.transform.position = shop.Counter + Vector2.down * 0.9f;
            Require(shop.IsNear(player), "The merchant cannot be reached from his counter.");
            ClearFloor(run);
            int wards = player.Powerups.Count(PowerupType.Armor) + player.Powerups.Count(PowerupType.PaladinWard) + CrystalShop.StoneskinWards;
            Require(!run.InShop && run.Floor == 5 && run.Shop == null, "The shop stairs did not lead to the boss.");
            Require(player.Damage == damage + 1 && player.Powerups.ArmorCharges == wards && Mathf.Abs(pouch.SpeedMultiplier - 1.2f) < 0.001f,
                "Arena boons did not apply in the boss arena.");
        }

        private static void TestEnemies(DungeonRun run)
        {
            var tank = run.Enemies.Find(enemy => enemy.IsTank);
            var normal = run.Enemies.Find(enemy => !enemy.IsTank && !enemy.IsRanged);
            var caster = run.Enemies.Find(enemy => enemy.IsRanged);
            Require(tank != null && tank.Health == normal.Health * 3 && tank.Speed < normal.Speed
                && tank.HitRadius > normal.HitRadius, "Tank spawn/stats failed.");
            Require(tank.GetComponent<SpriteRenderer>().sprite != normal.GetComponent<SpriteRenderer>().sprite
                && caster.GetComponent<SpriteRenderer>().sprite != normal.GetComponent<SpriteRenderer>().sprite, "Enemy silhouettes were not distinct.");
            var tactics = caster.GetComponent<EnemyTactics>();
            for (int x = 1; x < DungeonMap.Width - 2; x++)
                for (int y = 2; y < DungeonMap.Height - 2; y++)
                {
                    Vector2 p = new Vector2(x - 0.2f, y);
                    if (!run.Map.CanStand(p) || run.Map.CanStand(p + Vector2.left * 0.18f)
                        || !run.Map.CanStand(p + Vector2.up) || !run.Map.CanStand(p + Vector2.right)) continue;
                    caster.transform.position = p;
                    Vector2 direction = tactics.Direction(p + Vector2.right, true, false);
                    Require(direction.sqrMagnitude > 0.01f && run.Map.CanStand(p + direction * 0.18f), "Caster kept retreating into a wall.");
                    Require(tactics.Direction(p + Vector2.right, true, true) == Vector2.zero, "Charging caster moved.");
                    return;
                }
            throw new Exception("No wall-retreat test position found.");
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
            // A strike may also shock (1 damage) the other enemies within 2 units; that is not a chain.
            Require(run.Enemies[0].Health == 98 && run.Enemies[1].Health >= 99 && run.Enemies[2].Health >= 99,
                "Unupgraded lightning chained or dealt incorrect damage.");
            Require(!wizard.TryHeavyAttack(Vector2.right), "Lightning bypassed cooldown.");
            run.Player.Powerups.Add(PowerupType.LightningChains);
            run.Player.Powerups.Add(PowerupType.LightningChains);
            foreach (string field in new[] { "readyAt", "lightningReadyAt" })
                typeof(WizardAttack).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(wizard, 0f);
            for (int i = 0; i < 3; i++) { run.Enemies[i].Health = 100; run.Enemies[i].transform.position = origin + offsets[i]; }
            Require(wizard.TryHeavyAttack(Vector2.right), "Upgraded lightning did not cast.");
            for (int i = 0; i < 3; i++) Require(run.Enemies[i].Health <= 98 && run.Enemies[i].Health >= 96, "Conductivity failed to unlock chain targets.");
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
            Require(skills.Equipped(0) == AbilityType.Fireball && skills.Equipped(1) == AbilityType.FrostNova, "Q/E ordering failed.");
            Require(skills.Claim(AbilityType.Blink, 0) && skills.Equipped(1) == AbilityType.FrostNova, "Replacing Q overwrote E.");
            Require(!run.Player.Powerups.CanTake(PowerupType.FireballRadius), "Unequipped ability talent was offered.");
            Require(skills.Claim(AbilityType.Blink, 0) && skills.Claim(AbilityType.Blink, 0) && !skills.Claim(AbilityType.Blink, 0), "Artifact rank cap failed.");
            Require(!skills.Claim(AbilityType.Aegis, 1), "Cross-class ability was accepted.");
        }

        private static void TestClass(DungeonRun run, WeaponType type)
        {
            Require(run.Player.ClassWeapon == type, "Wrong class selected.");
            Require(run.Player.Abilities.EmptySlot == 0, "Class swap leaked artifacts.");
            float baseCharge = run.Player.Charge.Duration;
            run.Player.Powerups.Add(PowerupType.AttackSpeed);
            Require(Mathf.Abs(run.Player.Charge.Duration - baseCharge / 1.2f) < 0.001f, "Attack speed did not improve this class's charge speed.");
            if (type == WeaponType.Fists) TestBrawler(run);
            if (type == WeaponType.Tail) TestDemoness(run);
            int slot = 0;
            foreach (var ability in AbilityCatalog.All)
            {
                if (ability.ClassWeapon != type || slot >= 2) continue;
                Require(run.Player.Abilities.Claim(ability.Type, 1 - slot), "Class artifact failed to equip in chosen slot.");
                if (slot == 0) Require(run.Player.Abilities.Equipped(0) == AbilityType.None, "Choosing E filled Q instead.");
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
            if (type == WeaponType.Hammer)
                Require(run.Player.Shield == null && run.Player.Weapon is PaladinAttack && run.Player.MaxHealth == 7, "Paladin weapon/stats wrong.");
        }

        /// <summary>The cast above used a random roll; this checks its coins and then every outcome with fixed rolls.</summary>
        private static void TestGamblerArtifact(DungeonPlayer player, AbilityType type)
        {
            var coins = (GamblerAttack)player.Weapon;
            if (type == AbilityType.Windfall)
            {
                Require(coins.Coins == 1 + GamblerAttack.WindfallCoins, "Windfall did not add 5 coins.");
                coins.Windfall(2);
                Require(coins.Coins == 1 + GamblerAttack.WindfallCoins * 2 + 1, "Windfall rank did not add a coin.");
                Require(player.Powerups.Add(PowerupType.MintCondition) && coins.WindfallCoinsFor(1) == GamblerAttack.WindfallCoins + 2,
                    "Mint Condition did not add two Windfall coins.");
                return;
            }
            if (type == AbilityType.AllIn)
            {
                Require(coins.Coins == 1 || coins.Coins == 2, "All In neither doubled nor lost the starting coin.");
                coins.AddCoins(6 - coins.Coins);
                Require(coins.AllIn(1, 0.49f) && coins.Coins == 12, "Winning All In did not double the coins.");
                Require(!coins.AllIn(1, 0.51f) && coins.Coins == 1, "Losing All In did not take every coin.");
                Require(coins.AllIn(3, 0.55f), "All In ranks did not improve the odds.");
                Require(player.Powerups.Add(PowerupType.RiggedOdds) && Mathf.Abs(coins.AllInOdds(1) - 0.55f) < 0.001f && coins.AllIn(1, 0.54f),
                    "Rigged Odds did not improve All In's odds.");
                return;
            }
            Require(coins.Coins == 1, "Jackpot did not consume the coins.");
            // The random cast above may have won a buff already.
            player.Buffs.Clear();
            coins.AddCoins(9);
            Require(coins.Jackpot(1, 0.6f, 0f) == GamblerAttack.JackpotPrize.Nothing && coins.Coins == 1 && player.Buffs.JackpotSpeed == 1f,
                "A losing Jackpot still paid out or kept the coins.");
            coins.AddCoins(9);
            float move = player.Buffs.MoveMultiplier;
            Require(coins.Jackpot(1, 0.1f, 0.1f) == GamblerAttack.JackpotPrize.Speed && coins.Coins == 1
                && Mathf.Abs(player.Buffs.MoveMultiplier - move * 2f) < 0.001f, "A 10-coin speed Jackpot did not double movement.");
            coins.AddCoins(8);
            int damage = player.Damage;
            Require(coins.Jackpot(1, 0.1f, 0.5f) == GamblerAttack.JackpotPrize.Damage && player.Damage == damage + 3, "A 9-coin damage Jackpot did not add +3 damage.");
            player.Buffs.Clear();
            player.Hit();
            coins.AddCoins(3);
            Require(coins.Jackpot(1, 0.1f, 0.9f) == GamblerAttack.JackpotPrize.Heal && player.Health == player.MaxHealth, "A heal Jackpot did not heal.");
            Require(GamblerAttack.JackpotHealFor(4) == 2 && GamblerAttack.JackpotHealFor(1) == 1 && GamblerAttack.JackpotDamageFor(1) == 1
                && GamblerAttack.JackpotSpeedFor(100) == 2.5f, "Jackpot prizes do not scale with coins as intended.");
            Require(player.Powerups.Add(PowerupType.HighRoller) && Mathf.Abs(coins.JackpotTime(1) - GamblerAttack.JackpotDuration - 2f) < 0.001f,
                "High Roller did not lengthen the Jackpot buffs.");
            float toss = coins.ThrowRange, volley = coins.VolleyReach;
            Require(coins.PickupCoinsForRoll(0f) == 1 && player.Powerups.Add(PowerupType.LooseChange)
                && coins.PickupCoinsForRoll(0.049f) == 2 && coins.PickupCoinsForRoll(0.051f) == 1, "Loose Change did not give a 5% chance of an extra coin.");
            Require(player.Powerups.Add(PowerupType.LongToss) && Mathf.Abs(coins.ThrowRange - toss - 1f) < 0.001f
                && Mathf.Abs(coins.VolleyReach - volley - 1f) < 0.001f, "Long Toss did not add coin range.");
        }

        private static void TestBrawler(DungeonRun run)
        {
            var player = run.Player;
            Require(player.Weapon is BrawlerAttack && player.Shield == null && player.MaxHealth == 6, "Brawler weapon/stats missing.");
            Require(BrawlerAttack.InRectangle(new Vector2(1f, 0.3f), Vector2.right, 1.5f, 0.42f)
                && !BrawlerAttack.InRectangle(new Vector2(1f, 0.9f), Vector2.right, 1.5f, 0.42f)
                && !BrawlerAttack.InRectangle(new Vector2(-0.5f, 0f), Vector2.right, 1.5f, 0.42f), "Punch rectangle is wrong.");
            var target = run.Enemies[0];
            var bystander = run.Enemies[1];
            target.transform.position = player.transform.position + Vector3.right;
            bystander.transform.position = player.transform.position + Vector3.up * 1.4f;
            target.Health = bystander.Health = 100;
            Require(player.Weapon.TryAttack(Vector2.right, 0f) && (target.Health == 99 || target.Health == 98) && bystander.Health == 100,
                "Jab did not hit only inside its rectangle.");
            Require(!player.Weapon.TryAttack(Vector2.right, 0f), "Jab ignored its attack interval.");
            float charge = player.Charge.Duration;
            Require(player.Weapon.TryHeavyAttack(Vector2.right) && player.Buffs.IsEmpowered
                && Mathf.Abs(player.Charge.Duration - charge * 0.7f) < 0.001f, "Empower did not speed up charging.");
            Require(!player.Weapon.TryHeavyAttack(Vector2.right), "Empower bypassed its cooldown.");
            // A hit from behind shows the rear-hit marker.
            int markers = run.ProjectileRoot.GetComponentsInChildren<FadingSprite>().Length;
            CombatDamage.Apply(player, target, 1, DamageElement.Physical, (Vector2)target.transform.position - target.Facing.Direction);
            Require(run.ProjectileRoot.GetComponentsInChildren<FadingSprite>().Length > markers, "Rear hit showed no indicator.");
            player.Buffs.Clear();
            // Wild Leap needs a nearby target when it is cast as the class Q ability.
            target.transform.position = player.transform.position + Vector3.right * 2f;
        }

        private static void TestDemoness(DungeonRun run)
        {
            var player = run.Player;
            Require(player.Weapon is DemonessAttack && player.Shield == null && player.MaxHealth == 5, "Demoness weapon/stats missing.");
            var target = run.Enemies[0];
            var bystander = run.Enemies[1];
            // A tail stab strikes only the nearest enemy in its lane.
            target.transform.position = player.transform.position + Vector3.right;
            bystander.transform.position = player.transform.position + Vector3.right * 1.6f;
            target.Health = bystander.Health = 100;
            Require(player.Weapon.TryAttack(Vector2.right, 0f) && (target.Health == 99 || target.Health == 98) && bystander.Health == 100
                && !target.IsParalyzed, "Tail stab did not hit only the nearest enemy, or paralysed without a full charge.");
            Require(!player.Weapon.TryAttack(Vector2.right, 0f), "Tail stab ignored its attack interval.");
            // The sweep covers a half circle and hits paralysed enemies twice as hard.
            target.Paralyze(2f);
            Require(target.IsParalyzed && target.ActionSpeedMultiplier == 0f, "Paralysis did not stop the enemy.");
            bystander.transform.position = player.transform.position + new Vector3(0.2f, 1.2f);
            target.Health = bystander.Health = 100;
            Require(player.Weapon.TryHeavyAttack(Vector2.right) && (target.Health == 96 || target.Health == 92)
                && (bystander.Health == 98 || bystander.Health == 96), "Tail sweep missed its half circle or its bonus on paralysed enemies.");
            Require(!player.Weapon.TryHeavyAttack(Vector2.right), "Tail sweep bypassed its cooldown.");
            target.Curse(1f);
            Require(target.CursedDamage(1) == 2 && target.CursedDamage(4) == 6, "Curse did not raise damage taken by 50%.");
            // HEEEELP needs a nearby target when it is cast as the class Q ability.
            target.transform.position = player.transform.position + Vector3.right * 2f;
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

        private static Vector2 OpenSpot(DungeonRun run, Vector2 start)
        {
            for (int radius = 0; radius <= 6; radius++)
                for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                    {
                        Vector2 spot = start + new Vector2(x, y);
                        bool open = true;
                        for (float step = -PaladinRelics.SanctuaryRadius - 0.6f; step <= 3f && open; step += 0.25f)
                            open = run.Map.CanStand(spot + Vector2.right * step);
                        if (open) return spot;
                    }
            return start;
        }

        private static void TestActive(DungeonRun run, AbilityDefinition ability)
        {
            var player = run.Player;
            Require(player.Abilities.Claim(ability.Type, 0), "Ability failed to equip: " + ability.Name);
            var target = run.Enemies[0];
            // Casts aim right, so stand where the ground is open on both sides whatever the floor's layout.
            player.transform.position = OpenSpot(run, player.transform.position);
            Vector2 origin = player.transform.position;
            target.transform.position = origin + Vector2.right;
            target.Health = 100;
            if (ability.Type == AbilityType.HealingLight) player.Hit();
            Require(player.Abilities.TryUse(0, Vector2.right), "Ability failed to cast: " + ability.Name);
            activeTarget = target;
            activeOrigin = origin;
            foreach (var shot in run.ProjectileRoot.GetComponentsInChildren<SpellProjectile>()) shot.Advance(0.15f);
            foreach (var arrow in run.ProjectileRoot.GetComponentsInChildren<PlayerProjectile>()) arrow.Advance(0.15f);
            foreach (var knife in run.ProjectileRoot.GetComponentsInChildren<ReturningKnife>()) knife.Advance(0.15f);
            foreach (var piercing in run.ProjectileRoot.GetComponentsInChildren<PiercingArrow>()) piercing.Advance(0.15f);
            switch (ability.Type)
            {
                case AbilityType.ShieldRush:
                    Require(player.IsBusy && player.IsInvulnerable && Vector2.Distance(origin, player.transform.position) < 1f,
                        "Shield Rush was instant instead of a slow charge."); break;
                case AbilityType.KnuckleSandwich:
                    Require(player.IsBusy && target.Health == 100, "Knuckle Sandwich landed without a windup."); break;
                case AbilityType.Judgment:
                    Require(target.Health == 100 && run.ProjectileRoot.GetComponentInChildren<HolyLightVfx>() != null, "Judgment smote without a holy windup."); break;
                case AbilityType.Earthshatter:
                    break;
                case AbilityType.Sanctuary:
                    var bubble = run.ProjectileRoot.GetComponentInChildren<HolyBubble>();
                    Require(bubble != null && bubble.BlocksProjectiles && bubble.Contains(origin), "Sanctuary did not form a bubble around the Paladin.");
                    var incoming = EnemyProjectile.Spawn(run, run.ProjectileRoot, origin + Vector2.left * (PaladinRelics.SanctuaryRadius + 0.4f), Vector2.right);
                    incoming.Advance(0.2f);
                    var outgoing = PlayerProjectile.Spawn(run, origin, Vector2.left, 1);
                    outgoing.Advance(0.5f);
                    Require(incoming.IsSpent && outgoing.IsSpent && player.Health == player.MaxHealth, "Sanctuary let a projectile cross its edge.");
                    player.GetComponent<PaladinRelics>().EndSanctuary();
                    Require(!bubble.IsActive && !HolyBubble.SlowsAt(origin), "Recasting Sanctuary did not drop it.");
                    break;
                case AbilityType.Aegis:
                    Require(player.IsInvulnerable && run.ProjectileRoot.GetComponentInChildren<HolyBubble>() != null, "Aegis showed no bubble."); break;
                case AbilityType.PiercingShot:
                    var piercingShot = run.ProjectileRoot.GetComponentInChildren<PiercingArrow>();
                    Require(target.Health < 100 && piercingShot != null, "Piercing Shot did not strike its target.");
                    // It keeps flying far past the old 5-unit range when the room allows.
                    var distant = run.Enemies.Find(enemy => enemy != target);
                    Vector2 lane = origin + Vector2.right * 9f;
                    if (distant != null && run.HasLineOfSight(origin, lane))
                    {
                        distant.transform.position = lane;
                        distant.Health = 100;
                        for (int i = 0; i < 10 && !piercingShot.IsSpent; i++) piercingShot.Advance(0.1f);
                        Require(distant.Health < 100, "Piercing Shot fell short of its long range.");
                    }
                    break;
                case AbilityType.FanOfKnives:
                    Require(target.Health < 100, "Fan of Knives did not damage its target.");
                    var knives = run.ProjectileRoot.GetComponentsInChildren<ReturningKnife>();
                    Require(knives.Length >= 12, "Fan of Knives threw too few knives.");
                    int afterThrow = target.Health;
                    foreach (var knife in knives) knife.Advance(0.9f);
                    Require(System.Array.TrueForAll(knives, knife => knife.IsReturning || knife.IsSpent), "Knives did not turn back after one second.");
                    for (int step = 0; step < 40; step++)
                        foreach (var knife in knives) if (knife != null && !knife.IsSpent) knife.Advance(0.1f);
                    Require(target.Health < afterThrow, "Returning knives did not cut enemies on the way back.");
                    Require(System.Array.TrueForAll(knives, knife => knife.ReturnedHome), "Knives never made it back to the Assassin.");
                    // A knife always finds its thrower, even across the floor and through walls.
                    var far = ReturningKnife.Throw(player, Vector2.left, 0);
                    player.transform.position = (Vector2)run.Map.Centers[run.Map.Centers.Count - 1];
                    for (int step = 0; step < 80 && !far.IsSpent; step++) far.Advance(0.1f);
                    Require(far.ReturnedHome, "A knife did not return to a distant Assassin.");
                    player.transform.position = origin;
                    break;
                case AbilityType.FrostNova:
                    Require(target.Health == 100, "Frost Nova hit instantly instead of expanding."); break;
                case AbilityType.VenomVial:
                    var vial = run.ProjectileRoot.GetComponentInChildren<VenomVial>();
                    Require(target.Health == 100 && vial != null && !vial.HasLanded, "Venom Vial struck before it landed.");
                    Require(Vector2.Distance(vial.Landing, target.transform.position) < 0.2f, "Venom Vial did not land at the cursor.");
                    break;
                case AbilityType.ShadowVeil:
                    Require(player.IsInvulnerable && player.IsVeiled, "Shadow Veil did not hide and protect the Assassin.");
                    Require(!run.TryNearestVisibleHero(target.transform.position, out _), "Enemies could still see the veiled Assassin.");
                    break;
                case AbilityType.HealingLight:
                    Require(player.Health == player.MaxHealth, "Healing Light did not heal."); break;
                case AbilityType.WildLeap:
                    Require(player.IsBusy && player.IsInvulnerable, "Wild Leap did not start an invulnerable leap."); break;
                case AbilityType.ArchdemonTechnique:
                    Require(player.Buffs.IsAscended && Mathf.Abs(player.Abilities.CooldownRemaining(0) - ability.Cooldown) < 0.01f,
                        "Archdemon's Technique did not empower the Demoness or started its cooldown early.");
                    Require(player.Weapon.TryAttack(Vector2.right, 0f) && target.Health < 100 && target.IsParalyzed,
                        "Under Archdemon's Technique a click was not a fully charged, paralysing stab.");
                    player.Buffs.Clear(); break;
                case AbilityType.DemonPaw:
                    Require(target.Health == 100 && run.ProjectileRoot.GetComponentInChildren<DemonPawVfx>() != null, "HEEEELP slammed without a portal windup."); break;
                case AbilityType.DemonCurse:
                    Require(target.Health == 100 && run.ProjectileRoot.GetComponentInChildren<PentagramVfx>() != null, "Demon Curse struck without a pentagram."); break;
                case AbilityType.Windfall:
                case AbilityType.AllIn:
                case AbilityType.Jackpot:
                    TestGamblerArtifact(player, ability.Type); break;
                case AbilityType.PrimalRage:
                    Require(player.Buffs.IsRaging && player.Damage == player.BaseDamage * 2
                        && Mathf.Abs(player.Abilities.CooldownRemaining(0) - ability.Cooldown) < 0.01f,
                        "Primal Rage did not buff damage or started its cooldown before the rage ended."); break;
                case AbilityType.Blink:
                case AbilityType.Windstep:
                    Require(Vector2.Distance(origin, player.transform.position) > 0.5f && run.Map.CanStand(player.transform.position), "Dash failed or landed in a wall.");
                    Require(ability.Type != AbilityType.Blink || Vector2.Distance(origin, player.transform.position) <= PlayerAbilities.BlinkDistance + 0.01f,
                        "Arcane Blink went past its range.");
                    Require(ability.Type != AbilityType.Blink || target.Health == 100, "Arcane Blink dealt damage.");
                    break;
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

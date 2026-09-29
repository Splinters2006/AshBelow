using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    /// <summary>
    /// Play-mode smoke test for the reworked elemental effects, every class mechanic on R and the Gambler.
    /// Each class is started in turn with its mechanic bought; a class may wait a few frames before it is checked.
    /// </summary>
    public static class MechanicTests
    {
        private static double started;
        private static int hero = -1;
        private static bool failed, waiting;
        private static float waitUntil;
        private static DungeonEnemy watched;
        private static readonly WeaponType[] Classes =
        {
            WeaponType.Sword, WeaponType.Bow, WeaponType.Staff, WeaponType.Daggers, WeaponType.Hammer, WeaponType.Fists, WeaponType.Tail, WeaponType.Coins
        };

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("MechanicSmoke", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Application.logMessageReceived += Log;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity");
            SessionState.SetBool("MechanicSmoke", true);
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

        private static void StartHero(DungeonRun run)
        {
            run.ShowMainMenu();
            foreach (var character in run.Characters) if (character.Weapon == Classes[hero]) run.SelectCharacter(character);
            run.Restart();
        }

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying) return;
            var run = UnityEngine.Object.FindAnyObjectByType<DungeonRun>();
            if (run == null || run.Characters == null) return;
            try
            {
                if (hero < 0)
                {
                    Require(ContainsGambler(run), "The Gambler is missing from the hero list.");
                    run.Progress.AwardAsh(PermanentUpgradeCatalog.MechanicCost * Classes.Length);
                    foreach (var weapon in Classes) run.Progress.RecordGuardian(PermanentUpgradeCatalog.MechanicGuardians, weapon);
                    foreach (var weapon in Classes)
                        Require(run.TryBuyUpgrade(PermanentUpgradeCatalog.MechanicId(weapon)), "Could not buy the mechanic for " + weapon);
                    hero = 0;
                    StartHero(run);
                    return;
                }
                if (run.Player == null || run.Player.Weapon == null || run.Enemies.Count < 3) return;
                var player = run.Player;
                player.enabled = false;
                foreach (var enemy in run.Enemies)
                {
                    enemy.enabled = false;
                    var shooter = enemy.GetComponent<EnemyShooter>();
                    if (shooter != null) shooter.enabled = false;
                }
                if (waiting)
                {
                    if (Time.time < waitUntil) return;
                    waiting = false;
                    FinishWait(run);
                }
                else
                {
                    Require(player.ClassWeapon == Classes[hero], "Wrong hero started.");
                    Require(player.Mechanic != null, "Class mechanic missing for " + Classes[hero]);
                    if (TestHero(run)) return;
                }
                hero++;
                if (hero < Classes.Length) { StartHero(run); return; }
                Finish(!failed, "Burn / freeze / shock, Shield Taunt, Elemental Quiver, Wild Storm, Sharpened Dagger, Heavenly Host, Super Angry, Demonic Power and the Gambler");
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.Message); }
        }

        private static bool ContainsGambler(DungeonRun run)
        {
            foreach (var character in run.Characters) if (character.Weapon == WeaponType.Coins) return true;
            return false;
        }

        /// <summary>Moves the first three enemies next to the hero with plenty of health; returns them.</summary>
        private static DungeonEnemy[] Line(DungeonRun run, Vector2 aim)
        {
            Vector2 origin = run.Player.transform.position;
            foreach (var enemy in run.Enemies) { enemy.transform.position = origin - aim * 6f; enemy.Health = 1000; }
            var a = run.Enemies[0];
            var b = run.Enemies[1];
            var c = run.Enemies[2];
            a.transform.position = origin + aim * 1.2f;
            b.transform.position = origin + aim * 1.2f + Vector2.Perpendicular(aim) * 1.5f;
            c.transform.position = origin + aim * 1.2f + Vector2.Perpendicular(aim) * 3.5f;
            return new[] { a, b, c };
        }

        private static Vector2 OpenAim(DungeonRun run)
        {
            Vector2 origin = run.Player.transform.position;
            foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                if (run.Map.CanStand(origin + direction * 3f) && run.Map.CanStand(origin + direction * 1.2f + Vector2.Perpendicular(direction) * 1.5f))
                    return direction;
            throw new Exception("No open ground around the hero.");
        }

        /// <summary>Checks the current hero. True when it must wait before <see cref="FinishWait"/>.</summary>
        private static bool TestHero(DungeonRun run)
        {
            var player = run.Player;
            Vector2 aim = OpenAim(run);
            var enemies = Line(run, aim);
            switch (player.ClassWeapon)
            {
                case WeaponType.Sword:
                {
                    // Elemental rework, checked once with the Knight's run.
                    Require(CombatDamage.BurnTickDamage(6) == 3 && CombatDamage.BurnTickDamage(1) == 1 && CombatDamage.ShockDamage(8) == 2
                        && CombatDamage.ShockDamage(1) == 1, "Burn or shock damage is not half / a quarter of the hit.");
                    CombatDamage.ApplyEffect(player, enemies[0], DamageElement.Ice, 4);
                    Require(enemies[0].IsFrozen && enemies[0].ActionSpeedMultiplier == 0f, "Ice did not freeze.");
                    CombatDamage.ApplyEffect(player, enemies[1], DamageElement.Fire, 4);
                    Require(enemies[1].IsBurning, "Fire did not burn.");
                    CombatDamage.ApplyEffect(player, enemies[0], DamageElement.Lightning, 8);
                    Require(enemies[0].Health == 1000 && enemies[1].Health == 998 && enemies[2].Health == 1000,
                        "Shock did not strike only the other enemies within 2 units for a quarter of the hit.");

                    var taunt = (ShieldTaunt)player.Mechanic;
                    Require(taunt.TryActivate(aim) && taunt.IsTaunting && player.IsBusy && player.DrawsAggro, "Shield Taunt did not raise.");
                    Require(!taunt.TryActivate(aim), "Shield Taunt ignored its cooldown.");
                    int wards = player.Powerups.ArmorCharges;
                    Vector2 front = (Vector2)player.transform.position + aim * 1f;
                    Require(taunt.TryBlock(front, -aim) && player.Powerups.ArmorCharges == wards + 1, "Shield Taunt did not block and ward.");
                    Require(!taunt.TryBlock((Vector2)player.transform.position - aim, aim), "Shield Taunt blocked from behind.");
                    var bolt = EnemyProjectile.Spawn(run, run.ProjectileRoot, front + aim * 0.3f, -aim);
                    bolt.Advance(0.05f);
                    Require(bolt.IsSpent && !bolt.IsReflected && player.Powerups.ArmorCharges == wards + 2 && player.Health == player.MaxHealth,
                        "A bolt passed through the taunt shield or was reflected.");
                    Vector2 near = (Vector2)player.transform.position + aim * 5f;
                    Require(run.NearestHero(near) == (Vector2)player.transform.position, "Taunting Knight is not targeted.");
                    return false;
                }
                case WeaponType.Bow:
                {
                    var quiver = (ElementalQuiver)player.Mechanic;
                    Require(quiver.Element == DamageElement.Fire, "Quiver does not start with fire.");
                    quiver.TryActivate(aim);
                    Require(quiver.Element == DamageElement.Ice, "Quiver did not cycle to freeze.");
                    quiver.TryActivate(aim);
                    quiver.TryActivate(aim);
                    quiver.TryActivate(aim);
                    Require(quiver.Element == DamageElement.Ice, "Quiver did not wrap around.");
                    for (int i = 0; i < 5; i++) player.Powerups.Add(PowerupType.CriticalHits);
                    var target = enemies[0];
                    bool crit = false;
                    for (int i = 0; i < 40 && !target.IsFrozen; i++)
                    {
                        int before = target.Health;
                        CombatDamage.Apply(player, target, 2, DamageElement.Physical, player.transform.position, 0f, quiver.Element);
                        crit |= before - target.Health == 4;
                    }
                    Require(crit && target.IsFrozen, "A critical freeze arrow did not crit and freeze.");
                    var plain = enemies[2];
                    for (int i = 0; i < 20; i++) CombatDamage.Apply(player, plain, 2, DamageElement.Physical, player.transform.position, 0f);
                    Require(!plain.IsFrozen && !plain.IsBurning, "Plain physical hits set off an element.");
                    return false;
                }
                case WeaponType.Staff:
                {
                    var storm = (WildStorm)player.Mechanic;
                    Require(!storm.TryActivate(aim), "Wild Storm fired without charge.");
                    for (int i = 0; i < WildStorm.EffectsNeeded; i++) CombatDamage.ApplyEffect(player, enemies[2], DamageElement.Ice, 1);
                    Require(storm.IsCharged && storm.TryActivate(aim) && storm.IsRaging && storm.Charge == 0, "Wild Storm did not charge and summon.");
                    // Storm strikes skip the effect roll, so even a 5% effect chance always burns.
                    CombatDamage.Apply(player, enemies[1], 1, DamageElement.Fire, player.transform.position, 0f, guaranteedEffect: true);
                    Require(enemies[1].IsBurning, "A guaranteed storm effect did not proc.");
                    watched = enemies[0];
                    waiting = true;
                    waitUntil = Time.time + 2f;
                    return true;
                }
                case WeaponType.Daggers:
                {
                    var dagger = (SharpenedDagger)player.Mechanic;
                    int damage = player.Damage;
                    Require(dagger.TryActivate(aim) && player.Damage == damage + 1, "Sharpened Dagger gave no damage.");
                    var target = enemies[0];
                    target.Facing.Face(aim);
                    CombatDamage.Apply(player, target, 1, DamageElement.Physical, player.transform.position);
                    Require(dagger.BonusDamage == 2 && player.Damage == damage + 2, "A backstab did not sharpen the dagger further.");
                    target.Facing.Face(-aim);
                    CombatDamage.Apply(player, target, 1, DamageElement.Physical, player.transform.position);
                    Require(dagger.BonusDamage == 2, "A frontal hit sharpened the dagger.");
                    return false;
                }
                case WeaponType.Hammer:
                {
                    var host = (HeavenlyHost)player.Mechanic;
                    Require(host.Required == HeavenlyHost.BlessedDamageNeeded, "Solo Heavenly Host needs the wrong amount.");
                    player.Blessing.Apply(PaladinAttack.BlessingDamage, 30f, player);
                    for (int i = 0; i < 24; i++) CombatDamage.Apply(player, enemies[0], 1, DamageElement.Physical, player.transform.position, 0f);
                    Require(host.Charge == 48 && !host.IsCharged, "Blessed hits did not charge Heavenly Host by their bonus.");
                    CombatDamage.Apply(player, enemies[0], 1, DamageElement.Physical, player.transform.position, 0f);
                    Require(host.IsCharged, "Heavenly Host did not charge at 50 blessed damage.");
                    Require(!host.TryActivate(aim) && host.IsCharged, "Heavenly Host spent itself with everyone at full health.");
                    player.Hit();
                    player.Hit();
                    Require(player.Health < player.MaxHealth, "Test could not wound the Paladin.");
                    Require(host.TryActivate(aim) && player.Health == player.MaxHealth && host.Charge == 0, "Angels did not heal the weakest ally to full.");
                    return false;
                }
                case WeaponType.Fists:
                {
                    var angry = (SuperAngry)player.Mechanic;
                    player.Hit();
                    Require(angry.Charge == 1, "Taking damage did not stoke Super Angry.");
                    for (int i = 0; i < 4; i++) angry.OnDamaged();
                    float size = player.Buffs.AttackSizeMultiplier, move = player.Buffs.MoveMultiplier, charge = player.Buffs.ChargeDurationMultiplier;
                    int damage = player.Damage;
                    Require(angry.TryActivate(aim) && player.Buffs.IsFurious, "Super Angry did not activate after 5 damage.");
                    Require(player.Buffs.AttackSizeMultiplier > size && player.Buffs.MoveMultiplier > move && player.Buffs.ChargeDurationMultiplier < charge
                        && player.Damage >= damage * 2, "Super Angry did not boost reach, speed, charging and damage.");
                    return false;
                }
                case WeaponType.Tail:
                {
                    var power = (DemonicPower)player.Mechanic;
                    var tail = (DemonessAttack)player.Weapon;
                    Require(tail.TryAttack(aim, 1f) && enemies[0].IsParalyzed && power.Charge == 1, "A vital stab did not charge Demonic Power.");
                    for (int i = 0; i < DemonicPower.ParalysesNeeded - 1; i++) power.OnParalyzed();
                    enemies[1].Facing.Face(-(Vector2)(enemies[1].transform.position - player.transform.position));
                    Require(power.TryActivate(aim) && power.Charge == 0, "Demonic Power did not activate.");
                    Vector2 away = (enemies[1].transform.position - player.transform.position).normalized;
                    Require(enemies[1].IsParalyzed && Vector2.Dot(enemies[1].Facing.Direction, away) > 0.99f && enemies[1].Facing.IsBehind(player.transform.position),
                        "Feared enemies did not turn their backs and freeze.");
                    return false;
                }
                case WeaponType.Coins:
                {
                    var coins = (GamblerAttack)player.Weapon;
                    var purse = (GamblerPurse)player.Mechanic;
                    Require(coins.Coins == 1, "The Gambler should start with his purse's single coin.");
                    coins.AddCoins(2);
                    Require(coins.TryAttack(aim, 0f) && coins.Coins == 3, "Throwing a coin spent it.");
                    coins.Spend(2);
                    Require(Array.Exists(run.ProjectileRoot.GetComponentsInChildren<PlayerProjectile>(), shot => shot.name == "Coin"), "No coin was thrown.");
                    var victim = enemies[2];
                    Vector2 spot = victim.transform.position;
                    victim.Hit(100000);
                    var drops = run.ProjectileRoot.GetComponentsInChildren<GoldCoin>();
                    Require(drops.Length == 1, "A slain enemy did not drop a gold coin.");
                    player.transform.position = drops[0].transform.position;
                    watched = null;
                    waiting = true;
                    // Long enough for the coin's pop-out hop to land and the purse to draw it in.
                    waitUntil = Time.time + 0.8f;
                    return true;
                }
            }
            return false;
        }

        private static void FinishWait(DungeonRun run)
        {
            var player = run.Player;
            if (player.ClassWeapon == WeaponType.Staff)
            {
                Require(watched == null || watched.Health < 1000, "Wild Storm never struck the nearby enemy.");
                return;
            }
            var coins = (GamblerAttack)player.Weapon;
            var purse = (GamblerPurse)player.Mechanic;
            Require(coins.Coins == 2 && run.ProjectileRoot.GetComponentsInChildren<GoldCoin>().Length == 0, "Walking over a gold coin did not pick it up.");
            int before = run.ProjectileRoot.GetComponentsInChildren<PlayerProjectile>().Length;
            Require(player.Weapon.TryHeavyAttack(Vector2.right) && coins.Coins == 2
                && run.ProjectileRoot.GetComponentsInChildren<PlayerProjectile>().Length == before + 2, "The volley did not throw one coin per coin carried.");
            var bet = GamblerPurse.Offers[(int)GamblerPurse.Ware.DoubleOrNothing];
            Require(purse.TryActivate(Vector2.right) && purse.IsOpen, "R did not open the purse.");
            Require(purse.Buy(bet, 0.1f) && coins.Coins == 4, "Winning Double or Nothing did not double the coins.");
            Require(purse.Buy(bet, 0.9f) && coins.Coins == 1, "Losing Double or Nothing did not empty the purse to its last coin.");
            var dice = GamblerPurse.Offers[(int)GamblerPurse.Ware.Dice];
            // One coin left from the lost bet: one short of the price, then one over it.
            coins.AddCoins(dice.Cost - 2);
            Require(!purse.Buy(dice), "Loaded Dice sold without enough coins.");
            coins.AddCoins(3);
            int damage = player.Damage;
            Require(purse.Buy(dice) && player.Damage == damage + 1 && coins.Coins == 2, "Loaded Dice failed.");
            Require(!purse.Buy(GamblerPurse.Offers[(int)GamblerPurse.Ware.Draught]), "A full-health Gambler bought a Healing Draught.");
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool("MechanicSmoke", false);
            EditorApplication.update -= Check;
            Application.logMessageReceived -= Log;
            Time.timeScale = 1f;
            Debug.Log((success ? "MECHANIC_SMOKE_OK: " : "MECHANIC_SMOKE_FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}

using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Slopgame.Editor
{
    /// <summary>
    /// Play-mode smoke test for the reworked elemental effects, every class mechanic on R, the Gambler and the
    /// universal kill / shop boons (checked once with the Knight, whose shield gives a known class-skill cooldown).
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
            WeaponType.Sword, WeaponType.Bow, WeaponType.Staff, WeaponType.Daggers, WeaponType.Hammer, WeaponType.Fists, WeaponType.Tail, WeaponType.Coins,
            WeaponType.Beam
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
        private static void Near(float value, float expected, string message) => Require(Mathf.Abs(value - expected) < 0.01f, message);
        /// <summary>Sets the dodge cooldown directly, so the test never has to roll (a roll would block raising the shield).</summary>
        private static readonly FieldInfo RollReady = typeof(DungeonPlayer).GetField("rollReady", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PowerupType[] UniversalBoons =
        {
            PowerupType.NerveSnap, PowerupType.PyreBurst, PowerupType.Kindling, PowerupType.Massacre, PowerupType.Momentum,
            PowerupType.Bloodrush, PowerupType.StillHunter, PowerupType.Prospector, PowerupType.Haggler, PowerupType.MerchantsFavor
        };

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
                Finish(!failed, "Burn / freeze / shock, universal kill and shop boons, Shield Taunt, Elemental Quiver, Wild Storm, Sharpened Dagger, Heavenly Host, Super Angry, Demonic Power, the Gambler and Overclock");
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
                    Require(taunt.TryActivate(aim) && taunt.IsTaunting && !player.IsBusy && player.IsHoldingShield && player.DrawsAggro, "Shield Taunt did not raise.");
                    Require(!taunt.TryActivate(aim), "Shield Taunt ignored its cooldown.");
                    int wards = player.Powerups.ArmorCharges;
                    Vector2 front = (Vector2)player.transform.position + aim * 1f;
                    Require(taunt.TryBlock(front, -aim) && player.Powerups.ArmorCharges == wards + 1, "Shield Taunt did not block and ward.");
                    Require(taunt.TryBlock((Vector2)player.transform.position - aim, aim), "Shield Taunt did not block from behind.");
                    Require(!taunt.TryBlock((Vector2)player.transform.position + aim, aim), "Shield Taunt blocked a bolt flying away.");
                    var bolt = EnemyProjectile.Spawn(run, run.ProjectileRoot, front + aim * 0.3f, -aim);
                    bolt.Advance(0.05f);
                    Require(bolt.IsSpent && !bolt.IsReflected && player.Powerups.ArmorCharges == wards + 3 && player.Health == player.MaxHealth,
                        "A bolt passed through the taunt shield or was reflected.");
                    Vector2 near = (Vector2)player.transform.position + aim * 5f;
                    Require(run.NearestHero(near) == (Vector2)player.transform.position, "Taunting Knight is not targeted.");
                    TestUniversalBoons(player, aim, enemies);
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
                    for (int i = 0; i < DemonicPower.ParalysesNeeded - 1; i++) power.OnImmobilized();
                    float reach = tail.SweepReach;
                    Require(power.TryActivate(aim) && power.Charge == 0 && power.IsActive, "Demonic Power did not activate.");
                    Require(tail.HasTwinTails && tail.SweepArc == 360f && Mathf.Approximately(tail.SweepReach, reach * DemonicPower.SweepReachMultiplier),
                        "Twin tails did not double the sweep's cone and lengthen its reach.");
                    return false;
                }
                case WeaponType.Beam:
                {
                    var cannon = (CyborgAttack)player.Weapon;
                    var overclock = (Overclock)player.Mechanic;
                    Require(cannon.TryAttack(aim, 1f) && enemies[0].Health < 1000 && enemies[1].Health == 1000 && overclock.Charge == 1,
                        "The plasma ray missed, strayed off its line, or did not charge Overclock.");
                    for (int i = overclock.Charge; i < Overclock.HitsNeeded; i++) overclock.OnRayHits(1);
                    Require(overclock.TryActivate(aim) && overclock.Charge == 0 && cannon.IsOverclocked && cannon.HeavyCooldownRemaining == 0f,
                        "Overclock did not activate or vent the cannon.");
                    Require(cannon.CannonCooldownTime < CyborgAttack.CannonCooldown, "Overclock did not speed up the cannon.");
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

        /// <summary>
        /// The universal boons, all within one frame so every kill counts toward the same 1-second streak.
        /// On entry enemies[0] is frozen, enemies[1] is burning and enemies[2] is untouched; none has died yet.
        /// </summary>
        private static void TestUniversalBoons(DungeonPlayer player, Vector2 aim, DungeonEnemy[] enemies)
        {
            var powers = player.Powerups;
            var shield = player.Shield;
            var frozen = enemies[0];
            var burning = enemies[1];
            var plain = enemies[2];
            Require(shield != null && frozen.IsHeld && burning.IsBurning && !plain.IsHeld && !plain.IsBurning, "Universal boon test set-up is wrong.");

            // Any hero can find them as room rewards, and the crystal merchant sells each one as a relic.
            foreach (var type in UniversalBoons)
            {
                var boon = PowerupCatalog.Get(type);
                Require(!boon.ClassWeapon.HasValue && boon.RequiredAbility == AbilityType.None && powers.CanTake(type), boon.Name + " is not universal.");
                Require(Array.Exists(CrystalShop.Offers, offer => offer.Category == CrystalShop.Category.Relic && offer.Powerup == type),
                    "No crystal shop relic sells " + boon.Name + ".");
            }

            // Momentum: the second kill within a second resets the dodge (kills 1-2 of the streak).
            powers.Add(PowerupType.Momentum);
            RollReady.SetValue(player, Time.time + DungeonPlayer.RollCooldown);
            powers.OnKill(player, plain);
            Require(player.DodgeCooldownRemaining > 1f, "Momentum reset the dodge after a single kill.");
            powers.OnKill(player, plain);
            Require(player.DodgeCooldownRemaining == 0f, "Momentum did not reset the dodge after two quick kills.");

            // Massacre: the fifth kill within a second resets the class skill (kills 3-5).
            powers.Add(PowerupType.Massacre);
            Require(shield.Raise(aim) && player.Weapon.HeavyCooldownRemaining > 0f, "Could not raise the shield for Massacre.");
            powers.OnKill(player, plain);
            powers.OnKill(player, plain);
            Require(player.Weapon.HeavyCooldownRemaining > 0f, "Massacre reset the class skill before the fifth kill.");
            powers.OnKill(player, plain);
            Require(player.Weapon.HeavyCooldownRemaining == 0f, "Massacre did not reset the class skill on the fifth quick kill.");

            // Nerve Snap: killing a frozen or paralysed enemy resets the class skill; an ordinary kill does not.
            powers.Add(PowerupType.NerveSnap);
            Require(shield.Raise(aim), "Could not raise the shield for Nerve Snap.");
            powers.OnKill(player, plain);
            Require(player.Weapon.HeavyCooldownRemaining > 0f, "Nerve Snap reset the class skill on an ordinary kill.");
            powers.OnKill(player, frozen);
            Require(player.Weapon.HeavyCooldownRemaining == 0f, "Nerve Snap did not reset the class skill on a frozen kill.");

            // Still Hunter: a frozen or paralysed kill takes 0.5 s off every cooldown. Bloodrush: every kill does, and they stack.
            powers.Add(PowerupType.StillHunter);
            RollReady.SetValue(player, Time.time + DungeonPlayer.RollCooldown);
            powers.OnKill(player, plain);
            Near(player.DodgeCooldownRemaining, DungeonPlayer.RollCooldown, "Still Hunter cut cooldowns on an ordinary kill.");
            powers.OnKill(player, frozen);
            Near(player.DodgeCooldownRemaining, DungeonPlayer.RollCooldown - 0.5f, "Still Hunter did not take 0.5 s off the dodge.");
            powers.Add(PowerupType.Bloodrush);
            powers.OnKill(player, plain);
            Near(player.DodgeCooldownRemaining, DungeonPlayer.RollCooldown - 1f, "Bloodrush did not take 0.5 s off the dodge.");
            Require(shield.Raise(aim), "Could not raise the shield for Bloodrush.");
            float skill = player.Weapon.HeavyCooldownRemaining;
            powers.OnKill(player, plain);
            Near(player.Weapon.HeavyCooldownRemaining, skill - 0.5f, "Bloodrush did not shorten the class skill.");
            Require(player.DodgeCooldownRemaining == 0f, "A cooldown cut left the dodge below zero or did not finish it.");

            // Kindling: a freeze (or shock) sets the enemy burning as well.
            powers.Add(PowerupType.Kindling);
            CombatDamage.ApplyEffect(player, plain, DamageElement.Ice, 4);
            Require(plain.IsFrozen && plain.IsBurning, "Kindling did not set a frozen enemy burning.");

            // Pyre Burst: a burning enemy bursts when it dies, hurting the enemy beside it.
            powers.Add(PowerupType.PyreBurst);
            int before = frozen.Health;
            powers.OnKill(player, burning);
            Require(frozen.Health < before, "Pyre Burst did not hurt the enemy next to the burning one.");

            // Prospector (always at rank 2), Haggler (25% / 50% off) and Merchant's Favor (one reroll).
            Require(!powers.RollExtraCrystals(), "Extra crystals dropped without Prospector.");
            powers.Add(PowerupType.Prospector);
            powers.Add(PowerupType.Prospector);
            Require(powers.RollExtraCrystals() && !powers.Add(PowerupType.Prospector), "Rank-2 Prospector did not always drop extra crystals, or took a third rank.");
            Near(powers.ShopPriceMultiplier, 1f, "Shop prices discounted without Haggler.");
            powers.Add(PowerupType.Haggler);
            Near(powers.ShopPriceMultiplier, 0.75f, "Haggler rank 1 is not 25% off.");
            powers.Add(PowerupType.Haggler);
            Near(powers.ShopPriceMultiplier, 0.5f, "Haggler rank 2 is not 50% off.");
            Require(!powers.Add(PowerupType.Haggler), "Haggler took a third rank.");
            Require(powers.ShopRerolls == 0 && powers.Add(PowerupType.MerchantsFavor) && powers.ShopRerolls == 1,
                "Merchant's Favor did not grant one shop reroll.");
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
            Require(purse.TryActivate(Vector2.right) && purse.IsOpen, "R did not open the purse.");
            var dice = GamblerPurse.Offers[(int)GamblerPurse.Ware.Dice];
            // Two coins carried: one short of the price, then two over it.
            coins.AddCoins(dice.Cost - 3);
            Require(!purse.Buy(dice), "Loaded Dice sold without enough coins.");
            coins.AddCoins(3);
            int damage = player.Damage;
            Require(purse.Buy(dice) && player.Damage == damage + 1 && coins.Coins == 2, "Loaded Dice failed.");
            Require(!purse.Buy(GamblerPurse.Offers[(int)GamblerPurse.Ware.Draught]), "A full-health Gambler bought a Healing Draught.");
            // The purse is capped, so repeated doubling cannot overflow into a negative (or reset) count.
            coins.AddCoins(GamblerAttack.MaxCoins - 1000);
            for (int i = 0; i < 40; i++) coins.DoubleOrNothing(0f);
            coins.AddCoins(int.MaxValue);
            Require(coins.Coins == GamblerAttack.MaxCoins, "Gambler coins overflowed past the purse cap.");
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

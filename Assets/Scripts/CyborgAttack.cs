using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Augment's arm cannon. Left click fires a plasma ray: an instant beam that pierces every enemy in its line and
    /// stops at the first wall. Charging it makes the ray longer, wider and stronger. Right click winds up the cannon:
    /// hold to charge and release (or wait, and it fires itself) to launch a plasma orb that bursts into a fiery blast.
    /// A full charge is sure to set the blast's victims burning.
    /// </summary>
    public sealed class CyborgAttack : MonoBehaviour, IPlayerWeapon, IRunPersistent
    {
        public const float ChargeDuration = 1f, RayInterval = 0.45f;
        public const float RayRange = 5.5f, ChargedRayRange = 8f, RayWidth = 0.12f, ChargedRayWidth = 0.34f, RayKnockback = 0.35f;
        public const float CannonCooldown = 5f, CannonChargeTime = 1f, CannonHoldLimit = 0.4f;
        public static readonly Color Plasma = new Color(0.35f, 1f, 0.78f);
        public static readonly Color Core = new Color(0.88f, 1f, 0.96f);
        public static readonly Color MissileColor = new Color(1f, 0.6f, 0.3f);

        public DungeonPlayer Player { get; set; }
        private float readyAt, cannonReadyAt, cannonStartedAt, overclockUntil, nextHum;
        private bool cannonCharging;
        private SpriteRenderer chargeGlow;

        /// <summary>Salvage (his passive): scrap gathered and not yet spent on repairs.</summary>
        public int Scrap { get; private set; }
        public void SaveRun(HeroSnapshot hero) => hero.SetExtra("scrap", Scrap);
        public void LoadRun(HeroSnapshot hero) => Scrap = Mathf.Max(0, hero.Extra("scrap", Scrap));

        public void CollectScrap() { Scrap++; SpendScrap(); }

        /// <summary>Every three pieces repair 1 HP; at full health they are kept until he is hurt.</summary>
        private void SpendScrap()
        {
            while (Scrap >= ScrapPickup.PerHeal && Player.Health > 0 && Player.Health < Player.MaxHealth)
            {
                Scrap -= ScrapPickup.PerHeal;
                Player.Heal(ScrapPickup.HealAmount);
                HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.7f, Plasma, 10, 0.8f);
            }
        }

        public bool IsCannonCharging => cannonCharging;
        /// <summary>How far the cannon has charged, 0-1 (0 when it is not charging).</summary>
        public float CannonCharge => cannonCharging ? Mathf.Clamp01((Time.time - cannonStartedAt) / CannonChargeTime) : 0f;
        /// <summary>Overclock (the class mechanic): every shot fully charged, rays twice as fast, the cannon cooling twice as fast.</summary>
        public bool IsOverclocked => Time.time < overclockUntil;
        public float OverclockRemaining => Mathf.Max(0f, overclockUntil - Time.time);
        // Charging the cannon roots him like other heavy attacks: slower movement and no ray charge.
        public bool IsHeavyAttacking => cannonCharging;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, cannonReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => cannonReadyAt = Cooldowns.Shorten(cannonReadyAt, seconds);
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && !Player.IsBusy && !cannonCharging && Time.time >= readyAt;

        /// <summary>The ray's reach for a charge: Focused Lens adds a unit per stack.</summary>
        public float RayReach(float charge) => Mathf.Lerp(RayRange, ChargedRayRange, Mathf.Clamp01(charge)) + Player.Powerups.Count(PowerupType.FocusedLens);
        public static float RayWidthFor(float charge) => Mathf.Lerp(RayWidth, ChargedRayWidth, Mathf.Clamp01(charge));
        /// <summary>Twice the hero's damage, up to three times more at full charge; Overcharge adds one per stack.</summary>
        public int CannonDamage(float charge) => Player.Damage * 2 + Mathf.RoundToInt(Player.Damage * 3f * Mathf.Clamp01(charge))
            + Player.Powerups.Count(PowerupType.Overcharge);
        public float BlastRadius(float charge) => 1.1f + 0.9f * Mathf.Clamp01(charge) + 0.3f * Player.Powerups.Count(PowerupType.Overcharge);
        /// <summary>Capacitor Bank (Ash shop) shortens the cooldown; Overclock halves it.</summary>
        public float CannonCooldownTime => CannonCooldown * (Player.Permanent != null ? Player.Permanent.CannonCooldownMultiplier : 1f)
            * (IsOverclocked ? 0.5f : 1f) * Player.Powerups.SkillCooldownMultiplier;

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            if (IsOverclocked) charge = 1f;
            charge = Mathf.Clamp01(charge);
            aim.Normalize();
            int hits = FireRay(Player, Muzzle(aim), aim, RayReach(charge), RayWidthFor(charge) + Player.Powerups.Count(PowerupType.WideBeam) * 0.08f, Player.Charge.Damage(charge));
            (Player.Mechanic as Overclock)?.OnRayHits(hits);
            // Missile Rack (Overclock's R upgrade): while overclocked, every ray is joined by a homing mini missile.
            if (IsOverclocked && Player.Permanent != null && Player.Permanent.MechanicUpgraded) LaunchEscort(aim);
            if (charge >= 1f) ScreenFx.Shake(0.06f, 0.1f);
            readyAt = Time.time + RayInterval * Player.Powerups.AttackIntervalMultiplier * (IsOverclocked ? 0.5f : 1f);
            return true;
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || cannonCharging || HeavyCooldownRemaining > 0f
                || aim.sqrMagnitude < 0.001f) return false;
            Player.Charge.Cancel();
            cannonCharging = true;
            cannonStartedAt = Time.time;
            Vector2 muzzle = Muzzle(aim.normalized);
            HeroVfx.Pulse(Player.Run.ProjectileRoot, muzzle, 0.5f, Plasma, 0.25f);
            CoopFx.Pulse(Player.Run, muzzle, 0.5f, Plasma, 0.25f);
            return true;
        }

        private void Start() { if (Player != null) Player.Struck += OnStruck; }
        private void OnDestroy() { if (chargeGlow != null) Destroy(chargeGlow.gameObject); if (Player != null) Player.Struck -= OnStruck; }

        /// <summary>Where shots leave the arm cannon.</summary>
        private Vector2 Muzzle(Vector2 aim) => (Vector2)transform.position + aim * 0.45f + Vector2.down * 0.05f;

        private void Update()
        {
            if (Scrap >= ScrapPickup.PerHeal) SpendScrap();
            if (Player == null) return;
            var run = Player.Run;
            if (IsOverclocked && run.IsPlaying && Time.time >= nextHum && !cannonCharging)
            {
                nextHum = Time.time + 0.15f;
                HeroVfx.Motes(run.ProjectileRoot, transform.position, 0.45f, Plasma, 2, 0.4f);
            }
            if (!cannonCharging) return;
            if (!run.IsPlaying || Player.Health <= 0) { CancelCannon(); return; }
            // Overclocked, the cannon is charged the moment he raises it.
            float charge = IsOverclocked ? 1f : CannonCharge;
            Vector2 muzzle = Muzzle(Player.AimDirection);
            if (chargeGlow == null)
            {
                chargeGlow = DungeonVisuals.Create("Cannon charge", run.ProjectileRoot, muzzle, Vector2.one * 0.1f, Plasma, 8);
                chargeGlow.transform.rotation = Quaternion.Euler(0, 0, 45f);
            }
            float size = 0.12f + 0.3f * charge + (charge >= 1f ? 0.04f * Mathf.Sin(Time.time * 30f) : 0f);
            chargeGlow.transform.position = muzzle;
            chargeGlow.transform.localScale = Vector2.one * size;
            chargeGlow.color = Color.Lerp(Plasma, Core, charge >= 1f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 20f) : charge * 0.4f);
            if (Time.time >= nextHum)
            {
                nextHum = Time.time + 0.08f;
                HeroVfx.Motes(run.ProjectileRoot, muzzle, 0.3f + 0.4f * (1f - charge), Plasma, 3, 0.25f);
            }
            bool held = KeyBindings.IsHeld(GameAction.Special);
            if (!held || Time.time >= cannonStartedAt + CannonChargeTime + CannonHoldLimit) FireCannon(Player.AimDirection, charge);
        }

        private void FireCannon(Vector2 aim, float charge)
        {
            CancelCannon();
            if (aim.sqrMagnitude < 0.001f) aim = Vector2.right;
            aim.Normalize();
            PlasmaOrb.Fire(Player, Muzzle(aim), aim, CannonDamage(charge), BlastRadius(charge), charge);
            // Thermal Vent: the cannon's heat pushes him along.
            if (Player.Powerups.Count(PowerupType.ThermalVent) > 0) Player.Buffs.Vent(ThermalVentTime);
            cannonReadyAt = Time.time + CannonCooldownTime;
            readyAt = Mathf.Max(readyAt, Time.time + 0.25f);
            // The recoil shoves him back a step.
            transform.position = Player.Run.Map.Move(transform.position, -aim * (0.15f + 0.25f * charge));
            ScreenFx.Shake(0.08f + 0.12f * charge, 0.15f + 0.1f * charge);
        }

        private void CancelCannon()
        {
            cannonCharging = false;
            if (chargeGlow != null) Destroy(chargeGlow.gameObject);
            chargeGlow = null;
        }


        public void Hide()
        {
            Player.Charge.Cancel();
            CancelCannon();
        }

        /// <summary>Overclock: for <paramref name="duration"/> seconds every shot is fully charged; venting it readies the cannon.</summary>
        public void StartOverclock(float duration)
        {
            overclockUntil = Time.time + duration;
            ReduceHeavyCooldown(float.PositiveInfinity);
        }

        // ---------------------------------------------------------------- the plasma ray

        /// <summary>
        /// Fires a plasma ray from <paramref name="origin"/>: it runs to the first wall (or <paramref name="range"/>) and
        /// strikes every enemy along it. Returns how many enemies it damaged. Teammates see the ray too.
        /// </summary>
        public static int FireRay(DungeonPlayer shooter, Vector2 origin, Vector2 direction, float range, float width, int damage,
            float knockback = RayKnockback)
        {
            var run = shooter.Run;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 end = RayEnd(run, origin, direction, range);
            float length = Vector2.Distance(origin, end);
            int hits = 0;
            bool targeting = shooter.Powerups.Count(PowerupType.TargetingArray) > 0, overheat = shooter.Powerups.Count(PowerupType.Overheat) > 0;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                if (!BrawlerAttack.InRectangle((Vector2)enemy.transform.position - origin, direction, length, width * 0.5f, enemy.HitRadius)) continue;
                if (!enemy.IsInvulnerable) hits++;
                // Targeting Array: +1 at long range. Overheat: a second ray within 2 seconds sets the target burning.
                int dealt = damage + (targeting && Vector2.Distance(origin, enemy.transform.position) > TargetingRange ? 1 : 0);
                CombatDamage.Apply(shooter, enemy, dealt, DamageElement.Physical, origin, knockback);
                if (overheat && enemy != null && enemy.Health > 0)
                {
                    if (lastRayHit.TryGetValue(enemy, out float last) && Time.time - last <= OverheatWindow)
                        enemy.Burn(CombatDamage.BurnTicks, CombatDamage.BurnTickDamage(dealt));
                    lastRayHit[enemy] = Time.time;
                }
                HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, Core, 6, 4f, 0.22f, direction, 70f);
            }
            CyborgVfx.Ray(run.ProjectileRoot, origin, end, Plasma, width);
            CoopFx.PlasmaRay(run, origin, end, Plasma, width);
            return hits;
        }

        /// <summary>Where a ray from <paramref name="origin"/> stops: the first wall or Sanctuary edge, or its full range.</summary>
        public static Vector2 RayEnd(DungeonRun run, Vector2 origin, Vector2 direction, float range)
        {
            Vector2 end = origin;
            for (float travel = 0.1f; travel <= range + 0.001f; travel += 0.1f)
            {
                Vector2 next = origin + direction * Mathf.Min(travel, range);
                if (!run.Map.CanStand(next, 0.05f) || HolyBubble.Blocks(end, next)) break;
                end = next;
            }
            return end;
        }

        // ---------------------------------------------------------------- boss artifacts

        public const float TargetingRange = 6f, OverheatWindow = 2f, ThermalVentTime = 2f, PlatingRadius = 2f;
        private static readonly System.Collections.Generic.Dictionary<DungeonEnemy, float> lastRayHit = new System.Collections.Generic.Dictionary<DungeonEnemy, float>();

        /// <summary>Reactive Plating: a hit sets off a plasma burst around him.</summary>
        private void OnStruck(bool warded)
        {
            if (Player.Powerups.Count(PowerupType.ReactivePlating) == 0 || !Player.Run.IsPlaying) return;
            var run = Player.Run;
            Vector2 at = transform.position;
            HeroVfx.Pulse(run.ProjectileRoot, at, PlatingRadius, Plasma, 0.3f);
            CoopFx.Pulse(run, at, PlatingRadius, Plasma, 0.3f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= PlatingRadius + enemy.HitRadius)
                    CombatDamage.Apply(Player, enemy, Player.Damage, DamageElement.Physical, at, 1.5f);
        }

        public const float MissileRange = 9f, BoostDistance = 4.5f, BoostHitRadius = 0.9f, BoostBlastRadius = 1.4f;
        public const float TurretRange = 4f, TurretDuration = 6f;
        public const int BaseMissiles = 6;

        public bool CastArtifact(AbilityType type, Vector2 aim, int rank, float cursorDistance)
        {
            if (Player == null || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || aim.sqrMagnitude < 0.001f) return false;
            rank = Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank);
            aim.Normalize();
            switch (type)
            {
                case AbilityType.MicroMissiles: LaunchMissiles(aim, rank); return true;
                case AbilityType.RocketBoost: RocketBoost(aim, rank, cursorDistance); return true;
                case AbilityType.SentryTurret:
                    Vector2 spot = PlayerAbilities.FindGroundLanding(Player.Run.Map, transform.position, aim, Mathf.Min(TurretRange, cursorDistance));
                    SentryTurret.Deploy(Player, spot, TurretTime(rank), Player.Damage + rank - 1);
                    return true;
                default: return false;
            }
        }

        public int MissileCount(int rank) => BaseMissiles + rank - 1 + Player.Powerups.Count(PowerupType.Payload) * 2;
        public float BoostReach => BoostDistance + Player.Powerups.Count(PowerupType.Afterburner) * 0.6f;
        public float TurretTime(int rank) => TurretDuration + rank - 1 + Player.Powerups.Count(PowerupType.ExtendedBattery) * 2f;

        /// <summary>One mini missile fired alongside a ray, homing on the nearest enemy in sight (or flying straight with nobody to chase).</summary>
        private void LaunchEscort(Vector2 aim)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            DungeonEnemy target = null;
            float nearest = MissileRange;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                float distance = Vector2.Distance(origin, enemy.transform.position);
                if (distance > nearest || !run.HasLineOfSight(origin, enemy.transform.position)) continue;
                nearest = distance;
                target = enemy;
            }
            MicroMissile.Launch(Player, origin + aim * 0.3f, aim, target, Player.Damage);
        }

        /// <summary>A fan of homing missiles, shared out over the nearest enemies in sight.</summary>
        private void LaunchMissiles(Vector2 aim, int rank)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            var targets = new List<DungeonEnemy>();
            foreach (var enemy in run.Enemies)
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(origin, enemy.transform.position) <= MissileRange
                    && run.HasLineOfSight(origin, enemy.transform.position)) targets.Add(enemy);
            targets.Sort((a, b) => Vector2.SqrMagnitude((Vector2)a.transform.position - origin)
                .CompareTo(Vector2.SqrMagnitude((Vector2)b.transform.position - origin)));
            int count = MissileCount(rank);
            int damage = Player.Damage + rank;
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : -60f + 120f * i / (count - 1);
                Vector2 direction = Quaternion.Euler(0, 0, angle) * aim;
                MicroMissile.Launch(Player, origin + direction * 0.3f, direction, targets.Count > 0 ? targets[i % targets.Count] : null, damage);
            }
            HeroVfx.Sparks(run.ProjectileRoot, origin, MissileColor, 12, 4f, 0.3f, aim, 140f);
            ScreenFx.Shake(0.08f, 0.15f);
        }

        /// <summary>Leg thrusters: a fast dash that rams through enemies, ending in a burst of flame.</summary>
        private void RocketBoost(Vector2 aim, int rank, float cursorDistance)
        {
            var run = Player.Run;
            Vector2 from = transform.position;
            float distance = Mathf.Min(BoostReach, Mathf.Max(0.5f, cursorDistance));
            int damage = Player.Damage * 2 + rank - 1;
            var rammed = new HashSet<DungeonEnemy>();
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.15f));
            Player.Protect(0.45f);
            for (int i = 0; i < steps; i++)
            {
                transform.position = run.Map.Move(transform.position, aim * (distance / steps));
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || rammed.Contains(enemy)) continue;
                    if (Vector2.Distance(transform.position, enemy.transform.position) > BoostHitRadius + enemy.HitRadius) continue;
                    rammed.Add(enemy);
                    CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, from, 1.4f);
                }
            }
            Vector2 landing = transform.position;
            // Thruster exhaust along the path, and a touchdown burst of flame that may set the enemies around him alight.
            RocketBoostVfx.Play(run.ProjectileRoot, from, landing, BoostBlastRadius);
            CoopFx.RocketBoost(run, from, landing, BoostBlastRadius);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(landing, enemy.transform.position) <= BoostBlastRadius + enemy.HitRadius
                    && run.HasLineOfSight(landing, enemy.transform.position))
                    CombatDamage.Apply(Player, enemy, Player.Damage + rank - 1, DamageElement.Fire, landing, 0.8f);
            ScreenFx.Shake(0.12f, 0.18f);
        }
    }
}

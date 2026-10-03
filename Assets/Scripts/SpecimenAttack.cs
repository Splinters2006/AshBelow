using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Specimen: an escaped lab subject who starts every descent frail and grows into whatever his talents feed.
    /// Until he transforms, every floor offers him one Bulk and one Edge talent; the first path to reach three picks
    /// claims him for the rest of the descent, and six grow him again.
    /// <list type="bullet">
    /// <item>Frail: a weak palm strike or a charged snap kick, and a Flinch Guard that soaks one bolt.</item>
    /// <item>Behemoth (Bulk): heavy hands and kicks that move enemies (Palm Shove, Stomp Kick, Ground Pound, a charged
    /// Axe Kick). Anything he knocks into a wall or another enemy is wall-slammed. His Arm Guard soaks bolts as Force,
    /// bulldozes whatever stands in front of him, and lets it all go in a Repel. Enemies near him go for him first.</item>
    /// <item>Edge: a chain whip whose tip is a sweet spot (always a critical hit), a charged Chain Cyclone, and a Hook
    /// that yanks enemies in, or him to the heavy ones.</item>
    /// </list>
    /// His boss artifacts are cast by <see cref="SpecimenArts"/> and his R mechanic is <see cref="BreakingPoint"/>.
    /// </summary>
    public sealed class SpecimenAttack : MonoBehaviour, IPlayerWeapon, IRunPersistent
    {
        public const float FrailChargeTime = 0.9f, BehemothChargeTime = 1.1f, EdgeChargeTime = 1f;
        // Frail.
        public const float PalmReach = 1.15f, PalmCone = 80f, KickReach = 1.5f, KickCone = 50f, FlinchTime = 0.5f, FlinchCooldown = 4f, FlinchArc = 150f;
        // Behemoth.
        public const float BehemothScale = 1.35f, ColossusScale = 1.6f, RampageScale = 2.2f;
        public const float ShoveReach = 1.7f, ShoveCone = 110f, StompReach = 2.1f, StompCone = 60f, PoundRadius = 1.6f;
        public const float AxeLength = 3.2f, AxeWidth = 0.9f, AxeStun = 1f;
        public const float GuardTime = 3f, GuardCooldown = 4f, GuardArc = 140f, ColossusGuardArc = 180f;
        public const float RepelReach = 2.2f, RepelCone = 120f, RepelSpread = 50f, ForceBoltRange = 9f;
        public const int ForceCap = 5, ColossusForceCap = 8;
        public const float SlamStun = 0.5f, BigTargetRadius = 4f;
        // Edge.
        public const float LashReach = 3.2f, GrownLashReach = 0.8f, LashCone = 14f, SweetSpot = 0.6f, LashInterval = 0.42f, CycloneInterval = 0.7f;
        public const float HookRange = 7f, HookCooldown = 4f, HookStun = 0.5f;
        // Breaking Point.
        public const float RampageTime = 8f, OverdriveTime = 6f, SnapTime = 10f, RoarRadius = 6f, RoarStun = 2f, RoarTaunt = 3f, OverdriveKillTime = 0.5f;
        public const int SlamsPerHeal = 4;
        // What each form adds on top of his frail stats.
        public const int BehemothHealth = 4, ColossusHealth = 3, EdgeHealth = 1;
        public const float BehemothSpeed = -0.5f, ColossusSpeed = -0.2f, EdgeSpeed = 0.6f, EdgeGrownSpeed = 0.4f;
        public const float EdgeCrit = 0.1f, EdgeGrownCrit = 0.15f, RazorTipCrit = 0.1f;

        public DungeonPlayer Player { get; set; }

        // ---------------------------------------------------------------- form

        /// <summary>The path that claimed him this descent (None while frail); kept on his talent sheet.</summary>
        public SpecimenPath LockedPath => Player != null && Player.Powerups != null ? Player.Powerups.MutationPath : SpecimenPath.None;
        /// <summary>Six picks on his path: the Colossus, or the Edge's second stage.</summary>
        public bool IsGrown { get; private set; }
        private SpecimenPath snapPath;
        private float snapUntil;
        /// <summary>Breaking Point while frail: for a while he is the form he leans toward.</summary>
        public bool IsSnapped => snapPath != SpecimenPath.None && Time.time < snapUntil;
        public float SnapRemaining => IsSnapped ? snapUntil - Time.time : 0f;
        public SpecimenPath ActivePath => IsSnapped ? snapPath : LockedPath;
        public SpecimenForm Form => SpecimenCatalog.FormOf(ActivePath);
        public int BulkPoints => SpecimenCatalog.Points(Player != null ? Player.Powerups : null, SpecimenPath.Bulk);
        public int EdgePoints => SpecimenCatalog.Points(Player != null ? Player.Powerups : null, SpecimenPath.Edge);
        /// <summary>The form a frail Specimen is closer to (ties lean to the Behemoth).</summary>
        public SpecimenPath Lean => BulkPoints >= EdgePoints ? SpecimenPath.Bulk : SpecimenPath.Edge;
        public float BodyScale => Form != SpecimenForm.Behemoth ? 1f : IsRampaging ? RampageScale : IsGrown ? ColossusScale : BehemothScale;
        private float ReachScale => Form != SpecimenForm.Behemoth ? 1f : IsRampaging ? 1.35f : IsGrown ? 1.15f : 1f;

        /// <summary>
        /// A talent's ranks as they apply right now: his path's talents work while he is frail and in their own form,
        /// and sleep in the other one.
        /// </summary>
        public int Rank(PowerupType type)
        {
            if (Player == null || Player.Powerups == null) return 0;
            var path = SpecimenCatalog.PathOf(type);
            return path == SpecimenPath.None || ActivePath == SpecimenPath.None || path == ActivePath ? Player.Powerups.Count(type) : 0;
        }

        /// <summary>Heartbeat and Fight or Flight work in any form; the rest only in their own.</summary>
        public bool CanUseAbility(AbilityType type)
        {
            var path = SpecimenCatalog.PathOf(type);
            return path == SpecimenPath.None || path == ActivePath;
        }

        // ---------------------------------------------------------------- state

        private float readyAt, skillReadyAt, lastStrikeAt, flinchUntil, rampageUntil, overdriveUntil, flightUntil, roarUntil, ironSkinUntil, echoAt;
        private int comboStep, ironSkinCharges, rampageSlams, echoDamage;
        private bool guarding, rollCrit, roarPending, flightEdge;
        private float guardStartedAt;
        private Vector2 flinchAim, echoAim, lastGuardPosition;
        private Vector3 baseScale;
        private bool started;
        private int lookKey = -1;
        private ChainHook hook;
        private readonly Dictionary<DungeonEnemy, float> exposed = new Dictionary<DungeonEnemy, float>();
        private readonly Dictionary<DungeonEnemy, float> bound = new Dictionary<DungeonEnemy, float>();
        private readonly Dictionary<DungeonEnemy, float> tripped = new Dictionary<DungeonEnemy, float>();
        private readonly Dictionary<DungeonEnemy, float> trampled = new Dictionary<DungeonEnemy, float>();
        private float nextSweep;
        private GameObject previewObject;
        private FlameMesh preview;

        /// <summary>Bolts soaked by the Arm Guard, waiting to be thrown back by a Repel.</summary>
        public int Force { get; private set; }
        public int ForceCapacity => (IsGrown && LockedPath == SpecimenPath.Bulk ? ColossusForceCap : ForceCap) + Rank(PowerupType.IronForearms);
        public bool IsGuarding => guarding;
        public float GuardDuration => GuardTime + Rank(PowerupType.IronForearms);
        public float GuardRemaining => guarding ? Mathf.Max(0f, guardStartedAt + GuardDuration - Time.time) : 0f;
        private float GuardArcNow => IsGrown && LockedPath == SpecimenPath.Bulk ? ColossusGuardArc : GuardArc;
        /// <summary>Guarding slows him to half speed; Immovable takes the slow away.</summary>
        public float GuardMoveMultiplier => Mathf.Min(1f, 0.5f + 0.25f * Rank(PowerupType.Immovable));
        public bool IsFlinching => Time.time < flinchUntil;
        /// <summary>Set by Bulldoze while it charges: bolts striking his crossed arms become Force.</summary>
        public bool IsBulldozing { get; set; }
        public bool IsRampaging => Time.time < rampageUntil;
        public float RampageRemaining => Mathf.Max(0f, rampageUntil - Time.time);
        public bool IsOverdriven => Time.time < overdriveUntil;
        public float OverdriveRemaining => Mathf.Max(0f, overdriveUntil - Time.time);
        /// <summary>Apex Mutation: the roar that ends a Rampage draws every enemy to him for a while.</summary>
        public bool IsRoaring => Time.time < roarUntil && Player != null && Player.Health > 0;
        public bool IsFleeing => Time.time < flightUntil;
        public bool HasIronSkin => Time.time < ironSkinUntil && ironSkinCharges > 0;
        public int IronSkinCharges => HasIronSkin ? ironSkinCharges : 0;
        /// <summary>The Behemoth is a big target: enemies near him go for him first.</summary>
        public bool IsBigTarget => Form == SpecimenForm.Behemoth && Player != null && Player.Health > 0;
        /// <summary>Rooted Stance: enemies touching the Behemoth no longer hurt him.</summary>
        public bool IgnoresContact => Form == SpecimenForm.Behemoth && Rank(PowerupType.RootedStance) > 0;
        /// <summary>Featherweight's extra speed (added to his base speed).</summary>
        public float BonusSpeed => 0.4f * Rank(PowerupType.Featherweight);
        public float MoveMultiplier => IsFleeing ? 1.4f : 1f;
        public float DodgeCooldownMultiplier => (1f - 0.1f * Rank(PowerupType.Featherweight)) * (IsOverdriven ? 0.5f : 1f);
        public float LashReachNow => LashReach + 0.5f * Rank(PowerupType.LongChain) + (IsGrown && LockedPath == SpecimenPath.Edge ? GrownLashReach : 0f);
        public float SweetSpotNow => SweetSpot * (1f + 0.5f * Rank(PowerupType.WeightedTip));
        public float GroundPoundRadius => (PoundRadius + 0.5f * Rank(PowerupType.Aftershock)) * ReachScale;

        /// <summary>His body as teammates see it (see <see cref="PlayerStateMessage.SpecimenFormMask"/>).</summary>
        public int NetFormBits => ((Form == SpecimenForm.Behemoth && IsRampaging ? 3 : (int)Form) << PlayerStateMessage.SpecimenFormShift)
            | (IsGrown ? PlayerStateMessage.SpecimenGrown : 0);

        public static float ChargeTimeFor(DungeonPlayer player)
        {
            var form = player != null && player.Weapon is SpecimenAttack specimen ? specimen.Form : SpecimenForm.Frail;
            return form == SpecimenForm.Behemoth ? BehemothChargeTime : form == SpecimenForm.Edge ? EdgeChargeTime : FrailChargeTime;
        }

        // ---------------------------------------------------------------- IPlayerWeapon

        public bool IsHeavyAttacking => guarding;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, skillReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => skillReadyAt = Cooldowns.Shorten(skillReadyAt, seconds);
        public bool CanAttack => Player != null && Player.Run.IsPlaying && !Player.IsRolling && !Player.IsBusy && !guarding && Time.time >= readyAt;
        public string SpecialName => Form == SpecimenForm.Behemoth ? "Arm guard" : Form == SpecimenForm.Edge ? "Hook" : "Flinch guard";
        public float SpecialCooldownTime => Form == SpecimenForm.Behemoth ? GuardCooldown
            : Form == SpecimenForm.Edge ? Mathf.Max(1.5f, HookCooldown - Rank(PowerupType.ReelIn)) : FlinchCooldown;

        private float Interval(float seconds) => seconds * Player.Powerups.AttackIntervalMultiplier
            * (Form == SpecimenForm.Edge && IsOverdriven ? 0.5f : 1f) * (flightEdge && IsFleeing ? 1f / 1.5f : 1f);

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            aim.Normalize();
            switch (Form)
            {
                case SpecimenForm.Behemoth: return BehemothAttack(aim, charge);
                case SpecimenForm.Edge: return EdgeAttack(aim, charge);
                default: return FrailAttack(aim, charge);
            }
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (Player == null || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || guarding || HeavyCooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            aim.Normalize();
            switch (Form)
            {
                case SpecimenForm.Behemoth: return BeginGuard();
                case SpecimenForm.Edge: return FireHook(aim);
                default: return Flinch(aim);
            }
        }

        public void Hide()
        {
            if (Player == null) return;
            Player.Charge.Cancel();
            // A roll, an ability or a menu breaks the guard without a Repel; the Force stays stored.
            if (guarding) { guarding = false; skillReadyAt = Mathf.Max(skillReadyAt, Time.time + 1f); }
        }

        // ---------------------------------------------------------------- life cycle

        private void Start()
        {
            baseScale = transform.localScale;
            previewObject = new GameObject("Specimen preview");
            preview = new FlameMesh(previewObject, 5);
            started = true;
            Evaluate();
            ApplyLook(true);
        }

        private void OnDestroy()
        {
            preview?.Release();
            if (previewObject != null) Destroy(previewObject);
        }

        private void Update()
        {
            if (Player == null || !started) return;
            ApplyLook(false);
            var run = Player.Run;
            if (guarding)
            {
                if (!run.IsPlaying || Player.Health <= 0 || Player.IsBusy || Player.IsRolling) Hide();
                else if (!KeyBindings.IsHeld(GameAction.Special) || Time.time >= guardStartedAt + GuardDuration) Repel();
                else PushForward();
            }
            if (echoAt > 0f && Time.time >= echoAt)
            {
                echoAt = 0f;
                if (run.IsPlaying && Player.Health > 0 && Form == SpecimenForm.Edge) Lash(echoAim, echoDamage, true);
            }
            if (IsRampaging && run.IsPlaying && Player.Health > 0) Trample();
            if (roarPending && !IsRampaging)
            {
                roarPending = false;
                if (run.IsPlaying && Player.Health > 0) Roar();
            }
            if (Time.time >= nextSweep)
            {
                nextSweep = Time.time + 1f;
                Sweep(exposed); Sweep(bound); Sweep(tripped); Sweep(trampled);
            }
        }

        private static void Sweep(Dictionary<DungeonEnemy, float> marks)
        {
            if (marks.Count == 0) return;
            var stale = new List<DungeonEnemy>();
            foreach (var pair in marks) if (pair.Key == null || pair.Key.Health <= 0 || pair.Value < Time.time) stale.Add(pair.Key);
            foreach (var enemy in stale) marks.Remove(enemy);
        }

        private static bool Marked(Dictionary<DungeonEnemy, float> marks, DungeonEnemy enemy)
            => enemy != null && marks.TryGetValue(enemy, out float until) && Time.time < until;

        // ---------------------------------------------------------------- transforming

        public void SaveRun(HeroSnapshot hero) => hero.SetExtra("grown", IsGrown ? 1 : 0);

        /// <summary>
        /// Loaded before <see cref="Start"/>: with his path and growth back, its first look at his talents changes nothing
        /// (his stats already include every change of body).
        /// </summary>
        public void LoadRun(HeroSnapshot hero) => IsGrown = hero.Extra("grown", IsGrown ? 1 : 0) == 1 && LockedPath != SpecimenPath.None;

        /// <summary>A talent was taken: the first path to three picks claims him, and six grow him.</summary>
        public void OnTalentTaken(PowerupType type) => Evaluate();

        private void Evaluate()
        {
            if (Player == null || Player.Powerups == null) return;
            if (LockedPath == SpecimenPath.None)
            {
                if (BulkPoints >= SpecimenCatalog.TransformPicks) Transform(SpecimenPath.Bulk);
                else if (EdgePoints >= SpecimenCatalog.TransformPicks) Transform(SpecimenPath.Edge);
            }
            if (LockedPath != SpecimenPath.None && !IsGrown && SpecimenCatalog.Points(Player.Powerups, LockedPath) >= SpecimenCatalog.GrowPicks) Grow();
            RefreshStats();
        }

        /// <summary>
        /// The path claims him: his body changes for the rest of the descent. Leftover picks on the other path wither
        /// into health (Bulk Up already gave its own), and an ability learned from the other path is traded for one of his.
        /// </summary>
        private void Transform(SpecimenPath path)
        {
            Player.Powerups.MutationPath = path;
            snapPath = SpecimenPath.None;
            snapUntil = 0f;
            int withered = 0;
            foreach (var talent in PowerupCatalog.All)
                if (SpecimenCatalog.PathOf(talent.Type) == SpecimenCatalog.Other(path) && talent.Type != PowerupType.BulkUp)
                    withered += Player.Powerups.Count(talent.Type);
            if (withered > 0) Player.RaiseMaxHealth(withered);
            TradeStrayAbilities(path);
            if (path == SpecimenPath.Bulk) { Player.RaiseMaxHealth(BehemothHealth); Player.AdjustSpeed(BehemothSpeed); }
            else { Player.RaiseMaxHealth(EdgeHealth); Player.AdjustSpeed(EdgeSpeed); }
            Hide();
            Force = 0;
            HealFromChange();
            ApplyLook(true);
            SpecimenVfx.Transformation(Player.Run, transform, SpecimenCatalog.FormOf(path), false);
        }

        private void Grow()
        {
            IsGrown = true;
            if (LockedPath == SpecimenPath.Bulk) { Player.RaiseMaxHealth(ColossusHealth); Player.AdjustSpeed(ColossusSpeed); }
            else Player.AdjustSpeed(EdgeGrownSpeed);
            HealFromChange();
            ApplyLook(true);
            SpecimenVfx.Transformation(Player.Run, transform, Form, true);
        }

        /// <summary>Adaptive Tissue (Ash shop): every change of body heals him a little.</summary>
        private void HealFromChange()
        {
            int heal = Player.Permanent != null ? Player.Permanent.TransformHeal : 0;
            if (heal > 0) Player.Heal(heal);
        }

        private void TradeStrayAbilities(SpecimenPath path)
        {
            var abilities = Player.Abilities;
            if (abilities == null) return;
            var other = SpecimenCatalog.Other(path);
            foreach (var stray in new List<AbilityDefinition>(abilities.Learned))
            {
                if (SpecimenCatalog.PathOf(stray.Type) != other) continue;
                int rank = abilities.Rank(stray.Type);
                var pool = AbilityCatalog.PoolFor(WeaponType.Mutation, Player.Run != null ? Player.Run.Progress : null)
                    .FindAll(ability => SpecimenCatalog.PathOf(ability.Type) == path && !abilities.IsLearned(ability.Type));
                if (pool.Count > 0) { abilities.Replace(stray.Type, pool[Random.Range(0, pool.Count)].Type, rank); continue; }
                abilities.Forget(stray.Type);
                AbilityType raise = AbilityType.None;
                foreach (var own in abilities.Learned)
                    if (SpecimenCatalog.PathOf(own.Type) == path && abilities.Rank(own.Type) < PlayerAbilities.MaxRank) { raise = own.Type; break; }
                if (raise != AbilityType.None) abilities.Learn(raise);
                else Player.RaiseMaxHealth(1);
            }
        }

        /// <summary>
        /// Shapes a guardian's ability offers: once a path has claimed him, the other path's abilities are never offered;
        /// while he is frail, one Bulk and one Edge ability are always on the table, and the rest come from the abilities
        /// that work in any form (or from either path, when too few of those are left to fill the offer).
        /// </summary>
        public void ShapeAbilityOffers(List<AbilityDefinition> pool, int count, List<AbilityDefinition> offers)
        {
            if (pool == null || offers == null) return;
            if (LockedPath != SpecimenPath.None)
            {
                var other = SpecimenCatalog.Other(LockedPath);
                pool.RemoveAll(ability => SpecimenCatalog.PathOf(ability.Type) == other);
                return;
            }
            foreach (var path in new[] { SpecimenPath.Bulk, SpecimenPath.Edge })
            {
                if (offers.Count >= count) break;
                var options = pool.FindAll(ability => SpecimenCatalog.PathOf(ability.Type) == path && !offers.Contains(ability));
                if (options.Count == 0) continue;
                var pick = options[Random.Range(0, options.Count)];
                offers.Add(pick);
                pool.Remove(pick);
            }
            pool.RemoveAll(offers.Contains);
            if (pool.FindAll(ability => SpecimenCatalog.PathOf(ability.Type) == SpecimenPath.None).Count >= count - offers.Count)
                pool.RemoveAll(ability => SpecimenCatalog.PathOf(ability.Type) != SpecimenPath.None);
        }

        private void RefreshStats()
        {
            if (Player == null || Player.Powerups == null) return;
            float crit = RazorTipCrit * Rank(PowerupType.RazorTip);
            if (Form == SpecimenForm.Edge) crit += IsGrown && LockedPath == SpecimenPath.Edge ? EdgeGrownCrit : EdgeCrit;
            Player.Powerups.FormCritBonus = crit;
        }

        /// <summary>Swaps in the sprite and size of his current form (and keeps his stats in step with it).</summary>
        private void ApplyLook(bool force)
        {
            if (Player == null || !started) return;
            int key = (int)Form | (IsGrown ? 4 : 0) | (IsRampaging ? 8 : 0);
            if (!force && key == lookKey) return;
            bool changed = lookKey >= 0 && key != lookKey;
            lookKey = key;
            Player.SetLook(HeroSprites.SpecimenBody(Form), HeroSprites.SpecimenAccent(Form));
            float scale = BodyScale;
            transform.localScale = new Vector3(baseScale.x * scale, baseScale.y * scale, baseScale.z);
            RefreshStats();
            // A Snap wearing off (or starting) is a change of body too.
            if (changed && !force && Player.Run.ProjectileRoot != null) HeroVfx.Pulse(Player.Run.ProjectileRoot, transform.position, 1.2f * scale, SpecimenCatalog.FormColor(Form), 0.3f);
        }

        // ---------------------------------------------------------------- striking

        private Transform Root => Player.Run.ProjectileRoot;

        /// <summary>Hits every enemy in a cone; Behemoth blows knock them back and wall-slam them.</summary>
        private int Strike(Vector2 aim, float reach, float cone, int damage, float knockback, bool slam)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            int hit = 0;
            Breakable.SmashInCone(run, origin, aim, reach, cone);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 at = enemy.transform.position;
                if (!SwordAttack.OverlapsCone(at - origin, aim, reach, cone, enemy.HitRadius) || !run.HasLineOfSight(origin, at)) continue;
                HitAndShove(enemy, damage, origin, knockback, slam);
                hit++;
            }
            return hit;
        }

        /// <summary>
        /// Deals a blow that knocks the enemy away from <paramref name="from"/>. With <paramref name="slam"/>, an enemy
        /// knocked into a wall or into another enemy is wall-slammed: both take extra damage and are stunned.
        /// </summary>
        public void HitAndShove(DungeonEnemy enemy, int damage, Vector2 from, float knockback, bool slam, float extraStun = 0f)
        {
            if (enemy == null || enemy.Health <= 0) return;
            var run = Player.Run;
            Vector2 at = enemy.transform.position, away = at - from;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Player.AimDirection;
            bool wall = false;
            DungeonEnemy struck = null;
            // Mirrors how far DungeonEnemy.Hit shoves it, to see whether something stops it first.
            float distance = knockback * (enemy.IsTank ? 0.2f : 0.65f);
            if (slam && enemy.Boss == null && !enemy.IsInvulnerable && distance > 0.05f)
            {
                Vector2 landing = run.Map.Move(at, away * distance, enemy.MoveRadius);
                wall = Vector2.Distance(at, landing) < distance * 0.7f;
                foreach (var other in run.Enemies)
                {
                    if (other == null || other == enemy || other.Health <= 0) continue;
                    Vector2 offset = (Vector2)other.transform.position - at;
                    float along = Vector2.Dot(offset, away);
                    if (along <= 0f || along > distance + enemy.HitRadius + other.HitRadius) continue;
                    if ((offset - away * along).magnitude <= enemy.HitRadius + other.HitRadius) { struck = other; break; }
                }
            }
            CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, from, knockback);
            if (wall || struck != null) Slam(enemy, struck, extraStun);
        }

        /// <summary>A wall slam for an enemy he threw into a wall himself (Giant Swing's fling).</summary>
        public void WallSlam(DungeonEnemy enemy, float extraStun = 0f) { if (enemy != null && enemy.Health > 0) Slam(enemy, null, extraStun); }

        /// <summary>Wall Slam: extra damage and a short stun for the enemy (and for whatever it was knocked into).</summary>
        private void Slam(DungeonEnemy enemy, DungeonEnemy struck, float extraStun)
        {
            var run = Player.Run;
            int bonus = 1 + Rank(PowerupType.BoneBreaker);
            Vector2 at = enemy != null ? (Vector2)enemy.transform.position : (Vector2)transform.position;
            foreach (var victim in new[] { enemy, struck })
            {
                if (victim == null || victim.Health <= 0) continue;
                CombatDamage.Apply(Player, victim, bonus, DamageElement.Physical, victim.transform.position, 0f);
                if (victim.Health > 0) victim.Stun(SlamStun + extraStun);
            }
            HeroVfx.Sparks(Root, at, SpecimenCatalog.Stone, 12, 4f, 0.35f);
            HeroVfx.Pulse(Root, at, 0.7f, SpecimenCatalog.Amber, 0.25f);
            CoopFx.Pulse(run, at, 0.7f, SpecimenCatalog.Amber, 0.25f);
            ScreenFx.Shake(0.08f, 0.1f);
            if (IsRampaging && ++rampageSlams % SlamsPerHeal == 0) { Player.Heal(1); HeroVfx.Motes(Root, transform.position, 0.8f, HealVfx.Mint, 8, 0.7f); }
        }

        // ---------------------------------------------------------------- frail

        private bool FrailAttack(Vector2 aim, float charge)
        {
            bool kick = charge >= 1f;
            float reach = kick ? KickReach : PalmReach, cone = kick ? KickCone : PalmCone;
            Strike(aim, reach, cone, Player.Charge.Damage(charge), kick ? 2.4f : 0.8f, false);
            var color = kick ? Color.Lerp(SpecimenCatalog.Pale, SpecimenCatalog.Amber, 0.35f) : SpecimenCatalog.Pale;
            HeroVfx.Slash(Root, transform.position, aim, reach, cone, FlameMesh.Alpha(color, 0.5f), 0.14f);
            CoopFx.Slash(Player.Run, transform.position, aim, reach, cone, color);
            if (kick) HeroVfx.Sparks(Root, (Vector2)transform.position + aim * reach * 0.8f, color, 6, 3f, 0.25f, aim, 50f);
            readyAt = Time.time + Interval(kick ? 0.5f : 0.38f);
            return true;
        }

        /// <summary>Flinch Guard: he throws his arms up; the first bolt that hits them is soaked up.</summary>
        private bool Flinch(Vector2 aim)
        {
            flinchUntil = Time.time + FlinchTime;
            flinchAim = aim;
            skillReadyAt = Time.time + FlinchCooldown * Player.Powerups.SkillCooldownMultiplier;
            Player.Charge.Cancel();
            HeroVfx.Pulse(transform, transform.position, 0.8f, SpecimenCatalog.Pale, 0.25f);
            return true;
        }

        // ---------------------------------------------------------------- Behemoth

        private bool BehemothAttack(Vector2 aim, float charge)
        {
            if (IsRampaging)
            {
                // Rampaging, every blow is a Ground Pound.
                GroundPound(Player.Charge.Damage(charge) * 2, GroundPoundRadius + 0.8f);
                readyAt = Time.time + Interval(0.55f);
                return true;
            }
            if (charge >= 1f) return AxeKick(aim);
            comboStep = Time.time - lastStrikeAt > 1.2f ? 0 : (comboStep + 1) % 3;
            lastStrikeAt = Time.time;
            int damage = Player.Charge.Damage(charge);
            var run = Player.Run;
            Vector2 origin = transform.position;
            float rs = ReachScale;
            if (comboStep == 0)
            {
                // Palm Shove: a wide push.
                Strike(aim, ShoveReach * rs, ShoveCone, damage, 2.2f, true);
                HeroVfx.Slash(Root, origin, aim, ShoveReach * rs, ShoveCone, FlameMesh.Alpha(SpecimenCatalog.Stone, 0.55f), 0.18f);
                HeroVfx.Sparks(Root, origin + aim * ShoveReach * rs * 0.7f, SpecimenCatalog.Stone, 8, 3f, 0.3f, aim, 90f);
                CoopFx.Slash(run, origin, aim, ShoveReach * rs, ShoveCone, SpecimenCatalog.Stone);
                readyAt = Time.time + Interval(0.5f);
            }
            else if (comboStep == 1)
            {
                // Stomp Kick: launches the nearest enemy in front of him.
                var target = NearestInCone(aim, StompReach * rs, StompCone);
                Breakable.SmashInCone(run, origin, aim, StompReach * rs, StompCone);
                if (target != null) HitAndShove(target, Mathf.CeilToInt(damage * 1.5f), origin, 4f, true);
                Vector2 tip = origin + aim * StompReach * rs;
                CombatVfx.Bolt(Root, origin + aim * 0.3f, tip, SpecimenCatalog.Stone, 0.16f, 0.18f);
                HeroVfx.Sparks(Root, target != null ? (Vector2)target.transform.position : tip, SpecimenCatalog.Amber, 8, 4f, 0.3f, aim, 60f);
                CoopFx.Stab(run, origin, aim, StompReach * rs, SpecimenCatalog.Stone);
                readyAt = Time.time + Interval(0.55f);
            }
            else
            {
                // Ground Pound: both fists into the floor.
                GroundPound(damage * 2, GroundPoundRadius);
                readyAt = Time.time + Interval(0.85f);
            }
            return true;
        }

        private DungeonEnemy NearestInCone(Vector2 aim, float reach, float cone)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            DungeonEnemy best = null;
            float bestDistance = float.MaxValue;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                if (!SwordAttack.OverlapsCone(offset, aim, reach, cone, enemy.HitRadius) || !run.HasLineOfSight(origin, enemy.transform.position)) continue;
                if (offset.sqrMagnitude < bestDistance) { bestDistance = offset.sqrMagnitude; best = enemy; }
            }
            return best;
        }

        private void GroundPound(int damage, float radius)
        {
            var run = Player.Run;
            Vector2 center = transform.position;
            Breakable.SmashInCone(run, center, Vector2.right, radius, 360f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 at = enemy.transform.position;
                if (Vector2.Distance(center, at) > radius + enemy.HitRadius || !run.HasLineOfSight(center, at)) continue;
                HitAndShove(enemy, damage, center, 1.6f, true);
            }
            CombatVfx.Ring(Root, center, radius, SpecimenCatalog.Stone, 0.35f);
            HeroVfx.Pulse(Root, center, radius, FlameMesh.Alpha(SpecimenCatalog.Amber, 0.7f), 0.3f);
            HeroVfx.Sparks(Root, center, SpecimenCatalog.Stone, 16, 5f, 0.4f);
            CoopFx.Ring(run, center, radius, SpecimenCatalog.Stone, 0.35f);
            CoopFx.Pulse(run, center, radius, SpecimenCatalog.Amber, 0.3f);
            ScreenFx.Shake(0.14f, 0.18f);
        }

        /// <summary>Axe Kick (a full charge): his heel comes down and cracks the floor in a line, stunning what it catches.</summary>
        private bool AxeKick(Vector2 aim)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            float length = AxeLength * ReachScale;
            // The crack stops at the first wall.
            length = Mathf.Max(0.5f, Vector2.Distance(origin, PlayerAbilities.FindGroundLanding(run.Map, origin, aim, length)));
            int damage = Player.Charge.Damage(1f);
            Breakable.SmashInLane(run, origin, aim, length, AxeWidth * 0.5f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                float along = Vector2.Dot(offset, aim);
                if (along < -enemy.HitRadius || along > length + enemy.HitRadius) continue;
                if ((offset - aim * along).magnitude > AxeWidth * 0.5f + enemy.HitRadius) continue;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, origin, 0.4f);
                if (enemy != null && enemy.Health > 0) enemy.Stun(AxeStun);
            }
            Vector2 end = origin + aim * length;
            CombatVfx.Bolt(Root, origin + aim * 0.4f, end, SpecimenCatalog.Amber, 0.2f, 0.45f);
            for (int i = 1; i <= 5; i++)
                HeroVfx.Sparks(Root, origin + aim * length * i / 5f, SpecimenCatalog.Stone, 4, 3f, 0.3f, Vector2.Perpendicular(aim), 160f);
            CoopFx.Bolt(run, origin, end, SpecimenCatalog.Amber, true);
            ScreenFx.Shake(0.16f, 0.2f);
            readyAt = Time.time + Interval(0.8f);
            return true;
        }

        private bool BeginGuard()
        {
            guarding = true;
            guardStartedAt = Time.time;
            lastGuardPosition = transform.position;
            Player.Charge.Cancel();
            HeroVfx.Pulse(transform, transform.position, 0.9f, SpecimenCatalog.Amber, 0.2f);
            return true;
        }

        /// <summary>Walking forward with his guard up shoves whatever stands in front of him along.</summary>
        private void PushForward()
        {
            var run = Player.Run;
            Vector2 here = transform.position, moved = here - lastGuardPosition;
            lastGuardPosition = here;
            Vector2 aim = Player.AimDirection;
            float forward = Vector2.Dot(moved, aim);
            if (forward <= 0.0001f) return;
            float reach = Player.HitRadius + 0.55f;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.Boss != null) continue;
                Vector2 offset = (Vector2)enemy.transform.position - here;
                if (offset.magnitude > reach + enemy.HitRadius || Vector2.Angle(offset, aim) > GuardArcNow * 0.5f) continue;
                enemy.transform.position = run.Map.Move(enemy.transform.position, aim * (forward + 0.02f), enemy.MoveRadius);
            }
        }

        /// <summary>Lets the guard go: a shove wave in front of him, and every stored bolt thrown back out in a fan.</summary>
        private void Repel()
        {
            guarding = false;
            skillReadyAt = Time.time + GuardCooldown * Player.Powerups.SkillCooldownMultiplier;
            var run = Player.Run;
            Vector2 aim = Player.AimDirection, origin = transform.position;
            float reach = RepelReach * ReachScale;
            Strike(aim, reach, RepelCone, Player.Damage, 3f, true);
            HeroVfx.Slash(Root, origin, aim, reach, RepelCone, FlameMesh.Alpha(SpecimenCatalog.Amber, 0.6f), 0.22f);
            CoopFx.Slash(run, origin, aim, reach, RepelCone, SpecimenCatalog.Amber);
            ScreenFx.Shake(0.1f, 0.14f);
            int bolts = Force;
            Force = 0;
            if (bolts <= 0) return;
            int damage = Player.Damage + Rank(PowerupType.ReturnToSender);
            int pierces = Rank(PowerupType.ReturnToSender) > 0 ? 3 : 0;
            Vector2 muzzle = origin + aim * 0.5f * BodyScale;
            for (int i = 0; i < bolts; i++)
            {
                float angle = bolts == 1 ? 0f : -RepelSpread * 0.5f + RepelSpread * i / (bolts - 1);
                SpellProjectile.Spawn(Player, Quaternion.Euler(0, 0, angle) * aim, damage, DamageElement.Physical, SpecimenCatalog.Amber,
                    ForceBoltRange, 0f, pierces, muzzle);
            }
            HeroVfx.Sparks(Root, muzzle, SpecimenCatalog.Amber, 6 + bolts * 2, 4.5f, 0.3f, aim, RepelSpread + 20f);
        }

        /// <summary>Stores Force (up to his capacity) in his veins.</summary>
        public void StoreForce(int amount)
        {
            if (amount <= 0) return;
            Force = Mathf.Min(ForceCapacity, Force + amount);
        }

        /// <summary>
        /// An enemy bolt reached him: a Flinch Guard, the Arm Guard or a Bulldoze soaks it (true) instead of letting it
        /// hit. Soaked bolts become Force and feed Breaking Point.
        /// </summary>
        public bool TryAbsorb(Vector2 at, Vector2 incoming)
        {
            if (Player == null || Player.Health <= 0) return false;
            Vector2 offset = at - (Vector2)transform.position;
            float reach = Player.HitRadius + 0.5f;
            if (offset.sqrMagnitude > reach * reach) return false;
            if (IsFlinching && Facing(offset, flinchAim, FlinchArc))
            {
                flinchUntil = 0f;
                FeedMechanic(1);
                AbsorbVfx(at, SpecimenCatalog.Pale);
                return true;
            }
            if (Form != SpecimenForm.Behemoth || !(guarding || IsBulldozing)) return false;
            Vector2 aim = Player.AimDirection;
            if (Vector2.Dot(incoming, aim) > -0.1f || !Facing(offset, aim, GuardArcNow)) return false;
            StoreForce(1);
            FeedMechanic(1);
            AbsorbVfx(at, SpecimenCatalog.Amber);
            return true;
        }

        private static bool Facing(Vector2 offset, Vector2 aim, float arc) => offset.sqrMagnitude < 0.04f || Vector2.Angle(offset, aim) <= arc * 0.5f;

        private void AbsorbVfx(Vector2 at, Color color)
        {
            HeroVfx.Sparks(Root, at, color, 8, 3.5f, 0.25f, (Vector2)transform.position - at, 120f);
            HeroVfx.Pulse(Root, at, 0.4f, color, 0.2f);
        }

        /// <summary>Iron Skin: a hit is turned aside, and a shockwave goes back through the enemies around him.</summary>
        public bool TryIronSkin()
        {
            if (!HasIronSkin || Player.Health <= 0) return false;
            ironSkinCharges--;
            var run = Player.Run;
            Vector2 center = transform.position;
            float radius = 2.2f * Mathf.Max(1f, BodyScale * 0.8f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= radius + enemy.HitRadius)
                    HitAndShove(enemy, Player.Damage, center, 2f, true);
            if (Rank(PowerupType.Hardened) > 0) StoreForce(1);
            CombatVfx.Ring(Root, center, radius, SpecimenCatalog.Stone, 0.3f);
            HeroVfx.Pulse(Root, center, radius, FlameMesh.Alpha(SpecimenCatalog.Amber, 0.6f), 0.3f);
            CoopFx.Ring(run, center, radius, SpecimenCatalog.Stone, 0.3f);
            return true;
        }

        public void BeginIronSkin(float duration, int hits)
        {
            ironSkinUntil = Time.time + duration;
            ironSkinCharges = hits;
        }

        /// <summary>Rampaging, walking through enemies throws them aside.</summary>
        private void Trample()
        {
            Vector2 center = transform.position;
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                if (Vector2.Distance(center, enemy.transform.position) > Player.HitRadius + enemy.HitRadius + 0.1f) continue;
                if (Marked(trampled, enemy)) continue;
                trampled[enemy] = Time.time + 0.6f;
                HitAndShove(enemy, Player.Damage, center, 2.5f, true);
            }
        }

        // ---------------------------------------------------------------- Edge

        private bool EdgeAttack(Vector2 aim, float charge)
        {
            if (charge >= 1f) return Cyclone(aim);
            int damage = Player.Charge.Damage(charge);
            Lash(aim, damage, false);
            // The Edge's second stage: every lash cracks twice.
            if (IsGrown && LockedPath == SpecimenPath.Edge)
            {
                echoAt = Time.time + 0.12f;
                echoAim = aim;
                echoDamage = Mathf.Max(1, Mathf.CeilToInt(damage * 0.5f));
            }
            readyAt = Time.time + Interval(LashInterval);
            return true;
        }

        /// <summary>
        /// A lash: a long, narrow crack of the chain. Whatever the very tip (the sweet spot) catches takes a critical
        /// hit; Weighted Tip makes that spot longer and harder.
        /// </summary>
        private void Lash(Vector2 aim, int damage, bool echo)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            // The chain stops at the first wall, and its tip is wherever it stops.
            float reach = Mathf.Max(0.8f, Vector2.Distance(origin, PlayerAbilities.FindGroundLanding(run.Map, origin, aim, LashReachNow)));
            float sweet = SweetSpotNow;
            bool forced = !echo && ConsumeRollCrit();
            Breakable.SmashInCone(run, origin, aim, reach, LashCone);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                if (!SwordAttack.OverlapsCone(offset, aim, reach, LashCone, enemy.HitRadius) || !run.HasLineOfSight(origin, enemy.transform.position)) continue;
                bool tip = offset.magnitude + enemy.HitRadius >= reach - sweet;
                CombatDamage.Apply(Player, enemy, damage + (tip ? Rank(PowerupType.WeightedTip) : 0), DamageElement.Physical, origin, 0.4f,
                    guaranteedCrit: tip || forced);
            }
            Vector2 end = origin + aim * reach;
            SpecimenVfx.Lash(Root, transform, aim, reach, echo ? FlameMesh.Alpha(SpecimenCatalog.Steel, 0.6f) : SpecimenCatalog.Steel);
            CoopFx.Stab(run, origin, aim, reach, SpecimenCatalog.Steel);
            HeroVfx.Sparks(Root, end, SpecimenCatalog.Keen, echo ? 4 : 7, 3.5f, 0.2f, aim, 120f);
            // Overdrive: every lash also bursts out from its tip.
            if (IsOverdriven && !echo) TipBurst(end);
        }

        private void TipBurst(Vector2 at)
        {
            var run = Player.Run;
            const float radius = 1f;
            Breakable.SmashAt(run, at, radius);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= radius + enemy.HitRadius)
                    CombatDamage.Apply(Player, enemy, Player.Damage, DamageElement.Physical, at, 0.6f);
            HeroVfx.Pulse(Root, at, radius, SpecimenCatalog.Keen, 0.2f);
            CoopFx.Pulse(run, at, radius, SpecimenCatalog.Keen, 0.2f);
        }

        /// <summary>Chain Cyclone (a full charge): a full spin at the chain's whole reach; everything at the rim takes a tip crit.</summary>
        private bool Cyclone(Vector2 aim)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            float reach = LashReachNow, sweet = SweetSpotNow;
            int damage = Player.Charge.Damage(1f);
            bool forced = ConsumeRollCrit();
            Breakable.SmashInCone(run, origin, aim, reach, 360f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 at = enemy.transform.position;
                float distance = Vector2.Distance(origin, at);
                if (distance > reach + enemy.HitRadius || !run.HasLineOfSight(origin, at)) continue;
                bool tip = distance + enemy.HitRadius >= reach - sweet;
                CombatDamage.Apply(Player, enemy, damage + (tip ? Rank(PowerupType.WeightedTip) : 0), DamageElement.Physical, origin, 1f,
                    guaranteedCrit: tip || forced);
            }
            SpecimenVfx.Cyclone(Root, transform, aim, reach, SpecimenCatalog.Steel);
            CoopFx.Slash(run, origin, aim, reach, 360f, SpecimenCatalog.Steel);
            ScreenFx.Shake(0.08f, 0.12f);
            readyAt = Time.time + Interval(CycloneInterval);
            return true;
        }

        private bool FireHook(Vector2 aim)
        {
            if (hook != null) return false;
            float range = HookRange + (IsGrown && LockedPath == SpecimenPath.Edge ? 1f : 0f);
            hook = ChainHook.Fire(this, aim, range, IsGrown && LockedPath == SpecimenPath.Edge ? 2 : 1);
            skillReadyAt = Time.time + SpecialCooldownTime * Player.Powerups.SkillCooldownMultiplier;
            Player.Charge.Cancel();
            return true;
        }

        private AbilityType refund = AbilityType.None;
        /// <summary>Zipline: the ability that just cast should be ready again at once.</summary>
        public void RequestRefund(AbilityType type) => refund = type;
        /// <summary>Whether <paramref name="type"/> earned an instant refund as it was cast (and clears the request).</summary>
        public bool ConsumeRefund(AbilityType type)
        {
            if (refund != type) return false;
            refund = AbilityType.None;
            return true;
        }

        /// <summary>Called by his hook as it lands a catch: Reel In's landing blow.</summary>
        public int ReelInDamage => Rank(PowerupType.ReelIn) > 0 ? Player.Damage : 0;

        /// <summary>Light on His Feet: the next lash after a roll is a critical hit.</summary>
        public void OnRolled()
        {
            if (Form == SpecimenForm.Edge && Rank(PowerupType.LightOnHisFeet) > 0) rollCrit = true;
        }

        private bool ConsumeRollCrit()
        {
            if (!rollCrit) return false;
            rollCrit = false;
            return true;
        }

        // ---------------------------------------------------------------- marks (Heartbeat, Bind, Ankle Wrap)

        public void Expose(DungeonEnemy enemy, float duration) { if (enemy != null) exposed[enemy] = Time.time + duration; }
        public void Bind(DungeonEnemy enemy, float duration) { if (enemy != null) bound[enemy] = Time.time + duration; }
        public void Trip(DungeonEnemy enemy, float duration) { if (enemy != null) tripped[enemy] = Time.time + duration; }
        public bool IsBound(DungeonEnemy enemy) => Marked(bound, enemy);

        /// <summary>Exposed and bound enemies (and tripped ones, with Low Blow) take a critical hit from every blow.</summary>
        public bool ForcesCrit(DungeonEnemy enemy) => Marked(exposed, enemy) || Marked(bound, enemy)
            || (Rank(PowerupType.LowBlow) > 0 && Marked(tripped, enemy) && enemy.IsStunned);

        /// <summary>Bleeding Edge: his critical hits open a wound.</summary>
        public bool BleedsOnCritical => Rank(PowerupType.BleedingEdge) > 0;

        /// <summary>One of his hits was a critical hit: Bleeding Edge opens a wound, and the Edge's crits feed Breaking Point.</summary>
        public void OnCritical(DungeonEnemy enemy, int damage)
        {
            if (BleedsOnCritical && enemy != null && enemy.Health > 0) CombatDamage.InflictBleed(Player, enemy, damage);
            if (Form == SpecimenForm.Edge) FeedMechanic(1);
        }

        /// <summary>An enemy fell: Shackles passes a binding on, and Apex Mutation stretches an Overdrive.</summary>
        public void OnEnemyDied(DungeonEnemy enemy, bool localKill)
        {
            if (Player == null || enemy == null) return;
            if (localKill && IsOverdriven && Player.Permanent != null && Player.Permanent.MechanicUpgraded) overdriveUntil += OverdriveKillTime;
            if (bound.TryGetValue(enemy, out float until) && until > Time.time && Rank(PowerupType.Shackles) > 0)
            {
                bound.Remove(enemy);
                float left = until - Time.time;
                Vector2 from = enemy.transform.position;
                DungeonEnemy next = null;
                float best = 4f;
                foreach (var other in Player.Run.Enemies)
                {
                    if (other == null || other == enemy || other.Health <= 0 || other.IsInvulnerable) continue;
                    float distance = Vector2.Distance(from, other.transform.position);
                    if (distance <= best) { best = distance; next = other; }
                }
                if (next == null) return;
                next.Root(left);
                Bind(next, left);
                CombatVfx.Bolt(Root, from, next.transform.position, SpecimenCatalog.Steel, 0.06f, 0.3f);
                CoopFx.Bolt(Player.Run, from, next.transform.position, SpecimenCatalog.Steel);
            }
        }

        // ---------------------------------------------------------------- Breaking Point

        public bool IsBreaking => IsRampaging || IsOverdriven || IsSnapped;

        public void FeedMechanic(int amount)
        {
            if (!IsBreaking && Player != null && Player.Mechanic is BreakingPoint breaking) breaking.Feed(amount);
        }

        public void BeginRampage(float duration)
        {
            rampageUntil = Time.time + duration;
            rampageSlams = 0;
            roarPending = Player.Permanent != null && Player.Permanent.MechanicUpgraded;
            Hide();
            ApplyLook(true);
            SpecimenVfx.Transformation(Player.Run, transform, SpecimenForm.Behemoth, true);
            ScreenFx.Shake(0.3f, 0.4f);
        }

        /// <summary>Apex Mutation: the Rampage ends in a roar that draws every enemy to him and stuns everything nearby.</summary>
        private void Roar()
        {
            var run = Player.Run;
            roarUntil = Time.time + RoarTaunt;
            Vector2 center = transform.position;
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= RoarRadius + enemy.HitRadius) enemy.Stun(RoarStun);
            HeroVfx.Pulse(Root, center, RoarRadius, FlameMesh.Alpha(SpecimenCatalog.Amber, 0.6f), 0.5f);
            CombatVfx.Ring(Root, center, RoarRadius, SpecimenCatalog.Amber, 0.5f);
            CoopFx.Pulse(run, center, RoarRadius, SpecimenCatalog.Amber, 0.5f);
            ScreenFx.Flash(FlameMesh.Alpha(SpecimenCatalog.Amber, 0.25f), 0.35f);
            ScreenFx.Shake(0.3f, 0.45f);
        }

        public void BeginOverdrive(float duration)
        {
            overdriveUntil = Time.time + duration;
            HeroVfx.Pulse(Root, transform.position, 1.6f, SpecimenCatalog.Keen, 0.4f);
            HeroVfx.Motes(Root, transform.position, 0.9f, SpecimenCatalog.Keen, 16, 0.8f);
            CoopFx.Pulse(Player.Run, transform.position, 1.6f, SpecimenCatalog.Keen, 0.4f);
        }

        /// <summary>Breaking Point while frail: for a while he becomes the form he leans toward.</summary>
        public void BeginSnap(float duration)
        {
            snapPath = Lean;
            snapUntil = Time.time + duration;
            Hide();
            ApplyLook(true);
            SpecimenVfx.Transformation(Player.Run, transform, Form, false);
        }

        public void BeginFlight(float duration)
        {
            flightUntil = Time.time + duration;
            flightEdge = Form == SpecimenForm.Edge;
            Player.ResetDodge();
            if (Form == SpecimenForm.Behemoth) StoreForce(2);
        }

        // ---------------------------------------------------------------- previews

        private void LateUpdate()
        {
            if (preview == null) return;
            preview.Begin();
            if (Player != null && Player.Run.IsPlaying && Player.Health > 0)
            {
                Vector2 origin = transform.position, aim = Player.AimDirection;
                if (guarding || IsBulldozing) DrawGuard(origin, aim, SpecimenCatalog.Amber, GuardArcNow);
                else if (IsFlinching) DrawGuard(origin, flinchAim, SpecimenCatalog.Pale, FlinchArc);
                if (Player.Charge.IsCharging) DrawCharge(origin, aim, Player.Charge.Amount);
                if (Force > 0) DrawForce(origin);
                if (HasIronSkin) preview.Ring(origin, Player.HitRadius + 0.35f, 0.05f, FlameMesh.Alpha(SpecimenCatalog.Stone, 0.5f + 0.2f * Mathf.Sin(Time.time * 10f)), 28);
                foreach (var pair in bound)
                    if (pair.Key != null && pair.Key.Health > 0 && pair.Value > Time.time)
                        preview.Ring(pair.Key.transform.position, pair.Key.HitRadius + 0.15f, 0.05f, FlameMesh.Alpha(SpecimenCatalog.Steel, 0.8f), 18);
                foreach (var pair in exposed)
                    if (pair.Key != null && pair.Key.Health > 0 && pair.Value > Time.time)
                        preview.Ring(pair.Key.transform.position, pair.Key.HitRadius + 0.2f, 0.04f, FlameMesh.Alpha(SpecimenCatalog.Keen, 0.6f), 18);
            }
            preview.Commit();
        }

        /// <summary>His forearms crossed in front of him, and the arc they cover.</summary>
        private void DrawGuard(Vector2 origin, Vector2 aim, Color color, float arc)
        {
            float scale = BodyScale, reach = Player.HitRadius + 0.35f;
            Vector2 side = Vector2.Perpendicular(aim), center = origin + aim * reach;
            preview.Bar(center - side * 0.32f * scale + aim * 0.06f, (side + aim * 0.25f).normalized, 0.64f * scale, 0.12f * scale, FlameMesh.Alpha(color, 0.85f), FlameMesh.Alpha(color, 0.6f));
            preview.Bar(center + side * 0.32f * scale + aim * 0.06f, (-side + aim * 0.25f).normalized, 0.64f * scale, 0.12f * scale, FlameMesh.Alpha(color, 0.85f), FlameMesh.Alpha(color, 0.6f));
            float start = Mathf.Atan2(aim.y, aim.x) - arc * 0.5f * Mathf.Deg2Rad;
            const int Segments = 18;
            for (int i = 0; i < Segments; i++)
            {
                float a = start + arc * Mathf.Deg2Rad * i / Segments, b = start + arc * Mathf.Deg2Rad * (i + 1) / Segments;
                preview.Quad(origin + FlameMesh.Polar(a, reach), origin + FlameMesh.Polar(b, reach), origin + FlameMesh.Polar(b, reach + 0.08f),
                    origin + FlameMesh.Polar(a, reach + 0.08f), FlameMesh.Alpha(color, 0.35f), FlameMesh.Alpha(color, 0.35f), FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0f));
            }
        }

        private void DrawCharge(Vector2 origin, Vector2 aim, float charge)
        {
            if (Form == SpecimenForm.Edge)
            {
                float reach = LashReachNow, sweet = SweetSpotNow;
                if (charge >= 1f) { preview.Ring(origin, reach, 0.05f, FlameMesh.Alpha(SpecimenCatalog.Steel, 0.6f), 48); preview.Ring(origin, reach - sweet * 0.5f, sweet, FlameMesh.Alpha(SpecimenCatalog.Keen, 0.15f), 48); return; }
                preview.Bar(origin, aim, reach - sweet, 0.05f, FlameMesh.Alpha(SpecimenCatalog.Steel, 0.15f + 0.3f * charge), FlameMesh.Alpha(SpecimenCatalog.Steel, 0.35f));
                preview.Bar(origin + aim * (reach - sweet), aim, sweet, 0.09f, FlameMesh.Alpha(SpecimenCatalog.Keen, 0.6f), FlameMesh.Alpha(SpecimenCatalog.Keen, 0.9f));
                return;
            }
            if (Form == SpecimenForm.Behemoth && !IsRampaging)
            {
                float length = AxeLength * ReachScale;
                Color color = FlameMesh.Alpha(Color.Lerp(SpecimenCatalog.Stone, SpecimenCatalog.Amber, charge), 0.15f + 0.35f * charge);
                preview.Bar(origin, aim, length * Mathf.Max(0.25f, charge), AxeWidth * 0.5f, color, FlameMesh.Alpha(color, 0.05f));
                return;
            }
            float cone = (charge >= 1f ? KickCone : PalmCone) * Mathf.Deg2Rad, range = charge >= 1f ? KickReach : PalmReach;
            Color fill = FlameMesh.Alpha(SpecimenCatalog.Pale, 0.1f + 0.2f * charge);
            float from = Mathf.Atan2(aim.y, aim.x) - cone * 0.5f;
            const int Steps = 12;
            for (int i = 0; i < Steps; i++)
                preview.Triangle(origin, origin + FlameMesh.Polar(from + cone * i / Steps, range), origin + FlameMesh.Polar(from + cone * (i + 1) / Steps, range), fill, fill, fill);
        }

        /// <summary>Stored Force glows as amber motes circling his head.</summary>
        private void DrawForce(Vector2 origin)
        {
            float radius = 0.35f * BodyScale, height = 0.75f * BodyScale;
            for (int i = 0; i < Force; i++)
            {
                float angle = Time.time * 2.5f + i * Mathf.PI * 2f / Mathf.Max(1, Force);
                Vector2 at = origin + Vector2.up * height + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.35f);
                preview.Disc(at, 0.07f, FlameMesh.Alpha(FlameMesh.Core, 0.95f), FlameMesh.Alpha(SpecimenCatalog.Amber, 0.3f), 10);
            }
        }
    }
}

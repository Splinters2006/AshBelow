using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Samurai's katana. Tapping cuts a quick crescent; holding to a full charge looses a flurry of rapid, weak
    /// slashes. Right click dashes forward and slices everything in her path. Her boss artifacts are cast from here,
    /// and most of what she does opens wounds: see <see cref="DungeonEnemy.Bleed"/>.
    /// </summary>
    public sealed class SamuraiAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float Reach = 2.3f, SlashCone = 100f, SlashInterval = 0.32f, ChargeDuration = 1f;
        public const int FlurrySlashes = 6;
        public const float FlurryReach = 2.5f, FlurryCone = 80f, FlurryInterval = 0.07f, FlurryDamageShare = 0.5f;
        public const float DashCooldown = 5f, DashRange = 5.5f, DashMinimum = 1.5f, DashHalfWidth = 0.9f;
        public static readonly Color Blood = DungeonEnemy.BleedColor, Steel = new Color(0.93f, 0.95f, 1f);
        public DungeonPlayer Player { get; set; }
        private float readyAt, dashReadyAt;
        private bool backhand;
        private Coroutine flurry;
        private GameObject previewObject;
        private FlameMesh preview;

        public bool IsFlurrying => flurry != null;
        // The flurry roots her like other heavy attacks: slower movement and no new charge.
        public bool IsHeavyAttacking => IsFlurrying;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, dashReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => dashReadyAt = Cooldowns.Shorten(dashReadyAt, seconds);
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && !Player.IsBusy && !IsFlurrying && Time.time >= readyAt;
        private float Size => Player.Buffs.AttackSizeMultiplier;
        private float Interval => Player.Powerups.AttackIntervalMultiplier * Player.Buffs.AttackIntervalMultiplier * SwiftIntervalMultiplier;

        private void Start()
        {
            previewObject = new GameObject("Katana arc");
            preview = new FlameMesh(previewObject, 5);
        }

        private void OnDestroy()
        {
            preview?.Release();
            if (previewObject != null) Destroy(previewObject);
        }

        // ---------------------------------------------------------------- the katana

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            aim.Normalize();
            if (charge >= 1f)
            {
                flurry = StartCoroutine(Flurry());
                return true;
            }
            if (IsTechniqueActive) return TechniqueStrike(aim);
            Cut(aim, Player.Damage, Reach * Size, SlashCone, 1f, Steel);
            readyAt = Time.time + SlashInterval * Interval;
            return true;
        }

        /// <summary>One crescent cut: strikes everything in the cone and draws the blade's arc, alternating forehand and backhand.</summary>
        private int Cut(Vector2 aim, int damage, float reach, float cone, float knockback, Color color, float duration = 0.3f, List<DungeonEnemy> struck = null)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            int hits = Sweep(origin, aim, damage, reach, cone, knockback, struck);
            KatanaVfx.Crescent(run.ProjectileRoot, origin, aim, reach, cone, color, backhand, duration);
            CoopFx.KatanaCrescent(run, origin, aim, reach, cone, color, backhand, duration);
            backhand = !backhand;
            return hits;
        }

        /// <summary>Hits every enemy whose body reaches into the cone; returns how many, and lists them in <paramref name="struck"/>.</summary>
        private int Sweep(Vector2 origin, Vector2 aim, int damage, float reach, float cone, float knockback, List<DungeonEnemy> struck = null)
        {
            var run = Player.Run;
            int hits = 0;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 at = enemy.transform.position;
                if (!SwordAttack.OverlapsCone(at - origin, aim, reach, cone, enemy.HitRadius) || !run.HasLineOfSight(origin, at)) continue;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, origin, knockback);
                struck?.Add(enemy);
                hits++;
            }
            return hits;
        }

        /// <summary>A full charge: a string of rapid slashes, each only worth half a cut.</summary>
        private IEnumerator Flurry()
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            float step = FlurryInterval * Interval;
            ScreenFx.Shake(0.05f, FlurrySlashes * step);
            for (int i = 0; i < FlurrySlashes; i++)
            {
                if (!run.IsPlaying || Player.Health <= 0 || root != run.ProjectileRoot) break;
                // Each slash comes in at its own angle, so the flurry reads as a blur of crossing cuts.
                Vector2 aim = Quaternion.Euler(0, 0, Random.Range(-14f, 14f)) * Player.AimDirection;
                Cut(aim, Mathf.Max(1, Mathf.RoundToInt(Player.Damage * FlurryDamageShare)), FlurryReach * Size, FlurryCone, 0.15f,
                    Color.Lerp(Steel, Blood, 0.35f), 0.12f);
                yield return new WaitForSeconds(step);
            }
            flurry = null;
            readyAt = Time.time + 0.25f * Interval;
        }

        private void StopFlurry()
        {
            if (flurry == null) return;
            StopCoroutine(flurry);
            flurry = null;
            readyAt = Time.time + 0.25f * Interval;
        }

        // ---------------------------------------------------------------- dash slash (right click)

        /// <summary>Dashes toward the cursor and cuts everything she passes with one clean slice.</summary>
        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || IsFlurrying || HeavyCooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            var run = Player.Run;
            var root = run.ProjectileRoot;
            float distance = Mathf.Clamp(aim.magnitude, DashMinimum, DashRange);
            aim.Normalize();
            Player.Charge.Cancel();
            Vector2 from = transform.position;
            var cut = new List<DungeonEnemy>();
            int steps = Mathf.CeilToInt(distance / 0.15f);
            for (int i = 0; i < steps; i++)
            {
                transform.position = run.Map.Move(transform.position, aim * (distance / steps));
                foreach (var enemy in run.Enemies)
                    if (enemy != null && enemy.Health > 0 && !cut.Contains(enemy)
                        && Vector2.Distance(transform.position, enemy.transform.position) <= DashHalfWidth * Size + enemy.HitRadius) cut.Add(enemy);
            }
            Vector2 to = transform.position;
            Player.Protect(0.35f);
            // The swath the dash cut (as wide as it hits), dust kicked up where she left, and the follow-through of the
            // blade round the front of where she stops.
            KatanaVfx.Slice(root, from, to, Blood, 0.34f, DashHalfWidth * Size);
            CombatVfx.Ring(root, from, 0.7f, Steel, 0.2f);
            KatanaVfx.Crescent(root, to, aim, DashHalfWidth * Size, 180f, Steel, backhand, 0.24f);
            CoopFx.KatanaSlice(run, from, to, Blood, 0.34f, DashHalfWidth * Size);
            CoopFx.Ring(run, from, 0.7f, Steel, 0.2f);
            CoopFx.KatanaCrescent(run, to, aim, DashHalfWidth * Size, 180f, Steel, backhand, 0.24f);
            HeroVfx.Sparks(root, to, Steel, 14, 5.5f, 0.3f, to - from, 100f);
            int damage = Player.Damage * 2;
            foreach (var enemy in cut)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 at = enemy.transform.position;
                KatanaVfx.Slice(root, at - aim * (enemy.HitRadius + 0.6f), at + aim * (enemy.HitRadius + 0.6f), Blood, 0.3f);
                CoopFx.KatanaSlice(run, at - aim * (enemy.HitRadius + 0.6f), at + aim * (enemy.HitRadius + 0.6f), Blood, 0.3f);
                HeroVfx.Sparks(root, at, Blood, 12, 5.5f, 0.3f, Vector2.Perpendicular(aim), 70f);
                HeroVfx.Sparks(root, at, Blood, 12, 5.5f, 0.3f, -Vector2.Perpendicular(aim), 70f);
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, at - aim, 0.2f);
            }
            ScreenFx.Shake(cut.Count > 0 ? 0.2f + 0.03f * Mathf.Min(cut.Count, 5) : 0.07f, cut.Count > 0 ? 0.16f : 0.08f);
            if (cut.Count > 0) ScreenFx.Flash(new Color(1f, 1f, 1f, 0.12f), 0.08f);
            dashReadyAt = Time.time + DashCooldown * Player.Powerups.SkillCooldownMultiplier;
            readyAt = Mathf.Max(readyAt, Time.time + 0.15f);
            return true;
        }

        // ---------------------------------------------------------------- boss artifacts

        public const float ComboWindow = 2.5f, ComboGap = 0.15f;
        public const float TechniqueTime = 10f, ThrustReach = 3.3f, ThrustHalfWidth = 0.45f, ThrustMultiplier = 1.5f, TechniqueMultiplier = 1.5f;
        public const float SwiftAttackSpeed = 0.5f;
        /// <summary>Slice, Dice and Chunk: each slash's reach, cone and damage (in multiples of the hero's damage).</summary>
        public static readonly float[] ComboReach = { 2.4f, 3.1f, 3.9f }, ComboCone = { 90f, 125f, 165f };
        public static readonly int[] ComboDamage = { 2, 3, 5 };
        /// <summary>Fine Dicing's extra Slice Dice Chunk damage, by rank.</summary>
        public static readonly float[] FineDicingBonus = { 0f, 0.1f, 0.3f, 0.5f };
        /// <summary>By ability rank: how far Bloodscent resets wounds, how hard Bloodpop pops them, and how long Blood Shall Flow and Swift as the Wind last.</summary>
        public static readonly float[] BloodscentScale = { 1f, 1.25f, 1.5f }, BloodpopScale = { 1f, 1.15f, 1.25f },
            BloodFlowTime = { 5f, 7.5f, 10f }, SwiftTime = { 5f, 10f, 15f };
        private int comboStage, techniqueStep;
        private float comboUntil, comboNextAt, techniqueUntil, lastTechniqueAt, bloodFlowUntil, swiftUntil, nextMote;

        /// <summary>Blood Shall Flow: every katana hit opens a wound.</summary>
        public bool IsBloodFlowing => Time.time < bloodFlowUntil;
        public bool IsTechniqueActive => Time.time < techniqueUntil;
        public bool IsSwift => Time.time < swiftUntil;
        /// <summary>Swift as the Wind: what her attack and charge times are multiplied by.</summary>
        public float SwiftIntervalMultiplier => IsSwift ? 1f / (1f + SwiftAttackSpeed) : 1f;
        /// <summary>Slice or Dice has just hit, so the ability's key throws the next slash.</summary>
        public bool CanContinueCombo => comboStage > 0 && Time.time < comboUntil && Time.time >= comboNextAt && !Player.IsRolling && !Player.IsBusy;

        public bool CastArtifact(AbilityType type, Vector2 aim, int rank)
        {
            if (Player == null || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || aim.sqrMagnitude < 0.001f) return false;
            rank = Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank);
            aim.Normalize();
            var root = Player.Run.ProjectileRoot;
            switch (type)
            {
                case AbilityType.SliceDiceChunk:
                    comboStage = 0;
                    ComboSlash(aim, rank);
                    return true;
                case AbilityType.Bloodscent: return Bloodscent(rank);
                case AbilityType.BloodShallFlow:
                    bloodFlowUntil = Time.time + BloodFlowTime[rank - 1];
                    HeroVfx.Motes(root, transform.position, 0.8f, Blood, 16, 0.9f);
                    return true;
                case AbilityType.MaestrosTechnique:
                    techniqueUntil = Time.time + TechniqueTime + 2f * (rank - 1);
                    techniqueStep = -1;
                    lastTechniqueAt = 0f;
                    return true;
                case AbilityType.SwiftAsTheWind:
                    swiftUntil = Time.time + SwiftTime[rank - 1];
                    WindstepVfx.Play(root, (Vector2)transform.position - aim * 1.2f, transform.position);
                    CoopFx.Windstep(Player.Run, (Vector2)transform.position - aim * 1.2f, transform.position);
                    return true;
                case AbilityType.Bloodpop: return Bloodpop(rank);
                default: return false;
            }
        }

        /// <summary>The next slash of Slice Dice Chunk, cast again from its key.</summary>
        public bool ContinueCombo(Vector2 aim, int rank)
        {
            if (!CanContinueCombo || aim.sqrMagnitude < 0.001f) return false;
            Player.Charge.Cancel();
            StopFlurry();
            ComboSlash(aim.normalized, Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank));
            return true;
        }

        /// <summary>Slice, then Dice, then Chunk: each bigger than the last, each only open if the one before it hit. Chunk opens wounds.</summary>
        private void ComboSlash(Vector2 aim, int rank)
        {
            int stage = Mathf.Clamp(comboStage, 0, 2);
            bool chunk = stage == 2;
            float bonus = 1f + FineDicingBonus[Mathf.Clamp(Player.Powerups.Count(PowerupType.FineDicing), 0, FineDicingBonus.Length - 1)];
            int damage = Mathf.Max(1, Mathf.RoundToInt((Player.Damage * ComboDamage[stage] + (rank - 1) * (stage + 1)) * bonus));
            var struck = new List<DungeonEnemy>();
            int hits = Cut(aim, damage, ComboReach[stage] * Size, ComboCone[stage], chunk ? 2f : 0.6f, chunk ? Blood : Steel, chunk ? 0.45f : 0.3f, struck);
            // Each slash has its own look over the blade's arc: a ruled line, a lattice of cuts, a ground-splitting cleave.
            var root = Player.Run.ProjectileRoot;
            SliceDiceChunkVfx.Play(root, transform.position, aim, ComboReach[stage] * Size, ComboCone[stage], stage);
            CoopFx.SliceDiceChunk(Player.Run, transform.position, aim, ComboReach[stage] * Size, ComboCone[stage], stage);
            if (chunk)
            {
                foreach (var enemy in struck) CombatDamage.InflictBleed(Player, enemy, damage);
                if (hits > 0) HeroVfx.Sparks(root, (Vector2)transform.position + aim * ComboReach[stage] * Size * 0.6f, Blood, 18, 6f, 0.4f, aim, 110f, 1.3f);
                ScreenFx.Shake(0.25f, 0.2f);
            }
            else if (stage == 1) ScreenFx.Shake(0.1f, 0.12f);
            bool open = hits > 0 && !chunk;
            comboStage = open ? stage + 1 : 0;
            comboUntil = open ? Time.time + ComboWindow : 0f;
            comboNextAt = Time.time + ComboGap;
            readyAt = Mathf.Max(readyAt, Time.time + 0.2f);
        }

        /// <summary>Maestro's Technique: sweep, sweep, thrust, every hit half as hard again. The thrust hits harder still and opens a wound.</summary>
        private bool TechniqueStrike(Vector2 aim)
        {
            techniqueStep = Time.time - lastTechniqueAt > 1.2f ? 0 : (techniqueStep + 1) % 3;
            lastTechniqueAt = Time.time;
            if (techniqueStep < 2)
            {
                Cut(aim, Mathf.Max(1, Mathf.RoundToInt(Player.Damage * TechniqueMultiplier)), (Reach + 0.2f) * Size, 130f, 0.8f, Steel);
                readyAt = Time.time + SlashInterval * 0.85f * Interval;
                return true;
            }
            var run = Player.Run;
            Vector2 origin = transform.position;
            float length = ThrustReach * Size, halfWidth = ThrustHalfWidth * Size;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Player.Damage * TechniqueMultiplier * ThrustMultiplier));
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0
                    || !BrawlerAttack.InRectangle((Vector2)enemy.transform.position - origin, aim, length, halfWidth, enemy.HitRadius)
                    || !run.HasLineOfSight(origin, enemy.transform.position)) continue;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, origin, 1.6f);
                CombatDamage.InflictBleed(Player, enemy, damage);
            }
            KatanaVfx.Thrust(run.ProjectileRoot, origin, aim, length, halfWidth, Blood);
            CoopFx.KatanaThrust(run, origin, aim, length, halfWidth, Blood);
            ScreenFx.Shake(0.08f, 0.1f);
            readyAt = Time.time + SlashInterval * 1.4f * Interval;
            return true;
        }

        /// <summary>Every bleeding enemy's wounds start over. False (and no cooldown) when nothing is bleeding.</summary>
        private bool Bloodscent(int rank)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            int found = 0;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || !enemy.IsBleeding) continue;
                enemy.RefreshBleed(BloodscentScale[rank - 1]);
                Vector2 at = enemy.transform.position;
                BloodscentVfx.Play(run.ProjectileRoot, origin, at, enemy.HitRadius);
                CoopFx.Bloodscent(run, origin, at, enemy.HitRadius);
                found++;
            }
            if (found == 0) return false;
            BloodscentVfx.Sniff(run.ProjectileRoot, origin);
            CoopFx.Bloodscent(run, origin, origin, 0f);
            ScreenFx.Flash(FlameMesh.Alpha(Blood, 0.1f), 0.15f);
            return true;
        }

        /// <summary>Every bleeding enemy takes all the bleed damage it had left, at once. False (and no cooldown) when there is nothing to pop.</summary>
        private bool Bloodpop(int rank)
        {
            var run = Player.Run;
            int popped = 0;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || enemy.BleedRemaining <= 0f) continue;
                int damage = Mathf.RoundToInt(enemy.ConsumeBleed() * BloodpopScale[rank - 1]);
                Vector2 at = enemy.transform.position;
                BloodpopVfx.Play(run.ProjectileRoot, at, enemy.HitRadius);
                CoopFx.Bloodpop(run, at, enemy.HitRadius);
                if (damage > 0) enemy.Hit(damage, at, 0f);
                popped++;
            }
            if (popped == 0) return false;
            ScreenFx.Flash(FlameMesh.Alpha(Blood, 0.16f), 0.2f);
            ScreenFx.Shake(0.1f + 0.03f * Mathf.Min(popped, 6), 0.2f);
            return true;
        }

        // ---------------------------------------------------------------- Crimson Bloom (passive)

        public const float BloomRadius = 2.5f, BloomRadiusPerHealth = 0.025f, BloomMaxRadius = 5f, BloomDelay = 0.12f;

        /// <summary>How far a bleeding enemy's death bursts: 2.5 units, wider the more maximum health it had.</summary>
        public static float BloomRadiusFor(int maxHealth) => Mathf.Min(BloomMaxRadius, BloomRadius + Mathf.Max(0, maxHealth) * BloomRadiusPerHealth);

        /// <summary>An enemy died on this machine: with Crimson Bloom, one that died bleeding bursts with the bleed damage it had left.</summary>
        public void OnEnemyDied(DungeonEnemy enemy)
        {
            if (Player == null || Player.Permanent == null || !Player.Permanent.HasPassive(WeaponType.Katana)) return;
            int damage = Mathf.CeilToInt(enemy.BleedRemaining - 0.0001f);
            if (damage <= 0) return;
            StartCoroutine(Bloom(enemy.transform.position, BloomRadiusFor(enemy.PeakHealth), damage));
        }

        // A beat after the death, so a chain of blooms ripples outward rather than landing all at once.
        private IEnumerator Bloom(Vector2 center, float radius, int damage)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            yield return new WaitForSeconds(BloomDelay);
            if (!run.IsPlaying || root != run.ProjectileRoot) yield break;
            HeroVfx.Pulse(root, center, radius, Blood, 0.35f);
            CombatVfx.Ring(root, center, radius, Blood, 0.35f);
            HeroVfx.Sparks(root, center, Blood, 16, 5.5f, 0.4f);
            CoopFx.Pulse(run, center, radius, Blood, 0.35f);
            CoopFx.Ring(run, center, radius, Blood, 0.35f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= radius + enemy.HitRadius)
                    enemy.Hit(damage, center, 0.6f);
        }

        // ---------------------------------------------------------------- previews

        public void Hide()
        {
            StopFlurry();
            Player.Charge.Cancel();
        }

        private void Update()
        {
            if (Player == null || !Player.Run.IsPlaying || Time.time < nextMote || !(IsBloodFlowing || IsTechniqueActive || IsSwift)) return;
            nextMote = Time.time + 0.12f;
            HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.5f, IsBloodFlowing ? Blood : IsSwift ? new Color(0.8f, 0.95f, 0.9f) : Steel, 2, 0.5f);
        }

        private void LateUpdate()
        {
            if (preview == null) return;
            preview.Begin();
            if (Player != null && Player.Run.IsPlaying && Player.Health > 0 && Player.Charge.IsCharging && !Player.IsRolling && !IsFlurrying)
            {
                // The cut's cone, reddening and stretching toward the flurry's as the charge fills.
                float charge = Player.Charge.Amount;
                float reach = Mathf.Lerp(Reach, FlurryReach, charge) * Size, cone = Mathf.Lerp(SlashCone, FlurryCone, charge) * Mathf.Deg2Rad;
                Vector2 origin = transform.position, aim = Player.AimDirection;
                Color fill = FlameMesh.Alpha(Color.Lerp(Steel, Blood, charge), charge >= 1f ? 0.26f + 0.08f * Mathf.Sin(Time.time * 14f) : 0.08f + 0.14f * charge);
                float start = Mathf.Atan2(aim.y, aim.x) - cone * 0.5f;
                const int Segments = 20;
                for (int i = 0; i < Segments; i++)
                    preview.Triangle(origin, origin + FlameMesh.Polar(start + cone * i / Segments, reach),
                        origin + FlameMesh.Polar(start + cone * (i + 1) / Segments, reach), fill, fill, fill);
            }
            preview.Commit();
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Demoness's pointed tail. Tapping stabs the nearest enemy in a narrow lane; a full charge strikes its vitals
    /// and paralyses it. RMB sweeps the tail through a half circle, hitting immobilized enemies (paralysed, frozen,
    /// stunned or rooted, whoever held them) twice as hard. Her boss
    /// artifacts (Archdemon's Technique, HEEEELP and Demon Curse) are cast from here.
    /// </summary>
    public sealed partial class DemonessAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float ChargeDuration = 1.1f;
        public const float StabReach = 1.9f, VitalReach = 2.4f, StabHalfWidth = 0.3f;
        public const float VitalParalysis = 1.5f;
        public const float SweepRadius = 2.8f, SweepCone = 180f, SweepCooldown = 5f;
        public const int SweepDamage = 2, SweepParalyzedMultiplier = 2;
        /// <summary>Archdemon's Technique: a fully charged tail whip strikes a small cone.</summary>
        public const float WhipRadius = 2.8f, WhipCone = 70f;
        public const float AscendDuration = 8f;
        public const float PortalRange = 8f, PortalWindup = 0.65f, PawRadius = 1.6f, PawStun = 0.6f;
        // The pentagram is 25% wider than it used to be (2.2 → 2.75, and each rank adds 25% more than before).
        public const float CurseRange = 7f, CurseRadius = 2.75f, CurseRankGrowth = 0.3125f, CurseWindup = 0.35f, CurseParalysis = 3f, CurseDuration = 6f;
        public static readonly Color Violet = new Color(0.66f, 0.3f, 1f);
        public static readonly Color Abyss = new Color(0.07f, 0.02f, 0.12f);
        public static readonly Color Pale = new Color(0.96f, 0.92f, 1f);
        public DungeonPlayer Player { get; set; }
        public bool IsHeavyAttacking => false;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, sweepReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => sweepReadyAt = Cooldowns.Shorten(sweepReadyAt, seconds);
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && !Player.IsBusy && Time.time >= readyAt;
        /// <summary>Nerve Strike and Blood Pact: extra seconds on every paralysis she inflicts.</summary>
        public float ParalysisBonus => Player.Powerups.Count(PowerupType.NerveStrike) * 0.25f
            + (Player.Powerups.Count(PowerupType.BloodPact) > 0 ? 0.5f : 0f);
        /// <summary>How long a vital stab (or Archdemon's tail whip) holds its victims.</summary>
        public float ParalysisDuration => VitalParalysis + ParalysisBonus;
        /// <summary>Pressure Points: extra damage on every hit against an already immobilized enemy.</summary>
        public int ParalyzedBonusDamage => Player.Permanent.ParalyzedDamage + Player.Powerups.Count(PowerupType.CruelTouch);
        public float SweepReach => SweepRadius + Player.Powerups.Count(PowerupType.LongTail) * 0.3f;
        private float Interval => Player.Powerups.AttackIntervalMultiplier * Player.Buffs.AttackIntervalMultiplier;
        private Color TailColor => Player.Buffs.IsAscended ? HeroBuffs.AscendColor : Violet;
        private float readyAt, sweepReadyAt;
        private Transform previewRoot;
        private SpriteRenderer preview;

        /// <summary>The flicker of a paralysed enemy: pale lilac pulsing into violet.</summary>
        public static Color ParalyzedTint(float time) => Color.Lerp(new Color(0.88f, 0.8f, 1f), Violet, 0.5f + 0.5f * Mathf.Sin(time * 16f));

        private void Awake()
        {
            previewRoot = new GameObject("Tail preview").transform;
            previewRoot.SetParent(transform, false);
            // The hero sprite is scaled; keep the preview in world units so it matches the hit lane.
            previewRoot.localScale = new Vector3(1 / transform.lossyScale.x, 1 / transform.lossyScale.y, 1);
            preview = DungeonVisuals.Create("Tail reach", previewRoot, transform.position, Vector2.one, Violet, 5);
            preview.enabled = false;
        }

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            aim.Normalize();
            bool ascended = Player.Buffs.IsAscended;
            if (ascended && charge >= 1f)
            {
                TailWhip(aim);
                readyAt = Time.time + 0.5f * Interval;
                return true;
            }
            // Under Archdemon's Technique every click strikes as if fully charged.
            if (ascended || charge >= 1f) Stab(aim, VitalReach, Player.Charge.Damage(1f), VitalParalysis);
            else Stab(aim, Mathf.Lerp(StabReach, VitalReach, charge), Player.Charge.Damage(charge), 0f);
            readyAt = Time.time + 0.35f * Interval;
            return true;
        }

        /// <summary>Stabs the nearest enemy in the lane ahead; a vital stab (<paramref name="paralysis"/> above 0) also paralyses it.</summary>
        private void Stab(Vector2 aim, float reach, int damage, float paralysis)
        {
            Vector2 origin = transform.position;
            DungeonEnemy victim = null;
            float nearest = float.MaxValue;
            foreach (var enemy in Player.Run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                if (!BrawlerAttack.InRectangle(offset, aim, reach, StabHalfWidth, enemy.HitRadius)
                    || !Player.Run.HasLineOfSight(origin, enemy.transform.position)) continue;
                float along = Vector2.Dot(offset, aim);
                if (along < nearest) { nearest = along; victim = enemy; }
            }
            bool vital = paralysis > 0f;
            Color color = vital ? Color.Lerp(TailColor, Pale, 0.25f) : TailColor;
            // The tail stops in the victim rather than passing through it.
            float length = victim != null ? Mathf.Clamp(nearest, 0.6f, reach) : reach;
            TailVfx.Stab(Player.Run.ProjectileRoot, origin, aim, length, color);
            CoopFx.TailStab(Player.Run, origin, aim, length, color);
            if (victim == null) return;
            Vector2 hitPoint = victim.transform.position;
            CombatDamage.Apply(Player, victim, WithPressurePoints(victim, damage) + (vital ? TormentBonus(victim) : 0), DamageElement.Physical, origin, vital ? 0.2f : 0.5f);
            if (!vital || victim == null) return;
            ParalyzeCounted(victim, paralysis);
            var root = Player.Run.ProjectileRoot;
            HeroVfx.Sparks(root, hitPoint, Pale, 10, 4f, 0.3f, aim, 80f, 0.9f);
            HeroVfx.Pulse(root, hitPoint, 0.6f, Violet, 0.3f);
            ScreenFx.Shake(0.06f, 0.08f);
        }

        /// <summary>Archdemon's fully charged tail whip: every enemy in a small cone is struck hard and paralysed.</summary>
        private void TailWhip(Vector2 aim)
        {
            Vector2 origin = transform.position;
            int rank = Mathf.Max(1, Player.Abilities.Rank(AbilityType.ArchdemonTechnique));
            int damage = Player.Damage * (4 + rank);
            var root = Player.Run.ProjectileRoot;
            Color color = HeroBuffs.AscendColor;
            TailVfx.Sweep(root, origin, aim, WhipRadius, WhipCone, color);
            CoopFx.TailSweep(Player.Run, origin, aim, WhipRadius, WhipCone, color);
            HeroVfx.Pulse(root, origin + aim * WhipRadius * 0.6f, 1.2f, color, 0.35f);
            ScreenFx.Shake(0.2f, 0.2f);
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                if (!InCone(enemy, origin, aim, WhipRadius, WhipCone)) continue;
                CombatDamage.Apply(Player, enemy, WithPressurePoints(enemy, damage), DamageElement.Physical, origin, 1.2f);
                ParalyzeCounted(enemy, VitalParalysis);
                // Infernal Technique: the whips set enemies burning.
                if (enemy != null && enemy.Health > 0 && Player.Powerups.Count(PowerupType.InfernalTechnique) > 0)
                    enemy.Burn(CombatDamage.BurnTicks, CombatDamage.BurnTickDamage(damage));
                HeroVfx.Sparks(root, enemy.transform.position, Pale, 8, 4.5f, 0.3f, aim, 100f);
            }
        }

        /// <summary>RMB: the tail sweeps a half circle ahead. Immobilized enemies take double damage.</summary>
        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || HeavyCooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            aim.Normalize();
            Vector2 origin = transform.position;
            float reach = SweepReach;
            var root = Player.Run.ProjectileRoot;
            TailVfx.Sweep(root, origin, aim, reach, SweepCone, TailColor);
            CoopFx.TailSweep(Player.Run, origin, aim, reach, SweepCone, TailColor);
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                if (!InCone(enemy, origin, aim, reach, SweepCone)) continue;
                bool paralyzed = enemy.IsImmobilized;
                int damage = Player.Damage * SweepDamage * (paralyzed ? SweepParalyzedMultiplier : 1);
                if (paralyzed)
                {
                    HeroVfx.Slash(root, enemy.transform.position, aim, 0.8f, 90f, Pale, 0.18f);
                    HeroVfx.Sparks(root, enemy.transform.position, Violet, 10, 4.5f, 0.35f);
                }
                CombatDamage.Apply(Player, enemy, WithPressurePoints(enemy, damage), DamageElement.Physical, origin, paralyzed ? 0.3f : 1f);
            }
            sweepReadyAt = Time.time + SweepCooldown * Player.Powerups.SkillCooldownMultiplier;
            Player.Charge.Cancel();
            return true;
        }

        /// <summary>
        /// Paralyses the enemy for <paramref name="duration"/> plus her paralysis bonus; every paralysis that takes hold
        /// charges Demonic Power, and so does a paralysing hit that killed its target outright. (Her freezes, stuns and
        /// roots charge it from <see cref="DungeonEnemy"/>.)
        /// </summary>
        private void ParalyzeCounted(DungeonEnemy enemy, float duration)
        {
            if (enemy.Health <= 0 || enemy.Paralyze(duration + ParalysisBonus, Player.Powerups.Count(PowerupType.LingeringTerror) > 0)) Player.Mechanic?.OnImmobilized();
        }

        private int WithPressurePoints(DungeonEnemy enemy, int damage) => enemy.IsImmobilized ? damage + ParalyzedBonusDamage : damage;

        private bool InCone(DungeonEnemy enemy, Vector2 origin, Vector2 aim, float reach, float cone)
        {
            if (enemy == null || enemy.Health <= 0) return false;
            Vector2 offset = (Vector2)enemy.transform.position - origin;
            // Enemies overlapping the hero count whatever their angle.
            if (offset.magnitude > enemy.HitRadius && !SwordAttack.ContainsTarget(offset, aim, reach + enemy.HitRadius, cone)) return false;
            return Player.Run.HasLineOfSight(origin, enemy.transform.position);
        }

        /// <param name="cursorDistance">How far away the cursor is; Demon Curse lands there when it is in range.</param>
        public bool CastArtifact(AbilityType type, Vector2 aim, int rank, float cursorDistance)
        {
            if (Player == null || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || aim.sqrMagnitude < 0.001f) return false;
            rank = Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank);
            aim.Normalize();
            switch (type)
            {
                case AbilityType.ArchdemonTechnique: Ascend(rank); return true;
                case AbilityType.DemonPaw:
                    var target = FindPortalTarget(aim);
                    if (target == null) return false;
                    StartCoroutine(PawSlam(target, rank));
                    return true;
                case AbilityType.DemonCurse:
                    StartCoroutine(CurseMark(CursePoint(Player.Run.Map, transform.position, aim, cursorDistance), rank));
                    return true;
                case AbilityType.WingDash: StartCoroutine(WingDash(aim, WingDashDistance + (rank - 1) * 0.5f)); return true;
                case AbilityType.SoulSiphon: StartCoroutine(SoulSiphon(SiphonTime + (rank - 1) * 1f)); return true;
                case AbilityType.NightmareSnap: return NightmareSnap(rank);
                default: return false;
            }
        }

        /// <summary>
        /// Where Demon Curse brands its pentagram: at the cursor (within range), even past walls. Only the centre needs open
        /// floor; over a wall it slides back toward the Demoness to the nearest open spot, or lands at her feet.
        /// </summary>
        public static Vector2 CursePoint(DungeonMap map, Vector2 from, Vector2 aim, float cursorDistance)
            => PlayerAbilities.FindShadowstepLanding(map, from, aim, Mathf.Min(CurseRange, cursorDistance), out Vector2 spot) ? spot : from;

        public float AscendTime(int rank) => AscendDuration + (rank - 1) + Player.Powerups.Count(PowerupType.Bloodline);

        /// <summary>Archdemon's Technique: her father's power floods in for a few seconds.</summary>
        private void Ascend(int rank)
        {
            Player.Buffs.Ascend(AscendTime(rank));
            Player.Charge.Cancel();
            var root = Player.Run.ProjectileRoot;
            Color color = HeroBuffs.AscendColor;
            HeroVfx.Pulse(root, transform.position, 2.2f, color, 0.5f);
            HeroVfx.Motes(root, transform.position, 1f, Abyss, 18, 0.9f);
            HeroVfx.Sparks(root, transform.position, color, 20, 5f, 0.45f);
            CombatVfx.Ring(root, transform.position, 1.5f, Pale, 0.45f);
            CoopFx.Pulse(Player.Run, transform.position, 2.2f, color, 0.5f);
            CoopFx.Ring(Player.Run, transform.position, 1.5f, Pale, 0.45f);
            ScreenFx.Flash(new Color(0.4f, 0.1f, 0.7f, 0.22f), 0.25f);
            ScreenFx.Shake(0.15f, 0.2f);
        }

        /// <summary>Prefers enemies toward the aim, then nearer ones. The portal opens above them, so walls do not matter.</summary>
        private DungeonEnemy FindPortalTarget(Vector2 aim)
        {
            Vector2 origin = transform.position;
            DungeonEnemy best = null;
            float bestScore = float.MaxValue;
            foreach (var enemy in Player.Run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                float distance = offset.magnitude;
                if (distance > PortalRange + enemy.HitRadius) continue;
                float alignment = distance < 0.01f ? 1f : Vector2.Dot(offset / distance, aim);
                float score = distance * (2f - alignment);
                if (score < bestScore) { bestScore = score; best = enemy; }
            }
            return best;
        }

        public float PawSlamRadius(int rank) => PawRadius + (rank - 1) * 0.2f + Player.Powerups.Count(PowerupType.BigPaws) * 0.4f;

        /// <summary>HEEEELP: a portal tears open over the target and her demonic pet slams a giant paw down on it.</summary>
        private IEnumerator PawSlam(DungeonEnemy target, int rank)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            float radius = PawSlamRadius(rank);
            Vector2 center = target.transform.position;
            var paw = DemonPawVfx.Play(root, center, radius, PortalWindup);
            CoopFx.DemonPaw(run, center, radius, PortalWindup);
            for (float t = 0f; t < PortalWindup; t += Time.deltaTime)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot) yield break;
                // The paw tracks its prey while the portal opens, then commits to that spot.
                if (t < PortalWindup * 0.6f && target != null && target.Health > 0) center = target.transform.position;
                if (paw != null) paw.Center = center;
                yield return null;
            }
            if (!run.IsPlaying || root != run.ProjectileRoot) yield break;
            if (paw != null) paw.Center = center;
            int damage = Player.Damage * (5 + rank);
            ScreenFx.Shake(0.35f, 0.3f);
            CombatVfx.Ring(root, center, radius, Pale, 0.4f);
            HeroVfx.Pulse(root, center, radius * 1.2f, Violet, 0.4f);
            HeroVfx.Sparks(root, center, new Color(0.55f, 0.5f, 0.6f), 22, 5.5f, 0.45f);
            CoopFx.Ring(run, center, radius, Pale, 0.4f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || Vector2.Distance(center, enemy.transform.position) > radius + enemy.HitRadius
                    || !run.HasLineOfSight(center, enemy.transform.position)) continue;
                CombatDamage.Apply(Player, enemy, WithPressurePoints(enemy, damage), DamageElement.Physical, center + Vector2.up, 1.5f);
                ParalyzeCounted(enemy, PawStun);
            }
            // Cursed Paw: where the paw lands, a Demon Curse is branded.
            if (Player.Powerups.Count(PowerupType.CursedPaw) > 0) StartCoroutine(CurseMark(center, 1));
        }

        public float CurseAreaRadius(int rank) => CurseRadius + (rank - 1) * CurseRankGrowth;
        public float CurseHold(int rank) => CurseParalysis + (rank - 1) * 0.5f + Player.Powerups.Count(PowerupType.HexMastery) * 0.5f;

        /// <summary>Demon Curse: a pentagram flares at the cursor, paralysing and cursing every enemy on it.</summary>
        private IEnumerator CurseMark(Vector2 center, int rank)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            float radius = CurseAreaRadius(rank);
            PentagramVfx.Play(root, center, radius, CurseWindup);
            CoopFx.Pentagram(run, center, radius, CurseWindup);
            yield return new WaitForSeconds(CurseWindup);
            if (!run.IsPlaying || root != run.ProjectileRoot) yield break;
            float hold = CurseHold(rank);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || Vector2.Distance(center, enemy.transform.position) > radius + enemy.HitRadius
                    || !run.HasLineOfSight(center, enemy.transform.position)) continue;
                // The brand deals no damage itself; it sets enemies up for her other attacks.
                enemy.Curse(CurseDuration);
                ParalyzeCounted(enemy, hold);
            }
        }

        public void Hide()
        {
            Player.Charge.Cancel();
            if (preview != null) preview.enabled = false;
        }

        private void LateUpdate()
        {
            bool show = Player.Run.IsPlaying && Player.Charge.IsCharging && !Player.IsRolling;
            preview.enabled = show;
            if (!show) return;
            float amount = Player.Charge.Amount;
            Vector2 aim = Player.AimDirection;
            bool whip = Player.Buffs.IsAscended;
            // Under Archdemon's Technique the lane grows into the whip's reach as the charge fills.
            float length = whip ? Mathf.Lerp(VitalReach, WhipRadius, amount) : Mathf.Lerp(StabReach, VitalReach, amount);
            float halfWidth = whip ? Mathf.Lerp(StabHalfWidth, WhipRadius * Mathf.Sin(WhipCone * 0.5f * Mathf.Deg2Rad), amount) : StabHalfWidth;
            previewRoot.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            preview.transform.localPosition = new Vector2(length * 0.5f, 0f);
            preview.transform.localRotation = Quaternion.identity;
            preview.transform.localScale = new Vector3(length, halfWidth * 2f, 1f);
            Color full = whip ? HeroBuffs.AscendColor : Pale;
            preview.color = amount >= 1f
                ? new Color(full.r, full.g, full.b, 0.28f + 0.1f * Mathf.Sin(Time.time * 14f))
                : new Color(Violet.r, Violet.g, Violet.b, Mathf.Lerp(0.1f, 0.26f, amount));
        }
    }
}

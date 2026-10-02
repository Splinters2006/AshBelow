using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Brawler's fists. Tapping jabs quickly in a short rectangle; a slow full charge releases a barrage of
    /// punches in a bigger rectangle. RMB Empowers her for a few seconds, and her boss artifacts
    /// (Knuckle Sandwich, Wild Leap, Primal Rage) are cast from here.
    /// </summary>
    public sealed partial class BrawlerAttack : MonoBehaviour, IPlayerWeapon
    {
        // 15% faster than the original 1.8 seconds.
        public const float ChargeDuration = 1.8f / 1.15f;
        public const float JabLength = 1.5f, JabHalfWidth = 0.42f;
        public const float BarrageLength = 2.3f, BarrageHalfWidth = 0.85f;
        public const int BarragePunches = 6;
        public const float EmpowerCooldown = 10f, EmpowerDuration = 5f;
        public const float RageDuration = 10f, TiredDuration = 5f;
        public const float LeapRange = 7f, LeapDuration = 0.26f, LeapHeight = 1.6f;
        public const float SandwichWindup = 0.42f;
        /// <summary>Wild Leap's slam starts at this radius and grows by <see cref="LeapRadiusPerUnit"/> per unit travelled.</summary>
        public const float LeapBaseRadius = 1.5f, LeapRadiusPerUnit = 0.22f;
        public static readonly Color Glove = new Color(0.95f, 0.3f, 0.26f);
        public DungeonPlayer Player { get; set; }
        public bool IsBarraging => barrage != null;
        /// <summary>Mastered Technique (her passive): a roll no longer cuts her barrage short.</summary>
        public bool KeepsBarrageWhileRolling => Player.Permanent != null && Player.Permanent.PassiveUnlocked;
        public bool IsLeaping => leap != null;
        public bool IsWindingUp => sandwich != null;
        // The barrage roots the Brawler like other heavy attacks: slower movement and no new charge.
        public bool IsHeavyAttacking => IsBarraging;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, empowerReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => empowerReadyAt = Cooldowns.Shorten(empowerReadyAt, seconds);
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && !Player.IsBusy && !IsBarraging && Time.time >= readyAt;
        public int BarrageCount => BarragePunches + Player.Powerups.Count(PowerupType.Flurry) + Player.Permanent.BarragePunches;
        private float Size => Player.Buffs.AttackSizeMultiplier * (1f + Player.Powerups.Count(PowerupType.HeavyGloves) * 0.1f);
        private float Interval => Player.Powerups.AttackIntervalMultiplier * Player.Buffs.AttackIntervalMultiplier;
        private Color PunchColor => Player.Buffs.IsRaging ? HeroBuffs.RageColor : Player.Buffs.IsEmpowered ? HeroBuffs.EmpowerColor : Glove;
        private float readyAt, empowerReadyAt;
        private Coroutine barrage, leap, sandwich;
        private PunchVfx sandwichWindup, flurry;
        private Transform previewRoot;
        private SpriteRenderer preview;

        private void Awake()
        {
            previewRoot = new GameObject("Punch preview").transform;
            previewRoot.SetParent(transform, false);
            // The hero sprite is scaled; keep the preview in world units so it matches the hit area.
            previewRoot.localScale = new Vector3(1 / transform.lossyScale.x, 1 / transform.lossyScale.y, 1);
            preview = DungeonVisuals.Create("Punch reach", previewRoot, transform.position, Vector2.one, Glove, 5);
            preview.enabled = false;
        }

        public static bool InRectangle(Vector2 offset, Vector2 aim, float length, float halfWidth, float radius = 0f)
        {
            if (aim.sqrMagnitude < 0.0001f) return false;
            aim.Normalize();
            float along = Vector2.Dot(offset, aim);
            return along >= -radius && along <= length + radius
                && Mathf.Abs(Vector2.Dot(offset, Vector2.Perpendicular(aim))) <= halfWidth + radius;
        }

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            if (charge >= 1f)
            {
                barrage = StartCoroutine(Barrage());
                return true;
            }
            aim.Normalize();
            Vector2 origin = transform.position;
            float length = JabLength * Size, halfWidth = JabHalfWidth * Size;
            Strike(origin, aim, length, halfWidth, Player.Charge.Damage(charge), 0.6f);
            DrawPunch(origin, aim, length, halfWidth, PunchColor);
            readyAt = Time.time + 0.2f * Interval;
            return true;
        }

        private IEnumerator Barrage()
        {
            int count = BarrageCount;
            var root = Player.Run.ProjectileRoot;
            // The whole barrage is one attack (Massacre counts its kills together).
            int attack = Player.Powerups.ActiveAttack;
            bool finished = false;
            float flurryTime = count * 0.07f * Interval + 0.15f;
            flurry = BrawlerVfx.Flurry(root, transform, () => Player.AimDirection, BarrageLength * Size, BarrageHalfWidth * Size, PunchColor, flurryTime);
            CoopFx.Flurry(Player.Run, BarrageLength * Size, BarrageHalfWidth * Size, PunchColor, flurryTime);
            ScreenFx.Shake(0.05f, flurryTime);
            int landed = 0;
            for (int i = 0; i < count; i++)
            {
                if (!Player.Run.IsPlaying || Player.Health <= 0 || root != Player.Run.ProjectileRoot) break;
                bool finisher = i == count - 1;
                Vector2 origin = transform.position;
                Vector2 aim = Player.AimDirection;
                float length = BarrageLength * Size, halfWidth = BarrageHalfWidth * Size;
                // Light hits keep enemies inside the rectangle; the last punch sends them flying.
                // Combo Counter: every punch that landed adds 1 to the finisher. Knockout: the finisher stuns.
                int combo = finisher && Player.Powerups.Count(PowerupType.ComboCounter) > 0 ? landed : 0;
                float stun = finisher && Player.Powerups.Count(PowerupType.Knockout) > 0 ? KnockoutStun : 0f;
                using (Player.Powerups.ResumeAttack(attack))
                    if (Strike(origin, aim, length, halfWidth, finisher ? Player.Damage * 2 + combo : Player.Damage, finisher ? 2f : 0.1f, stun) > 0) landed++;
                float fist = finisher ? halfWidth : 0.28f;
                Vector2 lane = Vector2.Perpendicular(aim) * (finisher ? 0f : Random.Range(-1f, 1f) * (halfWidth - fist));
                DrawPunch(origin + lane, aim, length, fist, finisher ? Color.Lerp(PunchColor, Color.white, 0.3f) : PunchColor, finisher ? 0.22f : 0.1f);
                if (finisher)
                {
                    HeroVfx.Pulse(root, origin + aim * length, halfWidth, PunchColor, 0.3f);
                    HeroVfx.Sparks(root, origin + aim * length, Color.Lerp(PunchColor, Color.white, 0.4f), 14, 5f, 0.35f, aim, 120f, 1.2f);
                    ScreenFx.Shake(0.15f, 0.15f);
                    finished = true;
                }
                yield return new WaitForSeconds(0.07f * Interval);
            }
            StopFlurry();
            // Final Blow (her second passive): a finished barrage ends in an even bigger, harder punch.
            if (finished && Player.Permanent.HasSecondPassive(WeaponType.Fists) && Player.Run.IsPlaying && Player.Health > 0 && root == Player.Run.ProjectileRoot)
            {
                yield return new WaitForSeconds(FinalBlowDelay * Interval);
                if (Player.Run.IsPlaying && Player.Health > 0 && root == Player.Run.ProjectileRoot)
                    using (Player.Powerups.ResumeAttack(attack)) FinalBlow(landed);
            }
            barrage = null;
            readyAt = Time.time + 0.25f * Interval;
        }

        /// <summary>Final Blow: how much bigger the closing punch's rectangle is than the barrage's, and its windup.</summary>
        public const float FinalBlowSize = 1.5f, FinalBlowDelay = 0.12f;
        public const int FinalBlowDamageMultiplier = 4;

        /// <summary>The punch that closes a barrage under Final Blow: four times her damage (plus Combo Counter's tally) over a far bigger area.</summary>
        private void FinalBlow(int landed)
        {
            var root = Player.Run.ProjectileRoot;
            Vector2 origin = transform.position, aim = Player.AimDirection;
            float length = BarrageLength * Size * FinalBlowSize, halfWidth = BarrageHalfWidth * Size * FinalBlowSize;
            int combo = Player.Powerups.Count(PowerupType.ComboCounter) > 0 ? landed : 0;
            float stun = Player.Powerups.Count(PowerupType.Knockout) > 0 ? KnockoutStun : 0f;
            Strike(origin, aim, length, halfWidth, Player.Damage * FinalBlowDamageMultiplier + combo, 3f, stun);
            Color color = Color.Lerp(PunchColor, Color.white, 0.45f);
            DrawPunch(origin, aim, length, halfWidth, color, 0.3f);
            HeroVfx.Pulse(root, origin + aim * length * 0.6f, halfWidth * 1.6f, PunchColor, 0.4f);
            HeroVfx.Sparks(root, origin + aim * length, Color.Lerp(PunchColor, Color.white, 0.5f), 24, 6.5f, 0.4f, aim, 140f, 1.4f);
            CombatVfx.Ring(root, origin + aim * length * 0.6f, halfWidth * 1.4f, Color.white, 0.3f);
            CoopFx.Pulse(Player.Run, origin + aim * length * 0.6f, halfWidth * 1.6f, PunchColor, 0.4f);
            ScreenFx.Shake(0.3f, 0.25f);
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || HeavyCooldownRemaining > 0f) return false;
            Player.Buffs.Empower(EmpowerDuration + Player.Powerups.Count(PowerupType.Adrenaline));
            empowerReadyAt = Time.time + EmpowerCooldown * Player.Powerups.SkillCooldownMultiplier;
            var root = Player.Run.ProjectileRoot;
            HeroVfx.Pulse(root, transform.position, 1.3f, HeroBuffs.EmpowerColor, 0.4f);
            HeroVfx.Sparks(root, transform.position, HeroBuffs.EmpowerColor, 12, 4f, 0.35f);
            CombatVfx.Ring(root, transform.position, 0.8f, HeroBuffs.EmpowerColor);
            CoopFx.Pulse(Player.Run, transform.position, 1.3f, HeroBuffs.EmpowerColor, 0.4f);
            CoopFx.Ring(Player.Run, transform.position, 0.8f, HeroBuffs.EmpowerColor);
            return true;
        }

        public bool CastArtifact(AbilityType type, Vector2 aim, int rank)
        {
            if (Player == null || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || aim.sqrMagnitude < 0.001f) return false;
            rank = Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank);
            aim.Normalize();
            switch (type)
            {
                case AbilityType.KnuckleSandwich: sandwich = StartCoroutine(KnuckleSandwich(aim, rank)); return true;
                case AbilityType.WildLeap:
                    var target = FindLeapTarget(aim);
                    if (target == null) return false;
                    leap = StartCoroutine(Leap(target, rank));
                    return true;
                case AbilityType.PrimalRage: PrimalRage(rank); return true;
                case AbilityType.ThunderClap: StartCoroutine(ThunderClap(aim, rank)); return true;
                case AbilityType.HaymakerDash: StartCoroutine(HaymakerDash(aim, rank)); return true;
                case AbilityType.Suplex:
                    var grabbed = FindSuplexTarget();
                    if (grabbed == null) return false;
                    StartCoroutine(Suplex(grabbed, rank));
                    return true;
                default: return false;
            }
        }

        /// <summary>
        /// A HEAVY punch: she plants her feet and draws the fist back (the hit area glows as it charges),
        /// then lunges into a crushing blow with a shockwave, camera shake and a flash.
        /// </summary>
        private IEnumerator KnuckleSandwich(Vector2 aim, int rank)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            float bonus = Player.Powerups.Count(PowerupType.ExtraFilling) * 0.4f;
            float length = (3.2f + (rank - 1) * 0.6f + bonus) * Size;
            float halfWidth = (1.1f + (rank - 1) * 0.25f + bonus * 0.5f) * Size;
            Color color = Color.Lerp(PunchColor, AbilityCatalog.Gold, 0.35f);
            Player.Occupy(SandwichWindup + 0.12f);
            Player.Charge.Cancel();
            // The fist is drawn back behind her and swells with power while the target area glows.
            sandwichWindup = BrawlerVfx.Windup(root, transform, aim, length, halfWidth, color, SandwichWindup + 0.05f);
            for (float t = 0f; t < SandwichWindup; t += Time.deltaTime)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot || Player.Health <= 0) { EndSandwich(); yield break; }
                if (Random.value < 0.35f)
                    HeroVfx.Sparks(root, (Vector2)transform.position - aim * 0.5f, color, 2, 1.6f, 0.2f, aim, 120f, 0.7f);
                yield return null;
            }
            EndSandwich();
            Vector2 start = transform.position;
            // A short lunge into the blow.
            transform.position = run.Map.Move(start, aim * 0.4f);
            Vector2 from = transform.position, impact = from + aim * length;
            Strike(from, aim, length, halfWidth, Player.Damage * (4 + rank), 4f);
            if (Player.Powerups.Count(PowerupType.SandwichSpecial) > 0) StartCoroutine(SandwichShockwave(impact, aim, halfWidth, Player.Damage * 2 + rank));
            BrawlerVfx.HeavyPunch(root, from, aim, length, halfWidth, color);
            CoopFx.HeavyPunch(run, from, aim, length, halfWidth, color);
            HeroVfx.Pulse(root, impact, halfWidth * 1.6f, color, 0.45f);
            HeroVfx.Pulse(root, from + aim * length * 0.5f, length * 0.6f, Color.white, 0.2f);
            CombatVfx.Ring(root, impact, halfWidth * 1.4f, Color.white, 0.35f);
            HeroVfx.Sparks(root, impact, color, 30, 8f, 0.5f, aim, 100f, 1.6f);
            for (int i = 1; i <= 3; i++)
                HeroVfx.Sparks(root, from + aim * length * i / 4f, new Color(0.75f, 0.68f, 0.58f), 4, 3f, 0.35f, Vector2.Perpendicular(aim), 360f, 0.9f);
            CoopFx.Pulse(run, impact, halfWidth * 1.6f, color, 0.45f);
            CoopFx.Ring(run, impact, halfWidth * 1.4f, Color.white, 0.35f);
            ScreenFx.Shake(0.5f, 0.4f);
            ScreenFx.Flash(new Color(1f, 0.95f, 0.85f, 0.25f), 0.18f);
            sandwich = null;
        }

        private void EndSandwich()
        {
            if (sandwichWindup != null) sandwichWindup.Stop();
            sandwichWindup = null;
            sandwich = null;
        }

        private void StopFlurry()
        {
            if (flurry != null) flurry.Stop();
            flurry = null;
        }

        private void PrimalRage(int rank)
        {
            float duration = RageDuration + rank - 1 + Player.Powerups.Count(PowerupType.Bloodlust);
            Player.Buffs.Rage(duration, TiredDuration);
            var root = Player.Run.ProjectileRoot;
            // Her hackles, heartbeat and claws follow her for as long as she rages.
            PrimalRageVfx.Play(root, transform, duration);
            CoopFx.PrimalRage(Player.Run, duration);
            ScreenFx.Shake(0.25f, 0.25f);
            ScreenFx.Flash(new Color(0.8f, 0.05f, 0.05f, 0.18f), 0.2f);
            HeroVfx.Pulse(root, transform.position, 2f, HeroBuffs.RageColor, 0.5f);
            HeroVfx.Sparks(root, transform.position, HeroBuffs.RageColor, 22, 5f, 0.45f);
            CombatVfx.Ring(root, transform.position, 1.4f, HeroBuffs.RageColor, 0.5f);
            CoopFx.Pulse(Player.Run, transform.position, 2f, HeroBuffs.RageColor, 0.5f);
        }

        /// <summary>Prefers enemies toward the aim, then nearer ones. She leaps over walls, so any enemy in range can be chosen.</summary>
        private DungeonEnemy FindLeapTarget(Vector2 aim)
        {
            Vector2 origin = transform.position;
            DungeonEnemy best = null;
            float bestScore = float.MaxValue;
            foreach (var enemy in Player.Run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                float distance = offset.magnitude;
                if (distance > LeapRange + enemy.HitRadius || !CanLandNear(enemy)) continue;
                float alignment = distance < 0.01f ? 1f : Vector2.Dot(offset / distance, aim);
                float score = distance * (2f - alignment);
                if (score < bestScore) { bestScore = score; best = enemy; }
            }
            return best;
        }

        private bool CanLandNear(DungeonEnemy target) => TryLandingNear(transform.position, target, out _);

        /// <summary>A standable spot beside the target, preferring the side she leaps from. Walls in between do not matter.</summary>
        private bool TryLandingNear(Vector2 from, DungeonEnemy target, out Vector2 landing)
        {
            Vector2 goal = target.transform.position;
            Vector2 toTarget = goal - from;
            Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Player.AimDirection;
            float gap = target.HitRadius + 0.3f;
            for (int i = 0; i < 8; i++)
            {
                // Try the near side first, then fan out around the target.
                float turn = (i % 2 == 0 ? 1f : -1f) * ((i + 1) / 2) * 45f;
                landing = goal - (Vector2)(Quaternion.Euler(0, 0, turn) * direction) * gap;
                if (Player.Run.Map.CanStand(landing)) return true;
            }
            landing = goal;
            return Player.Run.Map.CanStand(goal);
        }

        private Vector2 LandingNear(Vector2 from, DungeonEnemy target, Vector2 fallback)
            => TryLandingNear(from, target, out Vector2 landing) ? landing : fallback;

        private IEnumerator Leap(DungeonEnemy target, int rank)
        {
            var root = Player.Run.ProjectileRoot;
            Player.Occupy(LeapDuration);
            Player.Protect(LeapDuration + 0.25f);
            Player.Charge.Cancel();
            Vector2 from = transform.position;
            Vector2 landing = LandingNear(from, target, from);
            HeroVfx.Sparks(root, from + Vector2.down * 0.3f, new Color(0.7f, 0.66f, 0.6f, 0.8f), 8, 2.6f, 0.3f, from - landing, 120f, 0.9f);
            for (float t = 0f; t < LeapDuration; t += Time.deltaTime)
            {
                if (!Player.Run.IsPlaying || root != Player.Run.ProjectileRoot) { leap = null; yield break; }
                // Home in on the target while airborne; keep the last landing spot if it dies mid-leap.
                if (target != null && target.Health > 0) landing = LandingNear(from, target, landing);
                float progress = t / LeapDuration;
                // A high arc carries her clean over walls.
                transform.position = Vector2.Lerp(from, landing, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * LeapHeight;
                yield return null;
            }
            transform.position = landing;
            Slam(landing, rank, Vector2.Distance(from, landing));
            leap = null;
        }

        public static float SlamRadius(float travelled, int craterMakers) => LeapBaseRadius + craterMakers * 0.5f + Mathf.Max(0f, travelled) * LeapRadiusPerUnit;

        private void Slam(Vector2 center, int rank, float travelled)
        {
            float radius = SlamRadius(travelled, Player.Powerups.Count(PowerupType.CraterMaker));
            ScreenFx.Shake(0.12f + travelled * 0.025f, 0.25f);
            int damage = Player.Damage * (4 + rank);
            var root = Player.Run.ProjectileRoot;
            Color color = AbilityCatalog.Gold;
            CombatVfx.Ring(root, center, radius, color, 0.5f);
            HeroVfx.Pulse(root, center, radius, color, 0.45f);
            HeroVfx.Sparks(root, center, new Color(0.75f, 0.68f, 0.58f), 20, 5f, 0.45f);
            CoopFx.Ring(Player.Run, center, radius, color, 0.5f);
            CoopFx.Pulse(Player.Run, center, radius, color, 0.45f);
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || Vector2.Distance(center, enemy.transform.position) > radius + enemy.HitRadius
                    || !Player.Run.HasLineOfSight(center, enemy.transform.position)) continue;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, center, 1.5f);
                if (enemy.Health <= 0) continue;
                enemy.Chill(1f);
                // Meteor Leap: the slam sets them burning.
                if (Player.Powerups.Count(PowerupType.MeteorLeap) > 0) enemy.Burn(CombatDamage.BurnTicks, CombatDamage.BurnTickDamage(damage));
            }
        }

        public const float KnockoutStun = 1f;

        /// <summary>Hits everything in the rectangle; returns how many it struck.</summary>
        private int Strike(Vector2 origin, Vector2 aim, float length, float halfWidth, int damage, float knockback, float stun = 0f)
        {
            int struck = 0;
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0
                    || !InRectangle((Vector2)enemy.transform.position - origin, aim, length, halfWidth, enemy.HitRadius)
                    || !Player.Run.HasLineOfSight(origin, enemy.transform.position)) continue;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, origin, knockback);
                struck++;
                if (stun > 0f && enemy != null && enemy.Health > 0) enemy.Stun(stun);
            }
            return struck;
        }

        private void DrawPunch(Vector2 origin, Vector2 aim, float length, float halfWidth, Color color, float duration = 0.16f)
        {
            BrawlerVfx.Punch(Player.Run.ProjectileRoot, origin, aim, length, halfWidth, color, duration);
            CoopFx.Punch(Player.Run, origin, aim, length, halfWidth, color);
        }

        public void Hide()
        {
            StopBarrage();
            Player.Charge.Cancel();
            if (preview != null) preview.enabled = false;
        }

        /// <summary>Cuts a running barrage short but leaves a held punch charging (a dodge keeps the Brawler's charge).</summary>
        public void StopBarrage()
        {
            if (barrage == null) return;
            StopCoroutine(barrage);
            StopFlurry();
            barrage = null;
            readyAt = Time.time + 0.25f * Interval;
        }

        private void LateUpdate()
        {
            bool show = Player.Run.IsPlaying && Player.Charge.IsCharging && !Player.IsRolling && !IsBarraging;
            preview.enabled = show;
            if (!show) return;
            float amount = Player.Charge.Amount;
            Vector2 aim = Player.AimDirection;
            float length = Mathf.Lerp(JabLength, BarrageLength, amount) * Size;
            float halfWidth = Mathf.Lerp(JabHalfWidth, BarrageHalfWidth, amount) * Size;
            previewRoot.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            preview.transform.localPosition = new Vector2(length * 0.5f, 0f);
            preview.transform.localRotation = Quaternion.identity;
            preview.transform.localScale = new Vector3(length, halfWidth * 2f, 1f);
            preview.color = amount >= 1f
                ? new Color(AbilityCatalog.Gold.r, AbilityCatalog.Gold.g, AbilityCatalog.Gold.b, 0.3f + 0.1f * Mathf.Sin(Time.time * 14f))
                : new Color(Glove.r, Glove.g, Glove.b, Mathf.Lerp(0.1f, 0.28f, amount));
        }
    }
}

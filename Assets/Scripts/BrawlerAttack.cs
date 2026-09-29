using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Brawler's fists. Tapping jabs quickly in a short rectangle; a slow full charge releases a barrage of
    /// punches in a bigger rectangle. RMB Empowers her for a few seconds, and her boss artifacts
    /// (Knuckle Sandwich, Wild Leap, Primal Rage) are cast from here.
    /// </summary>
    public sealed class BrawlerAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float ChargeDuration = 1.8f;
        public const float JabLength = 1.5f, JabHalfWidth = 0.42f;
        public const float BarrageLength = 2.3f, BarrageHalfWidth = 0.85f;
        public const int BarragePunches = 6;
        public const float EmpowerCooldown = 10f, EmpowerDuration = 5f;
        public const float RageDuration = 10f, TiredDuration = 5f;
        public const float LeapRange = 7f, LeapDuration = 0.26f;
        public const float SandwichWindup = 0.42f;
        /// <summary>Wild Leap's slam grows by this much radius per unit travelled.</summary>
        public const float LeapRadiusPerUnit = 0.3f;
        public static readonly Color Glove = new Color(0.95f, 0.3f, 0.26f);
        public DungeonPlayer Player { get; set; }
        public bool IsBarraging => barrage != null;
        public bool IsLeaping => leap != null;
        public bool IsWindingUp => sandwich != null;
        // The barrage roots the Brawler like other heavy attacks: slower movement and no new charge.
        public bool IsHeavyAttacking => IsBarraging;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, empowerReadyAt - Time.time));
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && !Player.IsBusy && !IsBarraging && Time.time >= readyAt;
        public int BarrageCount => BarragePunches + Player.Powerups.Count(PowerupType.Flurry) + Player.Permanent.BarragePunches;
        private float Size => Player.Buffs.AttackSizeMultiplier;
        private float Interval => Player.Powerups.AttackIntervalMultiplier * Player.Buffs.AttackIntervalMultiplier;
        private Color PunchColor => Player.Buffs.IsRaging ? HeroBuffs.RageColor : Player.Buffs.IsEmpowered ? HeroBuffs.EmpowerColor : Glove;
        private float readyAt, empowerReadyAt;
        private Coroutine barrage, leap, sandwich;
        private GameObject sandwichWindup;
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
            BrawlerVfx.Punch(root, transform.position, Player.AimDirection, BarrageLength * Size, BarrageHalfWidth * Size,
                PunchColor, count * 0.07f * Interval + 0.15f);
            for (int i = 0; i < count; i++)
            {
                if (!Player.Run.IsPlaying || Player.Health <= 0 || root != Player.Run.ProjectileRoot) break;
                bool finisher = i == count - 1;
                Vector2 origin = transform.position;
                Vector2 aim = Player.AimDirection;
                float length = BarrageLength * Size, halfWidth = BarrageHalfWidth * Size;
                // Light hits keep enemies inside the rectangle; the last punch sends them flying.
                Strike(origin, aim, length, halfWidth, finisher ? Player.Damage * 2 : Player.Damage, finisher ? 2f : 0.1f);
                float fist = finisher ? halfWidth : 0.28f;
                Vector2 lane = Vector2.Perpendicular(aim) * (finisher ? 0f : Random.Range(-1f, 1f) * (halfWidth - fist));
                DrawPunch(origin + lane, aim, length, fist, finisher ? Color.Lerp(PunchColor, Color.white, 0.3f) : PunchColor, finisher ? 0.22f : 0.1f);
                if (finisher) HeroVfx.Pulse(root, origin + aim * length, halfWidth, PunchColor, 0.3f);
                yield return new WaitForSeconds(0.07f * Interval);
            }
            barrage = null;
            readyAt = Time.time + 0.25f * Interval;
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || HeavyCooldownRemaining > 0f) return false;
            Player.Buffs.Empower(EmpowerDuration + Player.Powerups.Count(PowerupType.Adrenaline));
            empowerReadyAt = Time.time + EmpowerCooldown;
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
            sandwichWindup = new GameObject("Knuckle Sandwich windup");
            sandwichWindup.transform.SetParent(root, false);
            var area = DungeonVisuals.Create("Windup area", sandwichWindup.transform, transform.position, Vector2.one, color, 5);
            var edge = DungeonVisuals.Create("Windup edge", sandwichWindup.transform, transform.position, Vector2.one, Color.white, 6);
            var fist = DungeonVisuals.Create("Drawn fist", sandwichWindup.transform, transform.position, Vector2.one * 0.3f, Glove, 7);
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (float t = 0f; t < SandwichWindup; t += Time.deltaTime)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot || Player.Health <= 0) { EndSandwich(); yield break; }
                float progress = t / SandwichWindup;
                Vector2 origin = transform.position;
                float reach = length * Mathf.Lerp(0.25f, 1f, progress);
                area.transform.SetPositionAndRotation(origin + aim * reach * 0.5f, Quaternion.Euler(0, 0, angle));
                area.transform.localScale = new Vector3(reach, halfWidth * 2f, 1f);
                area.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.08f, 0.3f, progress) + 0.06f * Mathf.Sin(t * 40f));
                edge.transform.SetPositionAndRotation(origin + aim * reach, Quaternion.Euler(0, 0, angle));
                edge.transform.localScale = new Vector3(0.06f, halfWidth * 2f, 1f);
                edge.color = new Color(1f, 1f, 1f, 0.3f + 0.5f * progress);
                // The fist is drawn back behind her and swells with power.
                fist.transform.position = origin - aim * Mathf.Lerp(0.2f, 0.55f, progress) + Vector2.Perpendicular(aim) * 0.15f;
                fist.transform.localScale = Vector2.one * Mathf.Lerp(0.3f, 0.6f, progress);
                fist.color = Color.Lerp(Glove, Color.white, 0.5f * progress + 0.2f * Mathf.Sin(t * 50f));
                if (Random.value < 0.35f)
                    HeroVfx.Sparks(root, fist.transform.position, color, 2, 1.6f, 0.2f, aim, 120f, 0.7f);
                yield return null;
            }
            EndSandwich();
            Vector2 start = transform.position;
            // A short lunge into the blow.
            transform.position = run.Map.Move(start, aim * 0.4f);
            Vector2 from = transform.position, impact = from + aim * length;
            Strike(from, aim, length, halfWidth, Player.Damage * (4 + rank), 4f);
            DrawPunch(from, aim, length, halfWidth, color, 0.4f);
            HeroVfx.Pulse(root, impact, halfWidth * 1.6f, color, 0.45f);
            HeroVfx.Pulse(root, from + aim * length * 0.5f, length * 0.6f, Color.white, 0.2f);
            CombatVfx.Ring(root, impact, halfWidth * 1.4f, Color.white, 0.35f);
            HeroVfx.Sparks(root, impact, color, 30, 8f, 0.5f, aim, 100f, 1.6f);
            for (int i = 1; i <= 3; i++)
                HeroVfx.Sparks(root, from + aim * length * i / 4f, new Color(0.75f, 0.68f, 0.58f), 4, 3f, 0.35f, Vector2.Perpendicular(aim), 360f, 0.9f);
            CoopFx.Pulse(run, impact, halfWidth * 1.6f, color, 0.45f);
            CoopFx.Ring(run, impact, halfWidth * 1.4f, Color.white, 0.35f);
            ScreenFx.Shake(0.4f, 0.35f);
            ScreenFx.Flash(new Color(1f, 0.95f, 0.85f, 0.18f), 0.15f);
            sandwich = null;
        }

        private void EndSandwich()
        {
            if (sandwichWindup != null) Destroy(sandwichWindup);
            sandwichWindup = null;
            sandwich = null;
        }

        private void PrimalRage(int rank)
        {
            Player.Buffs.Rage(RageDuration + rank - 1 + Player.Powerups.Count(PowerupType.Bloodlust), TiredDuration);
            var root = Player.Run.ProjectileRoot;
            HeroVfx.Pulse(root, transform.position, 2f, HeroBuffs.RageColor, 0.5f);
            HeroVfx.Sparks(root, transform.position, HeroBuffs.RageColor, 22, 5f, 0.45f);
            CombatVfx.Ring(root, transform.position, 1.4f, HeroBuffs.RageColor, 0.5f);
            CoopFx.Pulse(Player.Run, transform.position, 2f, HeroBuffs.RageColor, 0.5f);
        }

        /// <summary>Prefers enemies toward the aim, then nearer ones; any visible enemy in range can be chosen.</summary>
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
                if (distance > LeapRange + enemy.HitRadius || !Player.Run.HasLineOfSight(origin, enemy.transform.position)) continue;
                float alignment = distance < 0.01f ? 1f : Vector2.Dot(offset / distance, aim);
                float score = distance * (2f - alignment);
                if (score < bestScore) { bestScore = score; best = enemy; }
            }
            return best;
        }

        private Vector2 LandingNear(Vector2 from, DungeonEnemy target)
        {
            Vector2 goal = target.transform.position;
            Vector2 toTarget = goal - from;
            Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Player.AimDirection;
            Vector2 spot = goal - direction * (target.HitRadius + 0.3f);
            return Player.Run.Map.CanStand(spot) ? spot : Player.Run.Map.Move(from, spot - from);
        }

        private IEnumerator Leap(DungeonEnemy target, int rank)
        {
            var root = Player.Run.ProjectileRoot;
            Player.Occupy(LeapDuration);
            Player.Protect(LeapDuration + 0.25f);
            Player.Charge.Cancel();
            Vector2 from = transform.position;
            Vector2 landing = LandingNear(from, target);
            HeroVfx.Sparks(root, from + Vector2.down * 0.3f, new Color(0.7f, 0.66f, 0.6f, 0.8f), 8, 2.6f, 0.3f, from - landing, 120f, 0.9f);
            for (float t = 0f; t < LeapDuration; t += Time.deltaTime)
            {
                if (!Player.Run.IsPlaying || root != Player.Run.ProjectileRoot) { leap = null; yield break; }
                // Home in on the target while airborne; keep the last landing spot if it dies mid-leap.
                if (target != null && target.Health > 0) landing = LandingNear(from, target);
                float progress = t / LeapDuration;
                transform.position = Vector2.Lerp(from, landing, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * 1.1f;
                yield return null;
            }
            transform.position = landing;
            Slam(landing, rank, Vector2.Distance(from, landing));
            leap = null;
        }

        public static float SlamRadius(float travelled, int craterMakers) => 1.8f + craterMakers * 0.5f + Mathf.Max(0f, travelled) * LeapRadiusPerUnit;

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
                if (enemy.Health > 0) enemy.Chill(1f);
            }
        }

        private void Strike(Vector2 origin, Vector2 aim, float length, float halfWidth, int damage, float knockback)
        {
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0
                    || !InRectangle((Vector2)enemy.transform.position - origin, aim, length, halfWidth, enemy.HitRadius)
                    || !Player.Run.HasLineOfSight(origin, enemy.transform.position)) continue;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, origin, knockback);
            }
        }

        private void DrawPunch(Vector2 origin, Vector2 aim, float length, float halfWidth, Color color, float duration = 0.16f)
        {
            BrawlerVfx.Punch(Player.Run.ProjectileRoot, origin, aim, length, halfWidth, color, duration);
            CoopFx.Punch(Player.Run, origin, aim, length, halfWidth, color);
        }

        public void Hide()
        {
            if (barrage != null)
            {
                StopCoroutine(barrage);
                barrage = null;
                readyAt = Time.time + 0.25f * Interval;
            }
            Player.Charge.Cancel();
            if (preview != null) preview.enabled = false;
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

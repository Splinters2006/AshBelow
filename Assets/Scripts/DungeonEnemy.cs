using UnityEngine;

namespace Slopgame
{
    [RequireComponent(typeof(EnemyFacing), typeof(EnemyTactics))]
    public sealed class DungeonEnemy : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        public int Health { get; set; }
        public float Speed { get; set; }
        private float hitUntil;
        private SpriteRenderer body;
        private EnemyShooter shooter;
        /// <summary>The special kind of floor enemy this is (including world-specific specialists), or null for the basic three.</summary>
        public EnemyVariant Variant => variant != null ? variant : variant = GetComponent<EnemyVariant>();
        private EnemyVariant variant;
        public DungeonBoss Boss { get; set; }
        public bool IsTank { get; set; }
        /// <summary>Summoned by a guardian mid-fight; dissolves when its guardian falls.</summary>
        public bool IsMinion { get; set; }
        public float HitRadius => Boss != null ? Boss.HitRadius : IsTrainingDummy ? TrainingDummy.Radius : IsTank ? 0.5f : 0.38f;
        /// <summary>True while a boss is out of reach (such as the Archdemon in flight); blows glance off.</summary>
        public bool IsInvulnerable => Boss != null && Boss.IsInvulnerable;
        public float MoveRadius => IsTank ? 0.42f : 0.28f;
        public bool IsBurning => burnTicks > 0 || netBurning;
        /// <summary>Elemental Immobilization: an element has touched this enemy (burn, chill, freeze or shock) since it was last seized.</summary>
        public bool ElementTouched { get; private set; }
        public void TouchWithElement() { if (!IsInvulnerable && Health > 0) ElementTouched = true; }
        /// <summary>Spends the element's touch; true if there was one to spend.</summary>
        public bool ConsumeElementTouch()
        {
            bool touched = ElementTouched;
            ElementTouched = false;
            return touched;
        }
        /// <summary>Spawn-order id shared by every machine in a co-op run.</summary>
        public ushort NetId { get; set; }
        private Vector2 netPosition;
        private bool netBurning, netFlashing, hasSnapshot;
        private EnemyTactics tactics;
        private SpriteRenderer burnIndicator, bleedIndicator;
        public bool IsChilled => Time.time < chilledUntil;
        /// <summary>Frozen solid by ice: like paralysis, a frozen enemy cannot move, turn or attack.</summary>
        public bool IsFrozen => Time.time < frozenUntil;
        /// <summary>True while paralysis or ice holds the enemy completely still.</summary>
        public bool IsHeld => IsParalyzed || IsFrozen || IsStunned;
        /// <summary>Stunned (Holy Lance, Thunder Clap, EMP Pulse...): held like paralysis, but not the Demoness's paralysis.</summary>
        public bool IsStunned => Time.time < stunnedUntil;
        /// <summary>Rooted (Net Shot, Bear Trap): cannot move, but still turns, shoots and swings.</summary>
        public bool IsRooted => Time.time < rootedUntil;
        /// <summary>Seconds the longest of this enemy's holds (paralysis, freeze, stun or root) still has to run.</summary>
        public float HoldRemaining => Mathf.Max(0f, Mathf.Max(Mathf.Max(paralyzedUntil, frozenUntil), Mathf.Max(stunnedUntil, rootedUntil)) - Time.time);
        /// <summary>Held or rooted: what talents mean by an immobilized enemy (paralysed, frozen, stunned or rooted).</summary>
        public bool IsImmobilized => IsHeld || IsRooted;
        /// <summary>
        /// True while a blow that holds its victim is landing (a vital stab, a paralysing sweep, a hit under a demonic
        /// rune, a freezing or seizing hit). If that blow kills, the enemy still counts as held for the "on paralysis" and
        /// "killing an immobilized enemy" talents, as if the hold had taken hold first. So does an enemy that dies with
        /// Bled Dry owed: death ends its bleeding, and with it comes the stun.
        /// </summary>
        public bool HoldPending => holdingBlows > 0 || BledDryPending || (Health <= 0 && Time.time < holdingBlowLingers);
        /// <summary>Immobilized, or killed by a blow that was holding it: what kill talents count as a held enemy.</summary>
        public bool CountsAsHeld => IsImmobilized || HoldPending;
        private int holdingBlows;
        private float holdingBlowLingers;

        /// <summary>A holding blow is about to land (see <see cref="HoldPending"/>); pair with <see cref="EndHoldingBlow"/>.</summary>
        public void BeginHoldingBlow() => holdingBlows++;

        public void EndHoldingBlow()
        {
            holdingBlows = Mathf.Max(0, holdingBlows - 1);
            // A co-op guest only learns of the kill when the host confirms it, so the mark lingers for that long.
            if (Health <= 0 && Run != null && Guest) holdingBlowLingers = Time.time + 1f;
        }

        /// <summary>
        /// The bleed damage a blow landing now would open, if it does not kill first. A blow that kills before its wounds
        /// open still counts its victim as bleeding for the "killing a bleeding enemy" talents (Trail of Blood), and
        /// Crimson Bloom bursts with the wounds it would have opened.
        /// </summary>
        public float BleedPending => bleedingBlows > 0 ? pendingBleed : Health <= 0 && Time.time < bleedingBlowLingers ? lingeringBleed : 0f;
        /// <summary>Bleeding, or killed by a blow that was opening a wound: what kill talents count as a bleeding enemy.</summary>
        public bool CountsAsBleeding => IsBleeding || BleedPending > 0f;
        /// <summary>The bleed damage left to deal, counting wounds a killing blow was about to open.</summary>
        public float BleedOwed => BleedRemaining + BleedPending;
        /// <summary>
        /// Dead with a Bled Dry stun owed, from wounds it already had or ones the killing blow was opening: its bleeding
        /// ended with its death, so it counts as stunned for the kill talents (unless a guardian would have shaken the stun off).
        /// </summary>
        public bool BledDryPending => Health <= 0 && (Boss == null || Time.time >= stunImmuneUntil) && ((wounds.Count > 0 && bleedStun > 0f)
            || (BleedPending > 0f && (bleedingBlows > 0 ? pendingBleedStun : Time.time < bleedingBlowLingers ? lingeringBleedStun : 0f) > 0f));
        private int bleedingBlows;
        private float pendingBleed, lingeringBleed, bleedingBlowLingers, pendingBleedStun, lingeringBleedStun;

        /// <summary>
        /// A blow that opens <paramref name="bleed"/> damage of wounds (with a Bled Dry <paramref name="stun"/>) is about to
        /// land; pair with <see cref="EndBleedingBlow"/>.
        /// </summary>
        public void BeginBleedingBlow(float bleed, float stun = 0f)
        {
            bleedingBlows++;
            pendingBleed += Mathf.Max(0f, bleed);
            if (bleed > 0f) pendingBleedStun = Mathf.Max(pendingBleedStun, stun);
        }

        public void EndBleedingBlow(float bleed)
        {
            // A co-op guest only learns of the kill when the host confirms it, so the mark lingers for that long.
            if (Health <= 0 && Run != null && Guest && pendingBleed > 0f)
            { lingeringBleed = pendingBleed; lingeringBleedStun = pendingBleedStun; bleedingBlowLingers = Time.time + 1f; }
            bleedingBlows = Mathf.Max(0, bleedingBlows - 1);
            pendingBleed = bleedingBlows > 0 ? Mathf.Max(0f, pendingBleed - Mathf.Max(0f, bleed)) : 0f;
            if (bleedingBlows == 0) pendingBleedStun = 0f;
        }

        /// <summary>
        /// True while a blow that sets its victim alight is landing (a fire hit whose effect went off, a Kindling freeze).
        /// If that blow kills, the enemy still died burning for the "killing a burning enemy" talents (Pyre Burst).
        /// </summary>
        public bool BurnPending => burningBlows > 0 || (Health <= 0 && Time.time < burningBlowLingers);
        /// <summary>Burning, or killed by a blow that was lighting it: what kill talents count as a burning enemy.</summary>
        public bool CountsAsBurning => IsBurning || BurnPending;
        private int burningBlows;
        private float burningBlowLingers;

        /// <summary>A blow that lights its victim is about to land (see <see cref="BurnPending"/>); pair with <see cref="EndBurningBlow"/>.</summary>
        public void BeginBurningBlow() => burningBlows++;

        public void EndBurningBlow()
        {
            burningBlows = Mathf.Max(0, burningBlows - 1);
            // A co-op guest only learns of the kill when the host confirms it, so the mark lingers for that long.
            if (Health <= 0 && Run != null && Guest) burningBlowLingers = Time.time + 1f;
        }
        public bool IsBleeding => wounds.Count > 0 || netBleeding;
        public bool IsPoisoned => poisonTicks > 0 || netPoisoned;
        /// <summary>Suffering any damage over time: burning, bleeding or poisoned.</summary>
        public bool HasDamageOverTime => IsBurning || IsBleeding || IsPoisoned;
        /// <summary>The most health this enemy has had: its starting health, since enemies never heal.</summary>
        public int PeakHealth => Mathf.Max(peakHealth, Health);
        public float HealthFraction => PeakHealth > 0 ? Health / (float)PeakHealth : 1f;
        /// <summary>Not yet hurt (Opening Strike).</summary>
        public bool IsUnhurt => Health >= PeakHealth;
        public static readonly Color StunnedTint = new Color(1f, 0.95f, 0.55f), RootedTint = new Color(0.7f, 0.6f, 0.4f);
        public static readonly Color BleedColor = new Color(0.85f, 0.08f, 0.12f), PoisonColor = new Color(0.45f, 0.95f, 0.3f);
        private float stunnedUntil, stunImmuneUntil, rootedUntil, nextPoison;
        /// <summary>Guests: when the host's longest hold on this enemy ends, as of its last snapshot.</summary>
        private float netHoldUntil;
        private int poisonTicks, poisonDamage, peakHealth;
        private bool netBleeding, netPoisoned;
        /// <summary>
        /// Death Mark: every hit taken is remembered. If the enemy dies while marked, all of it bursts out onto every
        /// enemy around it; if it survives, it takes it all again when the mark comes due.
        /// </summary>
        public bool IsDeathMarked => deathMarkDue > 0f;
        public const float DeathMarkTime = 6f, DeathMarkBurstRadius = 2.5f;
        private float deathMarkDue;
        private int deathMarkStored;

        public void DeathMark(float delay)
        {
            if (IsInvulnerable || delay <= 0f) return;
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.DeathMark, 0, transform.position, 0, delay); return; }
            deathMarkDue = Time.time + delay;
            deathMarkStored = 0;
        }

        /// <summary>Host: when the mark comes due, every hit it remembered lands again at once.</summary>
        private void SettleDeathMark()
        {
            if (deathMarkDue <= 0f || Time.time < deathMarkDue) return;
            deathMarkDue = 0f;
            int owed = deathMarkStored;
            deathMarkStored = 0;
            if (owed <= 0) return;
            HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 1.1f, new Color(0.85f, 0.2f, 0.3f), 0.35f);
            HeroVfx.Sparks(Run.ProjectileRoot, transform.position, new Color(0.85f, 0.2f, 0.3f), 16, 5f, 0.35f);
            Hit(owed, transform.position, 0f);
        }

        /// <summary>Hunter's Mark: takes +1 damage from every hit.</summary>
        public bool IsMarked => Time.time < markedUntil;
        private float markedUntil, markedAt;
        public static readonly Color MarkColor = new Color(1f, 0.28f, 0.24f);
        private SpriteRenderer markIndicator;

        public void Mark(float duration)
        {
            if (IsInvulnerable || duration <= 0f) return;
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.Mark, 0, transform.position, 0, duration); }
            if (!IsMarked)
            {
                markedAt = Time.time;
                if (Run.ProjectileRoot != null)
                {
                    CombatVfx.Ring(Run.ProjectileRoot, transform.position, 0.6f, MarkColor, 0.4f);
                    HeroVfx.Sparks(Run.ProjectileRoot, transform.position, MarkColor, 8, 3f, 0.3f);
                }
            }
            markedUntil = Mathf.Max(markedUntil, Time.time + duration);
        }

        private const float MarkSize = 0.5f;

        /// <summary>
        /// Hunter's Mark: a red reticle locks onto the marked enemy, snapping in from large, then turning slowly around
        /// it and blinking faster as the mark runs out.
        /// </summary>
        private void UpdateMarkIndicator()
        {
            bool marked = IsMarked && Health > 0;
            if (markIndicator == null)
            {
                if (!marked) return;
                markIndicator = DungeonVisuals.Create("Hunter's mark", transform, transform.position, Vector2.one * MarkSize, MarkColor, 10);
                markIndicator.sprite = MarkSprite;
            }
            markIndicator.gameObject.SetActive(marked);
            if (!marked) return;
            float lockOn = 1f - Mathf.Clamp01((Time.time - markedAt) / 0.25f);
            float left = markedUntil - Time.time;
            // The reticle hugs the body (bigger for brutes and guardians), and ignores the enemy's own scale.
            float size = (HitRadius * 2f + 0.3f) * (1f + 1.5f * lockOn * lockOn);
            Vector3 parent = transform.lossyScale;
            markIndicator.transform.localScale = new Vector3(size / Mathf.Max(0.001f, parent.x), size / Mathf.Max(0.001f, parent.y), 1f);
            markIndicator.transform.localPosition = Vector3.zero;
            markIndicator.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 45f);
            float blink = left < 1f ? (Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? 0.35f : 1f) : 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            markIndicator.color = FlameMesh.Alpha(MarkColor, blink);
        }

        private static Sprite markSprite;
        /// <summary>A crosshair reticle: four corner brackets and a dot in the middle.</summary>
        private static Sprite MarkSprite => markSprite != null ? markSprite : markSprite = DungeonVisuals.PaletteSprite("Hunter's mark", new[]
        {
            "WWW.....WWW", "W.........W", "W.........W", "...........", ".....W.....", "....WWW....", ".....W.....",
            "...........", "W.........W", "W.........W", "WWW.....WWW"
        }, key => key == 'W' ? Color.white : Color.clear);
        /// <summary>Paralysed enemies cannot move, turn or attack (the Demoness's vital stabs and curses).</summary>
        public bool IsParalyzed => Time.time < paralyzedUntil;
        /// <summary>Cursed enemies take more damage from every hit: <see cref="CurseDamageBonus"/> under the Demon Curse ability, <see cref="LesserCurseDamageBonus"/> under a lesser curse.</summary>
        public bool IsCursed => Time.time < cursedUntil;
        public const float CurseDamageBonus = 0.5f, LesserCurseDamageBonus = 0.25f;
        private float curseBonus = CurseDamageBonus;
        private bool curseShown;
        private float curseShownAt;
        /// <summary>Hex Mastery: how much more damage a curse adds per rank of the talent it was cast with.</summary>
        public const float CurseMasteryBonus = 0.25f;
        private int curseMastery;
        /// <summary>After a paralysis wears off, a guardian shrugs off new ones for this long.</summary>
        public const float BossParalysisImmunity = 5f;
        /// <summary>Guardians shake off crowd control: holds and chills last this fraction as long on them.</summary>
        public const float BossCrowdControlDuration = 0.35f;
        /// <summary>Guardians feel only this fraction of any slow's strength.</summary>
        public const float BossSlowResistance = 0.5f;
        public float ActionSpeedMultiplier => IsHeld ? 0f
            : Slowed((IsChilled ? 0.5f : 1f) * (HolyBubble.SlowsAt(transform.position) ? HolyBubble.SanctuarySlow : 1f) * (Time.time < terrorUntil ? TerrorSlow : 1f));

        /// <summary>A speed factor after the guardian's resistance: a 50% slow only slows a guardian by 25%.</summary>
        private float Slowed(float factor) => Boss != null ? 1f - (1f - factor) * BossSlowResistance : factor;
        public float MoveMultiplier => ActionSpeedMultiplier;
        // Attack timers follow local action time; status durations and damage-over-time use world time.
        public float ActionTime { get; private set; }
        /// <summary>
        /// How fast this enemy's attack clock runs: guardians fight at <see cref="DungeonBoss.AttackPace"/>, and deadlier
        /// worlds (<see cref="WorldDefinition.EnemyTempo"/>) speed every enemy up. Movement and telegraphs are unaffected.
        /// </summary>
        public float Tempo => (Boss != null ? DungeonBoss.AttackPace * (Run != null ? Run.World.GuardianTempo : 1f) : 1f) * (Run != null ? Run.World.EnemyTempo : 1f);
        private float contactReadyAt;
        public bool IsFlashing => Time.time < hitUntil;
        private float chilledUntil, nextBurn, paralyzedUntil, paralysisImmuneUntil, cursedUntil, frozenUntil, freezeImmuneUntil;
        private SpriteRenderer curseIndicator;
        private int burnTicks, burnDamage;
        private Color burnColor = new Color(1f, 0.4f, 0.16f);
        public bool IsRanged => shooter != null;
        public EnemyFacing Facing { get; private set; }
        public EnemyHitRegion LastHitRegion { get; private set; }
        public event System.Action<EnemyHitRegion> HitReceived;

        private void Awake()
        {
            Facing = GetComponent<EnemyFacing>();
            dummy = GetComponent<TrainingDummy>();
        }

        /// <summary>One of the crystal shop's training dummies: it stands still, never fights back and never dies.</summary>
        public bool IsTrainingDummy => dummy != null;
        private TrainingDummy dummy;
        /// <summary>
        /// True when the host drives this enemy and blows on it are reported rather than dealt. Training dummies are each
        /// machine's own (never synced), so even a guest strikes its dummies directly.
        /// </summary>
        private bool Guest => Run.IsGuest && !IsTrainingDummy;
        /// <summary>A training dummy at rest is drawn a touch dim, so the white flash of a hit shows on it.</summary>
        private static readonly Color DummyTint = new Color(0.82f, 0.82f, 0.82f);

        private void Start()
        {
            body = GetComponent<SpriteRenderer>();
            shooter = GetComponent<EnemyShooter>();
            var world = Run.World;
            if (IsRanged && Variant == null) gameObject.name = world.CasterName;
            tactics = GetComponent<EnemyTactics>();
            if (Boss == null && !IsTrainingDummy) body.sprite = Variant != null ? Variant.Sprite
                : world.HighTech ? NeonSprites.Enemy(IsRanged, IsTank) : DungeonVisuals.EnemySprite(IsRanged, IsTank);
            burnIndicator = DungeonVisuals.Create("Burn indicator", transform, transform.position,
                new Vector2(0.28f, 0.4f), burnColor, 9);
            burnIndicator.sprite = DungeonVisuals.FlameSprite;
            burnIndicator.transform.localPosition = new Vector2(0, 0.95f);
            burnIndicator.gameObject.SetActive(IsBurning);
            bleedIndicator = DungeonVisuals.Create("Bleed indicator", transform, transform.position,
                BleedDropSize, BleedColor, 9);
            bleedIndicator.sprite = DungeonVisuals.BloodDropSprite;
            bleedIndicator.gameObject.SetActive(false);
            curseIndicator = DungeonVisuals.Create("Curse indicator", transform, transform.position,
                Vector2.one * CurseSkullSize, HeroBuffs.AscendColor, 10);
            curseIndicator.sprite = DungeonVisuals.SkullSprite;
            curseIndicator.transform.localPosition = new Vector2(0, CurseSkullHeight);
            curseIndicator.gameObject.SetActive(false);
        }

        private static readonly Vector2 BleedDropSize = new Vector2(0.24f, 0.31f);

        /// <summary>The bleed status: blood runs off the enemy's body, and a drop hangs over it, swelling and falling again and again (beside the flame if it burns too).</summary>
        private void UpdateBleedIndicator()
        {
            if (bleedIndicator == null) return;
            bool bleeding = IsBleeding && Health > 0;
            bleedIndicator.gameObject.SetActive(bleeding);
            if (!bleeding) return;
            float drip = Mathf.Repeat(Time.time * 1.4f, 1f), fall = Mathf.Clamp01((drip - 0.5f) / 0.5f);
            bleedIndicator.transform.localPosition = new Vector2(IsBurning ? -0.32f : 0f, 0.95f - 0.3f * fall * fall);
            bleedIndicator.transform.localScale = (Vector3)(BleedDropSize * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(drip / 0.5f))) + Vector3.forward;
            bleedIndicator.color = FlameMesh.Alpha(BleedColor, 1f - fall * fall);
            // Blood keeps running off the body between the cuts, on every machine.
            if (Time.time < nextBleedDrip || Run.ProjectileRoot == null) return;
            nextBleedDrip = Time.time + Random.Range(0.12f, 0.22f);
            Vector2 wound = (Vector2)transform.position + Random.insideUnitCircle * HitRadius * 0.7f;
            HeroVfx.Sparks(Run.ProjectileRoot, wound, BleedColor, 2, 1.6f, 0.35f, Vector2.down, 50f, 0.8f);
        }
        private float nextBleedDrip;

        private const float CurseSkullSize = 0.42f, CurseSkullHeight = 1.4f, CurseSkullPop = 0.3f;

        /// <summary>
        /// A purple skull over a cursed enemy, whatever cursed it: it bursts in large as the curse takes hold, then bobs and
        /// pulses above the enemy until the curse lifts.
        /// </summary>
        private void UpdateCurseIndicator()
        {
            if (curseIndicator == null) return;
            bool cursed = IsCursed;
            curseIndicator.gameObject.SetActive(cursed);
            if (cursed && !curseShown)
            {
                curseShownAt = Time.time;
                var root = Run.ProjectileRoot;
                HeroVfx.Sparks(root, transform.position, HeroBuffs.AscendColor, 8, 3f, 0.35f);
                HeroVfx.Pulse(root, transform.position, 0.7f, HeroBuffs.AscendColor, 0.3f);
            }
            curseShown = cursed;
            if (!cursed) return;
            float pop = 1f - Mathf.Clamp01((Time.time - curseShownAt) / CurseSkullPop);
            curseIndicator.transform.localScale = Vector3.one * CurseSkullSize * (1f + 1.4f * pop * pop);
            curseIndicator.transform.localPosition = new Vector2(0, CurseSkullHeight + 0.06f * Mathf.Sin(Time.time * 4f));
            curseIndicator.color = FlameMesh.Alpha(Color.Lerp(HeroBuffs.AscendColor, DemonessAttack.Pale, 0.6f * pop),
                0.8f + 0.2f * Mathf.Sin(Time.time * 8f));
        }

        private void Update()
        {
            if (!Run.IsPlaying || Health <= 0) return;
            if (Guest) { GuestUpdate(); return; }
            ActionTime += Time.deltaTime * ActionSpeedMultiplier * DreadFactor() * Tempo;
            if (terrorPending && !IsParalyzed) { terrorPending = false; terrorUntil = Time.time + TerrorTime; }
            UpdateCurseIndicator();
            UpdateMarkIndicator();
            if (burnTicks > 0 && Time.time >= nextBurn)
            {
                burnTicks--;
                nextBurn = Time.time + 1f;
                dotTick = true;
                try { Hit(burnDamage); }
                finally { dotTick = false; }
                if (Health <= 0) return;
                CombatVfx.Ring(Run.ProjectileRoot, transform.position, 0.4f, burnColor, 0.2f);
            }
            SettleDeathMark();
            if (Health <= 0) return;
            int bled = TickWounds();
            if (bled > 0)
            {
                HeroVfx.Sparks(Run.ProjectileRoot, transform.position, BleedColor, 5, 2f, 0.3f, Vector2.down, 90f, 0.8f);
                dotTick = true;
                try { Hit(bled, transform.position, 0f); }
                finally { dotTick = false; }
                if (Health <= 0) return;
            }
            if (poisonTicks > 0 && Time.time >= nextPoison)
            {
                poisonTicks--;
                nextPoison = Time.time + 1f;
                HeroVfx.Sparks(Run.ProjectileRoot, transform.position, PoisonColor, 4, 1.4f, 0.4f, Vector2.up, 60f, 0.8f);
                dotTick = true;
                try { Hit(poisonDamage, transform.position, 0f); }
                finally { dotTick = false; }
                if (Health <= 0) return;
            }
            UpdateBleedIndicator();
            if (burnIndicator != null)
            {
                burnIndicator.gameObject.SetActive(IsBurning);
                burnIndicator.color = burnColor;
                burnIndicator.transform.localScale = new Vector3(0.28f, 0.4f, 1f) * (1f + 0.12f * Mathf.Sin(Time.time * 12f));
            }
            if (Boss != null) return;
            if (IsHeld || IsTrainingDummy) { UpdateColor(); return; }
            Vector2 position = transform.position;
            // Heroes in Shadow Veil are invisible: with nobody to see, the enemy holds still and keeps its facing.
            if (!Run.TryNearestVisibleHero(position, out Vector2 target))
            {
                UpdateColor();
                if (!IsRanged) TryContactHit(HitRadius + 0.27f);
                return;
            }
            float distance = Vector2.Distance(position, target);
            bool visible = Run.HasLineOfSight(position, target);
            if (IsRooted)
            {
                if (!IsRanged || !shooter.IsCharging) Facing.TurnToward(target - position, Time.deltaTime * ActionSpeedMultiplier);
                UpdateColor();
                if (!IsRanged) TryContactHit(HitRadius + 0.27f);
                return;
            }
            if (Variant != null && Variant.Move(this, target, visible))
            {
                UpdateColor();
                if (!IsRanged) TryContactHit(HitRadius + 0.27f);
                return;
            }
            if (distance < 14f)
            {
                if (!IsRanged || !shooter.IsCharging) Facing.TurnToward(target - position, Time.deltaTime * ActionSpeedMultiplier);
                Vector2 direction = tactics.Direction(target, visible, IsRanged && shooter.IsCharging);
                Vector2 step = Run.Map.Move(position, direction * Speed * MoveMultiplier * Time.deltaTime, MoveRadius);
                if (!IceWall.BlocksEnemy(this, position, step)) transform.position = step;
            }
            UpdateColor();
            if (!IsRanged) TryContactHit(HitRadius + 0.27f);
        }

        private void UpdateColor()
        {
            body.color = IsFlashing || netFlashing || (((IsRanged && shooter.IsCharging) || (Variant != null && Variant.IsWindingUp)) && !IsHeld) ? Color.white
                : IsParalyzed ? DemonessAttack.ParalyzedTint(Time.time)
                : IsStunned ? Color.Lerp(StunnedTint, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 14f))
                : IsRooted ? RootedTint
                : IsFrozen ? FrozenTint
                : IsChilled ? AbilityCatalog.Ice : Variant != null ? Variant.Tint
                : IsTrainingDummy ? DummyTint : IsTank ? Run.World.BruteTint : IsRanged ? Run.World.CasterTint : Run.World.BasicTint;
        }

        /// <summary>Co-op guest: follow the host's snapshots; only contact with the local hero is judged here.</summary>
        private void GuestUpdate()
        {
            ActionTime += Time.deltaTime * ActionSpeedMultiplier * Tempo;
            UpdateCurseIndicator();
            UpdateMarkIndicator();
            // The host deals the bleeding; here the wounds only count down, so this hero knows what is left of them.
            TickWounds();
            if (hasSnapshot)
                transform.position = Vector2.Distance(transform.position, netPosition) > 2.5f ? netPosition
                    : Vector2.Lerp(transform.position, netPosition, 1f - Mathf.Exp(-14f * Time.deltaTime));
            UpdateBleedIndicator();
            if (burnIndicator != null)
            {
                burnIndicator.gameObject.SetActive(IsBurning);
                burnIndicator.transform.localScale = new Vector3(0.28f, 0.4f, 1f) * (1f + 0.12f * Mathf.Sin(Time.time * 12f));
            }
            if (Boss != null) { if (Boss.DealsContactDamage) TryContactHit(Boss.ContactReach); return; }
            UpdateColor();
            if (!IsRanged) TryContactHit(HitRadius + 0.27f);
        }

        /// <summary>How long a guest trusts its own hits before the host's health overrides them (covers the round trip).</summary>
        private const float GuestPredictionWindow = 0.5f;
        private float lastLocalHitAt = -10f;
        private readonly System.Collections.Generic.List<Renderer> hiddenRenderers = new System.Collections.Generic.List<Renderer>();

        /// <summary>
        /// Co-op guest: the host's view of this enemy. Fresh local hits are kept until the host has had time to apply
        /// them; after that the host's health wins. Without this, hits the host rejected (the boss was untouchable
        /// there, or not cursed) piled up here until the guest saw the boss "die" and vanish while it fought on.
        /// </summary>
        public void ApplySnapshot(EnemySnapshot snapshot)
        {
            netPosition = snapshot.Position;
            if (!hasSnapshot) transform.position = netPosition;
            hasSnapshot = true;
            Facing.Face(snapshot.Facing);
            if (Boss != null && (snapshot.MoreFlags & EnemySnapshot.HasMaxHealth) != 0) Boss.SyncMaxHealth(snapshot.MaxHealth);
            if (snapshot.Health > 0 && Time.time - lastLocalHitAt > GuestPredictionWindow)
            {
                // The host says it is still alive: bring back an enemy this guest wrongly thought it had killed.
                if (Health <= 0) SetVisible(true);
                Health = snapshot.Health;
            }
            else if (Health > 0) Health = Mathf.Max(1, Mathf.Min(Health, snapshot.Health));
            netFlashing = (snapshot.Flags & EnemySnapshot.Flashing) != 0;
            netBurning = (snapshot.Flags & EnemySnapshot.Burning) != 0;
            chilledUntil = (snapshot.Flags & EnemySnapshot.Chilled) != 0 ? Time.time + 0.25f : Mathf.Min(chilledUntil, Time.time);
            paralyzedUntil = (snapshot.MoreFlags & EnemySnapshot.Paralyzed) != 0 ? Time.time + 0.25f : Mathf.Min(paralyzedUntil, Time.time);
            cursedUntil = (snapshot.MoreFlags & EnemySnapshot.Cursed) != 0 ? Time.time + 0.25f : Mathf.Min(cursedUntil, Time.time);
            frozenUntil = (snapshot.MoreFlags & EnemySnapshot.Frozen) != 0 ? Time.time + 0.25f : Mathf.Min(frozenUntil, Time.time);
            stunnedUntil = (snapshot.MoreFlags & EnemySnapshot.Stunned) != 0 ? Time.time + 0.25f : Mathf.Min(stunnedUntil, Time.time);
            rootedUntil = (snapshot.MoreFlags & EnemySnapshot.Rooted) != 0 ? Time.time + 0.25f : Mathf.Min(rootedUntil, Time.time);
            netHoldUntil = (snapshot.MoreFlags & EnemySnapshot.HoldBits) != 0 ? Time.time + snapshot.HoldLeft : Time.time;
            netBleeding = (snapshot.MoreFlags & EnemySnapshot.Bleeding) != 0;
            netPoisoned = (snapshot.MoreFlags & EnemySnapshot.Poisoned) != 0;
            bool charging = (snapshot.Flags & EnemySnapshot.Charging) != 0;
            if (shooter != null) shooter.SetCharging(charging);
            if (Variant != null) Variant.SetNetWindup(charging && (shooter == null || !shooter.IsCharging));
            if (Boss != null) Boss.ApplySnapshot(charging, (byte)(snapshot.Flags >> EnemySnapshot.BossStateShift));
        }

        /// <summary>Contact damage against the local hero (each machine judges its own hero).</summary>
        public void TryContactHit(float reach)
        {
            if (!Run.IsPlaying || Health <= 0 || IsHeld || ActionTime < contactReadyAt) return;
            if (SkeletonMinion.TryBlock(Run, transform.position, reach))
            {
                contactReadyAt = ActionTime + 1f;
                return;
            }
            if (Run.Player.IsInvulnerable || Run.Player.IgnoresContact || Run.Player.Health <= 0
                || Vector2.Distance(transform.position, Run.Player.transform.position) >= reach) return;
            Run.Player.Hit(Boss != null ? Run.World.GuardianHitDamage : 1);
            contactReadyAt = ActionTime + 1f;
        }

        public void Hit(int damage)
        {
            Hit(damage, Run.Player.transform.position);
        }

        public void Hit(int damage, Vector2 source, float knockback = 1f)
        {
            if (Health <= 0) return;
            if (IsInvulnerable) { Boss.Deflect(source); return; }
            peakHealth = Mathf.Max(peakHealth, Health);
            if (IsMarked && damage > 0 && !Guest) damage++;
            // Brittle Ice (the Wizard's second passive): the first blow after a freeze shatters for double. Burn, bleed and
            // poison ticks leave the ice alone.
            if (IsBrittle && damage > 0 && !dotTick)
            {
                brittle = false;
                damage *= 2;
                if (Run.ProjectileRoot != null)
                {
                    HeroVfx.Sparks(Run.ProjectileRoot, transform.position, Color.Lerp(AbilityCatalog.Ice, Color.white, 0.5f), 14, 4.5f, 0.35f);
                    CombatVfx.Ring(Run.ProjectileRoot, transform.position, HitRadius + 0.5f, Color.white, 0.25f);
                }
            }
            if (DebugMode.Enabled && !IsTrainingDummy) damage = Mathf.Max(damage, Health);
            LastHitRegion = Facing.RegionFrom(source);
            HitReceived?.Invoke(LastHitRegion);
            // True Poser: while the Samurai holds her pose, her blows deal nothing and are owed until she sheathes.
            if (damage > 0 && FromLocalHero && Run.Player.Mechanic is TruePoser poser && poser.IsPosing)
            {
                poser.Store(this, damage);
                hitUntil = Time.time + 0.15f;
                return;
            }
            if (Guest)
            {
                // Show the hit now; the host applies it (and any curse) and confirms any kill.
                Run.Coop.ReportDamage(this, CoopDamageKind.Hit, damage, source, knockback: knockback);
                Health = Mathf.Max(0, Health - CursedDamage(damage));
                hitUntil = Time.time + 0.15f;
                lastLocalHitAt = Time.time;
                if (Health <= 0) SetVisible(false);
                return;
            }
            if (dummy != null) dummy.Struck(CursedDamage(damage), dotTick);
            Health -= CursedDamage(damage);
            if (deathMarkDue > 0f) deathMarkStored += CursedDamage(damage);
            hitUntil = Time.time + 0.15f;
            if (Health <= 0)
            {
                if (Run.IsNetworked) Run.Coop.AnnounceKill(this);
                Die(Run.Coop == null || Run.Coop.IsLocalAttacker);
                return;
            }
            Vector2 away = ((Vector2)transform.position - source).normalized;
            if (Boss == null && knockback > 0f)
                transform.position = Run.Map.Move(transform.position, away * (IsTank ? 0.2f : 0.65f) * knockback, MoveRadius);
        }

        // Only renderers that were showing get restored, so indicators and telegraphs keep their own state.
        private void SetVisible(bool value)
        {
            if (!value)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) { renderer.enabled = false; hiddenRenderers.Add(renderer); }
                return;
            }
            foreach (var renderer in hiddenRenderers) if (renderer != null) renderer.enabled = true;
            hiddenRenderers.Clear();
        }

        /// <summary>Host: a marked enemy fell, and every hit it took while marked bursts out onto the enemies around it.</summary>
        private void DeathMarkBurst()
        {
            int owed = deathMarkStored;
            deathMarkDue = 0f;
            deathMarkStored = 0;
            if (owed <= 0) return;
            Vector2 at = transform.position;
            foreach (var enemy in Run.Enemies.ToArray())
                if (enemy != null && enemy != this && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= DeathMarkBurstRadius + enemy.HitRadius)
                    enemy.Hit(owed, at, 1.5f);
        }

        /// <summary>Removes the enemy with its death effects and rewards; kill talents apply only to the killer.</summary>
        public void Die(bool localKill)
        {
            Health = Mathf.Min(Health, 0);
            if (!Guest && deathMarkDue > 0f) DeathMarkBurst();
            Run.EnemyDefeated(this);
            if (localKill && Run.Player.Health > 0) Run.Player.Powerups.OnKill(Run.Player, this);
            Boss?.Defeated();
            if (Variant != null) Variant.OnDeath(this);
            // The Gambler collects a gold coin from every fallen enemy (each machine drops coins for its own hero).
            if (Run.Player != null && Run.Player.Weapon is GamblerAttack) GoldCoin.Drop(Run, transform.position);
            // Demonic Runes (the Demoness's passive): the immobilized may leave a rune behind.
            DemonicRune.TryDrop(Run, this);
            // Salvage (the Augment's passive): his own kills may drop scrap.
            if (localKill) ScrapPickup.TryDrop(Run, this);
            // The Reaper takes the souls of the soul-bound, and fear he has sown spreads from the fallen.
            if (Run.Player != null && Run.Player.Weapon is ReaperAttack reaper) reaper.OnEnemyDied(this, localKill);
            // Crimson Bloom (the Samurai's passive): whoever dies bleeding bursts with the blood it had left to lose; her
            // own kills of the bleeding feed Trail of Blood.
            if (Run.Player != null && Run.Player.Weapon is SamuraiAttack samurai) samurai.OnEnemyDied(this, localKill);
            // The Specimen's chains pass a binding on (Shackles), and kills stretch an upgraded Overdrive.
            if (Run.Player != null && Run.Player.Weapon is SpecimenAttack specimen) specimen.OnEnemyDied(this, localKill);
            // Every fallen enemy leaves crystals for the shop before the next boss. In co-op they are shared: every machine
            // drops the same ones, and whoever picks them up, the whole party is paid (Prospector's extras stay the hero's own).
            if (Run.Player != null)
            {
                Crystal.Drop(Run, transform.position, Crystal.ValueFor(this), Run.IsNetworked ? CoopSync.EnemyKey(NetId) : 0);
                if (Run.Player.Powerups.RollExtraCrystals()) Crystal.Drop(Run, transform.position, Crystal.ValueFor(this));
            }
            CombatVfx.Ring(Run.ProjectileRoot, transform.position, Boss != null ? 1.6f : 0.45f, AbilityCatalog.Gold);
            if (Run.Player != null)
            {
                HeroVfx.Sparks(Run.ProjectileRoot, transform.position, new Color(1f, 0.62f, 0.25f), Boss != null ? 28 : 12,
                    Boss != null ? 6f : 4.2f, Boss != null ? 0.6f : 0.4f);
                HeroVfx.Pulse(Run.ProjectileRoot, transform.position, Boss != null ? 2.4f : 0.8f, AbilityCatalog.Gold, Boss != null ? 0.6f : 0.3f);
            }
            if (Run.ProjectileRoot != null)
                DeathAnimation.Play(transform, Run.ProjectileRoot, Boss != null ? DeathAnimation.BossDuration : DeathAnimation.EnemyDuration);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        public void Chill(float duration)
        {
            if (IsInvulnerable) return;
            TouchWithElement();
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.Chill, 0, transform.position, 0, duration); return; }
            chilledUntil = Mathf.Max(chilledUntil, Time.time + duration * (Boss != null ? BossCrowdControlDuration : 1f));
        }

        /// <summary>Damage after the curse, rounded half up so even a 1-damage hit is worth more on a cursed enemy.</summary>
        public int CursedDamage(int damage) => IsCursed ? Mathf.FloorToInt(damage * (1f + curseBonus + curseMastery * CurseMasteryBonus) + 0.5f) : damage;

        public static readonly Color FrozenTint = new Color(0.72f, 0.93f, 1f);

        /// <summary>
        /// Holds the enemy in place. Guardians are held for a fraction of the time and then resist for a few seconds.
        /// False when nothing took hold (an untouchable or resisting guardian); a co-op guest assumes it lands.
        /// </summary>
        /// <param name="harmless">True when the hold must not set off the hero's damaging hold talents (Sow).</param>
        public bool Paralyze(float duration, bool lingering = false, bool harmless = false)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return false;
            bool fresh = !IsImmobilized;
            duration = HoldTime(duration);
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.Paralyze, 0, transform.position, lingering ? 1 : 0, duration); Held(fresh, duration, harmless); return true; }
            // Lingering Terror: once this paralysis wears off, the enemy stays slowed for a while.
            if (lingering) terrorPending = true;
            if (Boss != null)
            {
                // Covers the paralysis itself too, so repeated stabs cannot chain-lock a guardian.
                if (Time.time < paralysisImmuneUntil) return false;
                duration *= BossCrowdControlDuration;
            }
            paralyzedUntil = Mathf.Max(paralyzedUntil, Time.time + duration);
            if (Boss != null) paralysisImmuneUntil = paralyzedUntil + BossParalysisImmunity;
            Held(fresh, duration, harmless);
            return true;
        }

        /// <summary>Ice freezes the enemy solid. Like paralysis, guardians thaw far faster and then resist for a while.</summary>
        public void Freeze(float duration)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return;
            TouchWithElement();
            bool fresh = !IsImmobilized;
            duration = HoldTime(duration);
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.Freeze, 0, transform.position, 0, duration); Held(fresh, duration); Seized(); MakeBrittle(); return; }
            if (Boss != null)
            {
                if (Time.time < freezeImmuneUntil) return;
                duration *= BossCrowdControlDuration;
            }
            frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
            if (Boss != null) freezeImmuneUntil = frozenUntil + BossParalysisImmunity;
            MakeBrittle();
            if (Run.ProjectileRoot != null)
                HeroVfx.Sparks(Run.ProjectileRoot, transform.position, Color.Lerp(AbilityCatalog.Ice, Color.white, 0.5f), 8, 2.6f, 0.3f);
            Held(fresh, duration);
            Seized();
        }

        /// <summary>
        /// Demonic Power: terror turns the enemy's back on <paramref name="from"/> and paralyses it on the spot.
        /// Returns whether the paralysis took hold.
        /// </summary>
        /// <param name="harmless">True when the fear must deal no damage of its own (Sow): the damaging hold talents stay quiet.</param>
        public bool Fear(Vector2 from, float duration, bool harmless = false)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return false;
            if (Guest)
            {
                bool fresh = !IsImmobilized;
                duration = HoldTime(duration);
                Run.Coop.ReportDamage(this, CoopDamageKind.Fear, 0, from, 0, duration);
                fearedUntil = Mathf.Max(fearedUntil, Time.time + duration * (Boss != null ? BossCrowdControlDuration : 1f));
                Held(fresh, duration, harmless);
                return true;
            }
            Vector2 away = (Vector2)transform.position - from;
            if (away.sqrMagnitude > 0.0001f) Facing.Face(away.normalized);
            if (!Paralyze(duration, false, harmless)) return false;
            fearedUntil = paralyzedUntil;
            return true;
        }

        /// <summary>Afraid: paralysed by <see cref="Fear"/> rather than by any other hold (the Reaper's Reap and Sow look for this).</summary>
        public bool IsFeared => Time.time < fearedUntil;
        /// <summary>Seconds of fear still to run.</summary>
        public float FearRemaining => Mathf.Max(0f, fearedUntil - Time.time);
        private float fearedUntil;
        /// <summary>Fear Incarnate: this enemy gives up a soul when it dies.</summary>
        public bool SoulBound { get; set; }
        /// <summary>Sow: if above zero and the enemy dies afraid, fear of this many seconds spreads to the enemies around it.</summary>
        public float SownFear { get; set; }

        /// <summary>Reap: ends the fear (and the paralysis it holds the enemy with) now and says how long it still had to run.</summary>
        public float ConsumeFear()
        {
            float remaining = Mathf.Max(0f, fearedUntil - Time.time);
            fearedUntil = 0f;
            ConsumeParalysis();
            return remaining;
        }

        public const float TerrorSlow = 0.6f, TerrorTime = 2f, DreadRadius = 3f, DreadSlow = 0.75f;
        private bool terrorPending;
        private float terrorUntil;

        /// <summary>
        /// Nightmare Snap: ends every hold on the enemy now (paralysis, freeze, stun and root) and says how long the
        /// longest of them still had to run.
        /// </summary>
        public float ConsumeHolds()
        {
            float remaining = HoldRemaining;
            if (Guest)
            {
                // A guest only mirrors the host's holds a moment at a time; the host's own count comes with each snapshot.
                remaining = Mathf.Max(remaining, netHoldUntil - Time.time);
                netHoldUntil = Time.time;
                Run.Coop.ReportDamage(this, CoopDamageKind.ClearHolds, 0, transform.position, 0, 0f);
                return remaining;
            }
            paralyzedUntil = Mathf.Min(paralyzedUntil, Time.time);
            frozenUntil = Mathf.Min(frozenUntil, Time.time);
            stunnedUntil = Mathf.Min(stunnedUntil, Time.time);
            rootedUntil = Mathf.Min(rootedUntil, Time.time);
            return remaining;
        }

        /// <summary>Reap: ends the paralysis now and says how long it still had to run.</summary>
        public float ConsumeParalysis()
        {
            float remaining = Mathf.Max(0f, paralyzedUntil - Time.time);
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.ClearParalysis, 0, transform.position, 0, 0f); return remaining; }
            paralyzedUntil = Mathf.Min(paralyzedUntil, Time.time);
            return remaining;
        }

        /// <summary>Dread Aura: enemies near a Demoness who has it act (and so attack) 25% slower.</summary>
        private float DreadFactor()
        {
            var hero = Run.Player;
            return hero != null && hero.Health > 0 && hero.Powerups.Count(PowerupType.DreadAura) > 0
                && Vector2.Distance(transform.position, hero.transform.position) <= DreadRadius ? Slowed(DreadSlow) : 1f;
        }

        /// <summary>Whether the local hero inflicted this status, rather than the host applying a co-op guest's report.</summary>
        private bool FromLocalHero => Run.Player != null && Run.Player.Health > 0 && (Run.Coop == null || Run.Coop.IsLocalAttacker);

        /// <summary>Iron Grip lengthens holds the local hero inflicts; a guest's reports arrive already lengthened.</summary>
        private float HoldTime(float duration) => FromLocalHero ? duration * Run.Player.Powerups.HoldDurationMultiplier : duration;

        /// <summary>A hold took: if it caught the enemy moving freely, the local hero's hold talents go off.</summary>
        private void Held(bool fresh, float duration, bool harmless = false)
        {
            if (fresh && Health > 0 && FromLocalHero) Run.Player.Powerups.OnImmobilized(Run.Player, this, duration, harmless);
        }

        /// <summary>
        /// A freeze, stun or root from the local hero took hold: it feeds their class mechanic (the Demoness's Demonic
        /// Power). Her own paralyses are counted where she inflicts them, and fear never counts.
        /// </summary>
        private void Seized() { if (FromLocalHero) Run.Player.Mechanic?.OnImmobilized(); }

        /// <summary>Brittle Ice (the Wizard's second passive): the next blow this enemy takes deals double damage.</summary>
        public bool IsBrittle => brittle && Health > 0;
        private bool brittle, dotTick;

        /// <summary>A freeze from the local Wizard with Brittle Ice leaves the enemy brittle (each machine marks its own hero's freezes).</summary>
        private void MakeBrittle()
        {
            if (FromLocalHero && Run.Player.ClassWeapon == WeaponType.Staff && Run.Player.Permanent != null && Run.Player.Permanent.HasSecondPassive(WeaponType.Staff))
                brittle = true;
        }

        /// <summary>Breaks the enemy out of its ice at once (Shatter).</summary>
        public void Thaw() { if (!Guest) frozenUntil = Mathf.Min(frozenUntil, Time.time); }

        /// <summary>Stuns the enemy: held like paralysis. Guardians shake stuns off far faster and then resist for a while.</summary>
        public bool Stun(float duration)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return false;
            bool fresh = !IsImmobilized;
            duration = HoldTime(duration);
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.Stun, 0, transform.position, 0, duration); Held(fresh, duration); Seized(); return true; }
            if (Boss != null)
            {
                if (Time.time < stunImmuneUntil) return false;
                duration *= BossCrowdControlDuration;
            }
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
            if (Boss != null) stunImmuneUntil = stunnedUntil + BossParalysisImmunity;
            if (Run.ProjectileRoot != null) HeroVfx.Sparks(Run.ProjectileRoot, (Vector2)transform.position + Vector2.up * 0.5f, StunnedTint, 6, 1.6f, 0.4f);
            Held(fresh, duration);
            Seized();
            return true;
        }

        /// <summary>Roots the enemy in place: it can still turn and attack. Guardians are too massive to root.</summary>
        public bool Root(float duration)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0 || Boss != null) return false;
            bool fresh = !IsImmobilized;
            duration = HoldTime(duration);
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.Root, 0, transform.position, 0, duration); Held(fresh, duration); Seized(); return true; }
            rootedUntil = Mathf.Max(rootedUntil, Time.time + duration);
            Held(fresh, duration);
            Seized();
            return true;
        }

        /// <summary>A bleed runs for this long, cutting this many times, each for this share of the hit that opened it.</summary>
        public const float BleedDuration = 5f, BleedTickShare = 0.1f;
        public const int BleedTicks = 10;

        /// <summary>
        /// One bleed: every tick deals <see cref="PerTick"/> (times <see cref="HeldMultiplier"/> while the enemy is
        /// immobilized), and the fractions add up in <see cref="bleedCarry"/>.
        /// </summary>
        private sealed class Wound
        {
            public float PerTick, Interval, NextAt, HeldMultiplier = 1f;
            public int Ticks, TicksLeft;
        }
        private readonly System.Collections.Generic.List<Wound> wounds = new System.Collections.Generic.List<Wound>();
        private float bleedCarry, bleedStun;

        /// <summary>The damage every open wound still has to deal.</summary>
        public float BleedRemaining
        {
            get
            {
                float total = bleedCarry;
                foreach (var wound in wounds) total += wound.PerTick * wound.TicksLeft * (IsImmobilized ? wound.HeldMultiplier : 1f);
                return wounds.Count > 0 ? total : 0f;
            }
        }

        /// <summary>
        /// Bleeding: <paramref name="ticks"/> cuts over <paramref name="duration"/> seconds, each for a tenth of the
        /// <paramref name="hit"/> that opened the wound. Wounds stack: every bleed runs on its own, and one hit can open several.
        /// With a <paramref name="stun"/> (Bled Dry), the enemy is stunned that long once its last wound closes.
        /// </summary>
        /// <param name="heldMultiplier">Pinned Wounds: how much harder the wound cuts while the enemy is immobilized.</param>
        public void Bleed(int hit, float duration = BleedDuration, int ticks = BleedTicks, float stun = 0f, float heldMultiplier = 1f)
        {
            if (IsInvulnerable || hit <= 0 || ticks <= 0 || duration <= 0f || Health <= 0) return;
            heldMultiplier = Mathf.Max(1f, heldMultiplier);
            // The held multiplier crosses the wire in the message's (otherwise unused) knockback.
            if (Guest) Run.Coop.ReportDamage(this, CoopDamageKind.Bleed, hit, transform.position, ticks, duration, knockback: heldMultiplier);
            // The stun stays on the machine that opened the wound: a guest reports it to the host when its own count runs out.
            bleedStun = Mathf.Max(bleedStun, stun);
            float interval = duration / ticks;
            wounds.Add(new Wound { PerTick = hit * BleedTickShare, Interval = interval, NextAt = Time.time + interval, Ticks = ticks, TicksLeft = ticks,
                HeldMultiplier = heldMultiplier });
        }

        /// <summary>Runs every wound's clock and returns the whole damage that came due (fractions wait for the next tick).</summary>
        private int TickWounds()
        {
            if (wounds.Count == 0) return 0;
            for (int i = wounds.Count - 1; i >= 0; i--)
            {
                var wound = wounds[i];
                while (wound.TicksLeft > 0 && Time.time >= wound.NextAt)
                {
                    wound.TicksLeft--;
                    wound.NextAt += wound.Interval;
                    bleedCarry += wound.PerTick * (IsImmobilized ? wound.HeldMultiplier : 1f);
                }
                if (wound.TicksLeft <= 0) wounds.RemoveAt(i);
            }
            int due = Mathf.FloorToInt(bleedCarry + 0.0001f);
            // The last wound to close pays out what is left of the fractions, rounded.
            if (wounds.Count == 0) { due = Mathf.RoundToInt(bleedCarry); bleedCarry = 0f; BledDry(); }
            else bleedCarry -= due;
            return due;
        }

        /// <summary>Bled Dry: the bleeding has just ended, so the enemy seizes up for as long as its wounds promised.</summary>
        private void BledDry()
        {
            float stun = bleedStun;
            bleedStun = 0f;
            if (stun <= 0f || Health <= 0) return;
            if (Stun(stun) && Run.ProjectileRoot != null) CombatVfx.Ring(Run.ProjectileRoot, transform.position, HitRadius + 0.4f, BleedColor, 0.35f);
        }

        /// <summary>Bloodpop: closes every wound now and says how much damage they still had to deal.</summary>
        public float ConsumeBleed()
        {
            float remaining = BleedRemaining;
            bool bleeding = wounds.Count > 0;
            wounds.Clear();
            bleedCarry = 0f;
            if (bleeding) BledDry();
            if (Guest) Run.Coop.ReportDamage(this, CoopDamageKind.ClearBleed, 0, transform.position, 0, 0f);
            return remaining;
        }

        /// <summary>Bloodscent: every wound starts over, with <paramref name="scale"/> times the cuts (and so the time) it opened with.</summary>
        public void RefreshBleed(float scale)
        {
            if (scale <= 0f || Health <= 0) return;
            // A guest only knows its own wounds; the host restarts everyone's.
            if (Guest) Run.Coop.ReportDamage(this, CoopDamageKind.RefreshBleed, 0, transform.position, 0, scale);
            foreach (var wound in wounds)
            {
                wound.TicksLeft = Mathf.Max(wound.TicksLeft, Mathf.RoundToInt(wound.Ticks * scale));
                wound.NextAt = Time.time + wound.Interval;
            }
        }

        /// <summary>Poisoned: <paramref name="damage"/> a second for <paramref name="ticks"/> seconds; a fresh dose tops up rather than stacks.</summary>
        public void Poison(int ticks, int damage)
        {
            if (IsInvulnerable || ticks <= 0) return;
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.Poison, damage, transform.position, ticks, 0f); return; }
            if (poisonTicks == 0) nextPoison = Time.time + 1f;
            poisonDamage = Mathf.Max(poisonTicks > 0 ? poisonDamage : 0, damage);
            poisonTicks = Mathf.Max(poisonTicks, ticks);
        }

        /// <param name="mastery">Ranks of Hex Mastery behind the curse; a stronger curse is never weakened by a lesser one.</param>
        /// <param name="bonus">The share of extra damage the cursed enemy takes.</param>
        public void Curse(float duration, int mastery = 0, float bonus = CurseDamageBonus)
        {
            if (IsInvulnerable || duration <= 0f) return;
            curseMastery = IsCursed ? Mathf.Max(curseMastery, mastery) : mastery;
            curseBonus = IsCursed ? Mathf.Max(curseBonus, bonus) : bonus;
            // The bonus crosses the wire as a whole percentage in the message's amount.
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.Curse, Mathf.RoundToInt(bonus * 100f), transform.position, mastery, duration); return; }
            cursedUntil = Mathf.Max(cursedUntil, Time.time + duration);
        }

        public void Burn(int ticks, int damage, Color? color = null)
        {
            if (IsInvulnerable) return;
            TouchWithElement();
            if (Guest) { Run.Coop.ReportDamage(this, CoopDamageKind.Burn, damage, transform.position, ticks, 0f, color); return; }
            if (burnTicks == 0)
            {
                nextBurn = Time.time + 1f;
                burnDamage = damage;
            }
            else burnDamage = Mathf.Max(burnDamage, damage);
            burnTicks = Mathf.Max(burnTicks, ticks);
            burnColor = color ?? new Color(1f, 0.4f, 0.16f);
            if (burnIndicator != null)
            {
                burnIndicator.color = burnColor;
                burnIndicator.gameObject.SetActive(IsBurning);
            }
        }
    }
}

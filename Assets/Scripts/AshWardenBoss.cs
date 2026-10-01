using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The first guardian, the Rime Warden: a slow, crowned frost caster drawing, in a shuffled order, an aimed Icicle
    /// Fan, a full Frost Nova, a rotating Blizzard Spiral, Hailfall (hailstones crash onto every hero) and Ice Cage (a
    /// ring of icicles closes in on every hero). Once bloodied it blinks across the arena before Hailfall.
    /// It paces itself: a clear pause follows every attack, and after every third it stands winded for a few seconds,
    /// which is the opening to strike.
    /// In the Neon Arcology it returns as the Cryo Sentinel, a chrome frost machine with the same attacks.
    /// (The class keeps its original Ash Warden name so the boss roster and asset references stay put.)
    /// </summary>
    public sealed class AshWardenBoss : BossBehaviour
    {
        private const int Patterns = 5;
        private const int Fan = 0, Nova = 1, Spiral = 2, Hailfall = 3, Cage = 4;
        // Cage icicles start this far out and creep in slowly enough to find the gap.
        private const float CageRadius = 5.5f, CageBoltSpeed = 3.4f;
        // Spiral bolts drift slower than aimed ones so the rotating arms can be read and walked around.
        private const float SpiralBoltSpeed = 5f, SpiralDuration = 1.8f, SpiralShotInterval = 0.2f;
        // The pause after each attack, and the longer one after every AttacksPerBreath-th (shorter once bloodied).
        private const float Recovery = 1.6f, EnragedRecovery = 1.1f, WindedTime = 2.6f, EnragedWindedTime = 1.8f;
        private const int AttacksPerBreath = 3;
        /// <summary>Bit of <see cref="NetState"/> set while the Warden stands winded.</summary>
        private const byte WindedBit = 8;
        public const float Size = 1.8f;
        public static readonly Color Frost = new Color(0.55f, 0.85f, 1f);
        public static readonly Color Glacier = new Color(0.3f, 0.5f, 1f);
        private float readyAt, fireAt, spiralUntil, nextSpiralShot, spiralAngle, windedUntil;
        private bool charging, guestWinded;
        private int pattern, attacksSinceBreath;
        private readonly List<Vector2> cageCenters = new List<Vector2>();
        private readonly AttackDeck<int> deck = new AttackDeck<int>(Fan, Nova, Spiral, Hailfall, Cage);
        private Vector2 lockedAim;
        private WardenAura aura;

        public override string Title => TitleFor(HighTech);
        /// <summary>The guardian's name in a high-tech world or not (the encyclopedia reads it outside a fight).</summary>
        public static string TitleFor(bool highTech) => highTech ? "THE CRYO SENTINEL" : "THE RIME WARDEN";
        public override string Tell => IsCharging ? (pattern % Patterns) switch
        {
            Fan => "ICICLE FAN - SIDESTEP",
            Nova => "FROST NOVA - KEEP MOVING",
            Spiral => "BLIZZARD SPIRAL - CIRCLE WITH THE ARMS",
            Cage => "ICE CAGE - SLIP THROUGH THE GAP",
            _ => "HAILFALL - LEAVE THE MARKS"
        } : IsWinded ? "WINDED - STRIKE NOW" : IsEnraged ? "ENRAGED" : HighTech ? "CRYOGENIC CONTAINMENT UNIT" : "GUARDIAN OF THE RELIC";
        public override int BaseHealth(int floor) => 24 + floor * 3;
        public override bool IsCharging => charging || Spiraling;
        public override float HitRadius => 0.95f;
        public override float ContactReach => 1.15f;
        public override byte NetState => (byte)(pattern % Patterns | (IsWinded ? WindedBit : 0));
        public int Pattern => pattern % Patterns;
        /// <summary>Catching its breath after a run of attacks: it neither moves nor attacks.</summary>
        public bool IsWinded => Run.IsGuest ? guestWinded : !charging && !Spiraling && Enemy.ActionTime < windedUntil;
        public bool Spiraling => spiralUntil > Enemy.ActionTime;
        protected override BoltKind Bolts => BoltKind.Frost;
        protected override HazardStyle Hazards => HazardStyle.Frost;

        protected override void OnSetup()
        {
            Enemy.Speed = 2f;
            transform.localScale = Vector2.one * Size;
            readyAt = Enemy.ActionTime + 1.2f;
            // Always opens with the Icicle Fan so the fight starts with something readable.
            pattern = deck.Open(Fan);
            DungeonVisuals.DecorateWarden(transform, HighTech);
            aura = WardenAura.Attach(this);
            HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 3f, Frost, 0.8f);
        }

        public override Color BodyColor() => Flashing(HighTech ? (IsEnraged ? new Color(0.5f, 0.55f, 0.95f) : new Color(0.7f, 0.75f, 0.86f))
            : IsEnraged ? new Color(0.35f, 0.55f, 1f) : new Color(0.62f, 0.8f, 0.98f), Color.white, charging);

        public override void HostTick(Vector2 offset)
        {
            Enemy.Facing.TurnToward(offset, Time.deltaTime * Enemy.ActionSpeedMultiplier);
            if (Spiraling) { SpiralTick(); return; }
            if (charging)
            {
                if (Enemy.ActionTime < fireAt) return;
                charging = false;
                Release(offset);
            }
            else if (Enemy.ActionTime >= readyAt)
                BeginWindup(offset);
            else if (!IsWinded && offset.magnitude > 2f)
                transform.position = Run.Map.Move(transform.position, offset.normalized * Enemy.Speed * Enemy.MoveMultiplier * Time.deltaTime);
        }

        private void BeginWindup(Vector2 offset)
        {
            if (pattern % Patterns == Hailfall && IsEnraged) Blink(offset);
            charging = true;
            lockedAim = offset.sqrMagnitude > 0.01f ? offset.normalized : Vector2.down;
            fireAt = Enemy.ActionTime + 0.85f;
            CombatVfx.Ring(Run.ProjectileRoot, transform.position, 1.5f, Frost, 0.85f);
            CoopFx.Pulse(Run, transform.position, 1.8f, Glacier, 0.5f);
            cageCenters.Clear();
            if (pattern % Patterns == Cage)
                foreach (var hero in LivingHeroPositions())
                {
                    cageCenters.Add(hero);
                    CombatVfx.Ring(Run.ProjectileRoot, hero, CageRadius, Frost, 0.85f);
                    CoopFx.Ring(Run, hero, CageRadius, Frost, 0.85f);
                }
        }

        private void Release(Vector2 offset)
        {
            switch (pattern % Patterns)
            {
                case Fan:
                case Nova:
                    int shots = pattern % Patterns == Fan ? (IsEnraged ? 7 : 5) : (IsEnraged ? 14 : 10);
                    for (int i = 0; i < shots; i++)
                    {
                        float angle = pattern % Patterns == Fan ? (i - (shots - 1) * 0.5f) * 14f : i * 360f / shots;
                        Fire(transform.position, Quaternion.Euler(0, 0, angle) * lockedAim);
                    }
                    HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 1.6f, Frost, 0.35f);
                    Finish();
                    break;
                case Spiral:
                    spiralUntil = Enemy.ActionTime + SpiralDuration;
                    spiralAngle = Vector2.SignedAngle(Vector2.up, lockedAim);
                    nextSpiralShot = Enemy.ActionTime;
                    break;
                case Cage:
                    foreach (var center in cageCenters) CastCage(center);
                    HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 1.6f, Frost, 0.35f);
                    Finish();
                    break;
                default:
                    CastHailfall();
                    Finish();
                    break;
            }
        }

        private void Finish()
        {
            pattern = deck.Draw();
            readyAt = Enemy.ActionTime + (IsEnraged ? EnragedRecovery : Recovery);
            if (++attacksSinceBreath < AttacksPerBreath) return;
            // Three attacks in, it has to catch its breath.
            attacksSinceBreath = 0;
            readyAt = windedUntil = Enemy.ActionTime + (IsEnraged ? EnragedWindedTime : WindedTime);
            HeroVfx.Motes(Run.ProjectileRoot, transform.position, 1.2f, Frost, 10, 1f);
        }

        /// <summary>Two (bloodied: three) arms of icicles sweep around the Warden.</summary>
        private void SpiralTick()
        {
            while (Enemy.ActionTime >= nextSpiralShot && Spiraling)
            {
                nextSpiralShot += SpiralShotInterval;
                // The arms turn as far per second as before, with fewer icicles along them.
                spiralAngle += IsEnraged ? 18f : 15f;
                int arms = IsEnraged ? 3 : 2;
                for (int i = 0; i < arms; i++)
                    Fire(transform.position, Quaternion.Euler(0, 0, spiralAngle + i * 360f / arms) * Vector2.up, SpiralBoltSpeed);
            }
            if (!Spiraling)
            {
                spiralUntil = 0f;
                Finish();
            }
        }

        /// <summary>Hailstones crash onto every hero plus a few loose spots, each leaving a short-lived patch of biting frost.</summary>
        private void CastHailfall()
        {
            float telegraph = 1.1f, linger = IsEnraged ? 2.2f : 1.6f;
            foreach (var hero in LivingHeroPositions()) Hazard(HazardShape.Pool, hero, Vector2.up, 1.2f, 0f, telegraph, linger);
            var arena = DungeonMap.Arena;
            int extra = IsEnraged ? 4 : 2;
            for (int i = 0; i < extra; i++)
            {
                var spot = new Vector2(Random.Range(arena.xMin + 1f, arena.xMax - 2f), Random.Range(arena.yMin + 1f, arena.yMax - 2f));
                Hazard(HazardShape.Pool, spot, Vector2.up, 1.2f, 0f, telegraph + 0.2f + i * 0.15f, linger);
            }
        }

        /// <summary>
        /// Icicles appear in the ring marked around a hero at the windup and drift inward. A gap of missing icicles
        /// (narrower once bloodied) is the way out; stepping out of the ring before it closes works too.
        /// </summary>
        private void CastCage(Vector2 center)
        {
            int count = IsEnraged ? 22 : 18, gap = IsEnraged ? 2 : 3;
            int gapStart = Random.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                if ((i - gapStart + count) % count < gap) continue;
                Vector2 outward = FlameMesh.Polar(i * 2f * Mathf.PI / count, 1f);
                Vector2 spot = center + outward * CageRadius;
                // Fire() launches half a unit ahead of its origin, so back the origin off to keep the ring true.
                if (Run.Map.CanStand(spot, 0.11f)) Fire(spot + outward * 0.5f, -outward, CageBoltSpeed);
            }
        }

        /// <summary>Bloodied: shatters into a flurry of snow and reforms elsewhere in the arena, away from the heroes.</summary>
        private void Blink(Vector2 offset)
        {
            Vector2 hero = (Vector2)transform.position + offset, from = transform.position, best = from;
            float bestDistance = 0f;
            var arena = DungeonMap.Arena;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 candidate = DungeonMap.ClampToArena(new Vector2(Random.Range(arena.xMin, arena.xMax), Random.Range(arena.yMin, arena.yMax)), 2f);
                float distance = Vector2.Distance(candidate, hero);
                if (distance > bestDistance && distance < 9f) { best = candidate; bestDistance = distance; }
            }
            if (bestDistance < 3f) return;
            transform.position = best;
            foreach (var point in new[] { from, best })
            {
                HeroVfx.Pulse(Run.ProjectileRoot, point, 1.8f, Glacier, 0.45f);
                HeroVfx.Sparks(Run.ProjectileRoot, point, Frost, 14, 4f, 0.45f, null, 360f, 1.2f);
                CoopFx.Pulse(Run, point, 1.8f, Glacier, 0.45f);
            }
        }

        public override void ApplyNetState(bool isCharging, byte state)
        {
            charging = isCharging;
            pattern = (state & ~WindedBit) % Patterns;
            guestWinded = (state & WindedBit) != 0;
        }

        public override void OnDefeated()
        {
            ScreenFx.Shake(0.4f, 0.7f);
            for (int i = 0; i < 3; i++)
                HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 1.8f + i * 1.4f, i == 1 ? AbilityCatalog.Gold : Glacier, 0.5f + i * 0.25f);
            HeroVfx.Sparks(Run.ProjectileRoot, transform.position, Frost, 30, 7f, 0.8f, null, 360f, 1.5f);
            if (aura != null) Destroy(aura.gameObject);
        }
    }
}

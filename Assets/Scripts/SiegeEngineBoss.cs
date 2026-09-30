using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Area denial artillery, with a broadside that leaves a safe central corridor. Between volleys Bastion stamps the
    /// ground: an earthquake cracks open a band of the arena (two when enraged) and seals it off for a few seconds,
    /// squeezing the heroes into what is left while the guns keep firing.
    /// </summary>
    public sealed class SiegeEngineBoss : NeonBossBehaviour
    {
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "BASTION, THE SIEGE ENGINE";
        public override string Title => FixedTitle;
        protected override Color Accent => new Color(1f, 0.65f, 0.15f);
        protected override Sprite Chassis => GuardianSprites.Bastion;
        protected override Sprite Details => GuardianSprites.BastionDetails;
        protected override Color BodyTint => new Color(0.55f, 0.52f, 0.5f);
        protected override HazardStyle Hazards => HazardStyle.Artillery;
        protected override float Scale => 2.2f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells = { "MISSILE WALK - KEEP MOVING", "CLUSTER MINES - FIND CLEAR GROUND", "BROADSIDE - STAY BETWEEN THE RAILS" };
        public override int BaseHealth(int floor) => 28 + floor * 2;

        // ---------------------------------------------------------------- earthquakes

        /// <summary>The arena splits into this many bands across; a quake seals the band the nearest hero stands in.</summary>
        public const int QuakeBands = 4;
        /// <summary>Real seconds of cracking-ground warning before a band is sealed: enough to walk (or roll) out of it.</summary>
        public const float QuakeWarning = 1.6f;
        /// <summary>How long a sealed band stays deadly, in real seconds (longer while enraged).</summary>
        public const float QuakeHold = 4f, EnragedQuakeHold = 5f;
        /// <summary>Attack-clock seconds from one quake to the next, and the first one's delay into the fight.</summary>
        public const float QuakeInterval = 10f, FirstQuake = 5f;
        /// <summary>Real seconds the whole arena stays open between one quake's end and the next quake's warning.</summary>
        public const float QuakeGap = 1.5f;
        /// <summary>The spare snapshot bit (the Arcology's states only use 0-4) that tells co-op guests a quake is coming.</summary>
        private const byte QuakeBit = 8;
        private float nextQuake, quakeWarningUntil, quakeClearAt;
        private int quakes;

        public bool IsQuakeWarning => Time.time < quakeWarningUntil;
        public override byte NetState => (byte)(state | (IsQuakeWarning ? QuakeBit : 0));
        public override string Tell => IsQuakeWarning ? "EARTHQUAKE - GET OFF THE CRACKING GROUND" : base.Tell;

        protected override void OnSetup()
        {
            base.OnSetup();
            nextQuake = Enemy.ActionTime + FirstQuake;
        }

        public override void HostTick(Vector2 toHero)
        {
            base.HostTick(toHero);
            // The next quake waits for the attack clock and for the last sealed band to reopen, so bands never stack up.
            if (Enemy.ActionTime < nextQuake || Time.time < quakeClearAt + QuakeGap || !Run.IsPlaying) return;
            Quake();
            nextQuake = Enemy.ActionTime + QuakeInterval;
        }

        /// <summary>Co-op guest: the quake warning rides in the snapshot's spare bit; the rest is the Arcology's state.</summary>
        public override void ApplyNetState(bool charging, byte value)
        {
            quakeWarningUntil = (value & QuakeBit) != 0 ? Time.time + 0.5f : 0f;
            base.ApplyNetState(charging, (byte)(value & ~QuakeBit));
        }

        private void Quake()
        {
            var arena = DungeonMap.Arena;
            // Alternate between bands running up and down the arena and bands running across it.
            bool vertical = quakes++ % 2 == 0;
            float start = vertical ? arena.xMin - 0.5f : arena.yMin - 0.5f;
            float band = (vertical ? arena.width : arena.height) / (float)QuakeBands;
            float length = vertical ? arena.height : arena.width;
            Vector2 hero = Run.NearestHero(transform.position);
            int under = Mathf.Clamp(Mathf.FloorToInt(((vertical ? hero.x : hero.y) - start) / band), 0, QuakeBands - 1);
            float hold = IsEnraged ? EnragedQuakeHold : QuakeHold;
            SealBand(under, vertical, start, band, length, hold);
            // Enraged, a far band cracks too; the bands beside the hero's always stay open, so there is somewhere to go.
            if (IsEnraged) SealBand(under <= 1 ? QuakeBands - 1 : 0, vertical, start, band, length, hold);
            quakeWarningUntil = Time.time + QuakeWarning;
            quakeClearAt = Time.time + QuakeWarning + hold;
            Vector2 ground = GroundPosition;
            ScreenFx.Shake(0.35f, QuakeWarning);
            QuakeVfx.Play(Run.ProjectileRoot, ground, 5f, 4, 0.18f, Accent);
            CoopFx.Quake(Run, ground, 5f, 4, 0.18f, Accent);
        }

        /// <summary>One band of the arena cracks, then stays sealed: standing in it keeps hurting until it reopens.</summary>
        private void SealBand(int index, bool vertical, float start, float band, float length, float hold)
        {
            var arena = DungeonMap.Arena;
            float middle = start + (index + 0.5f) * band;
            Vector2 from = vertical ? new Vector2(middle, arena.yMin - 0.5f) : new Vector2(arena.xMin - 0.5f, middle);
            Hazard(HazardShape.Beam, from, vertical ? Vector2.up : Vector2.right, length, band, QuakeWarning, hold);
        }

        protected override float Attack(int index, Vector2 aim)
        {
            if (index == 0)
            {
                foreach (Vector2 hero in LivingHeroPositions())
                    for (int i = 0; i < (IsEnraged ? 5 : 4); i++)
                        Hazard(HazardShape.Pool, hero + aim * (i - 1) * 2.4f, aim, 1.25f, 0f, 1f + i * 0.4f, 0.6f);
                return 3.5f;
            }
            if (index == 1)
            {
                Vector2 target = Run.NearestHero(transform.position);
                for (int i = 0; i < 6; i++)
                    Hazard(HazardShape.Pool, target + FlameMesh.Polar(i * Mathf.PI / 3f, 3.5f), aim, IsEnraged ? 1.5f : 1.2f, 0f, 1.3f, 2.5f);
                return 4.2f;
            }
            Vector2 side = Vector2.Perpendicular(aim);
            for (int i = -1; i <= 1; i += 2)
                Hazard(HazardShape.Beam, (Vector2)transform.position + side * i * 2f, aim, 22f, 1.4f, 1.2f, 1.1f);
            if (IsEnraged)
                Hazard(HazardShape.Pool, Run.NearestHero(transform.position), aim, 1.2f, 0f, 2.4f, 0.5f);
            return 3.3f;
        }
        protected override void DrawCircuitry(FlameMesh mesh, Vector2 center, float intensity)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    Vector2 start = center + new Vector2(side * 1.25f, i * 0.3f - 0.5f);
                    mesh.Bar(start, Vector2.right * side, 0.35f + 0.15f * Mathf.Sin(Time.time * 12f + i), 0.12f,
                        FlameMesh.Alpha(Accent, intensity), FlameMesh.Alpha(Accent, 0.1f));
                }
        }
    }
}

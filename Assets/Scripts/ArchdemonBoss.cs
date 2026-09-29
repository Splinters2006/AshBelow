using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The third guardian, an archdemon of hellfire. Draws Infernal Cross, Brimstone Rain, Hellfire Nova and
    /// Serpent's Wake (eruptions racing toward every hero) in a shuffled order, and every fourth attack casts Cataclysm: the whole arena erupts except one small circle while he takes to the sky,
    /// invulnerable, and bombards the party. He crashes down afterwards and is briefly staggered.
    /// </summary>
    public sealed class ArchdemonBoss : BossBehaviour
    {
        private enum State : byte { Idle, Cross, Meteors, Nova, Ascend, Bombard, Descend, Staggered, Wake }
        private enum Attack { Cross, Meteors, Nova, Cataclysm, Wake }
        private const int CataclysmEvery = 4;
        private const float WakeStep = 1.25f, WakeLength = 19f;
        private readonly AttackDeck<Attack> deck = new AttackDeck<Attack>(Attack.Cross, Attack.Meteors, Attack.Nova, Attack.Wake);
        private const float FlightHeight = 2.6f, InfernoTelegraph = 4.6f, InfernoDuration = 6.5f;
        // The Cataclysm barrage is much slower than a regular bolt so it can be read and weaved through.
        private const float BombardBoltSpeed = 3.6f;
        public const float Size = 2.6f;
        public static readonly Color Hellfire = new Color(1f, 0.32f, 0.05f);
        private State state;
        private int rotation;
        private float stateUntil, readyAt, landAt, nextVolley, nextSpiral, spiralAngle, altitude, eruptAt, safeRadius, orbitAngle;
        private bool forcedCataclysm;
        private Vector2 ground, descendFrom, landing, safeCenter;
        private HellfireAura aura;

        public override string Title => "MALPHAS, THE HELLFIRE ARCHDEMON";
        public override string Tell => state switch
        {
            State.Cross => "INFERNAL CROSS - GET OFF THE LINES",
            State.Meteors => "BRIMSTONE RAIN - LEAVE THE MARKS",
            State.Nova => "HELLFIRE NOVA - ROLL THROUGH THE FLAMES",
            State.Wake => "SERPENT'S WAKE - STEP OFF THE PATH",
            State.Ascend or State.Bombard when Time.time < eruptAt => $"CATACLYSM IN {eruptAt - Time.time:0.0}s - GET INTO THE GOLD CIRCLE",
            State.Ascend or State.Bombard => "CATACLYSM - STAY IN THE CIRCLE  /  HE IS UNTOUCHABLE",
            State.Descend => "HE FALLS - MOVE",
            State.Staggered => "STAGGERED - PUNISH HIM",
            _ => IsEnraged ? "THE PIT OPENS WIDER" : "LORD OF THE BURNING PIT"
        };
        public override int BaseHealth(int floor) => 30 + floor * 4;
        public override byte NetState => (byte)state;
        public override bool IsCharging => state == State.Cross || state == State.Meteors || state == State.Nova || state == State.Wake || state == State.Descend;
        public override bool IsInvulnerable => state == State.Ascend || state == State.Bombard;
        public override bool DealsContactDamage => altitude < 0.4f;
        public override float HitRadius => 1f;
        public override float ContactReach => 1.3f;
        public override Vector2 GroundPosition => (Vector2)transform.position - Vector2.up * altitude;
        public float Altitude => altitude;
        private bool Airborne => state == State.Ascend || state == State.Bombard;
        private const float DescentTime = 0.8f;
        // He glides to the landing mark first, then drops out of the sky onto it.
        private float WantedAltitude => Airborne || (state == State.Descend && stateUntil - Time.time > 0.3f) ? FlightHeight : 0f;

        protected override void OnSetup()
        {
            Enemy.Speed = 1.3f;
            transform.localScale = Vector2.one * Size;
            ground = transform.position;
            GetComponent<SpriteRenderer>().sprite = DungeonVisuals.BossSprite(BossKind.Archdemon);
            DungeonVisuals.DecorateArchdemon(transform);
            aura = HellfireAura.Attach(this);
            readyAt = Enemy.ActionTime + 2.5f;
            HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 4f, Hellfire, 1f);
        }

        public override Color BodyColor()
        {
            if (Airborne) return Color.Lerp(new Color(1f, 0.45f, 0.2f), FlameMesh.Core, 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
            if (state == State.Staggered) return Color.Lerp(new Color(0.45f, 0.1f, 0.12f), Color.white, Enemy.IsFlashing ? 0.6f : 0f);
            return Flashing(IsEnraged ? new Color(1f, 0.2f, 0.12f) : new Color(0.72f, 0.12f, 0.14f), Hellfire, IsCharging);
        }

        private float Tempo => IsEnraged ? 1.25f : 1f;

        public override void HostTick(Vector2 toHero)
        {
            float dt = Time.deltaTime * Enemy.ActionSpeedMultiplier;
            Vector2 target = Run.NearestHero(ground);
            Vector2 toTarget = target - ground;
            Enemy.Facing.TurnToward(toTarget, dt);
            switch (state)
            {
                case State.Idle:
                    if (toTarget.magnitude > 3.5f)
                        ground = Run.Map.Move(ground, toTarget.normalized * Enemy.Speed * dt, 0.5f);
                    if (Enemy.ActionTime >= readyAt) BeginAttack(target);
                    break;
                case State.Cross:
                case State.Meteors:
                case State.Nova:
                case State.Wake:
                    if (Enemy.ActionTime >= stateUntil) Idle(1.6f);
                    break;
                case State.Ascend:
                    // Flight runs on world time so it stays in step with the inferno (chill can't stretch it).
                    ground = Vector2.MoveTowards(ground, HoverSpot(), 4.5f * Time.deltaTime);
                    if (Time.time >= stateUntil) { state = State.Bombard; nextVolley = Time.time + 0.3f; nextSpiral = Time.time + 1f; }
                    break;
                case State.Bombard:
                    // He circles the sanctuary from outside, raining bolts into it.
                    orbitAngle += 14f * Time.deltaTime;
                    ground = Vector2.MoveTowards(ground, HoverSpot(), 3f * Time.deltaTime);
                    Bombard(target);
                    if (Time.time >= landAt) BeginDescent(target);
                    break;
                case State.Descend:
                    ground = Vector2.Lerp(descendFrom, landing, Mathf.SmoothStep(0f, 1f, 1f - (stateUntil - Time.time) / DescentTime));
                    if (Time.time >= stateUntil) { ground = landing; Slam(); }
                    break;
                case State.Staggered:
                    if (Enemy.ActionTime >= stateUntil) Idle(1f);
                    break;
            }
            altitude = Mathf.MoveTowards(altitude, WantedAltitude, Time.deltaTime * (state == State.Descend ? 9f : 2.4f));
            transform.position = ground + Vector2.up * altitude;
        }

        private void Idle(float delay)
        {
            state = State.Idle;
            readyAt = Enemy.ActionTime + delay / Tempo;
        }

        private void BeginAttack(Vector2 target)
        {
            // The first time he is bloodied, the sky splits immediately, and the count to the next one starts over.
            bool forced = !forcedCataclysm && IsEnraged;
            if (forced) { forcedCataclysm = true; rotation = 0; }
            Attack attack = forced || ++rotation % CataclysmEvery == 0 ? Attack.Cataclysm : deck.Draw();
            switch (attack)
            {
                case Attack.Cross: InfernalCross(target); break;
                case Attack.Meteors: BrimstoneRain(); break;
                case Attack.Nova: HellfireNova(); break;
                case Attack.Wake: SerpentsWake(); break;
                default: Cataclysm(); break;
            }
            CoopFx.Pulse(Run, ground, 2.2f, Hellfire, 0.5f);
            HeroVfx.Pulse(Run.ProjectileRoot, ground, 2.2f, Hellfire, 0.5f);
        }

        /// <summary>Four (bloodied: eight) pillars of fire burst outward from him in a cross.</summary>
        private void InfernalCross(Vector2 target)
        {
            state = State.Cross;
            Vector2 aim = (target - ground).sqrMagnitude > 0.01f ? (target - ground).normalized : Vector2.up;
            for (int i = 0; i < 4; i++)
                Hazard(HazardShape.Beam, ground, Quaternion.Euler(0, 0, i * 90f) * aim, 18f, 1.6f, 1.05f, 0.75f);
            if (IsEnraged)
                for (int i = 0; i < 4; i++)
                    Hazard(HazardShape.Beam, ground, Quaternion.Euler(0, 0, 45f + i * 90f) * aim, 18f, 1.6f, 1.95f, 0.75f);
            stateUntil = Enemy.ActionTime + (IsEnraged ? 2.4f : 1.6f);
        }

        /// <summary>Meteors fall on every hero plus random spots, each leaving a burning crater.</summary>
        private void BrimstoneRain()
        {
            state = State.Meteors;
            AddMeteor(Run.Player.transform.position, 0f, Run.Player.Health > 0);
            if (Run.IsNetworked)
                foreach (var hero in Run.Coop.RemoteHeroes)
                    if (hero != null && hero.IsAlive) AddMeteor(hero.transform.position, 0.15f, true);
            int extra = IsEnraged ? 9 : 6;
            for (int i = 0; i < extra; i++)
            {
                var arena = DungeonMap.Arena;
                AddMeteor(new Vector2(Random.Range(arena.xMin + 1f, arena.xMax - 2f), Random.Range(arena.yMin + 1f, arena.yMax - 2f)), 0.25f + i * 0.12f, true);
            }
            stateUntil = Enemy.ActionTime + 1.2f;
        }

        private void AddMeteor(Vector2 at, float delay, bool enabled)
        {
            if (enabled) Hazard(HazardShape.Pool, at, Vector2.up, 1.5f, 0f, 1.25f + delay, IsEnraged ? 3.2f : 2.4f);
        }

        /// <summary>Walls of fire expand outward from him; too fast to outrun, so heroes must roll through.</summary>
        private void HellfireNova()
        {
            state = State.Nova;
            int rings = IsEnraged ? 3 : 2;
            for (int i = 0; i < rings; i++)
                Hazard(HazardShape.Ring, ground, Vector2.up, 17f, 0.9f, 0.9f + i * 0.85f, 2.4f);
            stateUntil = Enemy.ActionTime + 1.4f;
        }

        /// <summary>
        /// A serpent of eruptions bursts out of the ground from him toward every hero, one crater after another, too
        /// fast to outrun along its path. Bloodied, two more serpents fan out to either side.
        /// </summary>
        private void SerpentsWake()
        {
            state = State.Wake;
            foreach (var hero in LivingHeroPositions())
            {
                Vector2 aim = (hero - ground).sqrMagnitude > 0.01f ? (hero - ground).normalized : Vector2.down;
                SerpentLine(aim);
                if (IsEnraged)
                {
                    SerpentLine(Quaternion.Euler(0, 0, 28f) * aim);
                    SerpentLine(Quaternion.Euler(0, 0, -28f) * aim);
                }
            }
            stateUntil = Enemy.ActionTime + 1.8f;
        }

        private void SerpentLine(Vector2 aim)
        {
            for (int i = 1; i * WakeStep <= WakeLength; i++)
            {
                Vector2 at = ground + aim * (i * WakeStep);
                if (!Run.Map.CanStand(at, 0.3f)) break;
                Hazard(HazardShape.Pool, at, Vector2.up, 1.05f, 0f, 0.75f + i * 0.08f, 0.6f);
            }
        }

        /// <summary>The whole arena erupts except one small sanctuary while he flies above, untouchable.</summary>
        private void Cataclysm()
        {
            state = State.Ascend;
            float radius = (IsEnraged ? 4.2f : 4.7f) + 0.5f * (Run.PartySize - 1);
            var arena = DungeonMap.Arena;
            Vector2 safe = DungeonMap.ClampToArena(new Vector2(Random.Range(arena.xMin, arena.xMax), Random.Range(arena.yMin, arena.yMax)), radius + 1.5f);
            Hazard(HazardShape.Inferno, safe, Vector2.up, radius, 0f, InfernoTelegraph, InfernoDuration);
            safeCenter = safe;
            safeRadius = radius;
            Vector2 away = ground - safe;
            orbitAngle = away.sqrMagnitude > 0.01f ? Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg : 90f;
            stateUntil = Time.time + 1.1f;
            eruptAt = Time.time + InfernoTelegraph;
            landAt = Time.time + InfernoTelegraph + InfernoDuration;
            CoopFx.Pulse(Run, ground, 5f, Hellfire, 0.9f);
            HeroVfx.Pulse(Run.ProjectileRoot, ground, 5f, Hellfire, 0.9f);
        }

        /// <summary>
        /// Where he hovers during Cataclysm: on the orbit angle (or the nearest angle that fits in the arena), far
        /// enough outside the gold circle that neither his body in the sky nor his shadow overlaps it.
        /// </summary>
        private Vector2 HoverSpot()
        {
            var arena = DungeonMap.Arena;
            for (int step = 0; step <= 12; step++)
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    float angle = orbitAngle + sign * step * 15f;
                    Vector2 body = safeCenter + FlameMesh.Polar(angle * Mathf.Deg2Rad, safeRadius + 2.4f);
                    Vector2 shadow = DungeonMap.ClampToArena(body - Vector2.up * FlightHeight, 1.5f);
                    body = shadow + Vector2.up * FlightHeight;
                    bool bodyInView = body.y <= arena.yMax - 0.5f && body.x >= arena.xMin && body.x <= arena.xMax - 1f;
                    if (bodyInView && Vector2.Distance(body, safeCenter) >= safeRadius + 1.6f && Vector2.Distance(shadow, safeCenter) >= safeRadius + 0.8f)
                    {
                        orbitAngle = angle;
                        return shadow;
                    }
                    if (step == 0) break;
                }
            return ground;
        }

        private void Bombard(Vector2 target)
        {
            Vector2 from = ground;
            if (Time.time >= nextVolley)
            {
                nextVolley = Time.time + (IsEnraged ? 0.5f : 0.65f);
                Vector2 aim = (target - from).sqrMagnitude > 0.01f ? (target - from).normalized : Vector2.down;
                int shots = IsEnraged ? 5 : 3;
                for (int i = 0; i < shots; i++) Fire(from, Quaternion.Euler(0, 0, (i - (shots - 1) * 0.5f) * 11f) * aim, BombardBoltSpeed);
                CombatVfx.GlowBolt(Run.ProjectileRoot, transform.position, from, Hellfire);
                CoopFx.Bolt(Run, transform.position, from, Hellfire, true);
            }
            if (Time.time >= nextSpiral)
            {
                nextSpiral = Time.time + (IsEnraged ? 1.1f : 1.5f);
                spiralAngle += 17f;
                for (int i = 0; i < 10; i++) Fire(from, Quaternion.Euler(0, 0, spiralAngle + i * 36f) * Vector2.up, BombardBoltSpeed);
            }
        }

        private void BeginDescent(Vector2 target)
        {
            state = State.Descend;
            descendFrom = ground;
            landing = DungeonMap.ClampToArena(target, 1.5f);
            // The landing spot is marked just before he crashes into it.
            Hazard(HazardShape.Pool, landing, Vector2.up, 2.4f, 0f, DescentTime, 0.4f);
            stateUntil = Time.time + DescentTime;
        }

        private void Slam()
        {
            state = State.Staggered;
            ScreenFx.Shake(0.45f, 0.5f);
            HeroVfx.Sparks(Run.ProjectileRoot, ground, Hellfire, 26, 7f, 0.5f, null, 360f, 1.5f);
            stateUntil = Enemy.ActionTime + (IsEnraged ? 2.2f : 3f);
            for (int i = 0; i < 16; i++) Fire(ground, Quaternion.Euler(0, 0, i * 22.5f) * Vector2.up);
        }

        public override void VisualTick()
        {
            // Guests follow the host's transform; the shadow and aura only need a matching altitude.
            if (Run.IsGuest) altitude = Mathf.MoveTowards(altitude, Airborne ? FlightHeight : 0f, Time.deltaTime * (state == State.Descend ? 4f : 2.4f));
        }

        public override void ApplyNetState(bool charging, byte netState)
        {
            if (netState > (byte)State.Wake) return;
            var next = (State)netState;
            // Guests start their countdown when the host announces the Cataclysm.
            if (next == State.Ascend && state != State.Ascend) eruptAt = Time.time + InfernoTelegraph;
            if (next == State.Staggered && state != State.Staggered) altitude = 0f;
            state = next;
        }

        public override void OnDefeated()
        {
            ScreenFx.Shake(0.7f, 1.2f);
            ScreenFx.Flash(new Color(1f, 0.55f, 0.15f, 0.7f), 0.9f);
            for (int i = 0; i < 3; i++)
                HeroVfx.Pulse(Run.ProjectileRoot, GroundPosition, 2.5f + i * 2f, i == 1 ? AbilityCatalog.Gold : Hellfire, 0.6f + i * 0.3f);
            HeroVfx.Sparks(Run.ProjectileRoot, GroundPosition, Hellfire, 40, 9f, 0.9f, null, 360f, 1.8f);
            if (aura != null) Destroy(aura.gameObject);
        }
    }
}

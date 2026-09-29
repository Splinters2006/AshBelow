using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The second guardian: fragile but relentlessly mobile. Circles the party and rotates between chained dash
    /// strikes, a fan of thrown blades, and a Phantom Strike (vanishes, reappears behind a hero and whirls).
    /// Sidesteps out of combos when struck. Punish the recovery after each dash chain or whirl.
    /// </summary>
    public sealed class DuelistBoss : BossBehaviour
    {
        private enum State : byte { Stalk, Aim, Dash, Evade, Recover, Throw, Vanish, Whirl }
        private enum Attack { Dashes, BladeFan, Phantom }
        private static readonly Attack[] Rotation = { Attack.Dashes, Attack.BladeFan, Attack.Dashes, Attack.Phantom };
        private const float DashLength = 8f, DashSpeed = 26f, EvadeLength = 3.4f, EvadeSpeed = 20f;
        private const float ThrowWindup = 0.55f, BladeSpeed = 9f, VanishTime = 0.75f, WhirlTime = 0.45f, WhirlRadius = 2f;
        private static readonly Color Steel = new Color(0.78f, 0.84f, 0.95f);
        private static readonly Color Blade = new Color(0.45f, 0.95f, 1f);
        private State state;
        private float stateUntil, readyAt, evadeReadyAt, nextGhost, dashTravelled, strafeSign = 1f, strafeFlipAt;
        private int dashesLeft, phantomsLeft, rotation;
        private Vector2 dashDirection;
        private SpriteRenderer telegraph, body;
        private FlameMesh marks;
        private GameObject markObject;

        public override string Title => "THE ASHEN DUELIST";
        public override string Tell => state switch
        {
            State.Aim => "DASH STRIKE - STEP OFF THE LINE",
            State.Dash => "DASHING",
            State.Evade => "SIDESTEP",
            State.Recover => "WINDED - PUNISH NOW",
            State.Throw => "BLADE FAN - SLIP BETWEEN THE BLADES",
            State.Vanish => "PHANTOM STRIKE - LEAVE THE RING",
            State.Whirl => "WHIRLWIND",
            _ => IsEnraged ? "BLOODIED - FASTER CHAINS" : "A BLUR OF STEEL"
        };
        // Squishy: roughly half the Warden's health at the same depth.
        public override int BaseHealth(int floor) => 12 + floor * 2;
        public override bool IsCharging => state == State.Aim || state == State.Throw || state == State.Vanish;
        public override bool IsInvulnerable => state == State.Vanish;
        public override bool DealsContactDamage => state != State.Vanish;
        public override byte NetState => (byte)state;
        public override float HitRadius => 0.6f;
        public override float ContactReach => state == State.Whirl ? WhirlRadius : state == State.Dash ? 1.15f : 0.9f;

        protected override void OnSetup()
        {
            Enemy.Speed = 3.8f;
            transform.localScale = Vector2.one * 1.15f;
            body = GetComponent<SpriteRenderer>();
            body.sprite = DungeonVisuals.BossSprite(BossKind.Duelist);
            DungeonVisuals.DecorateDuelist(transform);
            telegraph = DungeonVisuals.Create("Dash telegraph", Run.ProjectileRoot, transform.position, new Vector2(DashLength, 0.9f), Blade, 2);
            telegraph.gameObject.SetActive(false);
            markObject = new GameObject("Phantom strike telegraph");
            markObject.transform.SetParent(Run.ProjectileRoot, false);
            marks = new FlameMesh(markObject, 2);
            readyAt = Enemy.ActionTime + 1.6f;
            Enemy.HitReceived += OnHit;
        }

        public override Color BodyColor()
        {
            var color = Flashing(IsEnraged ? new Color(1f, 0.55f, 0.6f) : Steel, Blade, state == State.Aim || state == State.Throw);
            // Half out of the world while he phases toward his mark.
            if (state == State.Vanish) color.a = 0.12f + 0.1f * Mathf.Sin(Time.time * 40f);
            return color;
        }

        private float Tempo => IsEnraged ? 1.3f : 1f;

        public override void HostTick(Vector2 toHero)
        {
            float dt = Time.deltaTime * Enemy.ActionSpeedMultiplier;
            switch (state)
            {
                case State.Stalk:
                    Enemy.Facing.TurnToward(toHero, dt * 2f);
                    Stalk(toHero, dt);
                    if (Enemy.ActionTime >= readyAt) BeginAttack(toHero);
                    break;
                case State.Aim:
                    // Tracks the target for most of the windup, then locks so the line can be read and dodged.
                    if (stateUntil - Enemy.ActionTime > 0.2f && toHero.sqrMagnitude > 0.01f) dashDirection = toHero.normalized;
                    Enemy.Facing.Face(dashDirection);
                    if (Enemy.ActionTime >= stateUntil) BeginDash();
                    break;
                case State.Dash:
                case State.Evade:
                    float speed = state == State.Dash ? DashSpeed : EvadeSpeed;
                    Vector2 before = transform.position;
                    transform.position = Run.Map.Move(before, dashDirection * speed * dt, Enemy.MoveRadius);
                    float moved = Vector2.Distance(before, transform.position);
                    dashTravelled += moved;
                    bool blocked = moved < speed * dt * 0.3f;
                    if (dashTravelled >= (state == State.Dash ? DashLength : EvadeLength) || blocked) EndDash(toHero);
                    break;
                case State.Throw:
                    Enemy.Facing.TurnToward(toHero, dt * 3f);
                    if (Enemy.ActionTime >= stateUntil) ThrowBlades(toHero);
                    break;
                case State.Vanish:
                    if (Enemy.ActionTime >= stateUntil) BeginWhirl();
                    break;
                case State.Whirl:
                    if (Enemy.ActionTime >= stateUntil) EndWhirl(toHero);
                    break;
                case State.Recover:
                    if (Enemy.ActionTime >= stateUntil)
                    {
                        state = State.Stalk;
                        readyAt = Enemy.ActionTime + 2.1f / Tempo;
                    }
                    break;
            }
        }

        private void BeginAttack(Vector2 toHero)
        {
            switch (Rotation[rotation++ % Rotation.Length])
            {
                case Attack.BladeFan:
                    state = State.Throw;
                    stateUntil = Enemy.ActionTime + ThrowWindup / Tempo;
                    break;
                case Attack.Phantom:
                    phantomsLeft = IsEnraged ? 2 : 1;
                    BeginVanish();
                    break;
                default:
                    dashesLeft = IsEnraged ? 4 : 3;
                    BeginAim(toHero, 0.55f);
                    break;
            }
        }

        private void Stalk(Vector2 toHero, float dt)
        {
            float distance = toHero.magnitude;
            if (distance < 0.01f) return;
            if (Enemy.ActionTime >= strafeFlipAt)
            {
                strafeSign = Random.value < 0.5f ? -1f : 1f;
                strafeFlipAt = Enemy.ActionTime + Random.Range(0.8f, 1.8f);
            }
            Vector2 radial = toHero / distance;
            Vector2 move = Vector2.Perpendicular(radial) * strafeSign + radial * Mathf.Clamp(distance - 4f, -1f, 1f);
            Vector2 before = transform.position;
            transform.position = Run.Map.Move(before, move.normalized * Enemy.Speed * Tempo * dt, Enemy.MoveRadius);
            if (Vector2.Distance(before, transform.position) < Enemy.Speed * dt * 0.2f) strafeSign = -strafeSign;
        }

        private void BeginAim(Vector2 toHero, float windup)
        {
            state = State.Aim;
            dashDirection = toHero.sqrMagnitude > 0.01f ? toHero.normalized : Enemy.Facing.Direction;
            stateUntil = Enemy.ActionTime + windup / Tempo;
        }

        private void BeginDash()
        {
            state = State.Dash;
            dashTravelled = 0f;
            HeroVfx.Sparks(Run.ProjectileRoot, transform.position, Blade, 10, 4f, 0.3f, -dashDirection, 80f);
            CoopFx.Pulse(Run, transform.position, 1f, Blade, 0.25f);
        }

        private void EndDash(Vector2 toHero)
        {
            HeroVfx.Slash(Run.ProjectileRoot, transform.position, dashDirection, 1.4f, 200f, Blade, 0.22f);
            CoopFx.Slash(Run, transform.position, dashDirection, 1.4f, 200f, Blade);
            if (state == State.Evade)
            {
                state = State.Stalk;
                return;
            }
            dashesLeft--;
            if (dashesLeft > 0) { BeginAim(toHero, 0.32f); return; }
            if (IsEnraged)
                // The final cut of a bloodied chain scatters a ring of blade shards.
                for (int i = 0; i < 10; i++) Fire(transform.position, Quaternion.Euler(0, 0, i * 36f) * dashDirection);
            Recover(IsEnraged ? 1.1f : 1.5f);
        }

        /// <summary>A fan of thrown blades; bloodied, a second fan follows offset into the gaps.</summary>
        private void ThrowBlades(Vector2 toHero)
        {
            Vector2 aim = toHero.sqrMagnitude > 0.01f ? toHero.normalized : Enemy.Facing.Direction;
            int count = IsEnraged ? 7 : 5;
            for (int i = 0; i < count; i++)
                Fire(transform.position, Quaternion.Euler(0, 0, (i - (count - 1) * 0.5f) * 14f) * aim, BladeSpeed);
            if (IsEnraged)
                for (int i = 0; i < count - 1; i++)
                    Fire(transform.position, Quaternion.Euler(0, 0, (i - (count - 2) * 0.5f) * 14f) * aim, BladeSpeed * 0.7f);
            HeroVfx.Slash(Run.ProjectileRoot, transform.position, aim, 1.2f, 120f, Blade, 0.2f);
            CoopFx.Slash(Run, transform.position, aim, 1.2f, 120f, Blade);
            state = State.Stalk;
            readyAt = Enemy.ActionTime + 1.4f / Tempo;
        }

        /// <summary>Fades out and reappears just behind the nearest hero, marking the ring he is about to whirl through.</summary>
        private void BeginVanish()
        {
            Vector2 from = transform.position, hero = Run.NearestHero(from);
            Vector2 away = hero - from;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector2.up;
            Vector2 landing = from;
            // Behind the hero if there is room, otherwise to either side, otherwise stay put.
            foreach (var offset in new[] { away, Vector2.Perpendicular(away), -Vector2.Perpendicular(away) })
            {
                Vector2 spot = hero + offset * 1.4f;
                if (Run.Map.CanStand(spot, Enemy.MoveRadius)) { landing = spot; break; }
            }
            HeroVfx.Sparks(Run.ProjectileRoot, from, Blade, 14, 4f, 0.35f, null, 360f);
            CoopFx.Pulse(Run, from, 1.2f, Blade, 0.3f);
            transform.position = landing;
            state = State.Vanish;
            stateUntil = Enemy.ActionTime + VanishTime / Tempo;
        }

        private void BeginWhirl()
        {
            state = State.Whirl;
            stateUntil = Enemy.ActionTime + WhirlTime;
            for (int i = 0; i < 3; i++)
            {
                Vector2 aim = Quaternion.Euler(0, 0, i * 120f) * Vector2.up;
                HeroVfx.Slash(Run.ProjectileRoot, transform.position, aim, WhirlRadius, 150f, Blade, 0.3f);
                CoopFx.Slash(Run, transform.position, aim, WhirlRadius, 150f, Blade);
            }
            ScreenFx.Shake(0.15f, 0.2f);
        }

        private void EndWhirl(Vector2 toHero)
        {
            if (--phantomsLeft > 0) { BeginVanish(); return; }
            Recover(IsEnraged ? 1f : 1.3f);
        }

        private void Recover(float duration)
        {
            state = State.Recover;
            stateUntil = Enemy.ActionTime + duration;
        }

        /// <summary>Host only: sometimes slips away from a combo with a sidestep.</summary>
        private void OnHit(EnemyHitRegion region)
        {
            if (Run.IsGuest || state != State.Stalk || Enemy.ActionTime < evadeReadyAt || Enemy.Health <= 0) return;
            evadeReadyAt = Enemy.ActionTime + (IsEnraged ? 1.8f : 2.6f);
            if (Random.value > 0.6f) return;
            Vector2 toHero = Run.NearestHero(transform.position) - (Vector2)transform.position;
            Vector2 side = Vector2.Perpendicular(toHero.sqrMagnitude > 0.01f ? toHero.normalized : Vector2.up) * (Random.value < 0.5f ? -1f : 1f);
            dashDirection = (side - toHero.normalized * 0.4f).normalized;
            dashTravelled = 0f;
            state = State.Evade;
        }

        public override void VisualTick()
        {
            bool aiming = state == State.Aim;
            telegraph.gameObject.SetActive(aiming);
            if (aiming)
            {
                // Guests read the dash line from the synced facing.
                Vector2 direction = Run.IsGuest ? Enemy.Facing.Direction : dashDirection;
                telegraph.transform.position = (Vector2)transform.position + direction * DashLength * 0.5f;
                telegraph.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                var color = Blade;
                color.a = 0.18f + 0.22f * (0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
                telegraph.color = color;
            }
            DrawMarks();
            if ((state == State.Dash || state == State.Evade || state == State.Whirl) && Time.time >= nextGhost)
            {
                nextGhost = Time.time + 0.03f;
                var ghost = DungeonVisuals.Create("Duelist afterimage", Run.ProjectileRoot, transform.position, transform.localScale,
                    new Color(Blade.r, Blade.g, Blade.b, 0.55f), 3);
                ghost.sprite = body.sprite;
                ghost.flipX = body.flipX;
                if (state == State.Whirl) ghost.transform.rotation = Quaternion.Euler(0, 0, Time.time * 1400f);
                ghost.gameObject.AddComponent<FadingSprite>().Duration = 0.22f;
            }
        }

        /// <summary>The Phantom Strike ring on the floor, and the fan of blade lines while he winds up a throw.</summary>
        private void DrawMarks()
        {
            if (marks == null) return;
            marks.Begin();
            Vector2 center = transform.position;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 24f);
            if (state == State.Vanish)
            {
                marks.Disc(center, WhirlRadius, FlameMesh.Alpha(Blade, 0.12f + 0.12f * pulse), FlameMesh.Alpha(Blade, 0.3f));
                marks.Ring(center, WhirlRadius, 0.1f, FlameMesh.Alpha(Blade, 0.6f + 0.4f * pulse), 56);
                for (int i = 0; i < 8; i++)
                {
                    float angle = Time.time * 3f + i * Mathf.PI / 4f;
                    marks.Diamond(center + FlameMesh.Polar(angle, WhirlRadius - 0.3f), 0.12f, FlameMesh.Alpha(Color.white, 0.8f));
                }
            }
            else if (state == State.Throw)
            {
                Vector2 aim = Enemy.Facing.Direction;
                int count = IsEnraged ? 7 : 5;
                for (int i = 0; i < count; i++)
                    marks.Bar(center, Quaternion.Euler(0, 0, (i - (count - 1) * 0.5f) * 14f) * aim, 5f, 0.08f,
                        FlameMesh.Alpha(Blade, 0.25f + 0.35f * pulse), FlameMesh.Alpha(Blade, 0f));
            }
            marks.Commit();
        }

        public override void ApplyNetState(bool charging, byte netState)
        {
            if (netState <= (byte)State.Whirl) state = (State)netState;
        }

        public override void OnDefeated() => ClearTelegraphs();

        private void ClearTelegraphs()
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
            if (marks == null) return;
            marks.Release();
            marks = null;
            if (markObject != null) Destroy(markObject);
        }

        private void OnDestroy()
        {
            if (Boss != null && Enemy != null) Enemy.HitReceived -= OnHit;
            ClearTelegraphs();
        }
    }
}

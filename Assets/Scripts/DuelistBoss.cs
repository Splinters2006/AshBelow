using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The second guardian: fragile but relentlessly mobile. Circles the party, chains telegraphed dash strikes,
    /// and sidesteps out of combos when struck. Punish the recovery after each dash chain.
    /// </summary>
    public sealed class DuelistBoss : BossBehaviour
    {
        private enum State : byte { Stalk, Aim, Dash, Evade, Recover }
        private const float DashLength = 8f, DashSpeed = 26f, EvadeLength = 3.4f, EvadeSpeed = 20f;
        private static readonly Color Steel = new Color(0.78f, 0.84f, 0.95f);
        private static readonly Color Blade = new Color(0.45f, 0.95f, 1f);
        private State state;
        private float stateUntil, readyAt, evadeReadyAt, nextGhost, dashTravelled, strafeSign = 1f, strafeFlipAt;
        private int dashesLeft;
        private Vector2 dashDirection;
        private SpriteRenderer telegraph, body;

        public override string Title => "THE ASHEN DUELIST";
        public override string Tell => state == State.Aim ? "DASH STRIKE - STEP OFF THE LINE"
            : state == State.Dash ? "DASHING" : state == State.Evade ? "SIDESTEP"
            : state == State.Recover ? "WINDED - PUNISH NOW" : IsEnraged ? "BLOODIED - FASTER CHAINS" : "A BLUR OF STEEL";
        // Squishy: roughly half the Warden's health at the same depth.
        public override int BaseHealth(int floor) => 12 + floor * 2;
        public override bool IsCharging => state == State.Aim;
        public override byte NetState => (byte)state;
        public override float HitRadius => 0.6f;
        public override float ContactReach => state == State.Dash ? 1.15f : 0.9f;

        protected override void OnSetup()
        {
            Enemy.Speed = 3.8f;
            transform.localScale = Vector2.one * 1.15f;
            body = GetComponent<SpriteRenderer>();
            body.sprite = DungeonVisuals.BossSprite(BossKind.Duelist);
            DungeonVisuals.DecorateDuelist(transform);
            telegraph = DungeonVisuals.Create("Dash telegraph", Run.ProjectileRoot, transform.position, new Vector2(DashLength, 0.9f), Blade, 2);
            telegraph.gameObject.SetActive(false);
            readyAt = Enemy.ActionTime + 1.6f;
            Enemy.HitReceived += OnHit;
        }

        public override Color BodyColor() => Flashing(IsEnraged ? new Color(1f, 0.55f, 0.6f) : Steel, Blade, state == State.Aim);

        private float Tempo => IsEnraged ? 1.3f : 1f;

        public override void HostTick(Vector2 toHero)
        {
            float dt = Time.deltaTime * Enemy.ActionSpeedMultiplier;
            switch (state)
            {
                case State.Stalk:
                    Enemy.Facing.TurnToward(toHero, dt * 2f);
                    Stalk(toHero, dt);
                    if (Enemy.ActionTime >= readyAt)
                    {
                        dashesLeft = IsEnraged ? 4 : 3;
                        BeginAim(toHero, 0.55f);
                    }
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
                case State.Recover:
                    if (Enemy.ActionTime >= stateUntil)
                    {
                        state = State.Stalk;
                        readyAt = Enemy.ActionTime + 2.1f / Tempo;
                    }
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
            state = State.Recover;
            stateUntil = Enemy.ActionTime + (IsEnraged ? 1.1f : 1.5f);
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
            if ((state == State.Dash || state == State.Evade) && Time.time >= nextGhost)
            {
                nextGhost = Time.time + 0.03f;
                var ghost = DungeonVisuals.Create("Duelist afterimage", Run.ProjectileRoot, transform.position, transform.localScale,
                    new Color(Blade.r, Blade.g, Blade.b, 0.55f), 3);
                ghost.sprite = body.sprite;
                ghost.flipX = body.flipX;
                ghost.gameObject.AddComponent<FadingSprite>().Duration = 0.22f;
            }
        }

        public override void ApplyNetState(bool charging, byte netState)
        {
            if (netState <= (byte)State.Recover) state = (State)netState;
        }

        public override void OnDefeated() { if (telegraph != null) Destroy(telegraph.gameObject); }

        private void OnDestroy()
        {
            if (Boss != null && Enemy != null) Enemy.HitReceived -= OnHit;
            if (telegraph != null) Destroy(telegraph.gameObject);
        }
    }
}

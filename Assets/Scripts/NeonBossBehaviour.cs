using UnityEngine;

namespace Slopgame
{
    /// <summary>Shared pacing and replicated state for the Arcology's three distinct machines.</summary>
    public abstract class NeonBossBehaviour : BossBehaviour
    {
        // 0: approach, 1-3: attack, 4: exposed recovery. All fit the snapshot's four bits.
        protected byte state;
        protected float until;
        private int nextAttack;
        private GameObject effectObject;
        private FlameMesh effect;
        protected abstract Color Accent { get; }
        protected abstract Sprite Chassis { get; }
        /// <summary>The fixed-colour layer over the tinted chassis: lenses, neon lights, warheads and stripes.</summary>
        protected abstract Sprite Details { get; }
        /// <summary>The chassis's base metal; enraged it glows toward <see cref="Accent"/>, and it flashes white while venting.</summary>
        protected abstract Color BodyTint { get; }
        protected abstract string[] AttackTells { get; }
        protected abstract float Attack(int index, Vector2 aim);
        protected virtual void AttackTick() { }
        protected virtual float Scale => 1.8f;
        protected override BoltKind Bolts => BoltKind.Plasma;
        public override byte NetState => state;
        public override bool IsCharging => state >= 1 && state <= 3;
        public override bool DealsContactDamage => false;
        public override string Tell => state == 4 ? "VENTING - ATTACK NOW" : state == 0
            ? (IsEnraged ? "OVERCLOCK ACTIVE" : "ACQUIRING TARGETS") : AttackTells[Mathf.Clamp(state - 1, 0, 2)];

        protected override void OnSetup()
        {
            transform.localScale = Vector3.one * Scale;
            GetComponent<SpriteRenderer>().sprite = Chassis;
            BossArt.AddDetails(transform, Title + " details", Details);
            Enemy.Speed = 2.4f;
            until = Enemy.ActionTime + 1.1f;
            effectObject = new GameObject(Title + " circuitry");
            effectObject.transform.SetParent(Run.ProjectileRoot, false);
            effect = new FlameMesh(effectObject, 5);
        }

        public override Color BodyColor() => Flashing(IsEnraged ? Color.Lerp(BodyTint, Accent, 0.3f) : BodyTint, Color.white, state == 4);
        public override void HostTick(Vector2 toHero)
        {
            Enemy.Facing.TurnToward(toHero, Time.deltaTime * Enemy.ActionSpeedMultiplier * 3f);
            if (state == 0 && toHero.magnitude > 4.5f)
                transform.position = Run.Map.Move(transform.position, toHero.normalized * Enemy.Speed * Time.deltaTime * Enemy.ActionSpeedMultiplier, Enemy.MoveRadius);
            if (state >= 1 && state <= 3) AttackTick();
            if (Enemy.ActionTime < until) return;
            if (state == 0)
            {
                int attack = nextAttack++ % 3;
                state = (byte)(attack + 1);
                until = Enemy.ActionTime + Attack(attack, toHero.sqrMagnitude > 0.01f ? toHero.normalized : Vector2.up);
            }
            else if (state == 4) { state = 0; until = Enemy.ActionTime + (IsEnraged ? 0.4f : 0.7f); }
            else { state = 4; until = Enemy.ActionTime + 1.1f; }
        }

        public override void ApplyNetState(bool charging, byte value) { if (value <= 4) state = value; }
        public override void VisualTick()
        {
            if (effect == null) return;
            effect.Begin();
            DrawCircuitry(effect, transform.position, IsCharging ? 1f : 0.45f);
            effect.Commit();
        }
        protected abstract void DrawCircuitry(FlameMesh mesh, Vector2 center, float intensity);
        protected void Beam(Vector2 center, Vector2 direction, float length, float delay, float width = 0.8f)
            => Hazard(HazardShape.Beam, center - direction * length * 0.5f, direction, length, width, delay, 0.45f);
        public override void OnDefeated() => ClearEffect();
        private void OnDestroy() => ClearEffect();
        private void ClearEffect()
        {
            if (effect == null) return;
            effect.Release(); effect = null;
            if (effectObject != null) Destroy(effectObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Shared pacing for guardians who hold court (the Infernal Court's and the Arcane Spire's): approach, a run of
    /// attacks each followed by a spent moment to punish, and every third attack a summoning of the world's minions.
    /// </summary>
    public abstract class CourtBossBehaviour : BossBehaviour
    {
        public const int MaxMinions = 4;
        public const float SummonTime = 1.2f, SummonRadius = 2.6f;
        // 0: approach, 1-AttackCount: attack, Spent (open to punishment), Summoning. All fit the snapshot's four bits.
        public const byte Spent = 14, Summoning = 15;
        protected byte state;
        protected float until;
        private float attackStart;
        private int nextAttack;
        private bool summonedThisCycle;
        private readonly List<Vector2> summonSpots = new List<Vector2>();
        private GameObject auraObject;
        private FlameMesh aura;

        protected abstract Color Accent { get; }
        protected abstract Sprite Body { get; }
        /// <summary>The fixed-colour layer drawn over the tinted body: faces, gold, gems, horns and magma.</summary>
        protected abstract Sprite Details { get; }
        /// <summary>The body's base colour; enraged it warms toward <see cref="Accent"/>, and it flashes the accent when spent.</summary>
        protected abstract Color BodyTint { get; }
        /// <summary>One tell per attack; there are as many attacks as tells (at most 13).</summary>
        protected abstract string[] AttackTells { get; }
        public int AttackCount => AttackTells.Length;
        /// <summary>The tell while the guardian closes in.</summary>
        protected abstract string ApproachTell { get; }
        /// <summary>How close the guardian walks before it stops to attack.</summary>
        protected virtual float ApproachDistance => 4f;
        /// <summary>The minion kinds summoned each time (see <see cref="DungeonRun.MinionBasic"/>); enraged adds another of the first.</summary>
        protected abstract byte[] Minions { get; }
        /// <summary>Starts an attack; returns how long it lasts.</summary>
        protected abstract float Attack(int index, Vector2 aim);
        /// <summary>Runs every frame of an attack with the seconds since it began.</summary>
        protected virtual void AttackTick(float time) { }
        protected abstract void DrawAura(FlameMesh mesh, Vector2 center, float intensity);
        /// <summary>Which attack comes next, given how many have been made; in order by default.</summary>
        protected virtual int NextAttack(int made) => made % AttackCount;
        protected virtual float Scale => 2f;
        protected virtual string SummonTell => "SUMMONING THE COURT";
        public override byte NetState => state;
        protected bool Attacking => state >= 1 && state <= AttackCount;
        public override bool IsCharging => Attacking || state == Summoning;
        public override string Tell => state == Spent ? "SPENT - STRIKE NOW" : state == Summoning ? SummonTell
            : state == 0 ? ApproachTell : AttackTells[Mathf.Clamp(state - 1, 0, AttackCount - 1)];

        protected override void OnSetup()
        {
            transform.localScale = Vector3.one * Scale;
            var body = GetComponent<SpriteRenderer>();
            body.sprite = Body;
            BossArt.AddDetails(transform, Title + " details", Details);
            Enemy.Speed = 2.3f;
            until = Enemy.ActionTime + 1.2f;
            auraObject = new GameObject(Title + " aura");
            auraObject.transform.SetParent(Run.ProjectileRoot, false);
            aura = new FlameMesh(auraObject, 5);
        }

        public override Color BodyColor() => Flashing(IsEnraged ? Color.Lerp(BodyTint, Accent, 0.3f) : BodyTint, Accent, state == Spent);

        public override void HostTick(Vector2 toHero)
        {
            Enemy.Facing.TurnToward(toHero, Time.deltaTime * Enemy.ActionSpeedMultiplier * 3f);
            if (state == 0 && toHero.magnitude > ApproachDistance)
                transform.position = Run.Map.Move(transform.position, toHero.normalized * Enemy.Speed * Time.deltaTime * Enemy.ActionSpeedMultiplier, Enemy.MoveRadius);
            if (Attacking) AttackTick(Enemy.ActionTime - attackStart);
            if (Enemy.ActionTime < until) return;
            if (state == 0)
            {
                if (nextAttack > 0 && nextAttack % 3 == 0 && !summonedThisCycle && BeginSummon()) return;
                summonedThisCycle = false;
                int attack = NextAttack(nextAttack++);
                state = (byte)(attack + 1);
                attackStart = Enemy.ActionTime;
                until = attackStart + Attack(attack, toHero.sqrMagnitude > 0.01f ? toHero.normalized : Vector2.down);
            }
            else if (state == Summoning) { FinishSummon(); state = 0; until = Enemy.ActionTime + 0.5f; }
            else if (state == Spent) { state = 0; until = Enemy.ActionTime + (IsEnraged ? 0.4f : 0.7f); }
            else { state = Spent; until = Enemy.ActionTime + SpentTime; }
        }

        /// <summary>How long the guardian stands spent after an attack.</summary>
        protected virtual float SpentTime => 1.2f;

        private int LivingMinions()
        {
            int count = 0;
            foreach (var enemy in Run.Enemies) if (enemy != null && enemy.IsMinion && enemy.Health > 0) count++;
            return count;
        }

        /// <summary>Marks glowing summoning circles around the guardian; the minions step out when the summoning ends.</summary>
        private bool BeginSummon()
        {
            summonedThisCycle = true;
            int room = MaxMinions - LivingMinions();
            int count = Mathf.Min(room, Minions.Length + (IsEnraged ? 1 : 0));
            if (count <= 0) return false;
            summonSpots.Clear();
            float start = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                Vector2 spot = DungeonMap.ClampToArena((Vector2)transform.position + FlameMesh.Polar(start + i * Mathf.PI * 2f / count, SummonRadius), 1f);
                summonSpots.Add(spot);
                CombatVfx.Ring(Run.ProjectileRoot, spot, 0.8f, Accent, SummonTime);
                CoopFx.Ring(Run, spot, 0.8f, Accent, SummonTime);
            }
            state = Summoning;
            until = Enemy.ActionTime + SummonTime;
            return true;
        }

        private void FinishSummon()
        {
            for (int i = 0; i < summonSpots.Count; i++)
            {
                byte kind = i < Minions.Length ? Minions[i] : Minions[0];
                Run.SpawnMinion(summonSpots[i], kind);
            }
            summonSpots.Clear();
        }

        public override void ApplyNetState(bool charging, byte value) { if (value <= AttackCount || value == Spent || value == Summoning) state = value; }

        public override void VisualTick()
        {
            if (aura == null) return;
            aura.Begin();
            Vector2 center = transform.position;
            DrawAura(aura, center, IsCharging ? 1f : 0.5f);
            if (state == Summoning) DrawSummonCircle(aura, center);
            aura.Commit();
        }

        /// <summary>The circle that turns beneath the guardian while it summons: a pentagram unless overridden.</summary>
        protected virtual void DrawSummonCircle(FlameMesh mesh, Vector2 center) => Star(mesh, center, 2.2f, 5, 2, Time.time * 1.5f, Accent);

        /// <summary>A ring with a {points/skip} star polygon inside it (5/2 a pentagram, 6/2 a hexagram).</summary>
        protected static void Star(FlameMesh mesh, Vector2 center, float radius, int points, int skip, float spin, Color color)
        {
            mesh.Ring(center, radius, 0.08f, FlameMesh.Alpha(color, 0.8f), 48);
            float step = Mathf.PI * 2f / points;
            for (int i = 0; i < points; i++)
            {
                Vector2 a = center + FlameMesh.Polar(spin + i * step, radius), b = center + FlameMesh.Polar(spin + (i + skip) * step, radius);
                mesh.Bar(a, (b - a).normalized, Vector2.Distance(a, b), 0.07f, FlameMesh.Alpha(color, 0.7f), FlameMesh.Alpha(color, 0.7f));
            }
        }

        /// <summary>A straight hazard line from <paramref name="start"/>.</summary>
        protected void Line(Vector2 start, Vector2 direction, float length, float width, float telegraph, float duration = 0.4f)
            => Hazard(HazardShape.Beam, start, direction, length, width, telegraph, duration);

        /// <summary>A hazard patch (a crater that lingers after the strike).</summary>
        protected void Scorch(Vector2 center, float radius, float telegraph, float duration)
            => Hazard(HazardShape.Pool, center, Vector2.right, radius, 0f, telegraph, duration);

        protected void FanAt(Vector2 from, Vector2 aim, int count, float spreadDegrees, float speed)
        {
            for (int i = 0; i < count; i++)
                Fire(from, Quaternion.Euler(0, 0, (i - (count - 1) * 0.5f) * spreadDegrees) * aim, speed);
        }

        /// <summary>Vanishes and reappears at <paramref name="to"/> in a burst of the accent colour (seen by guests too).</summary>
        protected void BlinkTo(Vector2 to)
        {
            Vector2 from = transform.position;
            HeroVfx.Pulse(Run.ProjectileRoot, from, 1.4f, Accent, 0.35f);
            CoopFx.Pulse(Run, from, 1.4f, Accent, 0.35f);
            HeroVfx.Pulse(Run.ProjectileRoot, to, 1.4f, Accent, 0.35f);
            CoopFx.Pulse(Run, to, 1.4f, Accent, 0.35f);
            transform.position = to;
        }

        public override void OnDefeated() => ClearAura();
        private void OnDestroy() => ClearAura();
        private void ClearAura()
        {
            if (aura == null) return;
            aura.Release(); aura = null;
            if (auraObject != null) Destroy(auraObject);
        }
    }
}

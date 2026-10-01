using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Slopgame
{
    /// <summary>
    /// Shared pacing for the hero worlds' guardians (the Neon Arcology's machines, the Infernal Court and the Arcane
    /// Spire): approach, a run of attacks each followed by a spent moment to punish, and every third attack a summoning
    /// of the world's minions (for guardians that have any). Also the shared moves: lunges and charges that carry the
    /// guardian across the arena, telegraphed melee sweeps, and effects timed to land with a telegraph.
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
        private AttackDeck<int> deck;
        private Vector2 lungeFrom, lungeTo;
        private float lungeAt = -1f, lungeTime;
        // Host-only follow-ups (slash and burst effects timed to land with a telegraph).
        private readonly List<(float at, Action action)> pending = new List<(float, Action)>();
        private byte shownState;
        private float shownSince;

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
        /// <summary>The minion kinds summoned each time (see <see cref="DungeonRun.MinionBasic"/>); enraged adds another of the first. Empty: never summons.</summary>
        protected abstract byte[] Minions { get; }
        /// <summary>Starts an attack; returns how long it lasts.</summary>
        protected abstract float Attack(int index, Vector2 aim);
        /// <summary>Runs every frame of an attack with the seconds since it began.</summary>
        protected virtual void AttackTick(float time) { }
        protected abstract void DrawAura(FlameMesh mesh, Vector2 center, float intensity);
        /// <summary>True to deal the attacks in a freshly shuffled order each cycle (see <see cref="AttackDeck{T}"/>) rather than in order.</summary>
        protected virtual bool ShuffleAttacks => false;
        protected virtual float Scale => 2f;
        protected virtual string SummonTell => "SUMMONING THE COURT";
        protected virtual string SpentTell => "SPENT - STRIKE NOW";
        /// <summary>What the body flashes while spent.</summary>
        protected virtual Color SpentFlash => Accent;
        /// <summary>Seconds since the attack state last changed, on every machine (drives weapon and limb animation).</summary>
        protected float StateAge => Time.time - shownSince;
        public override byte NetState => state;
        protected bool Attacking => state >= 1 && state <= AttackCount;
        public override bool IsCharging => Attacking || state == Summoning;
        public override string Tell => state == Spent ? SpentTell : state == Summoning ? SummonTell
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
            // Fire and ice guardians wear pixel-art auras to match their hazards.
            aura = new FlameMesh(auraObject, 5) { Pixelated = Hazards == HazardStyle.Hellfire || Hazards == HazardStyle.Brimstone || Hazards == HazardStyle.Frost };
        }

        public override Color BodyColor() => Flashing(IsEnraged ? Color.Lerp(BodyTint, Accent, 0.3f) : BodyTint, SpentFlash, state == Spent);

        private int NextAttack(int made)
        {
            if (!ShuffleAttacks) return made % AttackCount;
            if (deck == null)
            {
                var attacks = new int[AttackCount];
                for (int i = 0; i < attacks.Length; i++) attacks[i] = i;
                deck = new AttackDeck<int>(attacks);
            }
            return deck.Draw();
        }

        public override void HostTick(Vector2 toHero)
        {
            // A lunge never outlives its attack.
            if (!Attacking) lungeAt = -1f;
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
            else if (state == Spent) { state = 0; until = Enemy.ActionTime + ApproachPause; }
            else { state = Spent; until = Enemy.ActionTime + SpentTime; }
        }

        /// <summary>How long the guardian stands spent after an attack.</summary>
        protected virtual float SpentTime => 1.2f;
        /// <summary>How long it closes in again after being spent, before the next attack.</summary>
        protected virtual float ApproachPause => IsEnraged ? 0.4f : 0.7f;

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
            if (Minions.Length == 0) return false;
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
            if (state != shownState) { shownState = state; shownSince = Time.time; }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].at) continue;
                var action = pending[i].action;
                pending.RemoveAt(i);
                action();
            }
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

        /// <summary>Runs <paramref name="action"/> after <paramref name="delay"/> seconds (host only; used for effects timed to a telegraph).</summary>
        protected void After(float delay, Action action) => pending.Add((Time.time + delay, action));

        /// <summary>Where to stand to strike <paramref name="target"/> from <paramref name="reach"/> away, on the guardian's side of it.</summary>
        protected Vector2 StrikeSpot(Vector2 target, float reach)
        {
            Vector2 away = (Vector2)transform.position - target;
            if (away.sqrMagnitude < 0.01f) away = Vector2.up;
            return DungeonMap.ClampToArena(target + away.normalized * reach, 1.2f);
        }

        /// <summary>Plans a lunge to <paramref name="to"/> (kept inside the arena) starting <paramref name="at"/> seconds into the attack.</summary>
        protected void PlanLunge(Vector2 to, float at, float duration)
        {
            lungeFrom = transform.position;
            lungeTo = DungeonMap.ClampToArena(to, 1.2f);
            lungeAt = at;
            lungeTime = duration;
        }

        /// <summary>A charge: the lane is marked (and is itself the strike) and the guardian runs it when the telegraph ends.</summary>
        protected void PlanCharge(Vector2 aim, float length, float width, float at, float telegraph, float duration)
        {
            Vector2 from = transform.position, to = DungeonMap.ClampToArena(from + aim * length, 1.2f);
            float distance = Vector2.Distance(from, to);
            if (distance > 0.5f) Line(from, (to - from) / distance, distance, width, at + telegraph - CurrentAttackTime, duration);
            PlanLunge(to, at + telegraph, duration);
        }

        /// <summary>Seconds into the current attack.</summary>
        protected float CurrentAttackTime => Enemy.ActionTime - attackStart;

        /// <summary>Moves along the planned lunge; true on the frame it lands.</summary>
        protected bool TickLunge(float time)
        {
            if (lungeAt < 0f || time < lungeAt) return false;
            if (time < lungeAt + lungeTime)
            {
                transform.position = Vector2.Lerp(lungeFrom, lungeTo, (time - lungeAt) / lungeTime);
                return false;
            }
            transform.position = lungeTo;
            lungeAt = -1f;
            return true;
        }

        /// <summary>True while a planned lunge or charge is under way.</summary>
        protected bool Lunging => lungeAt >= 0f;

        /// <summary>A telegraphed melee sweep: a fan of short lines from <paramref name="origin"/>, and a slash when they strike.</summary>
        protected void Cleave(Vector2 origin, Vector2 aim, float reach, float cone, float telegraph, int lines = 5, float width = 1.1f)
        {
            for (int i = 0; i < lines; i++)
                Line(origin, Quaternion.Euler(0, 0, -cone * 0.5f + cone * i / (lines - 1)) * aim, reach, width, telegraph, 0.25f);
            SlashAfter(telegraph, origin, aim, reach, cone);
        }

        protected void SlashAfter(float delay, Vector2 origin, Vector2 aim, float reach, float cone) => After(delay, () =>
        {
            HeroVfx.Slash(Run.ProjectileRoot, origin, aim, reach, cone, Accent, 0.22f);
            CoopFx.Slash(Run, origin, aim, reach, cone, Accent);
        });

        protected void BurstAfter(float delay, Vector2 center, float radius) => After(delay, () =>
        {
            HeroVfx.Pulse(Run.ProjectileRoot, center, radius, Accent, 0.4f);
            CoopFx.Pulse(Run, center, radius, Accent, 0.4f);
        });

        /// <summary>A ring of <paramref name="count"/> bolts from <paramref name="center"/>.</summary>
        protected void BoltRing(Vector2 center, int count, float speed, float offsetDegrees = 0f)
        {
            for (int i = 0; i < count; i++) Fire(center, FlameMesh.Polar((offsetDegrees + i * 360f / count) * Mathf.Deg2Rad, 1f), speed);
        }

        protected Vector2 AimAt(Vector2 target)
        {
            Vector2 aim = target - (Vector2)transform.position;
            return aim.sqrMagnitude > 0.01f ? aim.normalized : Enemy.Facing.Direction;
        }

        protected Vector2 RandomArenaSpot()
        {
            var arena = DungeonMap.Arena;
            return new Vector2(Random.Range(arena.xMin + 1f, arena.xMax - 2f), Random.Range(arena.yMin + 1f, arena.yMax - 2f));
        }

        /// <summary>The middle of the living party.</summary>
        protected Vector2 PartyCenter()
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Vector2 hero in LivingHeroPositions()) { sum += hero; count++; }
            return count > 0 ? sum / count : (Vector2)transform.position;
        }

        /// <summary>
        /// Locks the whole arena except one safe circle a run away from the party: the party must get to it before the
        /// telegraph ends. The circle lies <paramref name="reach"/> from the party, roughly away from the arena's middle.
        /// A <paramref name="lethal"/> lockdown kills whoever is caught outside it.
        /// </summary>
        protected Vector2 LockdownAwayFromParty(float reach, float safeRadius, float telegraph, float duration, bool lethal = false)
        {
            Vector2 party = PartyCenter();
            Vector2 away = party - DungeonMap.Arena.center;
            Vector2 direction = Quaternion.Euler(0, 0, Random.Range(-60f, 60f)) * (away.sqrMagnitude > 1f ? -away.normalized : Random.insideUnitCircle.normalized);
            Vector2 safe = DungeonMap.ClampToArena(party + direction * reach, 3f);
            Hazard(HazardShape.Inferno, safe, Vector2.up, safeRadius, 0f, telegraph, duration, lethal);
            return safe;
        }

        /// <summary>A random quadrant, preferring one the party isn't standing in.</summary>
        protected (bool right, bool top) QuadrantAwayFromParty()
        {
            Vector2 party = PartyCenter(), middle = DungeonMap.Arena.center;
            bool right = party.x < middle.x, top = party.y < middle.y;
            // Usually the diagonally opposite quadrant; sometimes an adjacent one so it can't be pre-empted.
            if (Random.value < 0.4f) { if (Random.value < 0.5f) right = !right; else top = !top; }
            return (right, top);
        }

        /// <summary>
        /// Locks three quarters of the arena, leaving one quadrant open: the half that doesn't hold it, and the other
        /// quarter of the half that does. Returns the open quadrant's centre.
        /// </summary>
        protected Vector2 LockAllButQuadrant(bool right, bool top, float telegraph, float duration)
        {
            var arena = DungeonMap.Arena;
            float left = arena.xMin - 0.5f, bottom = arena.yMin - 0.5f, midX = arena.center.x - 0.5f, midY = arena.center.y - 0.5f;
            float halfWidth = arena.width * 0.5f, height = arena.height + 2f;
            // The locked half (full height), drawn as one wide lane along x.
            float lockedX = right ? left - 1f : midX;
            Line(new Vector2(lockedX, midY), Vector2.right, halfWidth + 1f, height, telegraph, duration);
            // The locked quarter inside the open half.
            float quarterX = right ? midX : left - 1f, quarterY = top ? bottom + arena.height * 0.25f - 0.5f : midY + arena.height * 0.25f + 0.5f;
            Line(new Vector2(quarterX, quarterY), Vector2.right, halfWidth + 1f, arena.height * 0.5f + 1f, telegraph, duration);
            return new Vector2(right ? midX + halfWidth * 0.5f : left + halfWidth * 0.5f, top ? midY + arena.height * 0.25f : bottom + arena.height * 0.25f);
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

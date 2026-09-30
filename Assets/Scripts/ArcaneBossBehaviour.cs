using System;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Arcane Spire's three mage guardians: <see cref="CourtBossBehaviour"/> pacing with five attacks dealt in a
    /// shuffled order, a mix of spellcraft and melee. Melee attacks lunge or blink in, then strike through telegraphed
    /// lines and circles; the slash itself plays the moment the telegraph strikes.
    /// </summary>
    public abstract class ArcaneBossBehaviour : CourtBossBehaviour
    {
        private AttackDeck<int> deck;
        private Vector2 lungeFrom, lungeTo;
        private float lungeAt = -1f, lungeTime;
        // Host-only follow-ups (slash and burst effects timed to land with a telegraph).
        private readonly List<(float at, Action action)> pending = new List<(float, Action)>();
        private byte shownState;
        private float shownSince;

        protected override BoltKind Bolts => BoltKind.Arcane;
        protected override HazardStyle Hazards => HazardStyle.Void;
        protected override string ApproachTell => IsEnraged ? "THE SPIRE'S FURY" : "THE SPIRE STIRS";
        protected override string SummonTell => "CALLING THE SPIRE'S SERVANTS";
        protected override float SpentTime => 1f;
        /// <summary>Seconds since the attack state last changed, on every machine (drives weapon animation).</summary>
        protected float StateAge => Time.time - shownSince;

        protected override int NextAttack(int made)
        {
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
            // A new attack drops any lunge left over from the last one.
            if (!Attacking) lungeAt = -1f;
            base.HostTick(toHero);
        }

        public override void VisualTick()
        {
            if (state != shownState) { shownState = state; shownSince = Time.time; }
            base.VisualTick();
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].at) continue;
                var action = pending[i].action;
                pending.RemoveAt(i);
                action();
            }
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

        /// <summary>Plans a lunge to <paramref name="to"/> starting <paramref name="at"/> seconds into the attack.</summary>
        protected void PlanLunge(Vector2 to, float at, float duration)
        {
            lungeFrom = transform.position;
            lungeTo = to;
            lungeAt = at;
            lungeTime = duration;
        }

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

        /// <summary>A telegraphed melee sweep: a fan of short lines from <paramref name="origin"/>, and a slash when they strike.</summary>
        protected void Cleave(Vector2 origin, Vector2 aim, float reach, float cone, float telegraph)
        {
            const int lines = 5;
            for (int i = 0; i < lines; i++)
                Line(origin, Quaternion.Euler(0, 0, -cone * 0.5f + cone * i / (lines - 1)) * aim, reach, 1.1f, telegraph, 0.25f);
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
            return new Vector2(UnityEngine.Random.Range(arena.xMin + 1f, arena.xMax - 2f), UnityEngine.Random.Range(arena.yMin + 1f, arena.yMax - 2f));
        }

        /// <summary>A hexagram with a turning ring of rune marks.</summary>
        protected override void DrawSummonCircle(FlameMesh mesh, Vector2 center)
        {
            float spin = Time.time * 1.2f;
            Star(mesh, center, 2.2f, 6, 2, spin, Accent);
            mesh.Ring(center, 2.6f, 0.05f, FlameMesh.Alpha(Accent, 0.5f), 48);
            for (int i = 0; i < 12; i++) mesh.Diamond(center + FlameMesh.Polar(-spin + i * Mathf.PI / 6f, 2.6f), 0.1f, FlameMesh.Alpha(Color.white, 0.8f));
        }

        /// <summary>A mage's staff: a dark shaft from <paramref name="foot"/> with a glowing head.</summary>
        protected static void Staff(FlameMesh mesh, Vector2 foot, Vector2 up, float length, Color wood, Color glow, float glowSize)
        {
            mesh.Bar(foot, up, length, 0.09f, wood, Color.Lerp(wood, Color.white, 0.25f));
            Vector2 head = foot + up * length;
            mesh.Disc(head, glowSize * 1.9f, FlameMesh.Alpha(glow, 0.35f), FlameMesh.Alpha(glow, 0f), 24);
            mesh.Diamond(head, glowSize, glow);
            mesh.Diamond(head, glowSize * 0.45f, Color.white);
        }
    }
}

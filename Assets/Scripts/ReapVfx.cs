using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Reap: a spectral scythe blade sweeps through every frightened enemy and cuts its fear loose, the mark it leaves
    /// hangs a moment, and one soul per full second reaped streams back to the Reaper along a curling path while a ring
    /// of fear draws in around him.
    /// </summary>
    public sealed class ReapVfx : MonoBehaviour
    {
        private const float CutTime = 0.22f, FlightStart = 0.14f, FlightTime = 0.42f, Stagger = 0.06f, Duration = 0.85f;

        private struct Victim { public Vector2 At; public int Souls; public float Radius, Tilt; }

        private readonly List<Victim> victims = new List<Victim>();
        private Transform hero, root;
        private FlameMesh mesh;
        private float age;
        private int arrived, expected;

        public static ReapVfx Begin(Transform root, Transform hero)
        {
            if (root == null || hero == null) return null;
            var vfx = new GameObject("Reap").AddComponent<ReapVfx>();
            vfx.transform.SetParent(root, false);
            vfx.root = root;
            vfx.hero = hero;
            vfx.mesh = new FlameMesh(vfx.gameObject, 8);
            return vfx;
        }

        /// <param name="souls">Full seconds of fear reaped from this enemy: how many souls fly back from it.</param>
        public void Add(Vector2 at, float bodyRadius, int souls)
        {
            victims.Add(new Victim { At = at, Souls = Mathf.Clamp(souls, 0, 6), Radius = bodyRadius + 0.75f, Tilt = Random.Range(-25f, 25f) });
            expected += Mathf.Clamp(souls, 0, 6);
            HeroVfx.Sparks(root, at, ReaperAttack.Soul, 10, 4f, 0.35f, Vector2.up, 200f, 1.1f);
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (hero == null || age >= Duration) { Destroy(gameObject); return; }
            Vector2 home = hero.position;
            mesh.Begin();
            int index = 0;
            foreach (var victim in victims)
            {
                DrawCut(victim);
                for (int i = 0; i < victim.Souls; i++) DrawSoul(victim, home, index++);
            }
            // Fear is drawn in around him while the souls are on their way.
            float gather = Mathf.Clamp01(age / (FlightStart + FlightTime));
            if (gather < 1f)
            {
                float radius = Mathf.Lerp(2.4f, 0.35f, gather * gather);
                mesh.Ring(home, radius, 0.06f + 0.1f * gather, FlameMesh.Alpha(ReaperAttack.Soul, 0.15f + 0.6f * gather), 48);
                for (int i = 0; i < 6; i++)
                    mesh.Diamond(home + FlameMesh.Polar(i * Mathf.PI / 3f + age * 6f, radius), 0.09f, FlameMesh.Alpha(ReaperAttack.Bone, 0.4f + 0.6f * gather));
            }
            mesh.Commit();
        }

        /// <summary>A crescent blade whipping across the enemy, then the pale scar it leaves fading out.</summary>
        private void DrawCut(Victim victim)
        {
            float swing = Mathf.Clamp01(age / CutTime), eased = 1f - (1f - swing) * (1f - swing);
            float fade = Mathf.Clamp01(1f - (age - CutTime) / 0.3f);
            if (fade <= 0f) return;
            const float Arc = 200f * Mathf.Deg2Rad;
            float start = (130f + victim.Tilt) * Mathf.Deg2Rad, head = start - Arc * eased;
            // The crescent: thick in its middle, tapering to points, drawn from where the cut began to the blade's edge.
            const int Segments = 22;
            for (int i = 0; i < Segments; i++)
            {
                float f0 = (float)i / Segments, f1 = (float)(i + 1) / Segments;
                if (f0 > eased) break;
                f1 = Mathf.Min(f1, eased);
                float a0 = start - Arc * f0, a1 = start - Arc * f1;
                float w0 = 0.02f + 0.24f * Mathf.Sin(f0 / Mathf.Max(0.05f, eased) * Mathf.PI), w1 = 0.02f + 0.24f * Mathf.Sin(f1 / Mathf.Max(0.05f, eased) * Mathf.PI);
                Color outer = FlameMesh.Alpha(Color.Lerp(ReaperAttack.Soul, Color.white, f1 / Mathf.Max(0.05f, eased)), 0.95f * fade);
                Color inner = FlameMesh.Alpha(ReaperAttack.Shade, 0.2f * fade);
                mesh.Quad(victim.At + FlameMesh.Polar(a0, victim.Radius - w0), victim.At + FlameMesh.Polar(a0, victim.Radius),
                    victim.At + FlameMesh.Polar(a1, victim.Radius), victim.At + FlameMesh.Polar(a1, victim.Radius - w1), inner, outer, outer, inner);
            }
            if (swing < 1f) mesh.Diamond(victim.At + FlameMesh.Polar(head, victim.Radius), 0.16f, Color.white);
            // The fear itself splits open where the blade passed.
            Vector2 across = FlameMesh.Polar(start - Arc * 0.5f + Mathf.PI * 0.5f, 1f);
            float gash = victim.Radius * 1.5f * eased;
            mesh.Bar(victim.At - across * gash * 0.5f, across, gash, 0.09f * fade, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(ReaperAttack.Soul, 0.3f * fade));
        }

        /// <summary>One soul curling from the enemy back to the Reaper, with a tail behind it.</summary>
        private void DrawSoul(Victim victim, Vector2 home, int index)
        {
            float t = (age - FlightStart - index * Stagger) / FlightTime;
            if (t <= 0f) return;
            if (t >= 1f)
            {
                if (index >= arrived)
                {
                    arrived = index + 1;
                    HeroVfx.Pulse(root, home, 0.55f, ReaperAttack.Soul, 0.2f);
                    if (arrived == expected) HeroVfx.Motes(root, home, 0.8f, ReaperAttack.Soul, 16, 0.8f);
                }
                return;
            }
            Vector2 Along(float f)
            {
                // Leaps up out of the body, then bends in toward him.
                float e = f * f * (3f - 2f * f);
                Vector2 line = Vector2.Lerp(victim.At, home, e), side = Vector2.Perpendicular(home - victim.At).normalized;
                return line + side * Mathf.Sin(f * Mathf.PI) * (index % 2 == 0 ? 0.9f : -0.9f) + Vector2.up * Mathf.Sin(f * Mathf.PI) * 0.5f;
            }
            Vector2 head = Along(t);
            const int Tail = 6;
            for (int i = 0; i < Tail; i++)
            {
                Vector2 a = Along(Mathf.Max(0f, t - i * 0.035f)), b = Along(Mathf.Max(0f, t - (i + 1) * 0.035f));
                float length = Vector2.Distance(a, b);
                if (length < 0.001f) continue;
                float strength = 1f - (float)i / Tail;
                mesh.Bar(a, (b - a) / length, length, 0.16f * strength, FlameMesh.Alpha(ReaperAttack.Soul, 0.8f * strength), FlameMesh.Alpha(ReaperAttack.Soul, 0.8f * (strength - 1f / Tail)));
            }
            mesh.Disc(head, 0.17f, Color.white, FlameMesh.Alpha(ReaperAttack.Soul, 0.5f), 12);
        }

        private void OnDestroy() { mesh?.Release(); }
    }
}

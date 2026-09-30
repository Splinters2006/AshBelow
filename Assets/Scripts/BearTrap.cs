using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Bear Trap: set at the cursor (two at most). A round iron trap: two toothed half-ring jaws hinged across a pressure
    /// plate, staked down by a short chain. The jaws spring open as it arms. The first enemy to step in is rooted and
    /// hurt as the jaws snap shut; the trap is spent.
    /// </summary>
    public sealed class BearTrap : MonoBehaviour
    {
        public const int MaxTraps = 2;
        public const float Range = 5f, ArmTime = 0.4f, TriggerRadius = 0.55f, Lifetime = 40f;
        private const float JawRadius = 0.4f, SnapTime = 0.07f, ShutTime = 0.6f, FadeTime = 0.3f;
        private const int Teeth = 7;
        private static readonly List<BearTrap> set = new List<BearTrap>();
        private static readonly Color Iron = new Color(0.62f, 0.62f, 0.68f), DarkIron = new Color(0.28f, 0.28f, 0.32f),
            Steel = new Color(0.88f, 0.9f, 0.95f), Plate = new Color(0.45f, 0.4f, 0.36f), Rust = new Color(0.5f, 0.3f, 0.2f);
        private DungeonPlayer player;
        private int damage;
        private float hold, setAt, armedAt, expiresAt, sprungAt = -1f;
        // FlameMesh draws in its owner's local space, so the owner stays at the origin and the trap's spot lives here.
        private Vector2 position;
        private FlameMesh mesh;
        /// <summary>The Elemental Quiver's element loaded when it was set: the pressure plate glows with it and a crit sets it off.</summary>
        private DamageElement infusion;
        private Color glow;

        public static void Set(DungeonPlayer player, Vector2 at, int damage, float hold)
        {
            set.RemoveAll(trap => trap == null);
            // Setting a third trap springs the oldest.
            while (set.Count >= MaxTraps) { if (set[0] != null) Destroy(set[0].gameObject); set.RemoveAt(0); }
            var run = player.Run;
            var trap = new GameObject("Bear trap").AddComponent<BearTrap>();
            trap.transform.SetParent(run.ProjectileRoot, false);
            trap.position = at;
            trap.mesh = new FlameMesh(trap.gameObject, 2);
            trap.player = player;
            trap.damage = damage;
            trap.hold = hold;
            trap.infusion = ElementalQuiver.InfusionOf(player);
            trap.glow = ElementalQuiver.ShotColor(trap.infusion, Steel);
            trap.setAt = Time.time;
            trap.armedAt = Time.time + ArmTime;
            trap.expiresAt = Time.time + Lifetime;
            set.Add(trap);
            HeroVfx.Sparks(run.ProjectileRoot, at, new Color(0.6f, 0.55f, 0.45f, 0.7f), 6, 1.8f, 0.2f, Vector2.up, 160f, 0.6f);
            CoopFx.Ring(run, at, 0.6f, Iron, 0.3f);
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null || Time.time >= expiresAt) { Destroy(gameObject); return; }
            if (sprungAt >= 0f)
            {
                if (Time.time - sprungAt >= ShutTime + FadeTime) { Destroy(gameObject); return; }
                Draw();
                return;
            }
            Draw();
            if (!run.IsPlaying || Time.time < armedAt) return;
            Vector2 at = position;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.Boss != null || Vector2.Distance(at, enemy.transform.position) > TriggerRadius + enemy.HitRadius * 0.5f) continue;
                enemy.Root(hold);
                CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, at, 0f, infusion);
                HeroVfx.Sparks(run.ProjectileRoot, at, glow, 12, 4f, 0.3f);
                CombatVfx.Ring(run.ProjectileRoot, at, 0.7f, Color.white, 0.25f);
                CoopFx.Ring(run, at, 0.7f, Color.white, 0.25f);
                ScreenFx.Shake(0.05f, 0.08f);
                // Spent: it stays shut on the spot a moment, then fades.
                sprungAt = Time.time;
                set.Remove(this);
                return;
            }
        }

        /// <summary>
        /// Seen from above, each jaw is a half ring hinged along the trap's vertical axis; closing folds both halves up
        /// toward that axis until their teeth meet.
        /// </summary>
        private void Draw()
        {
            Vector2 center = position;
            float open, alpha = 1f;
            if (sprungAt >= 0f)
            {
                float since = Time.time - sprungAt;
                open = Mathf.Lerp(1f, 0.08f, Mathf.Clamp01(since / SnapTime));
                alpha = Mathf.Clamp01((ShutTime + FadeTime - since) / FadeTime);
            }
            else
            {
                // Springs open as it arms, with a little overshoot, then sits still; armed traps glint now and then.
                float t = Mathf.Clamp01((Time.time - setAt) / ArmTime);
                open = t < 1f ? 1f - Mathf.Pow(1f - t, 3f) * Mathf.Cos(t * 9f) : 1f;
            }
            bool armed = sprungAt < 0f && Time.time >= armedAt;
            Color C(Color color) => FlameMesh.Alpha(color, alpha);
            mesh.Begin();
            mesh.Ellipse(center + Vector2.down * 0.05f, JawRadius + 0.12f, (JawRadius + 0.12f) * 0.8f, new Color(0f, 0f, 0f, 0.3f * alpha), new Color(0f, 0f, 0f, 0f), 24);

            // The stake and chain trailing off the bottom.
            Vector2 stake = center + new Vector2(0.15f, -JawRadius - 0.3f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 link = Vector2.Lerp(center + Vector2.down * JawRadius, stake, (i + 0.5f) / 3f);
                mesh.Ring(link, 0.04f, 0.022f, C(DarkIron), 10);
            }
            mesh.Disc(stake, 0.055f, C(Iron), C(DarkIron), 10);

            // Pressure plate in the middle.
            mesh.Disc(center, JawRadius * 0.5f, C(Plate), C(Rust), 20);
            // A loaded element glows up through the plate, pulsing while the trap is armed.
            if (infusion != DamageElement.Physical)
                mesh.Disc(center, JawRadius * (armed ? 0.5f + 0.06f * Mathf.Sin(Time.time * 6f) : 0.4f), C(FlameMesh.Alpha(glow, 0.6f)), C(FlameMesh.Alpha(glow, 0f)), 20);
            mesh.Ring(center, JawRadius * 0.5f, 0.03f, C(DarkIron), 20);

            // Spring bar across the hinge.
            mesh.Bar(center + Vector2.down * (JawRadius + 0.05f), Vector2.up, JawRadius * 2f + 0.1f, 0.06f, C(DarkIron), C(DarkIron));
            for (int side = -1; side <= 1; side += 2) DrawJaw(center, side, open, alpha, armed);
            // Hinge pins.
            mesh.Disc(center + Vector2.up * JawRadius, 0.05f, C(Steel), C(Iron), 10);
            mesh.Disc(center + Vector2.down * JawRadius, 0.05f, C(Steel), C(Iron), 10);
            mesh.Commit();
        }

        /// <summary>One half-ring jaw with teeth pointing into the trap, squashed toward the hinge axis as it closes.</summary>
        private void DrawJaw(Vector2 center, int side, float open, float alpha, bool armed)
        {
            const int Segments = 14;
            Vector2 Point(float angle, float radius) => center + new Vector2(side * Mathf.Sin(angle) * radius * open, Mathf.Cos(angle) * radius);
            Color iron = FlameMesh.Alpha(Iron, alpha), dark = FlameMesh.Alpha(DarkIron, alpha), steel = FlameMesh.Alpha(Steel, alpha);
            for (int i = 0; i < Segments; i++)
            {
                float a0 = Mathf.PI * i / Segments, a1 = Mathf.PI * (i + 1) / Segments;
                mesh.Quad(Point(a0, JawRadius - 0.04f), Point(a0, JawRadius + 0.04f), Point(a1, JawRadius + 0.04f), Point(a1, JawRadius - 0.04f), dark, iron, iron, dark);
            }
            // A glint slides round the armed jaws every couple of seconds.
            float glint = armed ? Mathf.Repeat(Time.time * 0.6f + (side > 0 ? 0.5f : 0f), 2f) : -1f;
            for (int i = 0; i < Teeth; i++)
            {
                float angle = Mathf.PI * (i + 0.5f) / Teeth, half = Mathf.PI * 0.35f / Teeth;
                Vector2 baseA = Point(angle - half, JawRadius - 0.03f), baseB = Point(angle + half, JawRadius - 0.03f);
                Vector2 tip = Point(angle, JawRadius * 0.62f);
                bool lit = glint >= 0f && Mathf.Abs(glint - (float)i / Teeth) < 0.12f;
                mesh.Triangle(baseA, tip, baseB, iron, lit ? Color.white : FlameMesh.Alpha(glow, alpha), iron);
            }
        }

        private void OnDestroy()
        {
            set.Remove(this);
            mesh?.Release();
        }
    }
}

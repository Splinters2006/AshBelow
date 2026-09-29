using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Wizard's Inferno Orb: while flying it is wreathed in roaring flame tongues that stream behind it, and it
    /// bursts into a white-hot flash, a ring of rising flames and a spray of embers.
    /// </summary>
    public sealed class InfernoVfx : MeshEffect
    {
        private Transform orb;
        private Vector2 center, heading;
        private float radius;
        private bool burst;

        /// <summary>Flames around a flying orb; the effect ends when the orb does.</summary>
        public static void Wreathe(Transform root, Transform orb, Vector2 heading, float size)
        {
            if (orb == null) return;
            var effect = Spawn<InfernoVfx>(root, 30f, 6);
            if (effect == null) return;
            effect.orb = orb;
            effect.center = orb.position;
            effect.heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector2.right;
            effect.radius = size;
            effect.Redraw();
        }

        public static void Blast(Transform root, Vector2 center, float radius)
        {
            var effect = Spawn<InfernoVfx>(root, 0.65f, 8);
            if (effect == null) return;
            effect.burst = true;
            effect.center = center;
            effect.radius = radius;
            effect.Redraw();
        }

        protected override void LateUpdate()
        {
            if (!burst)
            {
                if (orb == null || !orb.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
                center = orb.position;
            }
            base.LateUpdate();
        }

        protected override void Draw(float t)
        {
            if (burst) DrawBlast(t);
            else DrawOrb();
        }

        private void DrawOrb()
        {
            Vector2 back = -heading;
            Mesh.Disc(center, radius * 1.8f, FlameMesh.Alpha(FlameMesh.Orange, 0.35f), FlameMesh.Alpha(FlameMesh.Crimson, 0f), 24);
            // Flame tongues stream backward from the orb as it flies.
            for (int i = 0; i < 7; i++)
            {
                float spread = (i - 3) * 0.28f, seed = i * 1.37f;
                Vector2 up = (Vector2)(Quaternion.Euler(0, 0, spread * Mathf.Rad2Deg) * back);
                Mesh.Flame(center + up * radius * 0.3f, up, radius * 0.9f, radius * (2.2f - Mathf.Abs(i - 3) * 0.35f), seed);
            }
            Mesh.Disc(center, radius * 0.75f, FlameMesh.Alpha(FlameMesh.Core, 1f), FlameMesh.Alpha(FlameMesh.Yellow, 0.8f), 18);
        }

        private void DrawBlast(float t)
        {
            float grow = EaseOut(t * 1.8f), fade = 1f - t;
            // A white-hot flash at the heart of the explosion.
            Mesh.Disc(center, radius * (0.4f + 0.7f * grow), FlameMesh.Alpha(FlameMesh.Core, fade), FlameMesh.Alpha(FlameMesh.Orange, 0.5f * fade), 40);
            Mesh.Ring(center, radius * grow, 0.25f * fade + 0.05f, FlameMesh.Alpha(FlameMesh.Yellow, fade), FlameMesh.Alpha(FlameMesh.Crimson, 0.4f * fade), 48);
            // Flames leap up all around the blast ring.
            const int tongues = 18;
            for (int i = 0; i < tongues; i++)
            {
                float a = i * Mathf.PI * 2f / tongues + FlameMesh.Hash(i, 3.3f) * 0.3f;
                Vector2 root = center + FlameMesh.Polar(a, radius * grow * 0.9f);
                Vector2 up = (FlameMesh.Polar(a, 0.6f) + Vector2.up).normalized;
                Mesh.Flame(root, up, 0.35f, (0.6f + FlameMesh.Hash(i, 1.1f) * 0.6f) * (1f - t * 0.8f), i * 2.1f, fade);
            }
            // Embers fly out and fall.
            for (int i = 0; i < 16; i++)
            {
                float seed = FlameMesh.Hash(i, 7.4f), a = seed * Mathf.PI * 2f + i;
                Vector2 p = center + FlameMesh.Polar(a, radius * (0.3f + 1.2f * EaseOut(t)) * (0.6f + seed * 0.6f)) + Vector2.down * t * t * 0.8f;
                Mesh.Diamond(p, 0.05f + seed * 0.05f, FlameMesh.Alpha(Color.Lerp(FlameMesh.Yellow, FlameMesh.Orange, seed), fade));
            }
        }
    }
}

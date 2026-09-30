using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Wing Dash: a pair of leathery bat wings spread behind the Demoness and beat hard while she dashes. The wings drag
    /// back against her heading, their scalloped trailing edge glows violet, fading afterimages of each pose trail along
    /// her path, and every downstroke throws off a crescent of displaced air. Drawn in the same oxblood leather and
    /// near-black bone as the wings on her sprite.
    /// </summary>
    public sealed class WingFlapVfx : MonoBehaviour
    {
        public const float BeatsPerSecond = 7f, Span = 1.05f;
        private const float FoldTime = 0.15f, GhostEvery = 0.03f, GhostLife = 0.28f, GustLife = 0.3f, Drag = 0.35f;
        private static readonly Color Leather = new Color(0.16f, 0.06f, 0.08f, 0.95f), Crease = new Color(0.3f, 0.12f, 0.13f, 0.95f),
            Bone = new Color(0.05f, 0.02f, 0.03f), Claw = new Color(0.6f, 0.54f, 0.48f),
            Rim = new Color(0.66f, 0.3f, 1f, 0.9f), Ghost = new Color(0.45f, 0.18f, 0.75f, 0.4f), Gust = new Color(0.85f, 0.78f, 1f, 0.55f);

        private struct Pose { public Vector2 Body; public float Sweep, Beat, Open, Born; }
        private struct Puff { public Vector2 Center; public float Born; }

        private readonly List<Pose> ghosts = new List<Pose>();
        private readonly List<Puff> gusts = new List<Puff>();
        private Transform target;
        private Transform root;
        private FlameMesh mesh;
        private Vector2 heading;
        private float age, duration, nextGhost;
        private bool wasDown;

        public static void Play(Transform root, Transform target, float duration, Vector2 heading)
        {
            if (root == null || target == null) return;
            var wings = new GameObject("Demon wings").AddComponent<WingFlapVfx>();
            wings.transform.SetParent(root, false);
            wings.root = root;
            wings.target = target;
            wings.duration = duration;
            wings.heading = heading.sqrMagnitude > 0.001f ? heading.normalized : Vector2.right;
            // Just behind the hero's own sprite.
            wings.mesh = new FlameMesh(wings.gameObject, 3);
        }

        private void Update()
        {
            age += Time.deltaTime;
            bool flying = age < duration + FoldTime;
            if (target == null || age >= duration + FoldTime + GhostLife) { Destroy(gameObject); return; }
            ghosts.RemoveAll(g => age - g.Born > GhostLife);
            gusts.RemoveAll(g => age - g.Born > GustLife);

            mesh.Begin();
            // Afterimages first so the live wings sit on top of them.
            foreach (var ghost in ghosts)
            {
                float fade = 1f - (age - ghost.Born) / GhostLife;
                for (int side = -1; side <= 1; side += 2) DrawGhost(ghost, side, fade);
            }
            foreach (var gust in gusts) DrawGust(gust);

            if (flying)
            {
                // Snap open fast, fold in over the last moment.
                float open = Mathf.Clamp01((duration + FoldTime - age) / FoldTime) * Mathf.SmoothStep(0f, 1f, age / 0.08f);
                float beat = Mathf.Sin(age * BeatsPerSecond * Mathf.PI * 2f);
                // Raised high on the upstroke, swept low and forward on the downstroke.
                float sweep = Mathf.Lerp(-35f, 45f, 0.5f + 0.5f * beat);
                Vector2 body = (Vector2)target.position - heading * 0.12f + Vector2.up * 0.18f;
                var pose = new Pose { Body = body, Sweep = sweep, Beat = beat, Open = open, Born = age };
                for (int side = -1; side <= 1; side += 2) DrawWing(pose, side);

                if (age >= nextGhost && age < duration)
                {
                    ghosts.Add(pose);
                    nextGhost = age + GhostEvery;
                }
                bool down = beat < -0.6f;
                if (down && !wasDown)
                {
                    gusts.Add(new Puff { Center = body + Vector2.down * 0.15f, Born = age });
                    HeroVfx.Sparks(root, body + Vector2.down * 0.2f, new Color(0.75f, 0.7f, 0.85f, 0.6f), 5, 2.4f, 0.25f, Vector2.down, 120f, 0.8f);
                }
                wasDown = down;
            }
            mesh.Commit();
        }

        /// <summary>The bone layout of one wing for a pose: shoulder, wrist, three fingertips and the hip it anchors to.</summary>
        private void Skeleton(Pose pose, int side, out Vector2 wrist, Vector2[] tips, out Vector2 hip)
        {
            float span = Span * pose.Open;
            float sweep = pose.Sweep;
            Vector2 shoulder = pose.Body;
            Vector2 Dir(float degrees) => new Vector2(side * Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
            // Air resistance: the farther a point is from the shoulder, the more it drags back against the dash.
            Vector2 Trail(Vector2 point) => point - heading * Vector2.Distance(point, shoulder) * Drag;
            wrist = Trail(shoulder + Dir(sweep) * span * 0.55f);
            // Fingers fan from above the arm to hanging below it; they bunch up on the downstroke.
            float fan = Mathf.Lerp(0.7f, 1f, 0.5f + 0.5f * pose.Beat);
            tips[0] = Trail(wrist + Dir(sweep + 10f) * span * 0.55f);
            tips[1] = Trail(wrist + Dir(sweep - 35f * fan) * span * 0.6f);
            tips[2] = Trail(wrist + Dir(sweep - 75f * fan) * span * 0.55f);
            hip = shoulder + Vector2.down * 0.3f + new Vector2(side * 0.05f, 0f);
        }

        private readonly Vector2[] tipBuffer = new Vector2[3];

        /// <summary>
        /// One wing: an arm bone out to the wrist claw, three finger bones fanning down from it, and leather stretched
        /// between them with a scalloped, violet-lit trailing edge.
        /// </summary>
        private void DrawWing(Pose pose, int side)
        {
            float open = pose.Open;
            if (Span * open < 0.02f) return;
            Skeleton(pose, side, out var wrist, tipBuffer, out var hip);
            Vector2 shoulder = pose.Body;
            // A soft violet glow behind the leather, strongest on the downstroke.
            float power = 0.5f + 0.5f * -pose.Beat;
            Vector2 middle = (wrist + tipBuffer[1]) * 0.5f;
            mesh.Ellipse(middle, Span * 0.55f * open, Span * 0.4f * open, FlameMesh.Alpha(Rim, 0.18f * open * power), FlameMesh.Alpha(Rim, 0f), 20);

            Vector2 previous = tipBuffer[0];
            for (int i = 1; i <= tipBuffer.Length; i++)
            {
                Vector2 next = i < tipBuffer.Length ? tipBuffer[i] : hip;
                Vector2 half = Vector2.Lerp(previous, next, 0.5f);
                Vector2 dip = half + (wrist - half) * 0.25f;
                // Leather darkens toward the wrist and warms toward the edge, where it thins and lets light through.
                Color inner = FlameMesh.Alpha(i % 2 == 0 ? Crease : Leather, open);
                Color edge = FlameMesh.Alpha(Color.Lerp(Crease, Rim, 0.25f), open);
                mesh.Triangle(wrist, previous, dip, inner, edge, edge);
                mesh.Triangle(wrist, dip, next, inner, edge, edge);
                // A thin glowing rim along the scalloped edge.
                Color rim = FlameMesh.Alpha(Rim, open * (0.55f + 0.45f * power));
                DrawEdge(previous, dip, 0.035f * open, rim);
                DrawEdge(dip, next, 0.035f * open, rim);
                previous = next;
            }
            mesh.Triangle(shoulder, wrist, hip, FlameMesh.Alpha(Leather, open), FlameMesh.Alpha(Crease, open), FlameMesh.Alpha(Leather, open));
            // Bones on top.
            DrawBone(shoulder, wrist, 0.07f * open);
            foreach (var tip in tipBuffer) DrawBone(wrist, tip, 0.035f * open);
            Vector2 up = (wrist - shoulder).normalized;
            mesh.Diamond(wrist + up * 0.08f, 0.05f * open, Claw);
            foreach (var tip in tipBuffer) mesh.Diamond(tip, 0.03f * open, FlameMesh.Alpha(Rim, open));
        }

        /// <summary>A flat violet silhouette of an earlier pose, left hanging in the air behind her.</summary>
        private void DrawGhost(Pose pose, int side, float fade)
        {
            if (Span * pose.Open < 0.02f) return;
            Skeleton(pose, side, out var wrist, tipBuffer, out var hip);
            Color body = FlameMesh.Alpha(Ghost, fade * pose.Open), edge = FlameMesh.Alpha(Ghost, 0f);
            Vector2 previous = tipBuffer[0];
            for (int i = 1; i <= tipBuffer.Length; i++)
            {
                Vector2 next = i < tipBuffer.Length ? tipBuffer[i] : hip;
                mesh.Triangle(wrist, previous, next, body, edge, edge);
                previous = next;
            }
            mesh.Triangle(pose.Body, wrist, hip, body, body, edge);
        }

        /// <summary>A downstroke's crescent of air, spreading under her and fading.</summary>
        private void DrawGust(Puff gust)
        {
            float t = (age - gust.Born) / GustLife;
            float radius = Mathf.Lerp(0.25f, 0.9f, 1f - (1f - t) * (1f - t)), width = 0.07f * (1f - t);
            Color color = FlameMesh.Alpha(Gust, 1f - t);
            const int Segments = 12;
            // The lower half of a ring, flattened like a shockwave seen from above.
            Vector2 Arc(float a, float r) => gust.Center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.45f);
            for (int i = 0; i < Segments; i++)
            {
                float a0 = Mathf.PI * (1.1f + 0.8f * i / Segments), a1 = Mathf.PI * (1.1f + 0.8f * (i + 1) / Segments);
                mesh.Quad(Arc(a0, radius - width), Arc(a0, radius + width), Arc(a1, radius + width), Arc(a1, radius - width),
                    FlameMesh.Alpha(color, 0.3f), color, color, FlameMesh.Alpha(color, 0.3f));
            }
        }

        private void DrawEdge(Vector2 from, Vector2 to, float width, Color color)
        {
            float length = Vector2.Distance(from, to);
            if (length < 0.001f) return;
            mesh.Bar(from, (to - from) / length, length, width, color, color);
        }

        private void DrawBone(Vector2 from, Vector2 to, float width)
        {
            float length = Vector2.Distance(from, to);
            if (length < 0.001f) return;
            mesh.Bar(from, (to - from) / length, length, width, Bone, Bone);
        }

        private void OnDestroy() => mesh?.Release();
    }
}

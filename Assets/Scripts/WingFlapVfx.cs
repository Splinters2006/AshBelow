using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Wing Dash: a pair of leathery bat wings spread behind the Demoness and beat hard while she dashes, each downstroke
    /// throwing off a puff of air. Drawn in the same oxblood leather and near-black bone as the wings on her sprite.
    /// </summary>
    public sealed class WingFlapVfx : MonoBehaviour
    {
        public const float BeatsPerSecond = 7f, Span = 0.95f;
        private static readonly Color Leather = new Color(0.16f, 0.06f, 0.08f, 0.95f), Crease = new Color(0.3f, 0.12f, 0.13f, 0.95f),
            Bone = new Color(0.05f, 0.02f, 0.03f), Claw = new Color(0.6f, 0.54f, 0.48f);
        private Transform target;
        private Transform root;
        private FlameMesh mesh;
        private Vector2 heading;
        private float age, duration;
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
            if (target == null || age >= duration + 0.15f) { Destroy(gameObject); return; }
            // The wings fold in over the last moment.
            float open = Mathf.Clamp01((duration + 0.15f - age) / 0.15f) * Mathf.Clamp01(age / 0.06f);
            float beat = Mathf.Sin(age * BeatsPerSecond * Mathf.PI * 2f);
            // Raised high on the upstroke, swept low and forward on the downstroke.
            float sweep = Mathf.Lerp(-35f, 40f, 0.5f + 0.5f * beat);
            Vector2 body = (Vector2)target.position - heading * 0.12f + Vector2.up * 0.18f;
            mesh.Begin();
            for (int side = -1; side <= 1; side += 2) DrawWing(body, side, sweep, open, beat);
            mesh.Commit();
            bool down = beat < -0.6f;
            if (down && !wasDown)
                HeroVfx.Sparks(root, body + Vector2.down * 0.2f, new Color(0.75f, 0.7f, 0.85f, 0.6f), 5, 2.4f, 0.25f, Vector2.down, 120f, 0.8f);
            wasDown = down;
        }

        /// <summary>
        /// One wing: an arm bone out to the wrist claw, three finger bones fanning down from it, and leather stretched
        /// between them with a scalloped trailing edge.
        /// </summary>
        private void DrawWing(Vector2 shoulder, int side, float sweep, float open, float beat)
        {
            float span = Span * open;
            if (span < 0.02f) return;
            Vector2 Dir(float degrees) => new Vector2(side * Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
            Vector2 wrist = shoulder + Dir(sweep) * span * 0.55f;
            // Fingers fan from above the arm to hanging below it; they bunch up on the downstroke.
            float fan = Mathf.Lerp(0.7f, 1f, 0.5f + 0.5f * beat);
            Vector2[] tips =
            {
                wrist + Dir(sweep + 10f) * span * 0.55f,
                wrist + Dir(sweep - 35f * fan) * span * 0.6f,
                wrist + Dir(sweep - 75f * fan) * span * 0.55f,
            };
            Vector2 hip = shoulder + Vector2.down * 0.3f + new Vector2(side * 0.05f, 0f);
            // Leather: a panel between each pair of bones, dipping between the fingertips for the scalloped edge.
            Vector2 previous = tips[0];
            for (int i = 1; i <= tips.Length; i++)
            {
                Vector2 next = i < tips.Length ? tips[i] : hip;
                Vector2 dip = Vector2.Lerp(previous, next, 0.5f) + (wrist - Vector2.Lerp(previous, next, 0.5f)) * 0.25f;
                Color fill = i % 2 == 0 ? Crease : Leather;
                mesh.Triangle(wrist, previous, dip, FlameMesh.Alpha(fill, open), FlameMesh.Alpha(fill, open), FlameMesh.Alpha(fill, open));
                mesh.Triangle(wrist, dip, next, FlameMesh.Alpha(fill, open), FlameMesh.Alpha(fill, open), FlameMesh.Alpha(fill, open));
                previous = next;
            }
            mesh.Triangle(shoulder, wrist, hip, FlameMesh.Alpha(Leather, open), FlameMesh.Alpha(Crease, open), FlameMesh.Alpha(Leather, open));
            // Bones on top.
            DrawBone(shoulder, wrist, 0.07f * open);
            foreach (var tip in tips) DrawBone(wrist, tip, 0.035f * open);
            mesh.Diamond(wrist + Dir(sweep + 40f) * 0.08f, 0.05f * open, Claw);
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

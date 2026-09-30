using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Wing Dash: the bat wings on the Demoness's own sprite spread and beat hard while she dashes. For the flight her
    /// detail layer swaps to a wingless copy and each wing becomes its own layer pivoted at the shoulder, rising and
    /// sweeping down with every beat; violet afterimages of each pose trail along her path, and every downstroke throws
    /// off a crescent of displaced air. When she lands the wings settle back into her sprite.
    /// </summary>
    public sealed class WingFlapVfx : MonoBehaviour
    {
        public const float BeatsPerSecond = 7f;
        private const float FoldTime = 0.15f, GhostEvery = 0.04f, GhostLife = 0.25f, GustLife = 0.3f, Lift = 35f;
        private static readonly Color Ghost = new Color(0.55f, 0.25f, 0.9f, 0.45f), Gust = new Color(0.85f, 0.78f, 1f, 0.55f);

        private struct Puff { public Vector2 Center; public float Born; }

        private readonly List<Puff> gusts = new List<Puff>();
        private readonly SpriteRenderer[] wings = new SpriteRenderer[2];
        private Transform target;
        private Transform root;
        private Transform wingRoot;
        private SpriteRenderer details;
        private FlameMesh mesh;
        private float age, duration, nextGhost;
        private bool wasDown;

        public static void Play(Transform root, Transform target, float duration, Vector2 heading)
        {
            if (root == null || target == null) return;
            var vfx = new GameObject("Demon wing beat").AddComponent<WingFlapVfx>();
            vfx.transform.SetParent(root, false);
            vfx.root = root;
            vfx.target = target;
            vfx.duration = duration;
            vfx.mesh = new FlameMesh(vfx.gameObject, 3);
            vfx.Spread();
        }

        /// <summary>Lifts the wings off her sprite onto two layers of their own.</summary>
        private void Spread()
        {
            var folded = HeroSprites.Accent(WeaponType.Tail);
            foreach (var part in target.GetComponentsInChildren<SpriteRenderer>())
                if (part.sprite == folded || part.sprite == HeroSprites.WinglessAccent) { details = part; break; }
            if (details == null) return;
            details.sprite = HeroSprites.WinglessAccent;
            // Mirrored as a whole with the hero, the way flipX mirrors her sprite.
            wingRoot = new GameObject("Beating wings").transform;
            wingRoot.SetParent(details.transform, false);
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                var wing = new GameObject(side < 0 ? "Left wing" : "Right wing").AddComponent<SpriteRenderer>();
                wing.transform.SetParent(wingRoot, false);
                wing.transform.localPosition = HeroSprites.WingShoulder(side);
                wing.sprite = HeroSprites.Wing(side);
                // Just behind the hero's body, so a raised wing never covers her.
                wing.sortingOrder = details.sortingOrder - 2;
                wings[i] = wing;
            }
        }

        /// <summary>Settles the wings back into her sprite.</summary>
        private void Fold()
        {
            if (details != null) details.sprite = HeroSprites.Accent(WeaponType.Tail);
            if (wingRoot != null) Destroy(wingRoot.gameObject);
            details = null;
            wingRoot = null;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            bool flying = age < duration + FoldTime;
            if (target == null || age >= duration + FoldTime + GustLife) { Destroy(gameObject); return; }
            if (!flying) Fold();
            gusts.RemoveAll(g => age - g.Born > GustLife);

            mesh.Begin();
            foreach (var gust in gusts) DrawGust(gust);
            if (flying)
            {
                // Beat hard from the first frame, easing back to the sprite's resting pose over the last moment.
                float open = Mathf.Clamp01((duration + FoldTime - age) / FoldTime) * Mathf.SmoothStep(0f, 1f, age / 0.08f);
                float beat = Mathf.Sin(age * BeatsPerSecond * Mathf.PI * 2f);
                if (details != null) Pose(beat, open);

                Vector2 body = (Vector2)target.position + Vector2.up * 0.18f;
                bool down = beat < -0.6f;
                if (down && !wasDown)
                {
                    gusts.Add(new Puff { Center = body + Vector2.down * 0.15f, Born = age });
                    HeroVfx.Sparks(root, body + Vector2.down * 0.2f, new Color(0.75f, 0.7f, 0.85f, 0.6f), 5, 2.4f, 0.25f, Vector2.down, 120f, 0.8f);
                }
                wasDown = down;
                if (details != null && age >= nextGhost && age < duration)
                {
                    LeaveGhost();
                    nextGhost = age + GhostEvery;
                }
            }
            mesh.Commit();
        }

        /// <summary>
        /// Raised high on the upstroke and swept low on the downstroke; the leather foreshortens at either end of the
        /// stroke and stretches as it pulls down.
        /// </summary>
        private void Pose(float beat, float open)
        {
            wingRoot.localScale = new Vector3(details.flipX ? -1f : 1f, 1f, 1f);
            var color = details.color;
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                var wing = wings[i].transform;
                wing.localRotation = Quaternion.Euler(0f, 0f, side * Lift * beat * open);
                float spread = 1f + open * (0.2f - 0.4f * Mathf.Abs(beat)), stretch = 1f + open * 0.15f * Mathf.Max(0f, -beat);
                wing.localScale = new Vector3(spread, stretch, 1f);
                wings[i].color = color;
            }
        }

        /// <summary>A flat violet copy of both wings in their current pose, left hanging in the air behind her.</summary>
        private void LeaveGhost()
        {
            var ghost = new GameObject("Wing afterimage").transform;
            ghost.SetParent(root, false);
            foreach (var wing in wings)
            {
                var copy = new GameObject(wing.name).AddComponent<SpriteRenderer>();
                copy.transform.SetParent(ghost, false);
                copy.transform.position = wing.transform.position;
                // Rebuilt unmirrored: a flipped hero's wing is the flipped sprite turned the other way.
                float angle = wing.transform.localEulerAngles.z;
                copy.transform.rotation = Quaternion.Euler(0f, 0f, details.flipX ? -angle : angle);
                var scale = wing.transform.lossyScale;
                copy.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f);
                copy.flipX = details.flipX;
                copy.sprite = wing.sprite;
                copy.sortingOrder = wing.sortingOrder - 1;
                copy.color = FlameMesh.Alpha(Ghost, details.color.a);
            }
            ghost.gameObject.AddComponent<FadingSprite>().Duration = GhostLife;
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

        private void OnDestroy()
        {
            Fold();
            mesh?.Release();
        }
    }
}

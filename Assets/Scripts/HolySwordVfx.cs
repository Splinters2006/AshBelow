using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A holy sword called down from the sky: a golden sigil marks the ground, the blade plunges point-first out of
    /// the heavens, and on impact it stands in the ground in a burst of light before fading. Purely visual.
    /// </summary>
    public sealed class HolySwordVfx : MeshEffect
    {
        public const float Telegraph = 0.3f, Fall = 0.18f;
        private const float Linger = 0.5f, Height = 7f;
        private static readonly Color White = new Color(1f, 0.98f, 0.88f);
        private Vector2 target;
        private Transform follow;
        private bool struck;

        public static float ImpactDelay => Telegraph + Fall;

        /// <summary>The sword tracks <paramref name="follow"/> until it starts to fall, then commits to that spot.</summary>
        public static HolySwordVfx Play(Transform root, Vector2 target, Transform follow = null)
        {
            var effect = Spawn<HolySwordVfx>(root, ImpactDelay + Linger);
            if (effect == null) return null;
            effect.target = target;
            effect.follow = follow;
            effect.Redraw();
            return effect;
        }

        public Vector2 Target => target;

        protected override void LateUpdate()
        {
            if (follow != null && follow.gameObject.activeInHierarchy && Age < Telegraph) target = follow.position;
            if (!struck && Age >= ImpactDelay)
            {
                struck = true;
                var root = transform.parent;
                HeroVfx.Pulse(root, target, 1.3f, AbilityCatalog.Gold, 0.35f);
                HeroVfx.Sparks(root, target, White, 14, 5f, 0.4f, Vector2.up, 200f, 1.1f);
                CombatVfx.Ring(root, target, 1f, Color.white, 0.3f);
            }
            base.LateUpdate();
        }

        protected override void Draw(float t)
        {
            Color gold = AbilityCatalog.Gold;
            Vector2 ground = target + Vector2.down * 0.2f;
            if (Age < ImpactDelay)
            {
                // The sigil tightens as the blade approaches.
                float charge = Mathf.Clamp01(Age / ImpactDelay);
                float r = Mathf.Lerp(1.1f, 0.75f, charge);
                Mesh.Ellipse(ground, r, r * 0.45f, FlameMesh.Alpha(gold, 0.08f + 0.2f * charge), FlameMesh.Alpha(gold, 0.3f * charge), 32);
                for (int i = 0; i < 4; i++)
                {
                    float a = Age * 3f + i * Mathf.PI * 0.5f;
                    Vector2 p = ground + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.45f);
                    Mesh.Diamond(p, 0.08f, FlameMesh.Alpha(White, 0.4f + 0.6f * charge));
                }
                // A faint shaft of light shows where it will land.
                Mesh.Bar(ground, Vector2.up, Height, 0.5f * charge, FlameMesh.Alpha(gold, 0.18f * charge), FlameMesh.Alpha(gold, 0f));
                float drop = Age < Telegraph ? 0f : (Age - Telegraph) / Fall;
                DrawSword(target + Vector2.up * Mathf.Lerp(Height, 0f, drop * drop), Mathf.Clamp01(Age / Telegraph), drop > 0f);
                return;
            }
            float after = (Age - ImpactDelay) / Linger, fade = 1f - after;
            // A flash of light around the impact.
            Mesh.Disc(ground, 0.4f + 1.2f * EaseOut(after), FlameMesh.Alpha(White, 0.6f * fade), FlameMesh.Alpha(gold, 0f), 32);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 0.2f + 0.15f;
                Mesh.Bar(ground + FlameMesh.Polar(a, 0.3f), FlameMesh.Polar(a, 1f), 0.4f + 1.1f * after, 0.07f,
                    FlameMesh.Alpha(White, 0.9f * fade), FlameMesh.Alpha(gold, 0f));
            }
            DrawSword(target, fade, false);
        }

        /// <summary>Point-down sword whose tip is at <paramref name="tip"/>.</summary>
        private void DrawSword(Vector2 tip, float alpha, bool falling)
        {
            Color gold = AbilityCatalog.Gold;
            if (falling) Mesh.Bar(tip + Vector2.up * 1.6f, Vector2.up, 2.5f, 0.3f, FlameMesh.Alpha(White, 0.6f * alpha), FlameMesh.Alpha(gold, 0f));
            Vector2 bladeBase = tip + Vector2.up * 1.25f, guard = bladeBase + Vector2.up * 0.06f;
            Mesh.Bar(tip + Vector2.up * 0.1f, Vector2.up, 1.3f, 0.36f, FlameMesh.Alpha(gold, 0.35f * alpha), FlameMesh.Alpha(gold, 0.2f * alpha));
            Mesh.Triangle(tip + Vector2.up * 0.3f + Vector2.left * 0.09f, tip, tip + Vector2.up * 0.3f + Vector2.right * 0.09f,
                FlameMesh.Alpha(White, alpha), FlameMesh.Alpha(Color.white, alpha), FlameMesh.Alpha(White, alpha));
            Mesh.Rect(tip + new Vector2(-0.09f, 0.3f), bladeBase + Vector2.right * 0.09f, FlameMesh.Alpha(White, alpha));
            Mesh.Rect(tip + new Vector2(-0.015f, 0.3f), bladeBase + Vector2.right * 0.015f, FlameMesh.Alpha(gold, 0.7f * alpha));
            Mesh.Rect(guard + new Vector2(-0.34f, -0.06f), guard + new Vector2(0.34f, 0.06f), FlameMesh.Alpha(gold, alpha));
            Mesh.Rect(guard + new Vector2(-0.05f, 0.06f), guard + new Vector2(0.05f, 0.42f), FlameMesh.Alpha(new Color(0.55f, 0.36f, 0.18f), alpha));
            Mesh.Diamond(guard + Vector2.up * 0.5f, 0.1f, FlameMesh.Alpha(gold, alpha));
        }
    }
}

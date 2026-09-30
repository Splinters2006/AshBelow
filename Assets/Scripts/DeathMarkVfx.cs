using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Death Mark: a red skull hangs over the marked enemy inside a clock face. A red wedge sweeps smoothly away round
    /// the dial as the mark runs down, its edge ticking faster near the end, and when it empties the skull bursts. If the
    /// enemy dies while marked, the skull shatters and a blood-red blast rolls out over the burst radius.
    /// </summary>
    public sealed class DeathMarkVfx : MonoBehaviour
    {
        public static readonly Color Blood = new Color(0.85f, 0.2f, 0.3f);
        private const float DialRadius = 0.36f, PopTime = 0.25f;
        private static Sprite skullSprite;
        private Transform root;
        private DungeonEnemy target;
        private float duration, age, height;
        private FlameMesh mesh;
        private SpriteRenderer skull;
        private Vector2 lastPosition;
        /// <summary>The enemy died while marked: the mark detonates instead of simply running out.</summary>
        private bool burst;

        /// <summary>A red skull with hollow black eyes and a gap-toothed jaw. Draw it untinted.</summary>
        private static Sprite SkullSprite => skullSprite != null ? skullSprite : skullSprite = DungeonVisuals.PaletteSprite("Death mark skull", new[]
        {
            "..OOOOO..",
            ".ORRLLRO.",
            "ORRRRLRRO",
            "ORKKRKKRO",
            "ORKKRKKRO",
            "ORRRKRRRO",
            ".ORRRRRO.",
            "..RDRDR..",
            "..ODODO..",
        }, key => key switch
        {
            'O' => new Color(0.35f, 0.03f, 0.07f),
            'R' => new Color(0.92f, 0.18f, 0.24f),
            'L' => new Color(1f, 0.6f, 0.6f),
            'D' => new Color(0.6f, 0.08f, 0.12f),
            'K' => new Color(0.06f, 0f, 0.02f),
            _ => Color.clear
        });

        public static void Play(Transform root, DungeonEnemy target, float duration)
        {
            if (root == null || target == null || duration <= 0f) return;
            var mark = new GameObject("Death mark").AddComponent<DeathMarkVfx>();
            mark.transform.SetParent(root, false);
            mark.root = root;
            mark.target = target;
            mark.duration = duration;
            mark.height = target.HitRadius + 0.55f;
            mark.lastPosition = target.transform.position;
            mark.mesh = new FlameMesh(mark.gameObject, 9);
            mark.skull = DungeonVisuals.Create("Skull", mark.transform, mark.Anchor, Vector2.one * 0.42f, Color.white, 10);
            mark.skull.sprite = SkullSprite;
            HeroVfx.Pulse(root, target.transform.position, 0.9f, Blood, 0.3f);
        }

        private float PopLength => burst ? 0.45f : PopTime;

        private Vector2 Anchor => lastPosition + Vector2.up * height;

        private void Update()
        {
            age += Time.deltaTime;
            // Stays where the enemy fell if it dies first, then fades.
            if (target != null && target.Health > 0) lastPosition = target.transform.position;
            else if (age < duration)
            {
                age = duration;
                burst = true;
                // The stored damage bursts out: a blood-red blast over the whole area, and the skull shatters.
                ScreenFx.Shake(0.15f, 0.2f);
                HeroVfx.Pulse(root, lastPosition, DungeonEnemy.DeathMarkBurstRadius, FlameMesh.Alpha(Blood, 0.7f), 0.4f);
                HeroVfx.Sparks(root, lastPosition, Blood, 24, 6f, 0.45f, null, 360f, 1.3f);
                HeroVfx.Sparks(root, Anchor, new Color(1f, 0.6f, 0.6f), 10, 4f, 0.35f, Vector2.up, 160f, 1f);
            }
            if (age >= duration + PopLength) { Destroy(gameObject); return; }
            Vector2 center = Anchor + Vector2.up * Mathf.Sin(age * 3f) * 0.03f;
            float left = Mathf.Clamp01(1f - age / duration);
            // Popping: the skull swells and fades.
            float pop = age >= duration ? (age - duration) / PopLength : 0f;
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.15f));
            float alpha = appear * (1f - pop);
            // It throbs harder as the mark comes due.
            float urgency = 1f - left;
            float throb = 1f + 0.08f * urgency * Mathf.Sin(age * Mathf.Lerp(6f, 22f, urgency));
            skull.transform.position = center;
            skull.transform.localScale = Vector3.one * 0.42f * throb * (1f + pop * 0.8f) * Mathf.Lerp(0.6f, 1f, appear);
            skull.color = new Color(1f, 1f, 1f, alpha);

            mesh.Begin();
            float radius = DialRadius * (1f + pop * 0.6f);
            // The dial: a dark backing and a faint ring.
            mesh.Disc(center, radius, FlameMesh.Alpha(new Color(0.1f, 0f, 0.02f), 0.45f * alpha), FlameMesh.Alpha(new Color(0.1f, 0f, 0.02f), 0.25f * alpha), 40);
            mesh.Ring(center, radius, 0.035f, FlameMesh.Alpha(new Color(0.4f, 0.06f, 0.1f), 0.8f * alpha), 48);
            // Twelve hour ticks.
            for (int i = 0; i < 12; i++)
            {
                float a = Mathf.PI * 0.5f - i * Mathf.PI / 6f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                mesh.Bar(center + dir * (radius - 0.06f), dir, 0.05f, i % 3 == 0 ? 0.03f : 0.018f, FlameMesh.Alpha(Blood, 0.6f * alpha), FlameMesh.Alpha(Blood, 0.6f * alpha));
            }
            // The time left: a wedge that a clock hand eats away smoothly as it sweeps clockwise.
            if (left > 0f)
            {
                int segments = Mathf.Max(1, Mathf.CeilToInt(48 * left));
                // The hand has swept clockwise from twelve by the time spent; the wedge runs from it on round to twelve.
                float start = Mathf.PI * 0.5f - Mathf.PI * 2f * (1f - left), sweep = -Mathf.PI * 2f * left;
                for (int i = 0; i < segments; i++)
                {
                    float a0 = start + sweep * i / segments, a1 = start + sweep * (i + 1) / segments;
                    Vector2 p0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * (radius - 0.02f);
                    Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * (radius - 0.02f);
                    mesh.Triangle(center, p0, p1, FlameMesh.Alpha(Blood, 0.12f * alpha), FlameMesh.Alpha(Blood, 0.5f * alpha), FlameMesh.Alpha(Blood, 0.5f * alpha));
                    mesh.Quad(p0, center + (p0 - center) * 1.18f, center + (p1 - center) * 1.18f, p1,
                        FlameMesh.Alpha(Blood, alpha), FlameMesh.Alpha(Blood, 0.6f * alpha), FlameMesh.Alpha(Blood, 0.6f * alpha), FlameMesh.Alpha(Blood, alpha));
                }
                // The hand: a bright line on the wedge's leading edge.
                float hand = start;
                Vector2 handDir = new Vector2(Mathf.Cos(hand), Mathf.Sin(hand));
                mesh.Bar(center, handDir, radius * 1.1f, 0.035f, FlameMesh.Alpha(Color.white, 0.4f * alpha), FlameMesh.Alpha(new Color(1f, 0.75f, 0.75f), alpha));
            }
            if (pop > 0f) mesh.Ring(center, radius * (1f + pop), 0.08f * (1f - pop), FlameMesh.Alpha(Blood, 1f - pop), 40);
            if (burst && pop > 0f)
            {
                // The blast rolls out across the floor to the edge of the burst.
                float blast = 1f - (1f - pop) * (1f - pop);
                mesh.Disc(lastPosition, DungeonEnemy.DeathMarkBurstRadius * blast, FlameMesh.Alpha(Blood, 0.35f * (1f - pop)), FlameMesh.Alpha(Blood, 0.1f * (1f - pop)), 40);
                mesh.Ring(lastPosition, DungeonEnemy.DeathMarkBurstRadius * blast, 0.18f * (1f - pop), FlameMesh.Alpha(new Color(1f, 0.55f, 0.55f), 1f - pop), 56);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * Mathf.PI / 5f + FlameMesh.Hash(i, 2.9f) * 0.4f;
                    mesh.Bar(lastPosition, FlameMesh.Polar(a, 1f), DungeonEnemy.DeathMarkBurstRadius * blast, 0.06f * (1f - pop),
                        FlameMesh.Alpha(Color.white, 0.8f * (1f - pop)), FlameMesh.Alpha(Blood, 0f));
                }
            }
            mesh.Commit();
        }

        private void OnDestroy() => mesh?.Release();
    }
}

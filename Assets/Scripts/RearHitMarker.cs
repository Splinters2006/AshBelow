using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Flags hits that land on an enemy's back: a bright double chevron behind the enemy pointing through it,
    /// a ring around it and sparks bursting out of its front. Shown only for player-dealt hits.
    /// </summary>
    public static class RearHitMarker
    {
        public static readonly Color Color = new Color(1f, 0.3f, 0.78f);
        private const float Duration = 0.45f;

        public static void Show(DungeonRun run, DungeonEnemy enemy)
        {
            if (run == null || run.ProjectileRoot == null || enemy == null || enemy.Facing == null) return;
            Draw(run.ProjectileRoot, enemy.transform.position, enemy.Facing.Direction, enemy.HitRadius);
            CoopFx.RearHit(run, enemy.transform.position, enemy.Facing.Direction, enemy.HitRadius);
        }

        /// <summary>Draws the marker only; teammates replay it from <see cref="CoopFx"/>.</summary>
        public static void Draw(Transform root, Vector2 position, Vector2 facing, float hitRadius)
        {
            if (root == null || facing.sqrMagnitude < 0.0001f) return;
            facing.Normalize();
            var marker = new GameObject("Rear hit marker");
            marker.transform.SetParent(root, false);
            marker.transform.position = position - facing * (hitRadius + 0.32f);
            marker.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            // Two ">" chevrons pointing along the enemy's facing: "hit from behind".
            for (int i = 0; i < 2; i++)
            {
                float x = i * 0.2f;
                Color color = i == 0 ? new Color(Color.r, Color.g, Color.b, 0.6f) : Color;
                Bar(marker.transform, new Vector2(x - 0.07f, 0.075f), -37f, color);
                Bar(marker.transform, new Vector2(x - 0.07f, -0.075f), 37f, color);
            }
            marker.AddComponent<FadingSprite>().Duration = Duration;
            CombatVfx.Ring(root, position, hitRadius + 0.22f, Color, 0.3f);
            HeroVfx.Sparks(root, position + facing * hitRadius, Color, 7, 4.5f, 0.3f, facing, 70f, 1.1f);
        }

        private static void Bar(Transform parent, Vector2 localPosition, float angle, Color color)
        {
            var bar = DungeonVisuals.Create("Chevron", parent, parent.position, new Vector2(0.25f, 0.07f), color, 10);
            bar.transform.localPosition = localPosition;
            bar.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>Leaves fading, tinted copies of the hero's silhouette while dodge-rolling.</summary>
    public sealed class DodgeAfterimage : MonoBehaviour
    {
        private const float Interval = 0.045f;
        public DungeonPlayer Player { get; set; }
        public Color Tint { get; set; } = new Color(0.4f, 0.65f, 1f);
        private SpriteRenderer body;
        private float nextGhost;
        private bool wasRolling;

        private void Start() { body = GetComponent<SpriteRenderer>(); }

        private void LateUpdate()
        {
            if (Player == null || Player.Run == null || body == null || Player.Run.ProjectileRoot == null) return;
            bool rolling = Player.IsRolling && Player.Run.IsPlaying;
            if (rolling && !wasRolling)
            {
                nextGhost = 0f;
                // A puff of dust kicked back from the roll's starting point.
                HeroVfx.Sparks(Player.Run.ProjectileRoot, (Vector2)transform.position + Vector2.down * 0.2f,
                    new Color(0.7f, 0.78f, 0.85f, 0.7f), 7, 2.4f, 0.3f, -Player.RollDirection, 110f, 0.8f);
            }
            wasRolling = rolling;
            if (!rolling || Time.time < nextGhost) return;
            nextGhost = Time.time + Interval;
            var ghost = new GameObject("Dodge afterimage").transform;
            ghost.SetParent(Player.Run.ProjectileRoot, false);
            ghost.position = transform.position;
            var tint = Tint;
            tint.a = 0.5f;
            // Copy the whole silhouette (body plus class details) as one tinted ghost.
            foreach (var part in GetComponentsInChildren<SpriteRenderer>())
            {
                if (part.sprite == null || part.color.a < 0.05f) continue;
                var copy = new GameObject(part.name).AddComponent<SpriteRenderer>();
                copy.transform.SetParent(ghost, false);
                copy.transform.position = part.transform.position;
                copy.transform.rotation = part.transform.rotation;
                copy.transform.localScale = part.transform.lossyScale;
                copy.sprite = part.sprite;
                copy.sortingOrder = Mathf.Min(part.sortingOrder, body.sortingOrder) - 1;
                var color = part == body ? tint : Color.Lerp(tint, part.color, 0.25f);
                color.a = part == body ? tint.a : tint.a * 0.8f;
                copy.color = color;
            }
            ghost.gameObject.AddComponent<FadingSprite>().Duration = 0.22f;
        }
    }
}

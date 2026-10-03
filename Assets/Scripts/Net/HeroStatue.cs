using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A stone likeness of one hero in the co-op party hall. Walking up to it wakes it into the hero's colours; using it
    /// (<see cref="GameAction.Interact"/>) swaps the local hero for this one.
    /// </summary>
    public sealed class HeroStatue : MonoBehaviour
    {
        public const float UseRange = 1.3f;
        private static readonly Color Stone = new Color(0.46f, 0.47f, 0.52f), Plinth = new Color(0.2f, 0.21f, 0.25f);

        public CharacterDefinition Character { get; private set; }
        /// <summary>The hero's place in <see cref="DungeonRun.Characters"/>, which is what the party roster shares.</summary>
        public int Index { get; private set; }
        private SpriteRenderer body, details, plinth, glow;
        private float wake;

        public static HeroStatue Create(Transform parent, Vector2 position, CharacterDefinition character, int index)
        {
            var body = DungeonVisuals.Create(character.DisplayName + " statue", parent, position, Vector2.one * 0.65f, character.Color, 3);
            var statue = body.gameObject.AddComponent<HeroStatue>();
            statue.Character = character;
            statue.Index = index;
            statue.body = body;
            statue.details = DungeonVisuals.DecorateHero(body.transform, character.Weapon, character.Color);
            // The plinth sits under the feet; its glow marks the hero this player has picked.
            statue.glow = DungeonVisuals.Create("Statue glow", parent, position + Vector2.down * 0.5f, new Vector2(1.25f, 0.42f), Color.clear, 1);
            statue.plinth = DungeonVisuals.Create("Statue plinth", parent, position + Vector2.down * 0.5f, new Vector2(1f, 0.26f), Plinth, 2);
            statue.Paint(0f, false);
            return statue;
        }

        public bool IsNear(Vector2 point) => Vector2.Distance(transform.position, point) < UseRange;

        /// <summary>Wakes the statue while <paramref name="near"/>; <paramref name="picked"/> lights its plinth.</summary>
        public void Refresh(bool near, bool picked)
        {
            wake = Mathf.MoveTowards(wake, near || picked ? 1f : 0f, Time.deltaTime * 4f);
            Paint(wake, picked);
        }

        /// <summary>A little burst as the hero is taken up.</summary>
        public void Flash() => HeroVfx.Pulse(transform.parent, transform.position, 1.4f, Character.Color, 0.5f);

        private void Paint(float awake, bool picked)
        {
            body.color = Color.Lerp(Stone, Character.Color, awake);
            if (details != null) details.color = Color.Lerp(new Color(0.62f, 0.63f, 0.68f), Color.white, awake);
            plinth.color = picked ? Color.Lerp(Plinth, Character.Color, 0.55f) : Plinth;
            float pulse = 0.35f + 0.15f * Mathf.Sin(Time.time * 3f);
            glow.color = picked ? FlameMesh.Alpha(Character.Color, pulse) : Color.clear;
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>Rectangular punch effects for the Brawler: a flash of the hit area, a fist at its far edge and a spark spray.</summary>
    public static class BrawlerVfx
    {
        public static void Punch(Transform root, Vector2 origin, Vector2 aim, float length, float halfWidth, Color color, float duration = 0.16f)
        {
            if (root == null || aim.sqrMagnitude < 0.0001f) return;
            aim.Normalize();
            var holder = new GameObject("Punch");
            holder.transform.SetParent(root, false);
            holder.transform.position = origin;
            holder.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            float fist = Mathf.Clamp(halfWidth * 0.75f, 0.24f, 0.7f);
            Part(holder.transform, "Punch area", new Vector2(length * 0.5f, 0f), new Vector2(length, halfWidth * 2f), Faded(color, 0.22f), 5);
            Part(holder.transform, "Punch edge", new Vector2(length, 0f), new Vector2(0.07f, halfWidth * 2f), Faded(color, 0.75f), 6);
            Part(holder.transform, "Fist", new Vector2(length - fist * 0.5f, 0f), Vector2.one * fist, Color.Lerp(color, Color.white, 0.25f), 7);
            Part(holder.transform, "Knuckles", new Vector2(length - fist * 0.15f, 0f), new Vector2(fist * 0.3f, fist * 0.8f), Color.Lerp(color, Color.white, 0.6f), 8);
            holder.AddComponent<FadingSprite>().Duration = duration;
            HeroVfx.Sparks(root, origin + aim * length, Color.Lerp(color, Color.white, 0.5f), 5, 3.5f, 0.18f, aim, 70f, 0.8f);
        }

        private static Color Faded(Color color, float alpha) { color.a *= alpha; return color; }

        private static void Part(Transform parent, string name, Vector2 localPosition, Vector2 size, Color color, int order)
        {
            var part = DungeonVisuals.Create(name, parent, parent.position, size, color, order);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
        }
    }
}

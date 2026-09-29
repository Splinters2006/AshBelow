using UnityEngine;

namespace Slopgame
{
    /// <summary>Punch effects for the Brawler, shared by her own attacks and the copies teammates see.</summary>
    public static class BrawlerVfx
    {
        public static void Punch(Transform root, Vector2 origin, Vector2 aim, float length, float halfWidth, Color color, float duration = 0.16f)
        {
            if (root == null || aim.sqrMagnitude < 0.0001f) return;
            // Short jabs still need a moment to read, so the glove lingers a touch longer than the hit.
            PunchVfx.Play(root, PunchVfx.Style.Jab, origin, aim, length, halfWidth, color, Mathf.Max(0.2f, duration * 1.4f));
            HeroVfx.Sparks(root, origin + aim.normalized * length, Color.Lerp(color, Color.white, 0.5f), 5, 3.5f, 0.18f, aim, 70f, 0.8f);
        }

        /// <summary>Knuckle Sandwich's blow: a huge glove, cone shockwaves and cracked ground.</summary>
        public static void HeavyPunch(Transform root, Vector2 origin, Vector2 aim, float length, float halfWidth, Color color)
        {
            if (root == null || aim.sqrMagnitude < 0.0001f) return;
            PunchVfx.Play(root, PunchVfx.Style.Heavy, origin, aim, length, halfWidth, color, 0.6f);
        }

        /// <summary>Knuckle Sandwich's windup, locked to the Brawler's position and to the aim she committed to.</summary>
        public static PunchVfx Windup(Transform root, Transform hero, Vector2 aim, float length, float halfWidth, Color color, float duration)
        {
            if (root == null || hero == null) return null;
            return PunchVfx.Play(root, PunchVfx.Style.Windup, hero.position, aim, length, halfWidth, color, duration)?.Follow(hero, null);
        }

        /// <summary>The barrage's blur of gloves; it follows the hero and their aim until it ends or is stopped.</summary>
        public static PunchVfx Flurry(Transform root, Transform hero, System.Func<Vector2> aim, float length, float halfWidth, Color color, float duration)
        {
            if (root == null || hero == null) return null;
            return PunchVfx.Play(root, PunchVfx.Style.Flurry, hero.position, aim(), length, halfWidth, color, duration)?.Follow(hero, aim);
        }
    }
}

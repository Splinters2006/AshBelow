using UnityEngine;

namespace Slopgame
{
    /// <summary>Camera shake and full-screen colour flashes for big boss moments. Purely local and cosmetic.</summary>
    public static class ScreenFx
    {
        private static float shakeStrength, shakeUntil, shakeDuration;
        private static Color flashColor;
        private static float flashUntil, flashDuration;

        public static void Shake(float strength, float duration)
        {
            float remaining = ShakeAmount;
            shakeStrength = Mathf.Max(strength, remaining);
            shakeDuration = Mathf.Max(0.01f, duration);
            shakeUntil = Time.time + shakeDuration;
        }

        public static void Flash(Color color, float duration)
        {
            flashColor = color;
            flashDuration = Mathf.Max(0.01f, duration);
            flashUntil = Time.time + flashDuration;
        }

        private static float ShakeAmount => Time.time >= shakeUntil ? 0f : shakeStrength * (shakeUntil - Time.time) / shakeDuration;

        /// <summary>This frame's camera offset.</summary>
        public static Vector2 Offset
        {
            get
            {
                float amount = ShakeAmount;
                if (amount <= 0f) return Vector2.zero;
                float t = Time.time * 38f;
                return new Vector2(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * amount;
            }
        }

        /// <summary>This frame's flash colour (alpha fades out), or clear.</summary>
        public static Color FlashColor
        {
            get
            {
                if (Time.time >= flashUntil) return Color.clear;
                var color = flashColor;
                color.a *= (flashUntil - Time.time) / flashDuration;
                return color;
            }
        }

        public static void Clear() { shakeUntil = 0f; flashUntil = 0f; }
    }
}

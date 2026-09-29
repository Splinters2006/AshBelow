using UnityEngine;

namespace Slopgame
{
    public interface IPlayerWeapon
    {
        bool IsHeavyAttacking { get; }
        float HeavyCooldownRemaining { get; }
        bool CanAttack { get; }
        bool TryAttack(Vector2 aim, float charge = 0f);
        bool TryHeavyAttack(Vector2 aim);
        /// <summary>Takes time off the class skill's cooldown (kill talents); infinity makes it ready now.</summary>
        void ReduceHeavyCooldown(float seconds);
        void Hide();
    }

    public static class Cooldowns
    {
        /// <summary>A ready time <paramref name="seconds"/> sooner, but never in the past (so a ready skill stays as it was).</summary>
        public static float Shorten(float readyAt, float seconds) => readyAt <= Time.time ? readyAt : Mathf.Max(Time.time, readyAt - seconds);
    }
}

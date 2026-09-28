using UnityEngine;

namespace Slopgame
{
    public sealed class KnightShield : MonoBehaviour
    {
        public const float Duration = 0.8f;
        public const float Cooldown = 2.8f;
        public DungeonPlayer Player { get; set; }
        private float blockingUntil, readyAt;
        public bool IsBlocking => Player.Run.IsPlaying && !Player.IsRolling && Time.time < blockingUntil;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public Vector2 Direction { get; private set; }

        public bool Raise(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || CooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            Direction = aim.normalized;
            blockingUntil = Time.time + Duration;
            readyAt = Time.time + Cooldown;
            Player.Charge.Cancel();
            return true;
        }

        public bool CanReflect(Vector2 position, Vector2 incoming)
        {
            return IsBlocking && Vector2.Dot(incoming, Direction) < -0.1f
                && SwordAttack.ContainsTarget(position - (Vector2)transform.position, Direction, 0.9f, 120f);
        }

        public void Cancel() { blockingUntil = 0f; }
    }
}

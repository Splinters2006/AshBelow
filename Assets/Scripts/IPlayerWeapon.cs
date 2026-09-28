using UnityEngine;

namespace Slopgame
{
    public interface IPlayerWeapon
    {
        bool IsHeavyAttacking { get; }
        float HeavyCooldownRemaining { get; }
        bool TryAttack(Vector2 aim);
        bool TryHeavyAttack(Vector2 aim);
        void Hide();
    }
}

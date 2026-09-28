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
        void Hide();
    }
}

using UnityEngine;

namespace Slopgame
{
    public sealed class DamageBlessing : MonoBehaviour
    {
        private int bonus;
        private float expiresAt;
        public float Remaining => Mathf.Max(0f, expiresAt - Time.time);
        public int BonusDamage => Remaining > 0f ? bonus : 0;

        public void Apply(int damage, float duration)
        {
            bonus = Mathf.Max(BonusDamage, damage);
            expiresAt = Mathf.Max(expiresAt, Time.time + duration);
        }
    }
}

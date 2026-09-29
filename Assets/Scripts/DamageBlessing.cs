using UnityEngine;

namespace Slopgame
{
    public sealed class DamageBlessing : MonoBehaviour
    {
        private int bonus;
        private float expiresAt;
        // Who gave the latest blessing: a hero on this machine, or a co-op teammate's id.
        private DungeonPlayer blesser;
        private ulong teammate;
        private bool fromTeammate;
        public float Remaining => Mathf.Max(0f, expiresAt - Time.time);
        public int BonusDamage => Remaining > 0f ? bonus : 0;

        public void Apply(int damage, float duration, DungeonPlayer from = null)
        {
            Extend(damage, duration);
            blesser = from;
            fromTeammate = false;
        }

        /// <summary>A blessing from a Paladin on another machine.</summary>
        public void ApplyFromTeammate(int damage, float duration, ulong from)
        {
            Extend(damage, duration);
            blesser = null;
            teammate = from;
            fromTeammate = true;
        }

        private void Extend(int damage, float duration)
        {
            bonus = Mathf.Max(BonusDamage, damage);
            expiresAt = Mathf.Max(expiresAt, Time.time + duration);
        }

        /// <summary>A blessed hit landed: the bonus it added charges the blessing Paladin's Heavenly Host.</summary>
        public void Credit(DungeonPlayer hitter)
        {
            int amount = BonusDamage;
            if (amount <= 0) return;
            if (blesser != null) blesser.Mechanic?.OnBlessedHit(amount);
            else if (fromTeammate && hitter.Run != null && hitter.Run.IsNetworked)
                hitter.Run.Coop.SendSupport(teammate, SupportKind.BlessingCredit, amount, 0f);
        }
    }
}

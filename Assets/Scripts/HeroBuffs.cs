using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Timed self-buffs that scale a hero's core stats. Every multiplier is neutral (1 or 0) while nothing is
    /// active, so heroes that never gain a buff are unaffected. Used by the Brawler's Empower and Primal Rage.
    /// </summary>
    public sealed class HeroBuffs : MonoBehaviour
    {
        public static readonly Color EmpowerColor = new Color(1f, 0.72f, 0.3f);
        public static readonly Color RageColor = new Color(1f, 0.25f, 0.2f);
        public static readonly Color TiredColor = new Color(0.55f, 0.58f, 0.66f);
        public DungeonPlayer Player { get; set; }
        private float empoweredUntil, ragingUntil, tiredUntil, nextFx;

        public bool IsEmpowered => Time.time < empoweredUntil;
        public bool IsRaging => Time.time < ragingUntil;
        public bool IsTired => !IsRaging && Time.time < tiredUntil;
        public float EmpowerRemaining => Mathf.Max(0f, empoweredUntil - Time.time);
        public float RageRemaining => Mathf.Max(0f, ragingUntil - Time.time);
        public float TiredRemaining => IsTired ? tiredUntil - Time.time : 0f;
        /// <summary>Time until both the rage and the tiredness that follows it have worn off.</summary>
        public float RageCycleRemaining => Mathf.Max(0f, tiredUntil - Time.time);

        // Empower: faster movement, attacks and charging, slightly larger attacks and 0.5s off the dodge cooldown.
        // Primal Rage: huge damage, charge, movement and dodge buffs; Tired: all of those turned into penalties.
        public float MoveMultiplier => (IsEmpowered ? 1.25f : 1f) * (IsRaging ? 1.35f : IsTired ? 0.7f : 1f);
        public float AttackIntervalMultiplier => IsEmpowered ? 0.7f : 1f;
        public float ChargeDurationMultiplier => (IsEmpowered ? 0.7f : 1f) * (IsRaging ? 0.5f : IsTired ? 1.5f : 1f);
        public float AttackSizeMultiplier => IsEmpowered ? 1.1f : 1f;
        public float DamageMultiplier => IsRaging ? 2f : IsTired ? 0.5f : 1f;
        public float DodgeCooldownMultiplier => IsRaging ? 0.5f : IsTired ? 1.5f : 1f;
        public float DodgeCooldownReduction => IsEmpowered ? 0.5f : 0f;
        public float DodgeSpeedMultiplier => IsRaging ? 1.3f : IsTired ? 0.8f : 1f;

        public void Empower(float duration) { empoweredUntil = Mathf.Max(empoweredUntil, Time.time + duration); }

        public void Rage(float duration, float tiredDuration)
        {
            ragingUntil = Time.time + duration;
            tiredUntil = ragingUntil + tiredDuration;
        }

        public void Clear() { empoweredUntil = ragingUntil = tiredUntil = 0f; }

        /// <summary>The hero's body colour with a hint of the strongest active buff.</summary>
        public Color Tint(Color baseColor) => Tint(baseColor, IsEmpowered, IsRaging, IsTired);

        /// <summary>Shared with co-op teammates' heroes, which only know the buff flags.</summary>
        public static Color Tint(Color baseColor, bool empowered, bool raging, bool tired)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            if (raging) return Color.Lerp(baseColor, RageColor, 0.4f + 0.2f * wave);
            if (tired) return Color.Lerp(baseColor, TiredColor, 0.55f);
            if (empowered) return Color.Lerp(baseColor, EmpowerColor, 0.2f + 0.15f * wave);
            return baseColor;
        }

        private void Update()
        {
            if (Player == null || Player.Run == null || Player.Run.ProjectileRoot == null || !Player.Run.IsPlaying || Time.time < nextFx) return;
            var root = Player.Run.ProjectileRoot;
            Vector2 position = transform.position;
            if (IsRaging)
            {
                nextFx = Time.time + 0.14f;
                HeroVfx.Sparks(root, position + Vector2.down * 0.3f, RageColor, 3, 2.2f, 0.35f, Vector2.up, 80f, 0.9f);
            }
            else if (IsEmpowered)
            {
                nextFx = Time.time + 0.22f;
                HeroVfx.Motes(root, position, 0.45f, EmpowerColor, 3, 0.5f);
            }
            else if (IsTired)
            {
                nextFx = Time.time + 0.5f;
                HeroVfx.Motes(root, position + Vector2.up * 0.55f, 0.2f, TiredColor, 2, 0.8f);
            }
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Timed self-buffs that scale a hero's core stats. Every multiplier is neutral (1 or 0) while nothing is
    /// active, so heroes that never gain a buff are unaffected. Used by the Brawler's Empower and Primal Rage
    /// and the Demoness's Archdemon's Technique.
    /// </summary>
    public sealed class HeroBuffs : MonoBehaviour
    {
        public static readonly Color EmpowerColor = new Color(1f, 0.72f, 0.3f);
        public static readonly Color RageColor = new Color(1f, 0.25f, 0.2f);
        public static readonly Color TiredColor = new Color(0.55f, 0.58f, 0.66f);
        public static readonly Color AscendColor = new Color(0.62f, 0.2f, 1f);
        public static readonly Color FuryColor = new Color(1f, 0.45f, 0.1f);
        public static readonly Color TauntColor = new Color(0.95f, 0.08f, 0.05f);
        public DungeonPlayer Player { get; set; }
        private float empoweredUntil, ragingUntil, tiredUntil, ascendedUntil, furiousUntil, jackpotSpeedUntil, jackpotDamageUntil, nextFx;
        private float jackpotSpeed = 1f;
        private int jackpotDamage;

        public bool IsEmpowered => Time.time < empoweredUntil;
        public bool IsRaging => Time.time < ragingUntil;
        public bool IsTired => !IsRaging && Time.time < tiredUntil;
        /// <summary>Archdemon's Technique: the Demoness's attacks count as fully charged and charging winds up a tail whip.</summary>
        public bool IsAscended => Time.time < ascendedUntil;
        public float AscendRemaining => Mathf.Max(0f, ascendedUntil - Time.time);
        /// <summary>Super Angry: the Brawler's class mechanic.</summary>
        public bool IsFurious => Time.time < furiousUntil;
        public float FuryRemaining => Mathf.Max(0f, furiousUntil - Time.time);
        /// <summary>The Gambler's Jackpot winnings: a movement multiplier and flat bonus damage, each for a while.</summary>
        public float JackpotSpeed => Time.time < jackpotSpeedUntil ? jackpotSpeed : 1f;
        public int JackpotDamage => Time.time < jackpotDamageUntil ? jackpotDamage : 0;
        public float JackpotSpeedRemaining => Mathf.Max(0f, jackpotSpeedUntil - Time.time);
        public float JackpotDamageRemaining => Mathf.Max(0f, jackpotDamageUntil - Time.time);
        public float EmpowerRemaining => Mathf.Max(0f, empoweredUntil - Time.time);
        public float RageRemaining => Mathf.Max(0f, ragingUntil - Time.time);
        public float TiredRemaining => IsTired ? tiredUntil - Time.time : 0f;
        /// <summary>Time until both the rage and the tiredness that follows it have worn off.</summary>
        public float RageCycleRemaining => Mathf.Max(0f, tiredUntil - Time.time);

        // Empower: faster movement, attacks and charging, slightly larger attacks and 0.5s off the dodge cooldown.
        // Primal Rage: huge damage, charge, movement and dodge buffs; Tired: all of those turned into penalties.
        // Super Angry: massive speed, reach and area, much faster charging and double damage.
        public float MoveMultiplier => (IsEmpowered ? 1.25f : 1f) * (IsRaging ? 1.35f : IsTired ? 0.7f : 1f) * (IsFurious ? 1.5f : 1f) * JackpotSpeed
            * (Time.time < ventedUntil ? 1.2f : 1f);
        /// <summary>Thermal Vent: +20% speed for a moment after the plasma cannon fires.</summary>
        public void Vent(float duration) => ventedUntil = Mathf.Max(ventedUntil, Time.time + duration);
        private float ventedUntil;
        public float AttackIntervalMultiplier => (IsEmpowered ? 0.7f : 1f) * (IsFurious ? 0.7f : 1f);
        // Archdemon's Technique winds the Demoness's tail whip up 50% faster.
        public float ChargeDurationMultiplier => (IsEmpowered ? 0.7f : 1f) * (IsRaging ? 0.5f : IsTired ? 1.5f : 1f) * (IsFurious ? 0.4f : 1f)
            * (IsAscended ? 1f / 1.5f : 1f);
        public float AttackSizeMultiplier => (IsEmpowered ? 1.1f : 1f) * (IsFurious ? 1.5f : 1f);
        public float DamageMultiplier => (IsRaging ? 2f : IsTired ? 0.5f : 1f) * (IsFurious ? 2f : 1f) * (IsGreedy ? 2f : 1f);
        /// <summary>Snake Eyes: a lost All In doubles the Gambler's damage for a while.</summary>
        public bool IsGreedy => Time.time < greedUntil;
        private float greedUntil;
        public void Greed(float duration) => greedUntil = Mathf.Max(greedUntil, Time.time + duration);
        public float DodgeCooldownMultiplier => IsRaging ? 0.5f : IsTired ? 1.5f : 1f;
        public float DodgeCooldownReduction => IsEmpowered ? 0.5f : 0f;
        public float DodgeSpeedMultiplier => IsRaging ? 1.3f : IsTired ? 0.8f : 1f;

        public void Empower(float duration) { empoweredUntil = Mathf.Max(empoweredUntil, Time.time + duration); }

        public void Rage(float duration, float tiredDuration)
        {
            ragingUntil = Time.time + duration;
            tiredUntil = ragingUntil + tiredDuration;
        }

        public void Ascend(float duration) { ascendedUntil = Mathf.Max(ascendedUntil, Time.time + duration); }

        public void Fury(float duration) { furiousUntil = Mathf.Max(furiousUntil, Time.time + duration); }

        public void JackpotHaste(float multiplier, float duration)
        {
            jackpotSpeed = Mathf.Max(JackpotSpeed, multiplier);
            jackpotSpeedUntil = Mathf.Max(jackpotSpeedUntil, Time.time + duration);
        }

        public void JackpotMight(int damage, float duration)
        {
            jackpotDamage = Mathf.Max(JackpotDamage, damage);
            jackpotDamageUntil = Mathf.Max(jackpotDamageUntil, Time.time + duration);
        }

        public void Clear() { empoweredUntil = ragingUntil = tiredUntil = ascendedUntil = furiousUntil = jackpotSpeedUntil = jackpotDamageUntil = 0f; }

        /// <summary>The hero's body colour with a hint of the strongest active buff.</summary>
        public Color Tint(Color baseColor) => Tint(baseColor, IsEmpowered, IsRaging, IsTired, IsAscended, IsFurious);

        /// <summary>Shield Taunt: the Knight goes red in the face with rage, throbbing fast.</summary>
        public static Color AngryTint(Color baseColor)
            => Color.Lerp(baseColor, TauntColor, 0.6f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 18f)));

        /// <summary>Shared with co-op teammates' heroes, which only know the buff flags.</summary>
        public static Color Tint(Color baseColor, bool empowered, bool raging, bool tired, bool ascended = false, bool furious = false)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            if (furious) return Color.Lerp(baseColor, FuryColor, 0.45f + 0.25f * wave);
            if (ascended) return Color.Lerp(baseColor, AscendColor, 0.35f + 0.2f * wave);
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
            if (IsFurious)
            {
                nextFx = Time.time + 0.1f;
                HeroVfx.Sparks(root, position + Vector2.down * 0.3f, FuryColor, 4, 2.8f, 0.35f, Vector2.up, 90f, 1f);
            }
            else if (IsAscended)
            {
                nextFx = Time.time + 0.16f;
                HeroVfx.Sparks(root, position + Vector2.down * 0.35f, AscendColor, 3, 1.8f, 0.4f, Vector2.up, 70f, 0.9f);
                HeroVfx.Motes(root, position, 0.5f, new Color(0.08f, 0.02f, 0.14f), 2, 0.6f);
            }
            else if (IsRaging)
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
            else if (JackpotDamage > 0 || JackpotSpeed > 1f)
            {
                // Jackpot winnings glitter in the prize's colour until they run out.
                nextFx = Time.time + 0.2f;
                if (JackpotDamage > 0) HeroVfx.Motes(root, position, 0.45f, JackpotVfx.DamageColor, 2, 0.5f);
                if (JackpotSpeed > 1f) HeroVfx.Sparks(root, position + Vector2.down * 0.35f, JackpotVfx.SpeedColor, 2, 1.6f, 0.3f, Vector2.up, 70f, 0.8f);
            }
        }
    }
}

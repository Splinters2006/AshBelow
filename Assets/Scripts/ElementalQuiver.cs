using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Archer's class mechanic: arrows carry fire, freeze or shock, and the key cycles between them. Arrows still
    /// crit as normal, and a critical hit also sets off the arrow's element. With its R upgrade (Elemental Surge, from
    /// the Ash shop), holding the key imbues the quiver for a few seconds: every arrow sets off its element, crit or not.
    /// </summary>
    public sealed class ElementalQuiver : ClassMechanic
    {
        private static readonly DamageElement[] Cycle = { DamageElement.Fire, DamageElement.Ice, DamageElement.Lightning };
        public const float SurgeHold = 0.4f, SurgeDuration = 6f, SurgeCooldown = 30f;
        private int index;
        private float pressedAt, surgeUntil, surgeReadyAt, nextFx;
        private bool pressing;
        public bool IsSurging => Time.time < surgeUntil;
        public float SurgeCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, surgeReadyAt - Time.time));
        public override void ReduceCooldown(float seconds) => surgeReadyAt = Cooldowns.Shorten(surgeReadyAt, seconds);
        public DamageElement Element => Cycle[index];
        public override string Name => "Elemental Quiver";
        public override Color Color => CombatDamage.ElementColor(Element);
        public override float Readiness => !IsUpgraded ? 1f : IsSurging ? (surgeUntil - Time.time) / SurgeDuration : 1f - SurgeCooldownRemaining / SurgeCooldown;
        public override string Status => IsSurging ? $"{ElementName(Element)}  {Seconds(surgeUntil - Time.time)}" : ElementName(Element);

        /// <summary>True while <paramref name="player"/>'s quiver is surging: every infused hit sets off its element.</summary>
        public static bool IsImbued(DungeonPlayer player) => player != null && player.Mechanic is ElementalQuiver quiver && quiver.IsSurging;

        /// <summary>The element loaded in <paramref name="player"/>'s quiver, or physical for anyone who isn't an Archer.</summary>
        public static DamageElement InfusionOf(DungeonPlayer player) => player != null && player.Mechanic is ElementalQuiver quiver ? quiver.Element : DamageElement.Physical;

        /// <summary>What an Archer's shot looks like with <paramref name="infusion"/> loaded: its element's colour, or <paramref name="plain"/>.</summary>
        public static Color ShotColor(DamageElement infusion, Color plain)
            => infusion != DamageElement.Physical ? Color.Lerp(CombatDamage.ElementColor(infusion), Color.white, 0.25f) : plain;

        public static string ElementName(DamageElement element) => element == DamageElement.Fire ? "FIRE"
            : element == DamageElement.Ice ? "FREEZE" : element == DamageElement.Lightning ? "SHOCK" : "PLAIN";

        public override bool TryActivate(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.Health <= 0) return false;
            // With the surge ready, the press waits to see whether it is a tap (cycle) or a hold (surge); see Update.
            if (IsUpgraded && !IsSurging && SurgeCooldownRemaining <= 0f) { pressing = true; pressedAt = Time.time; return true; }
            CycleElement();
            return true;
        }

        private void CycleElement()
        {
            index = (index + 1) % Cycle.Length;
            HeroVfx.Pulse(transform, transform.position, 0.9f, Color, 0.25f);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, Color, 8, 2.5f, 0.3f);
        }

        /// <summary>Elemental Surge: for a few seconds every arrow sets off the loaded element.</summary>
        private void Surge()
        {
            surgeUntil = Time.time + SurgeDuration;
            surgeReadyAt = Time.time + SurgeCooldown;
            var root = Player.Run.ProjectileRoot;
            HeroVfx.Pulse(root, transform.position, 2.2f, Color, 0.45f);
            HeroVfx.Sparks(root, transform.position, Color, 22, 5f, 0.45f);
            CombatVfx.Ring(root, transform.position, 1.5f, Color.white, 0.4f);
            CoopFx.Pulse(Player.Run, transform.position, 2.2f, Color, 0.45f);
            ScreenFx.Shake(0.12f, 0.15f);
        }

        private void Update()
        {
            if (Player == null || Player.Run == null || !Player.Run.IsPlaying || Player.Health <= 0) { pressing = false; return; }
            if (pressing)
            {
                if (!KeyBindings.IsHeld(GameAction.Mechanic)) { pressing = false; CycleElement(); }
                else if (Time.time >= pressedAt + SurgeHold) { pressing = false; Surge(); }
            }
            if (!IsSurging || Time.time < nextFx) return;
            // The loaded element crackles around him while the surge lasts.
            nextFx = Time.time + 0.12f;
            HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.6f, Color, 2, 0.5f);
        }
    }
}

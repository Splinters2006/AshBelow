using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Archer's class mechanic: arrows carry fire, freeze or shock, and the key cycles between them. Arrows still
    /// crit as normal, and a critical hit also sets off the arrow's element.
    /// </summary>
    public sealed class ElementalQuiver : ClassMechanic
    {
        private static readonly DamageElement[] Cycle = { DamageElement.Fire, DamageElement.Ice, DamageElement.Lightning };
        private int index;
        public DamageElement Element => Cycle[index];
        public override string Name => "Elemental Quiver";
        public override Color Color => CombatDamage.ElementColor(Element);
        public override float Readiness => 1f;
        public override string Status => ElementName(Element);

        public static string ElementName(DamageElement element) => element == DamageElement.Fire ? "FIRE"
            : element == DamageElement.Ice ? "FREEZE" : element == DamageElement.Lightning ? "SHOCK" : "PLAIN";

        public override bool TryActivate(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.Health <= 0) return false;
            index = (index + 1) % Cycle.Length;
            HeroVfx.Pulse(transform, transform.position, 0.9f, Color, 0.25f);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, Color, 8, 2.5f, 0.3f);
            return true;
        }
    }
}

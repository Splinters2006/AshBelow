using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Arcology's three machines: <see cref="CourtBossBehaviour"/> pacing with their attacks shuffled, venting after
    /// each one. They summon nothing and never hurt on contact; every blow, melee included, is a telegraphed hazard.
    /// </summary>
    public abstract class NeonBossBehaviour : CourtBossBehaviour
    {
        private static readonly byte[] NoMinions = new byte[0];
        protected abstract Sprite Chassis { get; }
        protected override Sprite Body => Chassis;
        protected override byte[] Minions => NoMinions;
        protected override float Scale => 1.8f;
        protected override BoltKind Bolts => BoltKind.Plasma;
        protected override bool ShuffleAttacks => true;
        protected override float ApproachDistance => 4.5f;
        protected override float SpentTime => 1.1f;
        protected override string ApproachTell => IsEnraged ? "OVERCLOCK ACTIVE" : "ACQUIRING TARGETS";
        protected override string SpentTell => "VENTING - ATTACK NOW";
        protected override Color SpentFlash => Color.white;
        public override bool DealsContactDamage => false;

        /// <summary>A beam centred on <paramref name="center"/>.</summary>
        protected void Beam(Vector2 center, Vector2 direction, float length, float delay, float width = 0.8f)
            => Hazard(HazardShape.Beam, center - direction * length * 0.5f, direction, length, width, delay, 0.45f);
    }
}

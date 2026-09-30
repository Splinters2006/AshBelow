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

        /// <summary>A random quadrant, preferring one the party isn't standing in.</summary>
        protected (bool right, bool top) QuadrantAwayFromParty()
        {
            Vector2 party = PartyCenter(), middle = DungeonMap.Arena.center;
            bool right = party.x < middle.x, top = party.y < middle.y;
            // Usually the diagonally opposite quadrant; sometimes an adjacent one so it can't be pre-empted.
            if (Random.value < 0.4f) { if (Random.value < 0.5f) right = !right; else top = !top; }
            return (right, top);
        }
    }
}

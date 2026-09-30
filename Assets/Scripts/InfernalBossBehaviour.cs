namespace Slopgame
{
    /// <summary>
    /// The Infernal Court's three guardians: <see cref="CourtBossBehaviour"/> pacing with three attacks in order. Their
    /// brimstone strikes and hex bolts leave the hero burning, and the ground they scorch keeps burning a while.
    /// </summary>
    public abstract class InfernalBossBehaviour : CourtBossBehaviour
    {
        protected override BoltKind Bolts => BoltKind.Hex;
        protected override HazardStyle Hazards => HazardStyle.Brimstone;
        protected override string ApproachTell => IsEnraged ? "WRATH UNBOUND" : "THE COURT IS IN SESSION";
    }
}

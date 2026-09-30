using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Arcane Spire's three mage guardians: <see cref="CourtBossBehaviour"/> pacing with their attacks dealt in a
    /// shuffled order, a mix of spellcraft and melee, in arcane bolts and void-bright hazards.
    /// </summary>
    public abstract class ArcaneBossBehaviour : CourtBossBehaviour
    {
        protected override BoltKind Bolts => BoltKind.Arcane;
        protected override HazardStyle Hazards => HazardStyle.Void;
        protected override bool ShuffleAttacks => true;
        protected override string ApproachTell => IsEnraged ? "THE SPIRE'S FURY" : "THE SPIRE STIRS";
        protected override string SummonTell => "CALLING THE SPIRE'S SERVANTS";
        protected override float SpentTime => 1f;

        /// <summary>A hexagram with a turning ring of rune marks.</summary>
        protected override void DrawSummonCircle(FlameMesh mesh, Vector2 center)
        {
            float spin = Time.time * 1.2f;
            Star(mesh, center, 2.2f, 6, 2, spin, Accent);
            mesh.Ring(center, 2.6f, 0.05f, FlameMesh.Alpha(Accent, 0.5f), 48);
            for (int i = 0; i < 12; i++) mesh.Diamond(center + FlameMesh.Polar(-spin + i * Mathf.PI / 6f, 2.6f), 0.1f, FlameMesh.Alpha(Color.white, 0.8f));
        }

        /// <summary>A mage's staff: a dark shaft from <paramref name="foot"/> with a glowing head.</summary>
        protected static void Staff(FlameMesh mesh, Vector2 foot, Vector2 up, float length, Color wood, Color glow, float glowSize)
        {
            mesh.Bar(foot, up, length, 0.09f, wood, Color.Lerp(wood, Color.white, 0.25f));
            Vector2 head = foot + up * length;
            mesh.Disc(head, glowSize * 1.9f, FlameMesh.Alpha(glow, 0.35f), FlameMesh.Alpha(glow, 0f), 24);
            mesh.Diamond(head, glowSize, glow);
            mesh.Diamond(head, glowSize * 0.45f, Color.white);
        }
    }
}

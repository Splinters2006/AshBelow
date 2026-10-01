using UnityEngine;

namespace Slopgame
{
    /// <summary>The two ways the Specimen can grow: into the tank (Bulk) or the chain-whip fighter (Edge).</summary>
    public enum SpecimenPath : byte { None, Bulk, Edge }

    /// <summary>What the Specimen is right now: frail until a path claims him, then the Behemoth or the Edge.</summary>
    public enum SpecimenForm : byte { Frail, Behemoth, Edge }

    /// <summary>
    /// The Specimen's rules in one place: which talents and abilities belong to which path, and how many picks
    /// transform him (locking that path for the rest of the descent) and then grow him into its second stage.
    /// </summary>
    public static class SpecimenCatalog
    {
        /// <summary>Talent ranks on one path that transform him, and that grow him into the path's second stage.</summary>
        public const int TransformPicks = 3, GrowPicks = 6;

        public static readonly Color Vital = new Color(1f, 0.45f, 0.52f);
        public static readonly Color Amber = new Color(1f, 0.62f, 0.22f);
        public static readonly Color Stone = new Color(0.62f, 0.6f, 0.57f);
        public static readonly Color Keen = new Color(0.55f, 0.95f, 1f);
        public static readonly Color Steel = new Color(0.78f, 0.84f, 0.92f);
        public static readonly Color Pale = new Color(0.86f, 0.9f, 0.88f);

        public static SpecimenPath PathOf(PowerupType type)
        {
            switch (type)
            {
                case PowerupType.BulkUp:
                case PowerupType.IronForearms:
                case PowerupType.BoneBreaker:
                case PowerupType.Aftershock:
                case PowerupType.ReturnToSender:
                case PowerupType.Immovable:
                case PowerupType.RootedStance:
                case PowerupType.FreightTrain:
                case PowerupType.RubbleWall:
                case PowerupType.Hardened:
                    return SpecimenPath.Bulk;
                case PowerupType.RazorTip:
                case PowerupType.LongChain:
                case PowerupType.WeightedTip:
                case PowerupType.ReelIn:
                case PowerupType.Featherweight:
                case PowerupType.BleedingEdge:
                case PowerupType.LightOnHisFeet:
                case PowerupType.Zipline:
                case PowerupType.LowBlow:
                case PowerupType.Shackles:
                    return SpecimenPath.Edge;
                default:
                    return SpecimenPath.None;
            }
        }

        /// <summary>Which form an ability needs; None for the two he can use in any form (Heartbeat, Fight or Flight).</summary>
        public static SpecimenPath PathOf(AbilityType type)
        {
            switch (type)
            {
                case AbilityType.Bulldoze:
                case AbilityType.BoulderToss:
                case AbilityType.IronSkin:
                case AbilityType.GiantSwing:
                    return SpecimenPath.Bulk;
                case AbilityType.SwingLine:
                case AbilityType.AnkleWrap:
                case AbilityType.Bind:
                case AbilityType.RoundUp:
                    return SpecimenPath.Edge;
                default:
                    return SpecimenPath.None;
            }
        }

        public static SpecimenForm FormOf(SpecimenPath path) => path == SpecimenPath.Bulk ? SpecimenForm.Behemoth
            : path == SpecimenPath.Edge ? SpecimenForm.Edge : SpecimenForm.Frail;

        public static SpecimenPath Other(SpecimenPath path) => path == SpecimenPath.Bulk ? SpecimenPath.Edge
            : path == SpecimenPath.Edge ? SpecimenPath.Bulk : SpecimenPath.None;

        /// <summary>Talent ranks taken on <paramref name="path"/> (hybrids count for the path of their ability).</summary>
        public static int Points(PlayerPowerups powers, SpecimenPath path)
        {
            if (powers == null || path == SpecimenPath.None) return 0;
            int points = 0;
            foreach (var talent in PowerupCatalog.All)
                if (PathOf(talent.Type) == path) points += powers.Count(talent.Type);
            return points;
        }

        /// <summary>Once a path has claimed the Specimen, the other path's talents are never offered again.</summary>
        public static bool Allows(SpecimenPath locked, PowerupType type)
        {
            if (locked == SpecimenPath.None) return true;
            var path = PathOf(type);
            return path == SpecimenPath.None || path == locked;
        }

        public static string FormName(SpecimenForm form, bool grown) => form == SpecimenForm.Behemoth ? (grown ? "COLOSSUS" : "BEHEMOTH")
            : form == SpecimenForm.Edge ? (grown ? "EDGE II" : "EDGE") : "FRAIL";

        public static Color FormColor(SpecimenForm form) => form == SpecimenForm.Behemoth ? Amber : form == SpecimenForm.Edge ? Keen : Pale;
    }
}

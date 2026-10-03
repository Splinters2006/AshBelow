using System;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The developers' accounts. Nobody else can create these usernames; signed in to one, a player gets debug admin mode,
    /// the Specimen, and in co-op their own name font with a title beneath it. A badge of 0 is an ordinary player;
    /// 1 and up name the developer, and is what co-op sends so everyone draws their name the same way.
    /// </summary>
    public static class Developers
    {
        public const string Title = "DEVELOPER";
        private static readonly string[] Usernames = { "Splinters06", "tioqy" };
        // One font each, in Resources/Fonts, matching Usernames.
        private static readonly string[] FontPaths = { "Fonts/DejaVuSerif-BoldItalic", "Fonts/DejaVuSansMono-Bold" };
        private static readonly Font[] fonts = new Font[FontPaths.Length];

        public static int BadgeCount => Usernames.Length;

        public static int BadgeFor(string username)
        {
            if (string.IsNullOrEmpty(username)) return 0;
            for (int i = 0; i < Usernames.Length; i++)
                if (string.Equals(username, Usernames[i], StringComparison.OrdinalIgnoreCase)) return i + 1;
            return 0;
        }

        public static bool IsReserved(string username) => BadgeFor(username) > 0;

        /// <summary>The badge's name font, or null for ordinary players (and an unknown badge).</summary>
        public static Font FontFor(int badge)
        {
            if (badge < 1 || badge > FontPaths.Length) return null;
            return fonts[badge - 1] ??= Resources.Load<Font>(FontPaths[badge - 1]);
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>The co-op page of the main menu: host or join a party, which then gathers in the party hall.</summary>
    public sealed class CoopMenu
    {
        private const string NameKey = "AshBelow.CoopName", AddressKey = "AshBelow.CoopAddress";
        private string code = "", address, playerName;
        // The account display name last taken over, so signing in (or renaming on the account page) names you here too.
        private string accountName;

        public void Draw(DungeonRun run)
        {
            var session = run.Coop.Session;
            if (playerName == null)
            {
                playerName = Load(NameKey, "Player");
                address = Load(AddressKey, "127.0.0.1");
                session.RenameLocal(playerName);
            }
            // The colour is picked on the settings page; signing in to (or out of) a developer account changes how the name is drawn for the whole party.
            int nameColor = GameSettings.NameColor;
            if (session.LocalNameColor != nameColor || session.LocalBadge != run.Account.DeveloperBadge)
                session.RestyleLocal(nameColor, run.Account.DeveloperBadge);
            if (run.Account.DisplayName != accountName)
            {
                accountName = run.Account.DisplayName;
                if (!string.IsNullOrEmpty(accountName)) { playerName = accountName; Save(NameKey, playerName); session.RenameLocal(playerName); }
            }
            if (session.State == NetState.Lobby) DrawLobby();
            else if (session.State == NetState.Connecting) DrawConnecting(session);
            else DrawOffline(run, session);
            if (!string.IsNullOrEmpty(session.Status))
                DungeonUi.Label(new Rect(360, 606, 850, 40), session.Status, 15, AbilityCatalog.Gold);
        }

        private void DrawOffline(DungeonRun run, NetSession session)
        {
            SyncClass(run, session);
            DungeonUi.Panel(new Rect(70, 250, 540, 336), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(100, 270, 480, 24), "YOUR NAME", 14, DungeonUi.Muted);
            string renamed = DungeonUi.TextField("coopName", new Rect(100, 298, 480, 46), playerName, 16);
            if (renamed != playerName) { playerName = renamed; Save(NameKey, playerName); session.RenameLocal(playerName); }
            DungeonUi.Label(new Rect(100, 364, 480, 24), "ONLINE  /  UP TO 4 PLAYERS, NO PORT FORWARDING", 14, AbilityCatalog.Gold);
            if (DungeonUi.Button("coopHost", new Rect(100, 394, 480, 52), "Host a party", AbilityCatalog.Gold)) session.HostOnline();
            code = DungeonUi.TextField("coopCode", new Rect(100, 468, 300, 52), code, 12, 24).ToUpperInvariant();
            if (code.Length == 0 && GUI.GetNameOfFocusedControl() != "coopCode")
                DungeonUi.Label(new Rect(114, 468, 280, 52), "JOIN CODE", 20, DungeonUi.Muted * 0.7f, TextAnchor.MiddleLeft);
            if (DungeonUi.Button("coopJoin", new Rect(414, 468, 166, 52), "Join code", DungeonUi.Teal, code.Trim().Length > 0)) session.JoinOnline(code);
            DungeonUi.Label(new Rect(100, 530, 480, 44), "Hosts get a short join code to share with friends.", 15, DungeonUi.Muted);

            DungeonUi.Panel(new Rect(650, 250, 560, 336), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(680, 270, 500, 24), "DIRECT P2P  /  INTERNET OR LAN", 14, DungeonUi.Teal);
            DungeonUi.Label(new Rect(680, 300, 500, 70), $"Connect straight to the host, no online service. The game opens UDP port {NetSession.DefaultPort} on the host's router automatically when it can.", 16, DungeonUi.Muted);
            if (DungeonUi.Button("coopHostLan", new Rect(680, 394, 500, 52), "Host P2P", DungeonUi.Teal)) session.HostDirect();
            string typed = DungeonUi.TextField("coopAddress", new Rect(680, 468, 330, 52), address, 64, 22);
            if (typed != address) { address = typed; Save(AddressKey, address); }
            if (DungeonUi.Button("coopJoinLan", new Rect(1024, 468, 156, 52), "Join IP", DungeonUi.Teal)) session.JoinDirect(address);
            DungeonUi.Label(new Rect(680, 530, 500, 44), "Paste the address the host shares, e.g. 203.0.113.7:7777.", 15, DungeonUi.Muted);
        }

        private static void DrawConnecting(NetSession session)
        {
            DungeonUi.Panel(new Rect(70, 250, 1140, 200), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(100, 300, 1080, 50), "Connecting" + new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3), 32, DungeonUi.Text);
            if (DungeonUi.Button("coopCancel", new Rect(100, 380, 268, 48), "Cancel", DungeonUi.Muted)) { session.Leave(); session.Status = null; }
        }

        /// <summary>A party is joined in the party hall (see <see cref="PartyHall"/>); this only shows for the frame before it opens.</summary>
        private static void DrawLobby()
        {
            DungeonUi.Panel(new Rect(70, 250, 1140, 200), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(100, 300, 1080, 50), "Entering the party hall…", 32, DungeonUi.Text);
        }

        /// <summary>Starts the party with the hero last picked on the solo screen.</summary>
        private static void SyncClass(DungeonRun run, NetSession session)
        {
            for (int i = 0; i < run.Characters.Count; i++)
                if (run.Characters[i] == run.SelectedCharacter && session.LocalClassIndex != i) session.SetLocalClass(i);
        }

        /// <summary>The back button: leaves any party first.</summary>
        public static void Leave(DungeonRun run)
        {
            if (run.Coop.Session.State != NetState.Offline) run.Coop.Session.Leave();
            run.Coop.Session.Status = null;
        }

        private static string Load(string key, string fallback)
        {
            try { return PlayerPrefs.GetString(key, fallback); }
            catch { return fallback; }
        }

        private static void Save(string key, string value)
        {
            try { PlayerPrefs.SetString(key, value); }
            catch { /* Preferences are a convenience only. */ }
        }
    }
}

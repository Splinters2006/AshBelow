using UnityEngine;

namespace Slopgame
{
    /// <summary>The co-op page of the main menu: host or join a party, pick a hero, and begin the descent together.</summary>
    public sealed class CoopMenu
    {
        private const string NameKey = "AshBelow.CoopName", AddressKey = "AshBelow.CoopAddress";
        private string code = "", address, playerName;

        public void Draw(DungeonRun run)
        {
            var session = run.Coop.Session;
            if (playerName == null)
            {
                playerName = Load(NameKey, "Player");
                address = Load(AddressKey, "127.0.0.1");
                session.RenameLocal(playerName);
            }
            if (session.State == NetState.Lobby) DrawLobby(run, session);
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

        private static void DrawLobby(DungeonRun run, NetSession session)
        {
            DungeonUi.Panel(new Rect(70, 250, 540, 336), DungeonUi.PanelColor);
            if (!string.IsNullOrEmpty(session.JoinCode))
            {
                DungeonUi.Label(new Rect(100, 268, 300, 24), "JOIN CODE", 14, DungeonUi.Muted);
                DungeonUi.Label(new Rect(100, 290, 330, 54), session.JoinCode, 40, AbilityCatalog.Gold);
                if (DungeonUi.Button("coopCopy", new Rect(450, 290, 130, 44), "Copy", DungeonUi.Teal)) GUIUtility.systemCopyBuffer = session.JoinCode;
            }
            else if (session.IsHost) DrawDirectHost(session);
            else
            {
                DungeonUi.Label(new Rect(100, 268, 480, 24), "P2P PARTY", 14, DungeonUi.Muted);
                DungeonUi.Label(new Rect(100, 296, 480, 50), "Connected directly to the host.", 16, DungeonUi.Text);
            }
            DungeonUi.Label(new Rect(100, 360, 480, 24), $"PARTY  {session.Peers.Count} / {NetSession.MaxPlayers}", 14, DungeonUi.Muted);
            for (int i = 0; i < session.Peers.Count; i++)
            {
                var peer = session.Peers[i];
                var hero = run.Characters[Mathf.Clamp(peer.ClassIndex, 0, run.Characters.Count - 1)];
                string tags = (i == 0 ? "  /  HOST" : "") + (peer.Id == session.LocalId ? "  /  YOU" : "");
                DungeonUi.Label(new Rect(100, 390 + i * 46, 300, 40), peer.Name + tags, 18, DungeonUi.Text);
                DungeonUi.Label(new Rect(400, 390 + i * 46, 180, 40), hero.DisplayName, 18, hero.Color, TextAnchor.UpperRight);
            }

            DungeonUi.Panel(new Rect(650, 250, 560, 336), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(680, 268, 500, 24), "YOUR HERO", 14, DungeonUi.Muted);
            for (int i = 0; i < run.Characters.Count; i++)
            {
                var hero = run.Characters[i];
                bool selected = i == session.LocalClassIndex;
                if (DungeonUi.Button("coopClass" + i, new Rect(680 + i % 2 * 256, 300 + i / 2 * 58, 244, 48),
                    hero.DisplayName, selected ? hero.Color : DungeonUi.Muted))
                {
                    session.SetLocalClass(i);
                    run.SelectCharacter(hero);
                }
            }
            DungeonUi.Label(new Rect(680, 486, 500, 60), "Enemies grow tougher with every hero. Each player earns Ash into their own save.", 15, DungeonUi.Muted);

            if (session.IsHost)
            {
                if (DungeonUi.Button("coopBegin", new Rect(860, 598, 350, 48), "Begin descent", AbilityCatalog.Gold)) run.Coop.HostBeginRun();
            }
            else DungeonUi.Label(new Rect(860, 608, 350, 30), "Waiting for the host to begin…", 17, DungeonUi.Muted, TextAnchor.MiddleRight);
        }

        /// <summary>The share panel of a direct host: the internet address with a copy button, the LAN one and the router result.</summary>
        private static void DrawDirectHost(NetSession session)
        {
            string shared = session.PublicAddress ?? session.LanAddress;
            DungeonUi.Label(new Rect(100, 262, 330, 22), session.PublicAddress != null ? "SHARE THIS ADDRESS" : "LAN ADDRESS", 14, DungeonUi.Muted);
            DungeonUi.Label(new Rect(100, 282, 340, 40), shared ?? "Finding your address…", shared != null ? 26 : 18, AbilityCatalog.Gold);
            if (shared != null && DungeonUi.Button("coopCopy", new Rect(450, 280, 130, 40), "Copy", DungeonUi.Teal)) GUIUtility.systemCopyBuffer = shared;
            if (session.PublicAddress != null && session.LanAddress != null)
                DungeonUi.Label(new Rect(100, 318, 480, 20), "Same network: " + session.LanAddress, 13, DungeonUi.Muted);
            // Below the panels, where the status line would go (a lobby host has no status).
            if (!string.IsNullOrEmpty(session.PortStatus) && string.IsNullOrEmpty(session.Status))
                DungeonUi.Label(new Rect(70, 596, 770, 50), session.PortStatus, 14, DungeonUi.Muted);
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

using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The party hall's HUD (drawn by <see cref="DungeonHud"/>): the party panel in the top-right corner, with the join
    /// code or address to share, who is in and ready, and a Ready button; and the names over statues and teammates.
    /// </summary>
    public static class PartyHallHud
    {
        public static readonly Rect PanelRect = new Rect(816, 84, 440, 372);
        private const float RowTop = 128, RowStep = 42;
        // Each roster row's columns, from the panel's inner left edge: the player, their hero, READY, then the host's "..." menu.
        private const float NameWidth = 194, HeroLeft = 200, HeroWidth = 110, ReadyLeft = 314;
        // The guest whose "..." menu the host has open, and until when its Kick waits for a confirming second click.
        private static ulong? menuFor;
        private static float kickConfirmUntil;

        /// <summary>The party panel, on the HUD's top-right canvas.</summary>
        public static void DrawPanel(DungeonRun run)
        {
            var session = run.Coop.Session;
            var rect = PanelRect;
            DungeonUi.Panel(rect, DungeonUi.PanelColor);
            float left = rect.x + 20, width = rect.width - 40;
            int ready = 0;
            foreach (var peer in session.Peers) if (peer.Ready) ready++;
            DungeonUi.Label(new Rect(left, rect.y + 14, width, 22), $"PARTY  {session.Peers.Count} / {NetSession.MaxPlayers}", 15, DungeonUi.Muted);
            float countdown = run.Coop.CountdownLeft;
            DungeonUi.Label(new Rect(left, rect.y + 14, width, 22), countdown >= 0f ? $"STARTING IN {Mathf.CeilToInt(countdown)}" : $"{ready} READY", 15,
                ready == session.Peers.Count ? AbilityCatalog.Gold : DungeonUi.Muted, TextAnchor.UpperRight);
            DrawShare(session, new Rect(left, rect.y + 44, width, 64));
            DungeonUi.Divider(new Rect(left, rect.y + RowTop - 8, width, 1));
            for (int i = 0; i < session.Peers.Count; i++)
            {
                var peer = session.Peers[i];
                var hero = run.Characters[Mathf.Clamp(peer.ClassIndex, 0, run.Characters.Count - 1)];
                float y = rect.y + RowTop + i * RowStep;
                // Names and heroes too long for their column slide along it rather than being cut off.
                NameTag.Draw(new Rect(left, y, NameWidth, 26), peer.Name + (peer.Id == session.LocalId ? " (you)" : ""), peer.NameColor, peer.Badge, 17, titleBelow: false);
                DungeonUi.MarqueeLabel(new Rect(left + HeroLeft, y + 1, HeroWidth, 26), hero.DisplayName, 17, hero.Color, TextAnchor.UpperRight);
                DungeonUi.Label(new Rect(left + ReadyLeft, y + 3, 56, 26), peer.Ready ? "READY" : "—", 14, peer.Ready ? AbilityCatalog.Gold : DungeonUi.Muted, TextAnchor.UpperRight);
                // The host's menu for each guest, with Kick in it.
                if (session.IsHost && peer.Id != session.LocalId
                    && DungeonUi.Button("hallPeerMenu" + peer.Id, new Rect(left + width - 26, y - 1, 26, 28), "...", menuFor == peer.Id ? AbilityCatalog.Gold : DungeonUi.Muted, true, 15))
                {
                    menuFor = menuFor == peer.Id ? null : peer.Id;
                    kickConfirmUntil = 0f;
                }
            }
            DrawPeerMenu(session, left, rect.y);
            bool localReady = session.LocalReady;
            if (DungeonUi.Button("hallReady", new Rect(left, rect.yMax - 64, width, 48), localReady ? "Cancel ready" : "Ready up",
                localReady ? DungeonUi.Teal : AbilityCatalog.Gold))
                session.SetLocalReady(!localReady);
            // A direct host's router result is long, so it sits under the panel.
            if (session.IsHost && session.IsDirect && !string.IsNullOrEmpty(session.PortStatus))
                DungeonUi.Label(new Rect(rect.x, rect.yMax + 8, rect.width, 90), session.PortStatus, 14, DungeonUi.Muted);
        }

        /// <summary>
        /// The open "..." menu: Kick, over the guest's hero and ready columns (so it never sits on another button). Kick
        /// asks for a second click.
        /// </summary>
        private static void DrawPeerMenu(NetSession session, float left, float top)
        {
            if (menuFor == null) return;
            int row = -1;
            for (int i = 0; i < session.Peers.Count; i++) if (session.Peers[i].Id == menuFor) row = i;
            if (row < 0 || !session.IsHost) { menuFor = null; return; }
            var area = new Rect(left + HeroLeft - 4, top + RowTop + row * RowStep - 3, ReadyLeft + 56 - HeroLeft + 4, 32);
            DungeonUi.Panel(new Rect(area.x - 2, area.y - 2, area.width + 4, area.height + 4), DungeonUi.Background);
            bool confirming = Time.unscaledTime < kickConfirmUntil;
            if (DungeonUi.Button("hallKick", area, confirming ? "Confirm?" : "Kick", new Color(1f, 0.4f, 0.4f), true, 16))
            {
                if (!confirming) { kickConfirmUntil = Time.unscaledTime + 3f; return; }
                session.Kick(menuFor.Value);
                menuFor = null;
            }
        }

        /// <summary>What friends need to join: the join code, or a direct host's address, with a copy button.</summary>
        private static void DrawShare(NetSession session, Rect area)
        {
            string caption, shared;
            if (!string.IsNullOrEmpty(session.JoinCode)) { caption = "JOIN CODE"; shared = session.JoinCode; }
            else if (session.IsHost)
            {
                shared = session.PublicAddress ?? session.LanAddress;
                caption = session.PublicAddress != null ? "SHARE THIS ADDRESS" : "LAN ADDRESS";
            }
            else
            {
                DungeonUi.Label(new Rect(area.x, area.y, area.width, 22), "P2P PARTY", 14, DungeonUi.Muted);
                DungeonUi.Label(new Rect(area.x, area.y + 22, area.width, 28), "Connected directly to the host.", 17, DungeonUi.Text);
                return;
            }
            DungeonUi.Label(new Rect(area.x, area.y, area.width, 22), caption, 14, DungeonUi.Muted);
            DungeonUi.Label(new Rect(area.x, area.y + 22, area.width - 90, 40), shared ?? "Finding your address…", shared != null && shared.Length <= 10 ? 32 : 19, AbilityCatalog.Gold);
            if (shared != null && DungeonUi.Button("hallCopy", new Rect(area.xMax - 82, area.y + 22, 82, 38), "Copy", DungeonUi.Teal, true, 17))
                GUIUtility.systemCopyBuffer = shared;
        }

        /// <summary>
        /// Hero names under the statues, the portal's caption and READY over ready teammates, on the centred canvas.
        /// Drawn under the <see cref="DescentIris"/>; the countdown is drawn over it by <see cref="DrawCountdown"/>.
        /// </summary>
        public static void DrawWorldLabels(DungeonRun run)
        {
            var hall = run.Hall;
            if (hall == null || run.View == null || run.Player == null) return;
            Vector2 hero = run.Player.transform.position;
            var near = hall.StatueAt(hero);
            foreach (var statue in hall.Statues)
            {
                bool lit = statue == near || statue.Character == run.SelectedCharacter;
                Label(run, (Vector2)statue.transform.position + Vector2.down * 0.95f, statue.Character.DisplayName.ToUpperInvariant(), 12,
                    lit ? statue.Character.Color : DungeonUi.Muted);
            }
            var session = run.Coop.Session;
            Label(run, hall.Portal + Vector2.down * 1.35f, session.LocalReady ? "READY" : "READY PORTAL", 12, session.LocalReady ? AbilityCatalog.Gold : DungeonUi.Muted);
            foreach (var teammate in run.Coop.RemoteHeroes)
                if (teammate != null && session.Peer(teammate.Id)?.Ready == true)
                    Label(run, (Vector2)teammate.transform.position + Vector2.up * 1.6f, "READY", 11, AbilityCatalog.Gold);
        }

        /// <summary>The descent countdown, on the centred canvas over the iris.</summary>
        public static void DrawCountdown(DungeonRun run)
        {
            float countdown = run.Coop.CountdownLeft;
            if (countdown < 0f) return;
            // Each new second lands big and settles.
            float beat = 1f - Mathf.Repeat(countdown, 1f);
            int size = Mathf.RoundToInt(Mathf.Lerp(54f, 42f, Mathf.Clamp01(beat * 4f)));
            DungeonUi.Label(new Rect(340, 160, 600, 24), "THE DESCENT BEGINS IN", 15, AbilityCatalog.Gold, TextAnchor.MiddleCenter);
            DungeonUi.Label(new Rect(340, 184, 600, 70), Mathf.CeilToInt(countdown).ToString(), size, DungeonUi.Text, TextAnchor.MiddleCenter);
        }

        private static void Label(DungeonRun run, Vector2 world, string text, int size, Color color)
        {
            Vector3 screen = run.View.WorldToScreenPoint(world);
            if (screen.z < 0f) return;
            Vector2 point = DungeonUi.ScreenToCanvas(screen, GameSettings.HudScale);
            DungeonUi.Label(new Rect(point.x - 90, point.y - 9, 180, 20), text, size, color, TextAnchor.MiddleCenter);
        }
    }
}

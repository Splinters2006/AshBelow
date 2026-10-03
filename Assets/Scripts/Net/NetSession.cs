using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Slopgame
{
    /// <summary>One connected player as the host sees them; the host shares this roster with everyone.</summary>
    public sealed class CoopPeer
    {
        public ulong Id;
        public string Name;
        public int ClassIndex;
        public bool Ready;
        /// <summary>The picked name colour (<see cref="NameTag.Colors"/>) and developer badge (<see cref="Developers"/>).</summary>
        public int NameColor, Badge;
    }

    public enum NetState { Offline, Connecting, Lobby, InRun }

    /// <summary>
    /// Owns the Netcode connection (Unity Relay join codes or direct peer-to-peer IP), the lobby roster and message
    /// plumbing. The host is a listen server: guests only ever talk to the host, which relays to the other guests.
    /// Direct hosts open their router port automatically through <see cref="PortMapper"/> when the router allows it.
    /// </summary>
    public sealed class NetSession : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        public const ushort DefaultPort = 7777;
        private const string HelloMessage = "ab.hello", RosterMessage = "ab.roster";

        public NetState State { get; private set; }
        public bool IsHost => manager != null && manager.IsHost;
        public ulong LocalId => manager != null ? manager.LocalClientId : 0;
        public string JoinCode { get; private set; }
        public string Status { get; set; }
        /// <summary>Direct hosts only: the "ip:port" friends on the internet join, once it is known.</summary>
        public string PublicAddress { get; private set; }
        /// <summary>Direct hosts only: the "ip:port" players on the same network join.</summary>
        public string LanAddress { get; private set; }
        /// <summary>Direct hosts only: how reachable the party is from the internet, for the lobby.</summary>
        public string PortStatus { get; private set; }
        public bool IsDirect => State != NetState.Offline && session == null;
        public IReadOnlyList<CoopPeer> Peers => peers;
        public string LocalName { get; set; } = "Player";
        public int LocalNameColor { get; private set; }
        public int LocalBadge { get; private set; }
        public int LocalClassIndex { get; private set; }
        public bool LocalReady => Peer(LocalId)?.Ready == true;
        public bool AllReady => State == NetState.Lobby && peers.Count > 0 && peers.TrueForAll(peer => peer.Ready);
        public event Action<ulong> PeerLeft;
        public event Action Disconnected;

        private readonly List<CoopPeer> peers = new List<CoopPeer>();
        private readonly Dictionary<string, CustomMessagingManager.HandleNamedMessageDelegate> handlers =
            new Dictionary<string, CustomMessagingManager.HandleNamedMessageDelegate>();
        private NetworkManager manager;
        private UnityTransport transport;
        private ISession session;
        private PortMapper portMapper;

        public CoopPeer Peer(ulong id) => peers.Find(peer => peer.Id == id);

        /// <summary>Registers a message handler; it is (re)attached every time a connection starts.</summary>
        public void Handle(string name, CustomMessagingManager.HandleNamedMessageDelegate handler)
        {
            handlers[name] = handler;
            manager?.CustomMessagingManager?.RegisterNamedMessageHandler(name, handler);
        }

        private void EnsureManager()
        {
            if (manager != null) return;
            var holder = new GameObject("Network Manager");
            holder.SetActive(false);
            DontDestroyOnLoad(holder);
            transport = holder.AddComponent<UnityTransport>();
            manager = holder.AddComponent<NetworkManager>();
            // Everything in the dungeon is generated from the shared seed, so no scene or prefab syncing is needed.
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = false };
            manager.RunInBackground = true;
            holder.SetActive(true);
            manager.OnClientStarted += OnStarted;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            manager.OnTransportFailure += () => Drop("The connection failed.");
        }

        private void OnStarted()
        {
            foreach (var pair in handlers) manager.CustomMessagingManager.RegisterNamedMessageHandler(pair.Key, pair.Value);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(HelloMessage, ReceiveHello);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RosterMessage, ReceiveRoster);
            if (manager.IsHost)
            {
                peers.Clear();
                peers.Add(new CoopPeer { Id = manager.LocalClientId, Name = LocalName, ClassIndex = LocalClassIndex, NameColor = LocalNameColor, Badge = LocalBadge });
                State = NetState.Lobby;
            }
        }

        private void OnClientConnected(ulong id)
        {
            if (manager.IsHost)
            {
                if (id == manager.LocalClientId) return;
                if (State == NetState.InRun || peers.Count >= MaxPlayers)
                {
                    manager.DisconnectClient(id, State == NetState.InRun ? "That descent has already started." : "The party is full.");
                    return;
                }
                peers.Add(new CoopPeer { Id = id, Name = "Joining…", ClassIndex = 0 });
                BroadcastRoster();
            }
            else if (id == manager.LocalClientId)
            {
                State = NetState.Lobby;
                Status = null;
                SendHello();
            }
        }

        private void OnClientDisconnected(ulong id)
        {
            if (manager == null) return;
            if (manager.IsHost && id != manager.LocalClientId)
            {
                peers.RemoveAll(peer => peer.Id == id);
                BroadcastRoster();
                PeerLeft?.Invoke(id);
                return;
            }
            if (!manager.IsHost)
            {
                // Netcode reports its own technical text unless the host sent a reason (such as "The party is full.").
                string reason = manager.DisconnectReason;
                bool fromHost = !string.IsNullOrEmpty(reason) && !reason.StartsWith("[Disconnect Event]") && !reason.Contains("host shutting down");
                Drop(fromHost ? reason : State == NetState.Connecting ? "Could not reach that game." : "The host left the game.");
            }
        }

        public void SetLocalClass(int index)
        {
            if (LocalClassIndex == index) return;
            LocalClassIndex = index;
            var self = State != NetState.Offline ? Peer(LocalId) : null;
            if (self != null) { self.ClassIndex = index; self.Ready = false; }
            if (IsHost) BroadcastRoster();
            else if (State == NetState.Lobby) SendHello();
        }

        public void SetLocalReady(bool ready)
        {
            if (State != NetState.Lobby) return;
            var self = Peer(LocalId);
            if (self == null) return;
            self.Ready = ready;
            if (IsHost) BroadcastRoster();
            else SendHello();
        }

        public async void HostOnline()
        {
            if (!await BeginConnecting()) return;
            try
            {
                var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork();
                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                JoinCode = session.Code;
            }
            catch (Exception error) { Fail("Could not host online", error); }
        }

        public async void JoinOnline(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) { Status = "Enter the host's join code."; return; }
            if (!await BeginConnecting()) return;
            try
            {
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                JoinCode = code;
            }
            catch (Exception error) { Fail("Could not join " + code, error); }
        }

        /// <summary>Hosts a peer-to-peer party on <paramref name="port"/>, reachable on the LAN and, when the router allows it, the internet.</summary>
        public void HostDirect(ushort port = DefaultPort, bool openRouterPort = true)
        {
            EnsureManager();
            State = NetState.Connecting;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            JoinCode = null;
            var local = PortMapper.LocalAddress();
            LanAddress = local != null ? $"{local}:{port}" : null;
            PublicAddress = null;
            if (!manager.StartHost()) { Drop("Could not host on port " + port + ". Is another copy of the game already hosting?"); return; }
            Status = null;
            PortStatus = openRouterPort ? "Opening a port on your router…" : "Same network only.";
            if (openRouterPort) OpenRouterPort(port);
        }

        private async void OpenRouterPort(ushort port)
        {
            var mapper = portMapper = new PortMapper();
            PortMapResult result;
            try { result = await mapper.OpenAsync(port); }
            catch (Exception error) { result = new PortMapResult { Problem = error.Message }; }
            if (mapper != portMapper || State == NetState.Offline) { _ = mapper.CloseAsync(); return; }
            ushort shared = result.Opened ? result.ExternalPort : port;
            var external = result.ExternalAddress != null && !PortMapper.IsPrivate(result.ExternalAddress)
                ? result.ExternalAddress : await LookUpPublicAddress();
            if (mapper != portMapper || State == NetState.Offline) return;
            PublicAddress = external != null ? $"{external}:{shared}" : null;
            if (result.Opened && result.ExternalAddress != null && PortMapper.IsPrivate(result.ExternalAddress))
                PortStatus = "Your router opened the port, but it sits behind another router or your provider's shared address (CGNAT), "
                    + "so internet friends probably can't reach you. Use Host a party (online) instead.";
            else if (result.Opened)
                PortStatus = $"Router port opened automatically ({(result.Method == PortMapMethod.Upnp ? "UPnP" : "NAT-PMP")}). Friends anywhere can join.";
            else
                PortStatus = $"{result.Problem} Same-network friends can still join; for internet friends, forward UDP port {port} "
                    + "to this PC or use Host a party (online).";
        }

        /// <summary>Asks a public "what is my IP" service; only used when the router did not report a usable address.</summary>
        private static async Task<IPAddress> LookUpPublicAddress()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                string text = (await http.GetStringAsync("https://api.ipify.org")).Trim();
                return IPAddress.TryParse(text, out var address) && !PortMapper.IsPrivate(address) ? address : null;
            }
            catch { return null; }
        }

        /// <summary>Joins a direct host. Accepts "ip", "ip:port", "hostname:port" or "[ipv6]:port".</summary>
        public async void JoinDirect(string address, ushort port = DefaultPort)
        {
            if (string.IsNullOrWhiteSpace(address)) address = "127.0.0.1";
            if (!PortMapper.TryParseEndpoint(address, port, out string host, out port)) { Status = "That address doesn't look right. Use IP or IP:PORT."; return; }
            EnsureManager();
            State = NetState.Connecting;
            JoinCode = null;
            Status = "Connecting to " + host + "…";
            if (!IPAddress.TryParse(host, out var ip))
            {
                try
                {
                    var found = await Dns.GetHostAddressesAsync(host);
                    ip = Array.Find(found, a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ?? (found.Length > 0 ? found[0] : null);
                }
                catch { ip = null; }
                if (State != NetState.Connecting) return;
                if (ip == null) { Drop("Could not find " + host + "."); return; }
            }
            transport.SetConnectionData(ip.ToString(), port);
            if (!manager.StartClient()) Drop("Could not connect to " + address.Trim() + ".");
        }

        private async Task<bool> BeginConnecting()
        {
            EnsureManager();
            State = NetState.Connecting;
            Status = "Connecting to Unity services…";
            try
            {
                // Signed-in accounts play as themselves; everyone else gets an anonymous player.
                await PlayerAccount.EnsureSignedInAsync();
                return State == NetState.Connecting;
            }
            catch (Exception error)
            {
                Fail("Unity services are unavailable", error);
                return false;
            }
        }

        private void Fail(string what, Exception error)
        {
            Debug.LogWarning(what + ": " + error);
            Drop(what + ". " + FriendlyError(error));
        }

        private static string FriendlyError(Exception error)
        {
            string message = error.Message ?? "";
            // Service start-up failures arrive wrapped in several layers of inner exceptions.
            if (error.ToString().Contains("UnityProjectNotLinkedException"))
                return "Online play is not set up in this build yet (no Unity Cloud project). Use LAN / direct IP.";
            if (error is SessionException) return message;
            return "Check your internet connection and try again.";
        }

        public void StartRun() { if (State == NetState.Lobby) State = NetState.InRun; }
        public void ReturnToLobby()
        {
            if (State == NetState.InRun) State = NetState.Lobby;
            foreach (var peer in peers) peer.Ready = false;
            if (IsHost) BroadcastRoster();
        }

        /// <summary>Leaves the session and shuts Netcode down. Safe to call at any time.</summary>
        public void Leave()
        {
            var leaving = session;
            session = null;
            if (leaving != null) _ = LeaveSession(leaving);
            if (manager != null && !manager.ShutdownInProgress) manager.Shutdown();
            ClosePort(false);
            peers.Clear();
            JoinCode = null;
            PublicAddress = LanAddress = PortStatus = null;
            State = NetState.Offline;
        }

        private static async Task LeaveSession(ISession leaving)
        {
            try { await leaving.LeaveAsync(); }
            catch (Exception error) { Debug.LogWarning("Leaving the session failed: " + error.Message); }
        }

        private void Drop(string reason)
        {
            bool wasConnected = State != NetState.Offline;
            Leave();
            Status = reason;
            if (wasConnected) Disconnected?.Invoke();
        }

        private void ClosePort(bool wait)
        {
            var mapper = portMapper;
            portMapper = null;
            if (mapper == null) return;
            var closing = mapper.CloseAsync();
            // PortMapper never needs the main thread, so a short blocking wait on quit cannot deadlock.
            if (wait) try { closing.Wait(1500); } catch { /* Best effort; the lease expires anyway. */ }
        }

        private void OnApplicationQuit()
        {
            ClosePort(true);
            Leave();
        }

        // ---------------------------------------------------------------- messaging

        public static FastBufferWriter Writer(int size = 256) => new FastBufferWriter(size, Allocator.Temp, 64 * 1024);

        /// <summary>Guests send to the host; the host sends to every guest, optionally skipping one.</summary>
        public void Send(string name, FastBufferWriter writer, NetworkDelivery delivery = NetworkDelivery.ReliableSequenced, ulong? skip = null)
        {
            if (manager == null || !manager.IsListening || manager.CustomMessagingManager == null) return;
            if (!manager.IsHost)
            {
                manager.CustomMessagingManager.SendNamedMessage(name, NetworkManager.ServerClientId, writer, delivery);
                return;
            }
            foreach (var id in manager.ConnectedClientsIds)
                if (id != manager.LocalClientId && id != skip)
                    manager.CustomMessagingManager.SendNamedMessage(name, id, writer, delivery);
        }

        public void SendTo(ulong client, string name, FastBufferWriter writer, NetworkDelivery delivery = NetworkDelivery.ReliableSequenced)
        {
            if (manager == null || !manager.IsListening || manager.CustomMessagingManager == null) return;
            if (client == manager.LocalClientId) return;
            manager.CustomMessagingManager.SendNamedMessage(name, client, writer, delivery);
        }

        private void SendHello()
        {
            using var writer = Writer();
            writer.WriteValueSafe(LocalName ?? "Player");
            writer.WriteValueSafe(LocalClassIndex);
            writer.WriteValueSafe(LocalReady);
            writer.WriteValueSafe(LocalNameColor);
            writer.WriteValueSafe(LocalBadge);
            Send(HelloMessage, writer);
        }

        private void ReceiveHello(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || State != NetState.Lobby) return;
            reader.ReadValueSafe(out string name);
            reader.ReadValueSafe(out int classIndex);
            reader.ReadValueSafe(out bool ready);
            reader.ReadValueSafe(out int nameColor);
            reader.ReadValueSafe(out int badge);
            var peer = Peer(sender);
            if (peer == null) return;
            peer.Name = CleanName(name);
            peer.NameColor = NameTag.ClampColor(nameColor);
            peer.Badge = badge >= 0 && badge <= Developers.BadgeCount ? badge : 0;
            peer.Ready = peer.ClassIndex == classIndex && ready;
            peer.ClassIndex = classIndex;
            BroadcastRoster();
        }

        public void RenameLocal(string name)
        {
            LocalName = CleanName(name);
            var self = State != NetState.Offline ? Peer(LocalId) : null;
            if (self != null) self.Name = LocalName;
            if (IsHost) BroadcastRoster();
            else if (State == NetState.Lobby) SendHello();
        }

        /// <summary>The local name's colour and developer badge, shared with the party like the name.</summary>
        public void RestyleLocal(int nameColor, int badge)
        {
            LocalNameColor = NameTag.ClampColor(nameColor);
            LocalBadge = badge;
            var self = State != NetState.Offline ? Peer(LocalId) : null;
            if (self != null) { self.NameColor = LocalNameColor; self.Badge = LocalBadge; }
            if (IsHost) BroadcastRoster();
            else if (State == NetState.Lobby) SendHello();
        }

        public static string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > 16) name = name.Substring(0, 16);
            return name.Length == 0 ? "Player" : name;
        }

        private void BroadcastRoster()
        {
            var self = Peer(LocalId);
            if (self != null) { self.Name = LocalName; self.ClassIndex = LocalClassIndex; self.NameColor = LocalNameColor; self.Badge = LocalBadge; }
            using var writer = Writer();
            writer.WriteValueSafe(peers.Count);
            foreach (var peer in peers)
            {
                writer.WriteValueSafe(peer.Id);
                writer.WriteValueSafe(peer.Name);
                writer.WriteValueSafe(peer.ClassIndex);
                writer.WriteValueSafe(peer.Ready);
                writer.WriteValueSafe(peer.NameColor);
                writer.WriteValueSafe(peer.Badge);
            }
            Send(RosterMessage, writer);
        }

        private void ReceiveRoster(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int count);
            var previous = new List<ulong>();
            foreach (var peer in peers) previous.Add(peer.Id);
            peers.Clear();
            for (int i = 0; i < count; i++)
            {
                var peer = new CoopPeer();
                reader.ReadValueSafe(out peer.Id);
                reader.ReadValueSafe(out peer.Name);
                reader.ReadValueSafe(out peer.ClassIndex);
                reader.ReadValueSafe(out peer.Ready);
                reader.ReadValueSafe(out peer.NameColor);
                reader.ReadValueSafe(out peer.Badge);
                peers.Add(peer);
            }
            foreach (var id in previous)
                if (Peer(id) == null) PeerLeft?.Invoke(id);
        }
    }
}

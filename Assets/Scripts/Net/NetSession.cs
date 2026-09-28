using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
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
    }

    public enum NetState { Offline, Connecting, Lobby, InRun }

    /// <summary>
    /// Owns the Netcode connection (Unity Relay join codes or direct IP), the lobby roster and message plumbing.
    /// The host is a listen server: guests only ever talk to the host, which relays to the other guests.
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
        public IReadOnlyList<CoopPeer> Peers => peers;
        public string LocalName { get; set; } = "Player";
        public int LocalClassIndex { get; private set; }
        public event Action<ulong> PeerLeft;
        public event Action Disconnected;

        private readonly List<CoopPeer> peers = new List<CoopPeer>();
        private readonly Dictionary<string, CustomMessagingManager.HandleNamedMessageDelegate> handlers =
            new Dictionary<string, CustomMessagingManager.HandleNamedMessageDelegate>();
        private NetworkManager manager;
        private UnityTransport transport;
        private ISession session;
        private bool servicesReady;

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
                peers.Add(new CoopPeer { Id = manager.LocalClientId, Name = LocalName, ClassIndex = LocalClassIndex });
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
            LocalClassIndex = index;
            var self = State != NetState.Offline ? Peer(LocalId) : null;
            if (self != null) self.ClassIndex = index;
            if (IsHost) BroadcastRoster();
            else if (State == NetState.Lobby) SendHello();
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

        public void HostDirect(ushort port = DefaultPort)
        {
            EnsureManager();
            State = NetState.Connecting;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            JoinCode = null;
            if (!manager.StartHost()) Drop("Could not host on port " + port + ".");
            else Status = "Hosting on port " + port + ". Friends join with your IP address.";
        }

        public void JoinDirect(string address, ushort port = DefaultPort)
        {
            EnsureManager();
            State = NetState.Connecting;
            transport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), port);
            JoinCode = null;
            Status = "Connecting…";
            if (!manager.StartClient()) Drop("Could not connect to " + address + ".");
        }

        private async Task<bool> BeginConnecting()
        {
            EnsureManager();
            State = NetState.Connecting;
            Status = "Connecting to Unity services…";
            try
            {
                if (!servicesReady)
                {
                    // A per-process profile lets several copies of the game on one PC sign in as different players.
                    var options = new InitializationOptions().SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 12));
                    await UnityServices.InitializeAsync(options);
                    servicesReady = true;
                }
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
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
        public void ReturnToLobby() { if (State == NetState.InRun) State = NetState.Lobby; }

        /// <summary>Leaves the session and shuts Netcode down. Safe to call at any time.</summary>
        public void Leave()
        {
            var leaving = session;
            session = null;
            if (leaving != null) _ = LeaveSession(leaving);
            if (manager != null && !manager.ShutdownInProgress) manager.Shutdown();
            peers.Clear();
            JoinCode = null;
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

        private void OnApplicationQuit() { Leave(); }

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
            Send(HelloMessage, writer);
        }

        private void ReceiveHello(ulong sender, FastBufferReader reader)
        {
            if (!IsHost) return;
            reader.ReadValueSafe(out string name);
            reader.ReadValueSafe(out int classIndex);
            var peer = Peer(sender);
            if (peer == null) return;
            peer.Name = CleanName(name);
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

        public static string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > 16) name = name.Substring(0, 16);
            return name.Length == 0 ? "Player" : name;
        }

        private void BroadcastRoster()
        {
            var self = Peer(LocalId);
            if (self != null) { self.Name = LocalName; self.ClassIndex = LocalClassIndex; }
            using var writer = Writer();
            writer.WriteValueSafe(peers.Count);
            foreach (var peer in peers)
            {
                writer.WriteValueSafe(peer.Id);
                writer.WriteValueSafe(peer.Name);
                writer.WriteValueSafe(peer.ClassIndex);
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
                peers.Add(peer);
            }
            foreach (var id in previous)
                if (Peer(id) == null) PeerLeft?.Invoke(id);
        }
    }
}

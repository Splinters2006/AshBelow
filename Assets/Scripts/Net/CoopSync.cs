using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Keeps a co-op run in step. Every machine builds the same floors from the shared seed and simulates its own hero;
    /// the host runs enemy AI and decides kills, floor advances and the end of the run.
    /// </summary>
    public sealed class CoopSync : MonoBehaviour
    {
        private const float StateInterval = 0.05f, SnapshotInterval = 1f / 15f;

        public DungeonRun Run { get; set; }
        public NetSession Session { get; private set; }
        public bool Active => Session != null && Session.State == NetState.InRun;
        public bool IsHost => Session != null && Session.IsHost;
        public ulong LocalId => Session != null ? Session.LocalId : 0;
        public IReadOnlyList<RemoteHero> RemoteHeroes => remoteHeroes;
        /// <summary>The player whose attack is being applied on the host; kills credit them.</summary>
        public ulong Attacker { get; private set; }
        public bool WaitingForTeam { get; private set; }
        public int WaitingCount => IsHost ? Mathf.Max(0, Session.Peers.Count - choicesDone.Count) : 0;
        public bool RunOver { get; private set; }

        private readonly List<RemoteHero> remoteHeroes = new List<RemoteHero>();
        private readonly Dictionary<ushort, DungeonEnemy> enemies = new Dictionary<ushort, DungeonEnemy>();
        private readonly Dictionary<int, EnemyProjectile> bolts = new Dictionary<int, EnemyProjectile>();
        private readonly HashSet<ulong> choicesDone = new HashSet<ulong>(), dead = new HashSet<ulong>();
        private CoopChoice? openChoice;
        private float nextState, nextSnapshot;
        private int nextBolt;

        private void Awake()
        {
            Session = gameObject.AddComponent<NetSession>();
            Session.PeerLeft += OnPeerLeft;
            Session.Disconnected += () => { if (Run != null) Run.ShowMainMenu(true); };
            Session.Handle(CoopMessages.Start, OnStart);
            Session.Handle(CoopMessages.Lobby, (sender, reader) => { if (!IsHost) ReturnToLobbyLocal(); });
            Session.Handle(CoopMessages.State, OnState);
            Session.Handle(CoopMessages.Enemies, OnEnemies);
            Session.Handle(CoopMessages.Damage, OnDamage);
            Session.Handle(CoopMessages.Kill, OnKill);
            Session.Handle(CoopMessages.Bolt, OnBolt);
            Session.Handle(CoopMessages.BoltEvent, OnBoltEvent);
            Session.Handle(CoopMessages.Fx, OnFx);
            Session.Handle(CoopMessages.Support, OnSupport);
            Session.Handle(CoopMessages.Interact, OnInteract);
            Session.Handle(CoopMessages.Choice, OnChoice);
            Session.Handle(CoopMessages.ChoiceDone, OnChoiceDone);
            Session.Handle(CoopMessages.Advance, OnAdvance);
            Session.Handle(CoopMessages.Died, OnDied);
            Session.Handle(CoopMessages.Over, (sender, reader) => { if (!IsHost) EndRunLocal(); });
        }

        // ---------------------------------------------------------------- run lifecycle

        /// <summary>Host only: starts a new descent for the whole party.</summary>
        public void HostBeginRun()
        {
            if (!IsHost || (Session.State != NetState.Lobby && !RunOver)) return;
            int seed = Random.Range(0, 1000000);
            int party = Session.Peers.Count;
            using (var writer = NetSession.Writer())
            {
                writer.WriteValueSafe(seed);
                writer.WriteValueSafe(party);
                Session.Send(CoopMessages.Start, writer);
            }
            BeginRunLocal(seed, party);
        }

        private void OnStart(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int seed);
            reader.ReadValueSafe(out int party);
            BeginRunLocal(seed, party);
        }

        private void BeginRunLocal(int seed, int party)
        {
            if (Session.State == NetState.Lobby) Session.StartRun();
            RunOver = false;
            dead.Clear();
            choicesDone.Clear();
            openChoice = null;
            WaitingForTeam = false;
            ClearRemoteHeroes();
            Run.StartCoopRun(Run.Characters[Mathf.Clamp(Session.LocalClassIndex, 0, Run.Characters.Count - 1)], seed, party);
            foreach (var peer in Session.Peers)
            {
                if (peer.Id == LocalId) continue;
                var character = Run.Characters[Mathf.Clamp(peer.ClassIndex, 0, Run.Characters.Count - 1)];
                remoteHeroes.Add(RemoteHero.Create(Run, peer.Id, peer.Name, character, Run.Player.transform.position));
            }
        }

        /// <summary>Host only: sends everyone back to the party lobby.</summary>
        public void HostReturnToLobby()
        {
            if (!IsHost) return;
            using (var writer = NetSession.Writer(8)) Session.Send(CoopMessages.Lobby, writer);
            ReturnToLobbyLocal();
        }

        private void ReturnToLobbyLocal()
        {
            Session.ReturnToLobby();
            ClearRemoteHeroes();
            Run.ShowCoopLobby();
        }

        private void ClearRemoteHeroes()
        {
            foreach (var hero in remoteHeroes) if (hero != null) Destroy(hero.gameObject);
            remoteHeroes.Clear();
        }

        /// <summary>Called by the run whenever a floor is built, on every machine, in the same order.</summary>
        public void RegisterFloor(List<DungeonEnemy> spawned)
        {
            enemies.Clear();
            bolts.Clear();
            for (int i = 0; i < spawned.Count; i++)
            {
                spawned[i].NetId = (ushort)i;
                enemies[(ushort)i] = spawned[i];
            }
            WaitingForTeam = false;
            choicesDone.Clear();
            openChoice = null;
            if (Run.Player != null && Run.Player.Health <= 0) Run.Player.Revive();
            if (IsHost) dead.Clear();
            foreach (var hero in remoteHeroes) hero.Teleport(Run.Player.transform.position);
        }

        private void OnPeerLeft(ulong id)
        {
            var hero = remoteHeroes.Find(candidate => candidate.Id == id);
            if (hero != null) { remoteHeroes.Remove(hero); Destroy(hero.gameObject); }
            if (!IsHost || !Active) return;
            choicesDone.Remove(id);
            dead.Remove(id);
            ResolveChoice();
            CheckAllDead();
        }

        // ---------------------------------------------------------------- per-frame sync

        private void Update()
        {
            if (!Active || Run.Player == null || Run.Map == null) return;
            if (Time.unscaledTime >= nextState)
            {
                nextState = Time.unscaledTime + StateInterval;
                SendState();
            }
            if (IsHost && Time.unscaledTime >= nextSnapshot)
            {
                nextSnapshot = Time.unscaledTime + SnapshotInterval;
                SendSnapshot();
            }
        }

        private void SendState()
        {
            var player = Run.Player;
            byte flags = 0;
            if (player.IsRolling) flags |= PlayerStateMessage.Rolling;
            if (player.Shield != null && player.Shield.IsBlocking) flags |= PlayerStateMessage.Blocking;
            if (player.Charge != null && player.Charge.IsCharging) flags |= PlayerStateMessage.Charging;
            if (player.Health <= 0) flags |= PlayerStateMessage.Dead;
            if (player.IsInvulnerable) flags |= PlayerStateMessage.Invulnerable;
            if (player.Buffs != null)
            {
                if (player.Buffs.IsEmpowered) flags |= PlayerStateMessage.Empowered;
                if (player.Buffs.IsRaging) flags |= PlayerStateMessage.Raging;
                if (player.Buffs.IsTired) flags |= PlayerStateMessage.Tired;
            }
            var message = new PlayerStateMessage
            {
                Id = LocalId, Floor = Run.Floor, Position = player.transform.position,
                Aim = player.Shield != null && player.Shield.IsBlocking ? player.Shield.Direction : player.AimDirection,
                Flags = flags, Health = (short)player.Health, MaxHealth = (short)player.MaxHealth
            };
            using var writer = NetSession.Writer(64);
            message.Write(writer);
            Session.Send(CoopMessages.State, writer, NetworkDelivery.UnreliableSequenced);
        }

        private void OnState(ulong sender, FastBufferReader reader)
        {
            var message = PlayerStateMessage.Read(reader);
            if (IsHost)
            {
                message.Id = sender;
                using var writer = NetSession.Writer(64);
                message.Write(writer);
                Session.Send(CoopMessages.State, writer, NetworkDelivery.UnreliableSequenced, sender);
            }
            if (message.Floor != Run.Floor) return;
            remoteHeroes.Find(hero => hero.Id == message.Id)?.Apply(message);
        }

        private void SendSnapshot()
        {
            using var writer = NetSession.Writer(1024);
            writer.WriteValueSafe(Run.Floor);
            writer.WriteValueSafe((ushort)Run.Enemies.Count);
            foreach (var enemy in Run.Enemies)
            {
                byte flags = 0;
                if (enemy.IsFlashing) flags |= EnemySnapshot.Flashing;
                if (enemy.IsChilled) flags |= EnemySnapshot.Chilled;
                if (enemy.IsBurning) flags |= EnemySnapshot.Burning;
                var shooter = enemy.GetComponent<EnemyShooter>();
                if ((shooter != null && shooter.IsCharging) || (enemy.Boss != null && enemy.Boss.IsCharging)) flags |= EnemySnapshot.Charging;
                if (enemy.Boss != null && enemy.Boss.Pattern % 2 == 1) flags |= EnemySnapshot.PatternOdd;
                new EnemySnapshot { Id = enemy.NetId, Position = enemy.transform.position, Facing = enemy.Facing.Direction, Health = enemy.Health, Flags = flags }.Write(writer);
            }
            Session.Send(CoopMessages.Enemies, writer, NetworkDelivery.UnreliableSequenced);
        }

        private void OnEnemies(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out ushort count);
            if (floor != Run.Floor) return;
            for (int i = 0; i < count; i++)
            {
                var snapshot = EnemySnapshot.Read(reader);
                if (enemies.TryGetValue(snapshot.Id, out var enemy) && enemy != null) enemy.ApplySnapshot(snapshot);
            }
        }

        // ---------------------------------------------------------------- enemy damage and kills

        /// <summary>Guest only: forwards a hit, burn or chill on an enemy to the host.</summary>
        public void ReportDamage(DungeonEnemy enemy, CoopDamageKind kind, int amount, Vector2 source, int ticks = 0, float duration = 0f,
            Color? color = null, float knockback = 1f)
        {
            var message = new DamageMessage
            {
                Floor = Run.Floor, Enemy = enemy.NetId, Kind = kind, Amount = amount, Ticks = ticks, Duration = duration, Knockback = knockback,
                Source = source, Color = color ?? new Color(1f, 0.4f, 0.16f)
            };
            using var writer = NetSession.Writer(64);
            message.Write(writer);
            Session.Send(CoopMessages.Damage, writer);
        }

        private void OnDamage(ulong sender, FastBufferReader reader)
        {
            if (!IsHost) return;
            var message = DamageMessage.Read(reader);
            if (message.Floor != Run.Floor || !enemies.TryGetValue(message.Enemy, out var enemy) || enemy == null || enemy.Health <= 0) return;
            Attacker = sender;
            try
            {
                if (message.Kind == CoopDamageKind.Hit) enemy.Hit(message.Amount, message.Source, message.Knockback);
                else if (message.Kind == CoopDamageKind.Burn) enemy.Burn(message.Ticks, message.Amount, message.Color);
                else enemy.Chill(message.Duration);
            }
            finally { Attacker = LocalId; }
        }

        public bool IsLocalAttacker => !Active || Attacker == LocalId;

        /// <summary>Host only: tells guests an enemy died and who landed the blow.</summary>
        public void AnnounceKill(DungeonEnemy enemy)
        {
            using var writer = NetSession.Writer(32);
            writer.WriteValueSafe(Run.Floor);
            writer.WriteValueSafe(enemy.NetId);
            writer.WriteValueSafe(Attacker);
            Session.Send(CoopMessages.Kill, writer);
        }

        private void OnKill(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out ushort id);
            reader.ReadValueSafe(out ulong killer);
            if (floor != Run.Floor || !enemies.TryGetValue(id, out var enemy) || enemy == null) return;
            enemies.Remove(id);
            enemy.Die(killer == LocalId);
        }

        // ---------------------------------------------------------------- enemy bolts

        /// <summary>Host only: gives a new enemy bolt an id and shows it to the guests.</summary>
        public void AnnounceBolt(EnemyProjectile bolt, Vector2 position, Vector2 direction)
        {
            bolt.Id = ++nextBolt;
            bolts[bolt.Id] = bolt;
            using var writer = NetSession.Writer(48);
            writer.WriteValueSafe(Run.Floor);
            writer.WriteValueSafe(bolt.Id);
            writer.WriteValueSafe(position);
            writer.WriteValueSafe(direction);
            Session.Send(CoopMessages.Bolt, writer);
        }

        private void OnBolt(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out Vector2 position);
            reader.ReadValueSafe(out Vector2 direction);
            if (floor != Run.Floor || Run.ProjectileRoot == null) return;
            var bolt = EnemyProjectile.Spawn(Run, Run.ProjectileRoot, position, direction, false);
            bolt.Id = id;
            bolts[id] = bolt;
        }

        /// <summary>A local Knight reflected a bolt, or a bolt struck the local hero: everyone else mirrors it.</summary>
        public void ReportBolt(EnemyProjectile bolt, CoopBoltEventKind kind)
        {
            if (bolt.Id == 0) return;
            var message = new BoltEventMessage
            {
                Origin = LocalId, Floor = Run.Floor, Bolt = bolt.Id, Kind = kind, Position = bolt.transform.position, Direction = bolt.Direction
            };
            using var writer = NetSession.Writer(64);
            message.Write(writer);
            Session.Send(CoopMessages.BoltEvent, writer);
        }

        private void OnBoltEvent(ulong sender, FastBufferReader reader)
        {
            var message = BoltEventMessage.Read(reader);
            if (IsHost)
            {
                message.Origin = sender;
                using var writer = NetSession.Writer(64);
                message.Write(writer);
                Session.Send(CoopMessages.BoltEvent, writer, NetworkDelivery.ReliableSequenced, sender);
            }
            if (message.Floor != Run.Floor || !bolts.TryGetValue(message.Bolt, out var bolt) || bolt == null || bolt.IsSpent) return;
            if (message.Kind == CoopBoltEventKind.Consumed) bolt.Consume();
            else bolt.MirrorReflection(message.Position, message.Direction);
        }

        // ---------------------------------------------------------------- teammate effects and support

        public void SendFx(FxMessage message)
        {
            if (!Active) return;
            message.Origin = LocalId;
            message.Floor = Run.Floor;
            using var writer = NetSession.Writer(96);
            message.Write(writer);
            Session.Send(CoopMessages.Fx, writer);
        }

        private void OnFx(ulong sender, FastBufferReader reader)
        {
            var message = FxMessage.Read(reader);
            if (IsHost)
            {
                message.Origin = sender;
                using var writer = NetSession.Writer(96);
                message.Write(writer);
                Session.Send(CoopMessages.Fx, writer, NetworkDelivery.ReliableSequenced, sender);
            }
            if (message.Floor == Run.Floor && Run.ProjectileRoot != null && Run.IsPlaying) CoopFx.Play(Run, message);
        }

        /// <summary>Heals, shields or blesses every teammate within the radius of a local support ability.</summary>
        public void SupportAllies(Vector2 center, float radius, SupportKind kind, int amount, float duration)
        {
            if (!Active) return;
            foreach (var hero in remoteHeroes)
            {
                if (!hero.IsAlive || Vector2.Distance(center, hero.transform.position) > radius) continue;
                HeroVfx.Motes(Run.ProjectileRoot, hero.transform.position, 0.7f, AbilityCatalog.Gold, 14, 1f);
                var message = new SupportMessage { Origin = LocalId, Target = hero.Id, Kind = kind, Amount = amount, Duration = duration };
                using var writer = NetSession.Writer(48);
                message.Write(writer);
                if (IsHost) Session.SendTo(hero.Id, CoopMessages.Support, writer);
                else Session.Send(CoopMessages.Support, writer);
            }
        }

        private void OnSupport(ulong sender, FastBufferReader reader)
        {
            var message = SupportMessage.Read(reader);
            if (IsHost && message.Target != LocalId)
            {
                message.Origin = sender;
                using var writer = NetSession.Writer(48);
                message.Write(writer);
                Session.SendTo(message.Target, CoopMessages.Support, writer);
                return;
            }
            if (message.Target != LocalId || Run.Player == null || Run.Player.Health <= 0 || !Run.IsPlaying) return;
            Run.Player.ApplySupport(message.Kind, message.Amount, message.Duration);
            if (Run.ProjectileRoot != null) HeroVfx.Motes(Run.ProjectileRoot, Run.Player.transform.position, 0.7f, AbilityCatalog.Gold, 14, 1f);
        }

        // ---------------------------------------------------------------- floor flow: stairs, boons, artifacts

        /// <summary>A local player pressed F at the stairs or the artifact.</summary>
        public void RequestInteract(CoopChoice choice)
        {
            if (IsHost) { HostInteract(choice); return; }
            using var writer = NetSession.Writer(16);
            writer.WriteValueSafe(Run.Floor);
            writer.WriteValueSafe(choice);
            Session.Send(CoopMessages.Interact, writer);
        }

        private void OnInteract(ulong sender, FastBufferReader reader)
        {
            if (!IsHost) return;
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out CoopChoice choice);
            if (floor == Run.Floor) HostInteract(choice);
        }

        private void HostInteract(CoopChoice choice)
        {
            if (!Run.IsPlaying || openChoice.HasValue || Run.Enemies.Count != 0) return;
            if (choice == CoopChoice.Artifact && Run.Artifact == null) return;
            if (choice == CoopChoice.Upgrade && Run.Artifact != null) return;
            if (choice == CoopChoice.Upgrade && Run.IsBossFloor) { HostAdvance(); return; }
            openChoice = choice;
            choicesDone.Clear();
            using (var writer = NetSession.Writer(16))
            {
                writer.WriteValueSafe(Run.Floor);
                writer.WriteValueSafe(choice);
                Session.Send(CoopMessages.Choice, writer);
            }
            OpenChoiceLocal(choice);
        }

        private void OnChoice(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out CoopChoice choice);
            if (floor == Run.Floor) OpenChoiceLocal(choice);
        }

        private void OpenChoiceLocal(CoopChoice choice)
        {
            WaitingForTeam = false;
            if (choice == CoopChoice.Upgrade) Run.BeginUpgradeChoice();
            else Run.BeginArtifactChoice();
        }

        /// <summary>The local player finished their boon or artifact pick.</summary>
        public void FinishChoice()
        {
            WaitingForTeam = true;
            if (IsHost) { choicesDone.Add(LocalId); ResolveChoice(); return; }
            using var writer = NetSession.Writer(8);
            writer.WriteValueSafe(Run.Floor);
            Session.Send(CoopMessages.ChoiceDone, writer);
        }

        private void OnChoiceDone(ulong sender, FastBufferReader reader)
        {
            if (!IsHost) return;
            reader.ReadValueSafe(out int floor);
            if (floor != Run.Floor) return;
            choicesDone.Add(sender);
            ResolveChoice();
        }

        private void ResolveChoice()
        {
            if (!openChoice.HasValue) return;
            foreach (var peer in Session.Peers) if (!choicesDone.Contains(peer.Id)) return;
            var finished = openChoice.Value;
            openChoice = null;
            if (finished == CoopChoice.Upgrade) HostAdvance();
            else
            {
                using (var writer = NetSession.Writer(16))
                {
                    writer.WriteValueSafe(Run.Floor);
                    writer.WriteValueSafe((byte)1);
                    writer.WriteValueSafe(Session.Peers.Count);
                    Session.Send(CoopMessages.Advance, writer);
                }
                ResumeAfterArtifact();
            }
        }

        private void HostAdvance()
        {
            using (var writer = NetSession.Writer(16))
            {
                writer.WriteValueSafe(Run.Floor);
                writer.WriteValueSafe((byte)0);
                writer.WriteValueSafe(Session.Peers.Count);
                Session.Send(CoopMessages.Advance, writer);
            }
            Run.AdvanceCoopFloor(Session.Peers.Count);
        }

        private void OnAdvance(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out int party);
            if (floor != Run.Floor) return;
            if (kind == 0) Run.AdvanceCoopFloor(party);
            else ResumeAfterArtifact();
        }

        private void ResumeAfterArtifact()
        {
            WaitingForTeam = false;
            choicesDone.Clear();
            Run.CloseCoopArtifact();
        }

        // ---------------------------------------------------------------- death and the end of the run

        /// <summary>The local hero fell; the host ends the run when nobody is left standing.</summary>
        public void LocalDied()
        {
            if (IsHost) { dead.Add(LocalId); CheckAllDead(); return; }
            using var writer = NetSession.Writer(8);
            writer.WriteValueSafe(Run.Floor);
            Session.Send(CoopMessages.Died, writer);
        }

        private void OnDied(ulong sender, FastBufferReader reader)
        {
            if (!IsHost) return;
            reader.ReadValueSafe(out int floor);
            if (floor != Run.Floor) return;
            dead.Add(sender);
            CheckAllDead();
        }

        private void CheckAllDead()
        {
            if (!IsHost || !Active || RunOver) return;
            foreach (var peer in Session.Peers) if (!dead.Contains(peer.Id)) return;
            using (var writer = NetSession.Writer(8)) Session.Send(CoopMessages.Over, writer);
            EndRunLocal();
        }

        private void EndRunLocal()
        {
            RunOver = true;
            Run.EndRun();
        }

        /// <summary>A living teammate for a fallen player's camera to follow, if any.</summary>
        public RemoteHero SpectateTarget => remoteHeroes.Find(hero => hero.IsAlive);
    }
}

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
        /// <summary>Guests who asked to restart the descent; the host restarts once every guest has asked.</summary>
        public int RestartVotes { get; private set; }
        public int RestartVotesNeeded => Mathf.Max(1, Session != null ? Session.Peers.Count - 1 : 1);
        public bool VotedRestart { get; private set; }

        private readonly List<RemoteHero> remoteHeroes = new List<RemoteHero>();
        private readonly Dictionary<ushort, DungeonEnemy> enemies = new Dictionary<ushort, DungeonEnemy>();
        private readonly Dictionary<int, EnemyProjectile> bolts = new Dictionary<int, EnemyProjectile>();
        private readonly HashSet<ulong> choicesDone = new HashSet<ulong>(), dead = new HashSet<ulong>(), restartVotes = new HashSet<ulong>();
        private CoopChoice? openChoice;
        private float nextState, nextSnapshot;
        private int nextBolt;
        private readonly List<SkeletonSnapshot> localSkeletons = new List<SkeletonSnapshot>();
        private readonly Dictionary<ulong, List<SpriteRenderer>> remoteSkeletons = new Dictionary<ulong, List<SpriteRenderer>>();

        private void Awake()
        {
            Session = gameObject.AddComponent<NetSession>();
            Session.PeerLeft += OnPeerLeft;
            Session.Disconnected += () => { if (Run != null) Run.ShowMainMenu(true); };
            Session.Handle(CoopMessages.Start, OnStart);
            Session.Handle(CoopMessages.Lobby, (sender, reader) => { if (!IsHost) ReturnToLobbyLocal(); });
            Session.Handle(CoopMessages.State, OnState);
            Session.Handle(CoopMessages.Skeletons, OnSkeletons);
            Session.Handle(CoopMessages.Enemies, OnEnemies);
            Session.Handle(CoopMessages.Damage, OnDamage);
            Session.Handle(CoopMessages.Kill, OnKill);
            Session.Handle(CoopMessages.Minion, OnMinion);
            Session.Handle(CoopMessages.Bolt, OnBolt);
            Session.Handle(CoopMessages.BoltEvent, OnBoltEvent);
            Session.Handle(CoopMessages.Fx, OnFx);
            Session.Handle(CoopMessages.Support, OnSupport);
            Session.Handle(CoopMessages.Interact, OnInteract);
            Session.Handle(CoopMessages.Choice, OnChoice);
            Session.Handle(CoopMessages.ChoiceDone, OnChoiceDone);
            Session.Handle(CoopMessages.Advance, OnAdvance);
            Session.Handle(CoopMessages.Died, OnDied);
            Session.Handle(CoopMessages.Revived, OnRevived);
            Session.Handle(CoopMessages.Hazard, OnHazard);
            Session.Handle(CoopMessages.Smash, OnSmash);
            Session.Handle(CoopMessages.Pickup, OnPickup);
            Session.Handle(CoopMessages.RestartVote, OnRestartVote);
            Session.Handle(CoopMessages.RestartVotes, OnRestartVotes);
            Session.Handle(CoopMessages.Over, (sender, reader) => { if (!IsHost) EndRunLocal(); });
        }

        // ---------------------------------------------------------------- run lifecycle

        /// <summary>Host only: starts a new descent for the whole party, from the lobby, after a wipe, or mid-run as a restart.</summary>
        public void HostBeginRun()
        {
            if (!IsHost || (Session.State != NetState.Lobby && Session.State != NetState.InRun)) return;
            if (Session.State == NetState.Lobby && !Session.AllReady) return;
            int seed = Random.Range(0, 1000000);
            int party = Session.Peers.Count;
            // The host's travel-map pick decides where the whole party begins.
            int world = Run.StartWorld;
            using (var writer = NetSession.Writer())
            {
                writer.WriteValueSafe(seed);
                writer.WriteValueSafe(party);
                writer.WriteValueSafe(world);
                Session.Send(CoopMessages.Start, writer);
            }
            BeginRunLocal(seed, party, world);
        }

        private void OnStart(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int seed);
            reader.ReadValueSafe(out int party);
            reader.ReadValueSafe(out int world);
            BeginRunLocal(seed, party, world);
        }

        private void BeginRunLocal(int seed, int party, int world)
        {
            if (Session.State == NetState.Lobby) Session.StartRun();
            RunOver = false;
            restartVotes.Clear();
            RestartVotes = 0;
            VotedRestart = false;
            dead.Clear();
            choicesDone.Clear();
            openChoice = null;
            WaitingForTeam = false;
            ClearRemoteHeroes();
            Run.StartCoopRun(Run.Characters[Mathf.Clamp(Session.LocalClassIndex, 0, Run.Characters.Count - 1)], seed, party, world);
            foreach (var peer in Session.Peers)
            {
                if (peer.Id == LocalId) continue;
                var character = Run.Characters[Mathf.Clamp(peer.ClassIndex, 0, Run.Characters.Count - 1)];
                remoteHeroes.Add(RemoteHero.Create(Run, peer.Id, peer.Name, character, Run.Player.transform.position));
            }
        }

        /// <summary>Guest: asks the host to restart. The descent restarts once every guest has asked (the host can restart outright).</summary>
        public void VoteRestart()
        {
            if (!Active || VotedRestart) return;
            if (IsHost) { HostBeginRun(); return; }
            VotedRestart = true;
            // A vote stands for the rest of the descent, so it is not tied to the floor it was cast on.
            using var writer = NetSession.Writer(8);
            Session.Send(CoopMessages.RestartVote, writer);
        }

        private void OnRestartVote(ulong sender, FastBufferReader reader)
        {
            if (IsHost && Active && restartVotes.Add(sender)) ResolveRestartVotes();
        }

        private void ResolveRestartVotes()
        {
            if (restartVotes.Count >= RestartVotesNeeded) { HostBeginRun(); return; }
            RestartVotes = restartVotes.Count;
            using var writer = NetSession.Writer(8);
            writer.WriteValueSafe(RestartVotes);
            Session.Send(CoopMessages.RestartVotes, writer);
        }

        private void OnRestartVotes(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int votes);
            RestartVotes = votes;
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
            foreach (var id in new List<ulong>(remoteSkeletons.Keys)) ClearSkeletons(id);
        }

        /// <summary>Called by the run whenever a floor is built, on every machine, in the same order.</summary>
        public void RegisterFloor(List<DungeonEnemy> spawned)
        {
            enemies.Clear();
            bolts.Clear();
            pickups.Clear();
            takenPickups.Clear();
            for (int i = 0; i < spawned.Count; i++)
            {
                spawned[i].NetId = (ushort)i;
                enemies[(ushort)i] = spawned[i];
            }
            nextEnemyId = (ushort)spawned.Count;
            WaitingForTeam = false;
            choicesDone.Clear();
            openChoice = null;
            if (Run.Player != null && Run.Player.Health <= 0) Run.Player.Revive();
            if (IsHost) dead.Clear();
            foreach (var hero in remoteHeroes) hero.Teleport(Run.Player.transform.position);
        }

        private void OnPeerLeft(ulong id)
        {
            ClearSkeletons(id);
            var hero = remoteHeroes.Find(candidate => candidate.Id == id);
            if (hero != null) { remoteHeroes.Remove(hero); Destroy(hero.gameObject); }
            if (!IsHost || !Active) return;
            choicesDone.Remove(id);
            dead.Remove(id);
            ResolveChoice();
            if (restartVotes.Remove(id) || restartVotes.Count > 0) ResolveRestartVotes();
            CheckAllDead();
        }

        // ---------------------------------------------------------------- per-frame sync

        private void Update()
        {
            if (IsHost && Session.AllReady) HostBeginRun();
            if (!Active || Run.Player == null || Run.Map == null) return;
            if (Time.unscaledTime >= nextState)
            {
                nextState = Time.unscaledTime + StateInterval;
                SendState();
                SendSkeletons();
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
            var taunt = player.Mechanic as ShieldTaunt;
            bool taunting = taunt != null && taunt.IsTaunting;
            if ((player.Shield != null && player.Shield.IsBlocking) || taunting) flags |= PlayerStateMessage.Blocking;
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
                Aim = taunting ? taunt.Direction : player.Shield != null && player.Shield.IsBlocking ? player.Shield.Direction : player.AimDirection,
                Flags = flags, MoreFlags = (byte)((player.Blessing != null && player.Blessing.BonusDamage > 0 ? PlayerStateMessage.Blessed : 0)
                    | (player.IsVeiled ? PlayerStateMessage.Veiled : 0)
                    | (player.Buffs != null && player.Buffs.IsAscended ? PlayerStateMessage.Ascended : 0)
                    | (player.DrawsAggro ? PlayerStateMessage.Taunting : 0)
                    | (player.Buffs != null && player.Buffs.IsFurious ? PlayerStateMessage.Furious : 0)
                    | (player.Weapon is SpecimenAttack specimen ? specimen.NetFormBits : 0)),
                Health = (short)player.Health, MaxHealth = (short)player.MaxHealth,
                Charge = (byte)Mathf.RoundToInt((player.Charge != null ? player.Charge.Amount : 0f) * 255f)
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
                if ((shooter != null && shooter.IsCharging) || (enemy.Boss != null && enemy.Boss.IsCharging)
                    || (enemy.Variant != null && enemy.Variant.IsWindingUp)) flags |= EnemySnapshot.Charging;
                if (enemy.Boss != null) flags |= (byte)(enemy.Boss.NetState << EnemySnapshot.BossStateShift);
                byte more = (byte)((enemy.IsParalyzed ? EnemySnapshot.Paralyzed : 0) | (enemy.IsCursed ? EnemySnapshot.Cursed : 0)
                    | (enemy.IsFrozen ? EnemySnapshot.Frozen : 0) | (enemy.Boss != null ? EnemySnapshot.HasMaxHealth : 0)
                    | (enemy.IsStunned ? EnemySnapshot.Stunned : 0) | (enemy.IsRooted ? EnemySnapshot.Rooted : 0)
                    | (enemy.IsBleeding ? EnemySnapshot.Bleeding : 0) | (enemy.IsPoisoned ? EnemySnapshot.Poisoned : 0));
                new EnemySnapshot { Id = enemy.NetId, Position = enemy.transform.position, Facing = enemy.Facing.Direction, Health = enemy.Health,
                    MaxHealth = enemy.Boss != null ? enemy.Boss.MaxHealth : 0, Flags = flags, MoreFlags = more }.Write(writer);
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

        // ---------------------------------------------------------------- summoned minions

        private ushort nextEnemyId;

        /// <summary>Host only: numbers a guardian's new minion and tells the guests to spawn the same one.</summary>
        public void RegisterMinion(DungeonEnemy enemy, byte kind)
        {
            if (!IsHost) return;
            enemy.NetId = nextEnemyId++;
            enemies[enemy.NetId] = enemy;
            using var writer = NetSession.Writer(32);
            writer.WriteValueSafe(Run.Floor);
            writer.WriteValueSafe(enemy.NetId);
            writer.WriteValueSafe(kind);
            writer.WriteValueSafe((Vector2)enemy.transform.position);
            Session.Send(CoopMessages.Minion, writer);
        }

        private void OnMinion(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out ushort id);
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out Vector2 position);
            if (floor != Run.Floor || enemies.ContainsKey(id) || Run.Boss == null || Run.Boss.Enemy.Health <= 0) return;
            var enemy = Run.CreateMinion(position, kind);
            enemy.NetId = id;
            enemies[id] = enemy;
        }

        // ---------------------------------------------------------------- enemy damage and kills

        /// <summary>Guest only: forwards a hit, burn, chill, paralysis or curse on an enemy to the host.</summary>
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
                else if (message.Kind == CoopDamageKind.Paralyze) enemy.Paralyze(message.Duration, message.Ticks > 0);
                else if (message.Kind == CoopDamageKind.ClearParalysis) enemy.ConsumeParalysis();
                else if (message.Kind == CoopDamageKind.ClearHolds) enemy.ConsumeHolds();
                else if (message.Kind == CoopDamageKind.Curse) enemy.Curse(message.Duration, message.Ticks, message.Amount > 0 ? message.Amount / 100f : DungeonEnemy.CurseDamageBonus);
                else if (message.Kind == CoopDamageKind.Freeze) enemy.Freeze(message.Duration);
                else if (message.Kind == CoopDamageKind.Fear) enemy.Fear(message.Source, message.Duration);
                else if (message.Kind == CoopDamageKind.Stun) enemy.Stun(message.Duration);
                else if (message.Kind == CoopDamageKind.Root) enemy.Root(message.Duration);
                else if (message.Kind == CoopDamageKind.Bleed) enemy.Bleed(message.Amount, message.Duration, message.Ticks);
                else if (message.Kind == CoopDamageKind.ClearBleed) enemy.ConsumeBleed();
                else if (message.Kind == CoopDamageKind.RefreshBleed) enemy.RefreshBleed(message.Duration);
                else if (message.Kind == CoopDamageKind.Poison) enemy.Poison(message.Ticks, message.Amount);
                else if (message.Kind == CoopDamageKind.Mark) enemy.Mark(message.Duration);
                else if (message.Kind == CoopDamageKind.DeathMark) enemy.DeathMark(message.Duration);
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
            writer.WriteValueSafe(bolt.Speed);
            writer.WriteValueSafe(bolt.Kind);
            writer.WriteValueSafe((byte)Mathf.Clamp(bolt.Damage, 1, 255));
            Session.Send(CoopMessages.Bolt, writer);
        }

        private void OnBolt(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out int id);
            reader.ReadValueSafe(out Vector2 position);
            reader.ReadValueSafe(out Vector2 direction);
            reader.ReadValueSafe(out float speed);
            reader.ReadValueSafe(out BoltKind kind);
            reader.ReadValueSafe(out byte damage);
            if (floor != Run.Floor || Run.ProjectileRoot == null) return;
            var bolt = EnemyProjectile.Spawn(Run, Run.ProjectileRoot, position, direction, false, speed, kind, damage);
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

        /// <summary>Host only: shows a boss hazard to the guests, whose own copy judges their hero.</summary>
        public void AnnounceHazard(HazardSpec spec)
        {
            using var writer = NetSession.Writer(64);
            new HazardMessage { Floor = Run.Floor, Spec = spec }.Write(writer);
            Session.Send(CoopMessages.Hazard, writer);
        }

        private void OnHazard(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            var message = HazardMessage.Read(reader);
            if (message.Floor == Run.Floor && Run.ProjectileRoot != null) HellfireZone.Spawn(Run, message.Spec, false);
        }

        // ---------------------------------------------------------------- shared breakables and pickups

        private const int EnemyPickupKeys = 100000;
        private readonly Dictionary<int, ISharedPickup> pickups = new Dictionary<int, ISharedPickup>();
        private readonly HashSet<int> takenPickups = new HashSet<int>();

        /// <summary>The key shared by every machine's copy of a seeded urn's crystals (or its heart). Zero is never a key: it marks a pickup only this machine has.</summary>
        public static int BreakableKey(int breakable, bool heart) => 1 + breakable * 2 + (heart ? 1 : 0);
        /// <summary>The key shared by every machine's copy of the crystals a fallen enemy leaves.</summary>
        public static int EnemyKey(ushort enemy) => EnemyPickupKeys + enemy;

        /// <summary>A local hit smashed a seeded urn: it breaks on every other machine too.</summary>
        public void ReportSmash(int breakable, Vector2 source)
        {
            if (!Active) return;
            using var writer = NetSession.Writer(32);
            writer.WriteValueSafe(Run.Floor);
            writer.WriteValueSafe(breakable);
            writer.WriteValueSafe(source);
            Session.Send(CoopMessages.Smash, writer);
        }

        private void OnSmash(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out int breakable);
            reader.ReadValueSafe(out Vector2 source);
            if (IsHost)
            {
                using var writer = NetSession.Writer(32);
                writer.WriteValueSafe(floor);
                writer.WriteValueSafe(breakable);
                writer.WriteValueSafe(source);
                Session.Send(CoopMessages.Smash, writer, NetworkDelivery.ReliableSequenced, sender);
            }
            if (floor != Run.Floor || Run.InShop) return;
            foreach (var urn in Breakable.Active)
                if (urn != null && urn.NetId == breakable && urn.Run == Run) { urn.Smash(source, true); break; }
        }

        /// <summary>
        /// A shared crystal or heart landed on this machine. If a teammate already took their copy of it (their message
        /// outran the drop), this one is collected at once.
        /// </summary>
        public void RegisterPickup(int key, ISharedPickup pickup)
        {
            if (!Active || key == 0) return;
            if (takenPickups.Contains(key)) pickup.CollectRemote();
            else pickups[key] = pickup;
        }

        /// <summary>The local hero took a shared pickup: every other machine's copy goes with it.</summary>
        public void ReportPickup(int key)
        {
            if (!Active || key == 0 || !takenPickups.Add(key)) return;
            pickups.Remove(key);
            using var writer = NetSession.Writer(16);
            writer.WriteValueSafe(Run.Floor);
            writer.WriteValueSafe(key);
            Session.Send(CoopMessages.Pickup, writer);
        }

        private void OnPickup(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out int key);
            if (IsHost)
            {
                using var writer = NetSession.Writer(16);
                writer.WriteValueSafe(floor);
                writer.WriteValueSafe(key);
                Session.Send(CoopMessages.Pickup, writer, NetworkDelivery.ReliableSequenced, sender);
            }
            if (floor != Run.Floor || !takenPickups.Add(key) || !pickups.TryGetValue(key, out var pickup)) return;
            pickups.Remove(key);
            if (pickup is Object copy && copy != null) pickup.CollectRemote();
        }

        private void ClearSkeletons(ulong owner)
        {
            if (!remoteSkeletons.TryGetValue(owner, out var bodies)) return;
            foreach (var body in bodies) if (body != null) Destroy(body.gameObject);
            remoteSkeletons.Remove(owner);
        }

        private void SendSkeletons()
        {
            SkeletonMinion.Capture(Run, localSkeletons);
            using var writer = NetSession.Writer();
            writer.WriteValueSafe(LocalId); writer.WriteValueSafe(Run.Floor); writer.WriteValueSafe(localSkeletons.Count);
            foreach (var skeleton in localSkeletons) skeleton.Write(writer);
            Session.Send(CoopMessages.Skeletons, writer);
        }

        private void OnSkeletons(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong owner); reader.ReadValueSafe(out int floor); reader.ReadValueSafe(out int count);
            if (!Active || count < 0 || count > ArmyOfTheDead.MaxSkeletons) return;
            if (IsHost) owner = sender;
            if (owner == LocalId || Session.Peer(owner) == null) return;
            var snapshots = new List<SkeletonSnapshot>(count);
            for (int i = 0; i < count; i++) snapshots.Add(SkeletonSnapshot.Read(reader));
            if (IsHost)
            {
                using var writer = NetSession.Writer();
                writer.WriteValueSafe(owner); writer.WriteValueSafe(floor); writer.WriteValueSafe(count);
                foreach (var snapshot in snapshots) snapshot.Write(writer);
                Session.Send(CoopMessages.Skeletons, writer, NetworkDelivery.ReliableSequenced, sender);
            }
            if (floor != Run.Floor || Run.ProjectileRoot == null) return;
            if (!remoteSkeletons.TryGetValue(owner, out var bodies))
                remoteSkeletons[owner] = bodies = new List<SpriteRenderer>();
            while (bodies.Count > count)
            {
                var body = bodies[bodies.Count - 1];
                if (body != null) Destroy(body.gameObject);
                bodies.RemoveAt(bodies.Count - 1);
            }
            for (int i = 0; i < count; i++)
            {
                if (i == bodies.Count) bodies.Add(null);
                if (bodies[i] == null)
                {
                    bodies[i] = DungeonVisuals.Create("Teammate skeleton", Run.ProjectileRoot, snapshots[i].Position, Vector2.one, Color.white, 4);
                    bodies[i].sprite = SkeletonMinion.Sprite;
                }
                var body = bodies[i];
                var snapshot = snapshots[i];
                body.transform.position = snapshot.Position;
                body.transform.localScale = new Vector3(snapshot.Scale.x, snapshot.Scale.y, 1f);
                body.transform.rotation = Quaternion.Euler(0f, 0f, snapshot.Angle);
                body.flipX = snapshot.Flip;
                body.color = snapshot.Color;
            }
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
                // Heals draw their own green light (HealVfx); everything else glitters gold.
                if (kind != SupportKind.Heal) HeroVfx.Motes(Run.ProjectileRoot, hero.transform.position, 0.7f, AbilityCatalog.Gold, 14, 1f);
                SendSupport(hero.Id, kind, amount, duration);
            }
        }

        /// <summary>Sends support to one teammate (through the host when this machine is a guest).</summary>
        public void SendSupport(ulong target, SupportKind kind, int amount, float duration)
        {
            if (!Active || target == LocalId) return;
            var message = new SupportMessage { Origin = LocalId, Target = target, Kind = kind, Amount = amount, Duration = duration };
            using var writer = NetSession.Writer(48);
            message.Write(writer);
            if (IsHost) Session.SendTo(target, CoopMessages.Support, writer);
            else Session.Send(CoopMessages.Support, writer);
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
            if (message.Target != LocalId || Run.Player == null || !Run.IsPlaying) return;
            if (message.Kind == SupportKind.BlessingCredit) { Run.Player.Mechanic?.OnBlessedHit(message.Amount); return; }
            if (message.Kind == SupportKind.Revive) { ReviveLocal(); return; }
            if (Run.Player.Health <= 0) return;
            Run.Player.ApplySupport(message.Kind, message.Amount, message.Duration, message.Origin);
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
            // The monolith opens no pick: the host just tells every machine to release the first wave.
            if (choice == CoopChoice.Waves)
            {
                if (!Run.WavesPending) return;
                using (var writer = NetSession.Writer(16))
                {
                    writer.WriteValueSafe(Run.Floor);
                    writer.WriteValueSafe(choice);
                    Session.Send(CoopMessages.Choice, writer);
                }
                Run.StartWaves();
                return;
            }
            if (Run.WavesPending) return;
            if (choice == CoopChoice.Upgrade && Run.Artifact != null) return;
            if (choice == CoopChoice.Upgrade && Run.IsBossFloor)
            {
                if (WorldCatalog.CompletesWorld(Run.Floor)) HostWorldComplete();
                else HostAdvance();
                return;
            }
            // Nobody is dragged out of the crystal shop mid-purchase: the party leaves together.
            if (choice == CoopChoice.Upgrade && Run.InShop) { if (PartyAtStairs()) HostAdvance(); return; }
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

        public const float GatherRadius = 2.5f;

        /// <summary>True when every living hero stands near the stairs.</summary>
        private bool PartyAtStairs()
        {
            var player = Run.Player;
            if (player.Health > 0 && Vector2.Distance(player.transform.position, Run.Exit) > GatherRadius) return false;
            foreach (var hero in remoteHeroes)
                if (hero != null && hero.IsAlive && Vector2.Distance(hero.transform.position, Run.Exit) > GatherRadius) return false;
            return true;
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
            if (choice == CoopChoice.Waves) { Run.StartWaves(); return; }
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
                    writer.WriteValueSafe(AdvanceArtifact);
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
                writer.WriteValueSafe(AdvanceFloor);
                writer.WriteValueSafe(Session.Peers.Count);
                Session.Send(CoopMessages.Advance, writer);
            }
            Run.AdvanceCoopFloor(Session.Peers.Count);
        }

        /// <summary>
        /// Advance message kinds: descend to the next floor, resume after the artifact pick, show the world-cleared screen,
        /// or travel to the world the host picked on the travel map (its index follows the party size).
        /// </summary>
        private const byte AdvanceFloor = 0, AdvanceArtifact = 1, AdvanceWorldComplete = 2, AdvanceTravel = 3;

        /// <summary>Host only: the party beat a world's third guardian; everyone sees the world-cleared screen.</summary>
        private void HostWorldComplete()
        {
            using (var writer = NetSession.Writer(16))
            {
                writer.WriteValueSafe(Run.Floor);
                writer.WriteValueSafe(AdvanceWorldComplete);
                writer.WriteValueSafe(Session.Peers.Count);
                Session.Send(CoopMessages.Advance, writer);
            }
            Run.ShowWorldComplete();
        }

        /// <summary>Host only, from the world-cleared screen past the last world: the whole party keeps descending.</summary>
        public void HostContinueFromWorld()
        {
            if (!IsHost || !Active || !Run.WorldComplete) return;
            HostAdvance();
        }

        /// <summary>Host only, from the travel map: the whole party travels to world <paramref name="index"/>.</summary>
        public void HostTravel(int index)
        {
            if (!IsHost || !Active || !Run.WorldComplete || index < 0 || index >= WorldCatalog.All.Length) return;
            using (var writer = NetSession.Writer(16))
            {
                writer.WriteValueSafe(Run.Floor);
                writer.WriteValueSafe(AdvanceTravel);
                writer.WriteValueSafe(Session.Peers.Count);
                writer.WriteValueSafe(index);
                Session.Send(CoopMessages.Advance, writer);
            }
            Run.JumpToWorld(index, Session.Peers.Count);
        }

        private void OnAdvance(ulong sender, FastBufferReader reader)
        {
            if (IsHost) return;
            reader.ReadValueSafe(out int floor);
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out int party);
            if (floor != Run.Floor) return;
            if (kind == AdvanceFloor) Run.AdvanceCoopFloor(party);
            else if (kind == AdvanceWorldComplete) Run.ShowWorldComplete();
            else if (kind == AdvanceTravel)
            {
                reader.ReadValueSafe(out int world);
                Run.JumpToWorld(world, party);
            }
            else if (kind == AdvanceArtifact) ResumeAfterArtifact();
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

        /// <summary>A teammate's Heavenly Host raised the fallen local hero; the host stops counting them as dead.</summary>
        private void ReviveLocal()
        {
            if (Run.Player.Health > 0) return;
            Run.Player.Revive();
            if (Run.ProjectileRoot != null) HeroVfx.Motes(Run.ProjectileRoot, Run.Player.transform.position, 1f, AbilityCatalog.Gold, 24, 1.2f);
            if (IsHost) { dead.Remove(LocalId); return; }
            using var writer = NetSession.Writer(8);
            writer.WriteValueSafe(Run.Floor);
            Session.Send(CoopMessages.Revived, writer);
        }

        private void OnRevived(ulong sender, FastBufferReader reader)
        {
            if (!IsHost) return;
            reader.ReadValueSafe(out int floor);
            if (floor == Run.Floor) dead.Remove(sender);
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

        private ulong spectating;

        /// <summary>
        /// The living teammate a fallen player's camera follows, if any: the one last picked with
        /// <see cref="CycleSpectate"/> while they live, otherwise the first still standing.
        /// </summary>
        public RemoteHero SpectateTarget
        {
            get
            {
                RemoteHero first = null;
                foreach (var hero in remoteHeroes)
                {
                    if (hero == null || !hero.IsAlive) continue;
                    if (hero.Id == spectating) return hero;
                    if (first == null) first = hero;
                }
                if (first != null) spectating = first.Id;
                return first;
            }
        }

        /// <summary>Living teammates a fallen player can watch.</summary>
        public int SpectateCount
        {
            get { int count = 0; foreach (var hero in remoteHeroes) if (hero != null && hero.IsAlive) count++; return count; }
        }

        /// <summary>Switches the fallen player's camera to the next (or, with -1, the previous) living teammate.</summary>
        public void CycleSpectate(int step)
        {
            var current = SpectateTarget;
            if (current == null) return;
            var living = remoteHeroes.FindAll(hero => hero != null && hero.IsAlive);
            int index = living.IndexOf(current);
            spectating = living[((index + step) % living.Count + living.Count) % living.Count].Id;
        }
    }
}

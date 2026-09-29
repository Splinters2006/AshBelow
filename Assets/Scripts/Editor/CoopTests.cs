using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Slopgame.Editor
{
    /// <summary>
    /// Co-op checks that need no second machine: message round trips, the shared flow field, health scaling and
    /// seed determinism. <see cref="BuildSmokePlayer"/> builds the Linux development player for the two-process smoke test.
    /// </summary>
    public static class CoopTests
    {
        public static void Run()
        {
            try
            {
                TestMessages();
                TestFlowField();
                TestScaling();
                TestDeterminism();
                TestP2pAddresses();
                Debug.Log("COOP_TESTS_OK: message round trips, multi-hero flow field, party health scaling, seed determinism, P2P address parsing");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        private static void TestP2pAddresses()
        {
            Require(PortMapper.TryParseEndpoint(" 203.0.113.7:7788 ", 7777, out string host, out ushort port) && host == "203.0.113.7" && port == 7788,
                "IP:port did not parse.");
            Require(PortMapper.TryParseEndpoint("192.168.1.20", 7777, out host, out port) && host == "192.168.1.20" && port == 7777, "Bare IP lost the default port.");
            Require(PortMapper.TryParseEndpoint("[2001:db8::1]:9000", 7777, out host, out port) && host == "2001:db8::1" && port == 9000, "IPv6 did not parse.");
            Require(PortMapper.TryParseEndpoint("2001:db8::1", 7777, out host, out port) && host == "2001:db8::1" && port == 7777, "Bare IPv6 did not parse.");
            Require(PortMapper.TryParseEndpoint("friend.example.com:7777", 1, out host, out port) && host == "friend.example.com" && port == 7777, "Hostname did not parse.");
            Require(!PortMapper.TryParseEndpoint("1.2.3.4:99999", 7777, out _, out _) && !PortMapper.TryParseEndpoint("  ", 7777, out _, out _)
                && !PortMapper.TryParseEndpoint("1.2.3.4:0", 7777, out _, out _), "Bad addresses were accepted.");
            Require(PortMapper.IsPrivate(System.Net.IPAddress.Parse("192.168.0.4")) && PortMapper.IsPrivate(System.Net.IPAddress.Parse("100.72.1.1"))
                && PortMapper.IsPrivate(System.Net.IPAddress.Parse("10.0.0.1")) && PortMapper.IsPrivate(System.Net.IPAddress.Parse("172.20.0.1"))
                && !PortMapper.IsPrivate(System.Net.IPAddress.Parse("203.0.113.7")) && !PortMapper.IsPrivate(System.Net.IPAddress.Parse("172.32.0.1")),
                "Private / CGNAT address detection is wrong.");
            Require(PortMapper.SsdpLocation("HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=120\r\nLocation: http://192.168.1.1:5000/rootDesc.xml\r\n\r\n")
                == "http://192.168.1.1:5000/rootDesc.xml", "SSDP location was not read.");
        }

        private static T RoundTrip<T>(T value, Action<T, FastBufferWriter> write, Func<FastBufferReader, T> read)
        {
            using var writer = NetSession.Writer();
            write(value, writer);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            return read(reader);
        }

        private static void TestMessages()
        {
            byte buffFlags = PlayerStateMessage.Rolling | PlayerStateMessage.Dead | PlayerStateMessage.Raging | PlayerStateMessage.Tired;
            var state = new PlayerStateMessage { Id = 3, Floor = 7, Position = new Vector2(1.5f, -2f), Aim = Vector2.up, Flags = buffFlags, MoreFlags = PlayerStateMessage.Blessed, Health = 4, MaxHealth = 8, Charge = 200 };
            var stateBack = RoundTrip(state, (m, w) => m.Write(w), PlayerStateMessage.Read);
            Require(stateBack.Id == 3 && stateBack.Floor == 7 && stateBack.Position == state.Position && stateBack.Aim == Vector2.up
                && stateBack.Flags == buffFlags && stateBack.MoreFlags == PlayerStateMessage.Blessed && stateBack.Health == 4 && stateBack.MaxHealth == 8 && stateBack.Charge == 200, "Player state did not round-trip.");

            var enemy = new EnemySnapshot { Id = 12, Position = new Vector2(20f, 9f), Facing = Vector2.left, Health = 31, Flags = EnemySnapshot.Burning | EnemySnapshot.Charging,
                MoreFlags = EnemySnapshot.Paralyzed | EnemySnapshot.Cursed };
            var enemyBack = RoundTrip(enemy, (m, w) => m.Write(w), EnemySnapshot.Read);
            Require(enemyBack.Id == 12 && enemyBack.Position == enemy.Position && enemyBack.Facing == Vector2.left && enemyBack.Health == 31
                && enemyBack.Flags == enemy.Flags && enemyBack.MoreFlags == enemy.MoreFlags, "Enemy snapshot did not round-trip.");

            var damage = new DamageMessage { Floor = 2, Enemy = 5, Kind = CoopDamageKind.Burn, Amount = 3, Ticks = 4, Duration = 1.5f, Knockback = 0.1f, Source = Vector2.one, Color = new Color32(1, 2, 3, 4) };
            var damageBack = RoundTrip(damage, (m, w) => m.Write(w), DamageMessage.Read);
            Require(damageBack.Floor == 2 && damageBack.Enemy == 5 && damageBack.Kind == CoopDamageKind.Burn && damageBack.Amount == 3 && damageBack.Ticks == 4
                && Mathf.Approximately(damageBack.Duration, 1.5f) && Mathf.Approximately(damageBack.Knockback, 0.1f) && damageBack.Source == Vector2.one && damageBack.Color.Equals(damage.Color), "Damage did not round-trip.");

            var fx = new FxMessage { Origin = 1, Floor = 3, Kind = FxKind.Spell, A = Vector2.right, B = Vector2.down, Color = new Color32(9, 8, 7, 255), F1 = 7f, F2 = 1.7f, N = 4 };
            var fxBack = RoundTrip(fx, (m, w) => m.Write(w), FxMessage.Read);
            Require(fxBack.Origin == 1 && fxBack.Floor == 3 && fxBack.Kind == FxKind.Spell && fxBack.A == Vector2.right && fxBack.B == Vector2.down
                && fxBack.Color.Equals(fx.Color) && Mathf.Approximately(fxBack.F1, 7f) && Mathf.Approximately(fxBack.F2, 1.7f) && fxBack.N == 4, "Effect did not round-trip.");

            var support = new SupportMessage { Origin = 2, Target = 3, Kind = SupportKind.Bless, Amount = 2, Duration = 6f };
            var supportBack = RoundTrip(support, (m, w) => m.Write(w), SupportMessage.Read);
            Require(supportBack.Origin == 2 && supportBack.Target == 3 && supportBack.Kind == SupportKind.Bless && supportBack.Amount == 2
                && Mathf.Approximately(supportBack.Duration, 6f), "Support did not round-trip.");

            var bolt = new BoltEventMessage { Origin = 1, Floor = 5, Bolt = 44, Kind = CoopBoltEventKind.Reflected, Position = new Vector2(3f, 4f), Direction = Vector2.left };
            var boltBack = RoundTrip(bolt, (m, w) => m.Write(w), BoltEventMessage.Read);
            Require(boltBack.Origin == 1 && boltBack.Floor == 5 && boltBack.Bolt == 44 && boltBack.Kind == CoopBoltEventKind.Reflected
                && boltBack.Position == bolt.Position && boltBack.Direction == Vector2.left, "Bolt event did not round-trip.");

            var hazard = new HazardMessage
            {
                Floor = 15,
                Spec = new HazardSpec { Shape = HazardShape.Inferno, Center = new Vector2(27f, 19f), Direction = Vector2.up, Radius = 2.8f, Width = 0.9f, Telegraph = 2.4f, Duration = 6.5f }
            };
            var hazardBack = RoundTrip(hazard, (m, w) => m.Write(w), HazardMessage.Read);
            Require(hazardBack.Floor == 15 && hazardBack.Spec.Shape == HazardShape.Inferno && hazardBack.Spec.Center == hazard.Spec.Center
                && hazardBack.Spec.Direction == Vector2.up && Mathf.Approximately(hazardBack.Spec.Radius, 2.8f) && Mathf.Approximately(hazardBack.Spec.Width, 0.9f)
                && Mathf.Approximately(hazardBack.Spec.Telegraph, 2.4f) && Mathf.Approximately(hazardBack.Spec.Duration, 6.5f), "Hellfire hazard did not round-trip.");
            Require(DungeonBoss.KindForFloor(5) == BossKind.AshWarden && DungeonBoss.KindForFloor(10) == BossKind.Duelist
                && DungeonBoss.KindForFloor(15) == BossKind.Archdemon && DungeonBoss.KindForFloor(20) == BossKind.GridOverseer, "Boss rotation is wrong.");
            // Floors past the last world stay in it, and no world begins after the last one.
            int lastFloor = WorldCatalog.All.Length * WorldCatalog.FloorsPerWorld;
            Require(!WorldCatalog.ForFloor(1).HighTech && !WorldCatalog.ForFloor(15).HighTech && WorldCatalog.ForFloor(16).HighTech
                && WorldCatalog.ForFloor(30).HighTech && WorldCatalog.EntersWorld(16) && !WorldCatalog.EntersWorld(1) && WorldCatalog.EntersWorld(31)
                && !WorldCatalog.EntersWorld(lastFloor + 1) && WorldCatalog.ForFloor(lastFloor + 45) == WorldCatalog.All[WorldCatalog.All.Length - 1],
                "The third guardian does not lead into the Neon Arcology, or the later worlds are out of order.");
            Require((byte)((0x0F << EnemySnapshot.BossStateShift) & (EnemySnapshot.Flashing | EnemySnapshot.Chilled | EnemySnapshot.Burning | EnemySnapshot.Charging)) == 0,
                "Boss state bits overlap the enemy snapshot flags.");
        }

        private static void TestFlowField()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var map = new DungeonMap(seed * 7919);
                var sources = new List<Vector2Int> { map.Centers[0], map.Centers[map.Centers.Count - 1], map.Centers[map.Centers.Count / 2] };
                var combined = new int[DungeonMap.Width, DungeonMap.Height];
                DungeonRun.BuildFlowField(map, sources, combined);
                var single = new List<int[,]>();
                foreach (var source in sources)
                {
                    var field = new int[DungeonMap.Width, DungeonMap.Height];
                    DungeonRun.BuildFlowField(map, new List<Vector2Int> { source }, field);
                    single.Add(field);
                }
                for (int x = 0; x < DungeonMap.Width; x++)
                    for (int y = 0; y < DungeonMap.Height; y++)
                    {
                        int nearest = int.MaxValue;
                        foreach (var field in single) nearest = Mathf.Min(nearest, field[x, y]);
                        Require(combined[x, y] == nearest, $"Seed {seed}: cell {x},{y} should be {nearest} steps from the nearest hero, not {combined[x, y]}.");
                    }
                foreach (var source in sources) Require(combined[source.x, source.y] == 0, "A hero's own cell must be distance 0.");
            }
        }

        private static void TestScaling()
        {
            Require(DungeonRun.ScaleHealth(2, 1) == 2, "Solo health must be unchanged.");
            Require(DungeonRun.ScaleHealth(2, 2) == 3, "Two heroes: 1.5x health.");
            Require(DungeonRun.ScaleHealth(3, 2) == 5, "Scaled health rounds up.");
            Require(DungeonRun.ScaleHealth(27, 4) == 68, "Four heroes: 2.5x health.");
            Require(DungeonRun.ScaleHealth(5, 0) == 5, "A missing party size counts as solo.");
            Require(DungeonBoss.ScaledHealth(10, 1) == 30 && DungeonBoss.ScaledHealth(10, 0) == 30, "Solo guardians: 3x base health.");
            Require(DungeonBoss.ScaledHealth(10, 2) == 60 && DungeonBoss.ScaledHealth(10, 4) == 120, "Every hero adds a full guardian's health.");
        }

        private static void TestDeterminism()
        {
            for (int floor = 1; floor <= 12; floor++)
            {
                int seed = 424242 + floor * 7919;
                bool boss = floor % 5 == 0;
                var a = new DungeonMap(seed, boss);
                var b = new DungeonMap(seed, boss);
                Require(a.Centers.Count == b.Centers.Count, "Same seed, different room count.");
                for (int i = 0; i < a.Centers.Count; i++) Require(a.Centers[i] == b.Centers[i], "Same seed, different rooms.");
                for (int x = 0; x < DungeonMap.Width; x++)
                    for (int y = 0; y < DungeonMap.Height; y++)
                        Require(a.IsFloor(x, y) == b.IsFloor(x, y), "Same seed, different walls.");
            }
        }

        /// <summary>Builds a Linux development player to Builds/CoopSmoke for the two-process smoke test.</summary>
        public static void BuildSmokePlayer()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/CoopSmoke"));
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Dungeon.unity" },
                locationPathName = Path.Combine(folder, "AshBelow.x86_64"),
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Co-op smoke build failed: " + report.summary.result);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log("COOP_SMOKE_BUILD_OK: " + folder);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}

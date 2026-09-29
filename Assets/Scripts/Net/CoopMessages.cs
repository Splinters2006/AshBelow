using Unity.Netcode;
using UnityEngine;

namespace Slopgame
{
    /// <summary>Named-message ids and payloads for co-op. Every payload has matching Write/Read helpers.</summary>
    public static class CoopMessages
    {
        public const string Start = "ab.start", Lobby = "ab.lobby", State = "ab.state", Enemies = "ab.enemies",
            Damage = "ab.damage", Kill = "ab.kill", Bolt = "ab.bolt", BoltEvent = "ab.boltevent", Fx = "ab.fx",
            Support = "ab.support", Interact = "ab.interact", Choice = "ab.choice", ChoiceDone = "ab.done",
            Advance = "ab.advance", Died = "ab.died", Over = "ab.over", Hazard = "ab.hazard",
            RestartVote = "ab.restartvote", RestartVotes = "ab.restartvotes";
    }

    public enum CoopChoice : byte { Upgrade, Artifact }
    public enum CoopDamageKind : byte { Hit, Burn, Chill, Paralyze, Curse }
    public enum CoopBoltEventKind : byte { Reflected, Consumed }
    public enum SupportKind : byte { Heal, Protect, Bless }
    public enum FxKind : byte { Arrow, Spell, Slash, Bolt, GlowBolt, Ring, Pulse, Rift, Execution, Singularity, Punch, RearHit, Knife, PiercingShot, Aegis, Holy, Sanctuary,
        Windstep, Shadowstep, Stab, Quake, HolySword, Venom, HeavyPunch, Flurry, SanctuaryEnd, TailStab, TailSweep, Pentagram, DemonPaw }

    public struct PlayerStateMessage
    {
        public const byte Rolling = 1, Blocking = 2, Charging = 4, Dead = 8, Invulnerable = 16, Empowered = 32, Raging = 64, Tired = 128;
        /// <summary>Bits of <see cref="MoreFlags"/>.</summary>
        public const byte Blessed = 1, Veiled = 2, Ascended = 4;
        public ulong Id;
        public int Floor;
        public Vector2 Position, Aim;
        public byte Flags, MoreFlags;
        public short Health, MaxHealth;
        /// <summary>How far the current attack charge has filled, 0-255.</summary>
        public byte Charge;

        public void Write(FastBufferWriter w)
        {
            w.WriteValueSafe(Id); w.WriteValueSafe(Floor); w.WriteValueSafe(Position); w.WriteValueSafe(Aim);
            w.WriteValueSafe(Flags); w.WriteValueSafe(MoreFlags); w.WriteValueSafe(Health); w.WriteValueSafe(MaxHealth);
            w.WriteValueSafe(Charge);
        }

        public static PlayerStateMessage Read(FastBufferReader r)
        {
            var m = new PlayerStateMessage();
            r.ReadValueSafe(out m.Id); r.ReadValueSafe(out m.Floor); r.ReadValueSafe(out m.Position); r.ReadValueSafe(out m.Aim);
            r.ReadValueSafe(out m.Flags); r.ReadValueSafe(out m.MoreFlags); r.ReadValueSafe(out m.Health); r.ReadValueSafe(out m.MaxHealth);
            r.ReadValueSafe(out m.Charge);
            return m;
        }
    }

    public struct EnemySnapshot
    {
        public const byte Flashing = 1, Chilled = 2, Burning = 4, Charging = 8;
        /// <summary>The top four bits carry the boss's attack state (see <see cref="DungeonBoss.NetState"/>).</summary>
        public const int BossStateShift = 4;
        /// <summary>Bits of <see cref="MoreFlags"/>.</summary>
        public const byte Paralyzed = 1, Cursed = 2;
        public ushort Id;
        public Vector2 Position, Facing;
        public int Health;
        public byte Flags, MoreFlags;

        public void Write(FastBufferWriter w)
        {
            w.WriteValueSafe(Id); w.WriteValueSafe(Position); w.WriteValueSafe(Facing); w.WriteValueSafe(Health); w.WriteValueSafe(Flags); w.WriteValueSafe(MoreFlags);
        }

        public static EnemySnapshot Read(FastBufferReader r)
        {
            var m = new EnemySnapshot();
            r.ReadValueSafe(out m.Id); r.ReadValueSafe(out m.Position); r.ReadValueSafe(out m.Facing); r.ReadValueSafe(out m.Health); r.ReadValueSafe(out m.Flags);
            r.ReadValueSafe(out m.MoreFlags);
            return m;
        }
    }

    public struct DamageMessage
    {
        public int Floor;
        public ushort Enemy;
        public CoopDamageKind Kind;
        public int Amount, Ticks;
        public float Duration, Knockback;
        public Vector2 Source;
        public Color32 Color;

        public void Write(FastBufferWriter w)
        {
            w.WriteValueSafe(Floor); w.WriteValueSafe(Enemy); w.WriteValueSafe(Kind); w.WriteValueSafe(Amount);
            w.WriteValueSafe(Ticks); w.WriteValueSafe(Duration); w.WriteValueSafe(Knockback); w.WriteValueSafe(Source); w.WriteValueSafe(Color);
        }

        public static DamageMessage Read(FastBufferReader r)
        {
            var m = new DamageMessage();
            r.ReadValueSafe(out m.Floor); r.ReadValueSafe(out m.Enemy); r.ReadValueSafe(out m.Kind); r.ReadValueSafe(out m.Amount);
            r.ReadValueSafe(out m.Ticks); r.ReadValueSafe(out m.Duration); r.ReadValueSafe(out m.Knockback); r.ReadValueSafe(out m.Source); r.ReadValueSafe(out m.Color);
            return m;
        }
    }

    public struct BoltEventMessage
    {
        public ulong Origin;
        public int Floor, Bolt;
        public CoopBoltEventKind Kind;
        public Vector2 Position, Direction;

        public void Write(FastBufferWriter w)
        {
            w.WriteValueSafe(Origin); w.WriteValueSafe(Floor); w.WriteValueSafe(Bolt); w.WriteValueSafe(Kind);
            w.WriteValueSafe(Position); w.WriteValueSafe(Direction);
        }

        public static BoltEventMessage Read(FastBufferReader r)
        {
            var m = new BoltEventMessage();
            r.ReadValueSafe(out m.Origin); r.ReadValueSafe(out m.Floor); r.ReadValueSafe(out m.Bolt); r.ReadValueSafe(out m.Kind);
            r.ReadValueSafe(out m.Position); r.ReadValueSafe(out m.Direction);
            return m;
        }
    }

    public struct FxMessage
    {
        public ulong Origin;
        public int Floor;
        public FxKind Kind;
        public Vector2 A, B;
        public Color32 Color;
        public float F1, F2;
        public int N;

        public void Write(FastBufferWriter w)
        {
            w.WriteValueSafe(Origin); w.WriteValueSafe(Floor); w.WriteValueSafe(Kind); w.WriteValueSafe(A); w.WriteValueSafe(B);
            w.WriteValueSafe(Color); w.WriteValueSafe(F1); w.WriteValueSafe(F2); w.WriteValueSafe(N);
        }

        public static FxMessage Read(FastBufferReader r)
        {
            var m = new FxMessage();
            r.ReadValueSafe(out m.Origin); r.ReadValueSafe(out m.Floor); r.ReadValueSafe(out m.Kind); r.ReadValueSafe(out m.A); r.ReadValueSafe(out m.B);
            r.ReadValueSafe(out m.Color); r.ReadValueSafe(out m.F1); r.ReadValueSafe(out m.F2); r.ReadValueSafe(out m.N);
            return m;
        }
    }

    public struct HazardMessage
    {
        public int Floor;
        public HazardSpec Spec;

        public void Write(FastBufferWriter w)
        {
            w.WriteValueSafe(Floor); w.WriteValueSafe(Spec.Shape); w.WriteValueSafe(Spec.Center); w.WriteValueSafe(Spec.Direction);
            w.WriteValueSafe(Spec.Radius); w.WriteValueSafe(Spec.Width); w.WriteValueSafe(Spec.Telegraph); w.WriteValueSafe(Spec.Duration);
        }

        public static HazardMessage Read(FastBufferReader r)
        {
            var m = new HazardMessage();
            r.ReadValueSafe(out m.Floor); r.ReadValueSafe(out m.Spec.Shape); r.ReadValueSafe(out m.Spec.Center); r.ReadValueSafe(out m.Spec.Direction);
            r.ReadValueSafe(out m.Spec.Radius); r.ReadValueSafe(out m.Spec.Width); r.ReadValueSafe(out m.Spec.Telegraph); r.ReadValueSafe(out m.Spec.Duration);
            return m;
        }
    }

    public struct SupportMessage
    {
        public ulong Origin, Target;
        public SupportKind Kind;
        public int Amount;
        public float Duration;

        public void Write(FastBufferWriter w)
        {
            w.WriteValueSafe(Origin); w.WriteValueSafe(Target); w.WriteValueSafe(Kind); w.WriteValueSafe(Amount); w.WriteValueSafe(Duration);
        }

        public static SupportMessage Read(FastBufferReader r)
        {
            var m = new SupportMessage();
            r.ReadValueSafe(out m.Origin); r.ReadValueSafe(out m.Target); r.ReadValueSafe(out m.Kind); r.ReadValueSafe(out m.Amount); r.ReadValueSafe(out m.Duration);
            return m;
        }
    }
}

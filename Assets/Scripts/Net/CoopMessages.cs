using Unity.Netcode;
using UnityEngine;

namespace Slopgame
{
    /// <summary>Named-message ids and payloads for co-op. Every payload has matching Write/Read helpers.</summary>
    public static class CoopMessages
    {
        public const string Skeletons = "ab.skeletons";
        public const string Start = "ab.start", Lobby = "ab.lobby", State = "ab.state", Enemies = "ab.enemies",
            Damage = "ab.damage", Kill = "ab.kill", Bolt = "ab.bolt", BoltEvent = "ab.boltevent", Fx = "ab.fx",
            Support = "ab.support", Interact = "ab.interact", Choice = "ab.choice", ChoiceDone = "ab.done",
            Advance = "ab.advance", Died = "ab.died", Revived = "ab.revived", Over = "ab.over", Hazard = "ab.hazard",
            RestartVote = "ab.restartvote", RestartVotes = "ab.restartvotes", Minion = "ab.minion", Smash = "ab.smash", Pickup = "ab.pickup";
    }

    /// <summary>A crystal or heart every machine drops in the same place: once one hero takes it, it is gone for everyone.</summary>
    public interface ISharedPickup
    {
        /// <summary>A teammate took this pickup: remove this machine's copy (crystals still pay the local hero their share).</summary>
        void CollectRemote();
    }

    public enum CoopChoice : byte { Upgrade, Artifact, Waves }
    public enum CoopDamageKind : byte { Hit, Burn, Chill, Paralyze, Curse, Freeze, Fear, Stun, Root, Bleed, Poison, Mark, DeathMark, ClearParalysis, ClearHolds, ClearBleed, RefreshBleed, Scorchblood }
    public enum CoopBoltEventKind : byte { Reflected, Consumed }
    /// <summary>
    /// Heal, Protect and Bless help a teammate. BlessingCredit tells a Paladin how much bonus damage their blessing
    /// dealt (it charges their angels); Revive raises a fallen teammate with half health.
    /// </summary>
    public enum SupportKind : byte { Heal, Protect, Bless, BlessingCredit, Revive, Ward, Intervention }
    public enum FxKind : byte { Arrow, Spell, Slash, Bolt, GlowBolt, Ring, Pulse, Punch, RearHit, Knife, PiercingShot, Aegis, Holy, Sanctuary,
        Windstep, Shadowstep, Stab, Quake, HolySword, Venom, HeavyPunch, Flurry, SanctuaryEnd, TailStab, TailSweep, Pentagram, DemonPaw, Coin, DemonHead, Sharpen, CoinFlip, CoinRain, Angel, Jackpot,
        PlasmaRay, PlasmaOrb, SentryTurret, MicroMissile, IceWall, Wings, SoulSiphon, Ricochet, Net, ShadowClone, DeathMark, RocketBoost, OrbitalLaser, Grapple, BrawlerMove, Insurance, InsuranceClaim, Card, Dice,
        Shield, Whirlwind, WarBanner, Consecration, Heal, Lance, Intervention, BallLightning, IceBreak, NightmareSnap, SnapTether, InterventionSaved, AvatarOfDeath, DemonicPower, SuperAngry, ShieldTaunt, Retribution, RockCover, HallowedGround, KatanaCrescent, KatanaThrust, KatanaSlice, KatanaSheathe, SliceDiceChunk, Bloodpop, PrimalRage, Bloodscent, CrimsonBloom, PyreBurst, Scorchburst }

    public struct PlayerStateMessage
    {
        public const byte Rolling = 1, Blocking = 2, Charging = 4, Dead = 8, Invulnerable = 16, Empowered = 32, Raging = 64, Tired = 128;
        /// <summary>Bits of <see cref="MoreFlags"/>.</summary>
        public const byte Blessed = 1, Veiled = 2, Ascended = 4, Taunting = 8, Furious = 16;
        /// <summary>The Specimen's body in <see cref="MoreFlags"/>: bits 5-6 hold his form (0 frail, 1 Behemoth, 2 Edge,
        /// 3 rampaging Behemoth) and bit 7 is set once he has grown (the Colossus, or the Edge's second stage).</summary>
        public const byte SpecimenFormMask = 96, SpecimenGrown = 128;
        public const int SpecimenFormShift = 5;
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

    public struct SkeletonSnapshot
    {
        public Vector2 Position, Scale;
        public float Angle;
        public bool Flip;
        public Color32 Color;

        public void Write(FastBufferWriter writer)
        {
            writer.WriteValueSafe(Position); writer.WriteValueSafe(Scale); writer.WriteValueSafe(Angle);
            writer.WriteValueSafe(Flip); writer.WriteValueSafe(Color);
        }

        public static SkeletonSnapshot Read(FastBufferReader reader)
        {
            var value = new SkeletonSnapshot();
            reader.ReadValueSafe(out value.Position); reader.ReadValueSafe(out value.Scale); reader.ReadValueSafe(out value.Angle);
            reader.ReadValueSafe(out value.Flip); reader.ReadValueSafe(out value.Color);
            return value;
        }
    }

    public struct EnemySnapshot
    {
        public const byte Flashing = 1, Chilled = 2, Burning = 4, Charging = 8;
        /// <summary>The top four bits carry the boss's attack state (see <see cref="DungeonBoss.NetState"/>).</summary>
        public const int BossStateShift = 4;
        /// <summary>Bits of <see cref="MoreFlags"/>.</summary>
        public const byte Paralyzed = 1, Cursed = 2, Frozen = 4, HasMaxHealth = 8, Stunned = 16, Rooted = 32, Bleeding = 64, Poisoned = 128;
        /// <summary>Any hold (paralysis, freeze, stun or root): snapshots with one also carry <see cref="HoldLeft"/>.</summary>
        public const byte HoldBits = Paralyzed | Frozen | Stunned | Rooted;
        public ushort Id;
        public Vector2 Position, Facing;
        public int Health;
        /// <summary>A guardian's maximum health on the host (sent only with <see cref="HasMaxHealth"/>), so every health bar agrees.</summary>
        public int MaxHealth;
        public byte Flags, MoreFlags;
        /// <summary>Seconds the enemy's longest hold still has to run on the host (sent only with a hold flag), so a guest's
        /// Nightmare Snap deals what the host would.</summary>
        public float HoldLeft;

        public void Write(FastBufferWriter w)
        {
            w.WriteValueSafe(Id); w.WriteValueSafe(Position); w.WriteValueSafe(Facing); w.WriteValueSafe(Health); w.WriteValueSafe(Flags); w.WriteValueSafe(MoreFlags);
            if ((MoreFlags & HasMaxHealth) != 0) w.WriteValueSafe(MaxHealth);
            if ((MoreFlags & HoldBits) != 0) w.WriteValueSafe((ushort)Mathf.Clamp(Mathf.RoundToInt(HoldLeft * 100f), 0, ushort.MaxValue));
        }

        public static EnemySnapshot Read(FastBufferReader r)
        {
            var m = new EnemySnapshot();
            r.ReadValueSafe(out m.Id); r.ReadValueSafe(out m.Position); r.ReadValueSafe(out m.Facing); r.ReadValueSafe(out m.Health); r.ReadValueSafe(out m.Flags);
            r.ReadValueSafe(out m.MoreFlags);
            if ((m.MoreFlags & HasMaxHealth) != 0) r.ReadValueSafe(out m.MaxHealth);
            if ((m.MoreFlags & HoldBits) != 0) { r.ReadValueSafe(out ushort hold); m.HoldLeft = hold / 100f; }
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
            w.WriteValueSafe(Spec.Style); w.WriteValueSafe(Spec.Lethal); w.WriteValueSafe((byte)Mathf.Clamp(Spec.Damage, 0, 255));
        }

        public static HazardMessage Read(FastBufferReader r)
        {
            var m = new HazardMessage();
            r.ReadValueSafe(out m.Floor); r.ReadValueSafe(out m.Spec.Shape); r.ReadValueSafe(out m.Spec.Center); r.ReadValueSafe(out m.Spec.Direction);
            r.ReadValueSafe(out m.Spec.Radius); r.ReadValueSafe(out m.Spec.Width); r.ReadValueSafe(out m.Spec.Telegraph); r.ReadValueSafe(out m.Spec.Duration);
            r.ReadValueSafe(out m.Spec.Style); r.ReadValueSafe(out m.Spec.Lethal); r.ReadValueSafe(out byte damage);
            m.Spec.Damage = damage;
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

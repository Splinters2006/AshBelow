using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Samurai's demos: her katana, True Poser and her six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static Color Blood => SamuraiAttack.Blood;
        private static Color Steel => SamuraiAttack.Steel;
        private bool backhand;

        private IEnumerator Samurai() => kind switch
        {
            DemoKind.Charged => SamuraiFlurry(),
            DemoKind.Heavy => SamuraiDash(),
            DemoKind.Mechanic => TruePoserDemo(),
            _ => ability.Type switch
            {
                AbilityType.SliceDiceChunk => SliceDiceChunk(),
                AbilityType.Bloodscent => Bloodscent(),
                AbilityType.BloodShallFlow => BloodShallFlow(),
                AbilityType.MaestrosTechnique => MaestrosTechnique(),
                AbilityType.SwiftAsTheWind => SwiftAsTheWind(),
                AbilityType.Bloodpop => Bloodpop(),
                _ => null
            }
        };

        /// <summary>One crescent cut of the katana: everything in the cone is struck, and bleeds if <paramref name="bleed"/> is set.</summary>
        private void KatanaCut(Vector2 aim, float reach, float cone, Color color, float duration = 0.3f, float knockback = 0.3f, bool bleed = false)
        {
            Face(aim);
            KatanaVfx.Crescent(fx, HeroAt, aim, reach, cone, color, backhand, duration);
            backhand = !backhand;
            foreach (var dummy in InCone(HeroAt, aim, reach, cone))
            {
                dummy.Hit(HeroAt, knockback);
                if (bleed) dummy.Bleed(DungeonEnemy.BleedDuration);
            }
        }

        /// <summary>A full charge: the cone reddens as it fills, then a blur of six crossing slashes.</summary>
        private IEnumerator SamuraiFlurry()
        {
            Set(4f, new Vector2(-1.4f, 0f), new Vector2(0.4f, 0.5f), new Vector2(0.6f, -0.6f));
            yield return Wait(0.5f);
            yield return Charge(SamuraiAttack.ChargeDuration, (mesh, charge) => Cone(mesh, HeroAt, Vector2.right,
                Mathf.Lerp(SamuraiAttack.Reach, SamuraiAttack.FlurryReach, charge), Mathf.Lerp(SamuraiAttack.SlashCone, SamuraiAttack.FlurryCone, charge),
                FlameMesh.Alpha(Color.Lerp(Steel, Blood, charge), charge >= 1f ? 0.26f + 0.08f * Mathf.Sin(Time.time * 14f) : 0.08f + 0.14f * charge)));
            for (int i = 0; i < SamuraiAttack.FlurrySlashes; i++)
            {
                Vector2 aim = Quaternion.Euler(0, 0, Random.Range(-14f, 14f)) * Vector2.right;
                KatanaCut(aim, SamuraiAttack.FlurryReach, SamuraiAttack.FlurryCone, Color.Lerp(Steel, Blood, 0.35f), 0.12f, 0.05f);
                yield return Wait(SamuraiAttack.FlurryInterval);
            }
            yield return Wait(1.2f);
        }

        /// <summary>Right click: she is suddenly on the far side of the pack, and every enemy she passed parts along a clean line.</summary>
        private IEnumerator SamuraiDash()
        {
            Set(5f, new Vector2(-3f, 0f), new Vector2(-1.2f, 0.3f), new Vector2(0f, -0.3f), new Vector2(1.1f, 0.35f));
            yield return Wait(0.7f);
            Vector2 aim = Vector2.right, from = HeroAt, to = from + aim * SamuraiAttack.DashRange;
            Face(aim);
            HeroAt = to;
            KatanaVfx.Slice(fx, from, to, Blood, 0.34f, SamuraiAttack.DashHalfWidth);
            CombatVfx.Ring(fx, from, 0.7f, Steel, 0.2f);
            KatanaVfx.Crescent(fx, to, aim, SamuraiAttack.DashHalfWidth, 180f, Steel, backhand, 0.24f);
            HeroVfx.Sparks(fx, to, Steel, 14, 5.5f, 0.3f, to - from, 100f);
            foreach (var dummy in InLane(from, aim, SamuraiAttack.DashRange, SamuraiAttack.DashHalfWidth))
            {
                Vector2 at = dummy.Position;
                KatanaVfx.Slice(fx, at - aim * (DummyRadius + 0.6f), at + aim * (DummyRadius + 0.6f), Blood, 0.3f);
                HeroVfx.Sparks(fx, at, Blood, 12, 5.5f, 0.3f, Vector2.Perpendicular(aim), 70f);
                HeroVfx.Sparks(fx, at, Blood, 12, 5.5f, 0.3f, -Vector2.Perpendicular(aim), 70f);
                dummy.Hit(at - aim, 0.2f);
            }
            yield return Wait(1.8f);
        }

        /// <summary>R: she strikes a pose and her cuts deal nothing; then she sheathes, and every cut she owed lands at once.</summary>
        private IEnumerator TruePoserDemo()
        {
            Set(4.5f, new Vector2(-1.2f, -0.4f), new Vector2(0.7f, 0.2f), new Vector2(0.9f, -1f), new Vector2(0.2f, 1.2f));
            yield return Wait(0.5f);
            HeroVfx.Pulse(fx, HeroAt, 1.2f, Steel, 0.35f);
            CombatVfx.Ring(fx, HeroAt, 0.9f, Blood, 0.4f);
            yield return Wait(0.5f);
            // Posing: the blade passes through them and nothing happens, yet.
            for (int i = 0; i < 4; i++)
            {
                Vector2 aim = Quaternion.Euler(0, 0, i % 2 == 0 ? 12f : -18f) * Vector2.right;
                Face(aim);
                KatanaVfx.Crescent(fx, HeroAt, aim, SamuraiAttack.Reach, SamuraiAttack.SlashCone, Steel, backhand, 0.3f);
                backhand = !backhand;
                foreach (var dummy in InCone(HeroAt, aim, SamuraiAttack.Reach, SamuraiAttack.SlashCone))
                    HeroVfx.Sparks(fx, dummy.Position, Steel, 3, 1.5f, 0.2f);
                HeroVfx.Motes(fx, HeroAt, 0.55f, Steel, 2, 0.5f);
                yield return Wait(SamuraiAttack.SlashInterval);
            }
            yield return Wait(0.3f);
            // The katana slides home; the click comes 72% of the way through the effect, exactly when the cuts land.
            KatanaVfx.Sheathe(fx, HeroAt, Vector2.right, Blood, TruePoser.SheatheTime / 0.72f);
            yield return Wait(TruePoser.SheatheTime);
            foreach (var dummy in dummies)
            {
                Vector2 at = dummy.Position, across = Random.insideUnitCircle.normalized * (DummyRadius + 0.5f);
                KatanaVfx.Slice(fx, at - across, at + across, Blood, 0.5f);
                HeroVfx.Sparks(fx, at, Blood, 16, 6f, 0.45f);
                HeroVfx.Sparks(fx, at, Steel, 6, 3f, 0.25f);
                CombatVfx.Ring(fx, at, DummyRadius + 0.6f, Blood, 0.3f);
                dummy.Hit(HeroAt, 0.6f);
            }
            yield return Wait(1.8f);
        }

        /// <summary>Slice, then Dice, then Chunk: each slash bigger than the last, and the last leaves them bleeding.</summary>
        private IEnumerator SliceDiceChunk()
        {
            Set(5f, new Vector2(-1.6f, 0f), new Vector2(0.2f, 0.2f), new Vector2(0.9f, -0.9f), new Vector2(1.1f, 1.1f));
            yield return Wait(0.6f);
            for (int stage = 0; stage < 3; stage++)
            {
                bool chunk = stage == 2;
                Cast();
                KatanaCut(Vector2.right, SamuraiAttack.ComboReach[stage], SamuraiAttack.ComboCone[stage], chunk ? Blood : Steel, chunk ? 0.45f : 0.3f, chunk ? 0.9f : 0.2f, chunk);
                SliceDiceChunkVfx.Play(fx, HeroAt, Vector2.right, SamuraiAttack.ComboReach[stage], SamuraiAttack.ComboCone[stage], stage);
                if (chunk) HeroVfx.Sparks(fx, HeroAt + Vector2.right * SamuraiAttack.ComboReach[stage] * 0.6f, Blood, 18, 6f, 0.4f, Vector2.right, 110f, 1.3f);
                yield return Wait(0.75f);
            }
            yield return Wait(1.6f);
        }

        /// <summary>The pack's wounds have nearly run their course; Bloodscent starts every one of them over.</summary>
        private IEnumerator Bloodscent()
        {
            Set(4.5f, new Vector2(-1.4f, 0f), new Vector2(0.4f, 0.5f), new Vector2(0.6f, -0.6f), new Vector2(-0.2f, 1.5f));
            foreach (var dummy in dummies) dummy.Bleed(2.6f);
            // The wounds are nearly spent...
            yield return Wait(2f);
            Cast();
            foreach (var dummy in dummies)
            {
                dummy.Bleed(DungeonEnemy.BleedDuration);
                CombatVfx.Ring(fx, dummy.Position, DummyRadius + 0.3f, Blood, 0.4f);
                HeroVfx.Motes(fx, dummy.Position, 0.4f, Blood, 6, 0.6f);
            }
            // ...and now they run their whole course again.
            yield return Wait(3f);
        }

        /// <summary>For a few seconds every cut of the katana opens a wound.</summary>
        private IEnumerator BloodShallFlow()
        {
            Set(4.5f, new Vector2(-1.4f, 0f), new Vector2(0.4f, 0.5f), new Vector2(0.6f, -0.6f));
            yield return Wait(0.5f);
            Cast();
            HeroVfx.Motes(fx, HeroAt, 0.8f, Blood, 16, 0.9f);
            TintHero(Blood, 3f);
            yield return Wait(0.6f);
            for (int i = 0; i < 4; i++)
            {
                KatanaCut(Quaternion.Euler(0, 0, i % 2 == 0 ? 10f : -10f) * Vector2.right, SamuraiAttack.Reach, SamuraiAttack.SlashCone, Steel, 0.3f, 0.15f, true);
                HeroVfx.Motes(fx, HeroAt, 0.5f, Blood, 2, 0.5f);
                yield return Wait(SamuraiAttack.SlashInterval);
            }
            yield return Wait(2f);
        }

        /// <summary>Sweep, sweep, thrust: the thrust drives down a lane and opens a wound.</summary>
        private IEnumerator MaestrosTechnique()
        {
            Set(5f, new Vector2(-2f, 0f), new Vector2(-0.2f, 0.4f), new Vector2(0f, -0.5f), new Vector2(0.9f, 0f));
            yield return Wait(0.5f);
            Cast();
            yield return Wait(0.5f);
            for (int round = 0; round < 2; round++)
            {
                for (int step = 0; step < 2; step++)
                {
                    KatanaCut(Vector2.right, SamuraiAttack.Reach + 0.2f, 130f, Steel, 0.3f, 0.2f);
                    HeroVfx.Motes(fx, HeroAt, 0.5f, Steel, 2, 0.5f);
                    yield return Wait(SamuraiAttack.SlashInterval * 0.85f);
                }
                Face(Vector2.right);
                KatanaVfx.Thrust(fx, HeroAt, Vector2.right, SamuraiAttack.ThrustReach, SamuraiAttack.ThrustHalfWidth, Blood);
                foreach (var dummy in InLane(HeroAt, Vector2.right, SamuraiAttack.ThrustReach, SamuraiAttack.ThrustHalfWidth))
                {
                    dummy.Hit(HeroAt, 0.8f);
                    dummy.Bleed(DungeonEnemy.BleedDuration);
                }
                yield return Wait(SamuraiAttack.SlashInterval * 1.4f + 0.3f);
            }
            yield return Wait(1.2f);
        }

        /// <summary>A gust takes her, and her cuts come half as fast again.</summary>
        private IEnumerator SwiftAsTheWind()
        {
            Set(4.5f, new Vector2(-1.4f, 0f), new Vector2(0.4f, 0.5f), new Vector2(0.6f, -0.6f));
            yield return Wait(0.4f);
            // At her usual pace first.
            for (int i = 0; i < 2; i++)
            {
                KatanaCut(Vector2.right, SamuraiAttack.Reach, SamuraiAttack.SlashCone, Steel, 0.3f, 0.1f);
                yield return Wait(SamuraiAttack.SlashInterval);
            }
            yield return Wait(0.3f);
            Cast();
            WindstepVfx.Play(fx, HeroAt - Vector2.right * 1.2f, HeroAt);
            Color wind = new Color(0.8f, 0.95f, 0.9f);
            TintHero(wind, 3f);
            yield return Wait(0.5f);
            for (int i = 0; i < 7; i++)
            {
                KatanaCut(Quaternion.Euler(0, 0, i % 2 == 0 ? 8f : -8f) * Vector2.right, SamuraiAttack.Reach, SamuraiAttack.SlashCone, Steel, 0.3f, 0.1f);
                HeroVfx.Motes(fx, HeroAt, 0.5f, wind, 2, 0.5f);
                yield return Wait(SamuraiAttack.SlashInterval / (1f + SamuraiAttack.SwiftAttackSpeed));
            }
            yield return Wait(1f);
        }

        /// <summary>Three enemies are bleeding; she pops every wound at once.</summary>
        private IEnumerator Bloodpop()
        {
            Set(4.5f, new Vector2(-1.8f, 0f), new Vector2(0.8f, 0.9f), new Vector2(1.4f, -0.8f), new Vector2(-0.2f, -1.6f));
            foreach (var dummy in dummies) dummy.Bleed(1.8f);
            yield return Wait(1.6f);
            Cast();
            foreach (var dummy in dummies)
            {
                BloodpopVfx.Play(fx, dummy.Position, DummyRadius);
                dummy.BleedingUntil = 0f;
            }
            // The blister swells for a beat before it bursts.
            yield return Wait(0.15f);
            foreach (var dummy in dummies) dummy.Hit(dummy.Position, 0f);
            yield return Wait(2f);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Demoness's demos: her tail, Demonic Power and her six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private IEnumerator Demoness() => kind switch
        {
            DemoKind.Charged => DemonessCharged(),
            DemoKind.Heavy => DemonessSweep(),
            DemoKind.Mechanic => DemonicPowerDemo(),
            _ => ability.Type switch
            {
                AbilityType.ArchdemonTechnique => ArchdemonTechnique(),
                AbilityType.DemonPaw => DemonPaw(),
                AbilityType.DemonCurse => DemonCurse(),
                AbilityType.WingDash => WingDash(),
                AbilityType.SoulSiphon => SoulSiphon(),
                AbilityType.NightmareSnap => NightmareSnap(),
                _ => null
            }
        };

        /// <summary>A vital stab down the lane at <paramref name="victim"/>: the tail stops in it and it seizes up.</summary>
        private void VitalStab(Dummy victim, Color color)
        {
            Vector2 origin = HeroAt, offset = victim.Position - origin, aim = offset.normalized;
            Face(aim);
            TailVfx.Stab(fx, origin, aim, Mathf.Clamp(offset.magnitude, 0.6f, DemonessAttack.VitalReach), Color.Lerp(color, DemonessAttack.Pale, 0.25f));
            HeroVfx.Sparks(fx, victim.Position, DemonessAttack.Pale, 10, 4f, 0.3f, aim, 80f, 0.9f);
            HeroVfx.Pulse(fx, victim.Position, 0.6f, DemonessAttack.Violet, 0.3f);
            victim.Hit(origin, 0.1f);
            victim.Paralyze(DemonessAttack.VitalParalysis);
        }

        /// <summary>She holds the charge while the lane fills, then strikes the vitals: the enemy is paralysed.</summary>
        private IEnumerator DemonessCharged()
        {
            Set(4f, new Vector2(-1.6f, 0f), new Vector2(2.6f, 0.1f));
            yield return Wait(0.4f);
            // A quick stab first, for contrast: it only hurts.
            Vector2 aim = (dummies[0].Position - HeroAt).normalized;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                Advance(HeroAt + aim * 1.2f, 1.6f);
                yield return null;
            }
            yield return Charge(DemonessAttack.ChargeDuration, (mesh, charge) =>
                Lane(mesh, HeroAt, aim, Mathf.Lerp(DemonessAttack.StabReach, DemonessAttack.VitalReach, charge), DemonessAttack.StabHalfWidth,
                    charge >= 1f ? FlameMesh.Alpha(DemonessAttack.Pale, 0.28f + 0.1f * Mathf.Sin(Time.time * 14f)) : FlameMesh.Alpha(DemonessAttack.Violet, Mathf.Lerp(0.1f, 0.26f, charge))));
            VitalStab(dummies[0], DemonessAttack.Violet);
            yield return Wait(DemonessAttack.VitalParalysis + 0.4f);
        }

        /// <summary>One sweep of the tail across a half circle; anything already held takes the heavier cut.</summary>
        private void TailSweep(Color color, float reach, float cone, bool cursing)
        {
            Face(Vector2.right);
            TailVfx.Sweep(fx, HeroAt, Vector2.right, reach, cone, color);
            foreach (var dummy in dummies)
            {
                if (Vector2.Distance(HeroAt, dummy.Position) > reach + DummyRadius) continue;
                if (dummy.IsHeld)
                {
                    HeroVfx.Slash(fx, dummy.Position, Vector2.right, 0.8f, 90f, DemonessAttack.Pale, 0.18f);
                    HeroVfx.Sparks(fx, dummy.Position, DemonessAttack.Violet, 10, 4.5f, 0.35f);
                }
                dummy.Hit(HeroAt, dummy.IsHeld ? 0.1f : 0.5f);
                if (!cursing) continue;
                dummy.Paralyze(DemonessAttack.VitalParalysis);
                dummy.Curse(DemonessAttack.SweepCurseDuration);
            }
        }

        /// <summary>Right click: she paralyses one enemy with a vital stab, then sweeps her tail through the pack.</summary>
        private IEnumerator DemonessSweep()
        {
            Set(4.2f, new Vector2(-1f, 0f), new Vector2(1f, 0.2f), new Vector2(0.6f, 1.5f), new Vector2(0.9f, -1.4f));
            yield return Wait(0.6f);
            VitalStab(dummies[0], DemonessAttack.Violet);
            yield return Wait(0.6f);
            TailSweep(DemonessAttack.Violet, DemonessAttack.SweepRadius, DemonessAttack.SweepCone, false);
            yield return Wait(1.8f);
        }

        /// <summary>R: rune circles flare around her, her tail splits in two, and her sweeps cover twice the cone.</summary>
        private IEnumerator DemonicPowerDemo()
        {
            Set(5f, new Vector2(-0.6f, 0f), new Vector2(2f, 1.2f), new Vector2(2.2f, -1f), new Vector2(-2.6f, 1.6f), new Vector2(-2.8f, -1.3f));
            yield return Wait(0.5f);
            DemonicPowerVfx.Play(fx, hero.transform, 3.6f);
            HeroVfx.Pulse(fx, HeroAt, 2.6f, HeroBuffs.AscendColor, 0.5f);
            HeroVfx.Motes(fx, HeroAt, 1.4f, DemonessAttack.Abyss, 30, 1.2f);
            HeroVfx.Sparks(fx, HeroAt, Color.Lerp(DemonessAttack.Violet, DemonessAttack.Pale, 0.3f), 26, 6f, 0.5f, null, 360f, 1.3f);
            yield return Wait(1f);
            for (int i = 0; i < 2; i++)
            {
                // Twin tails: the sweep reaches right round her.
                TailSweep(DemonessAttack.Violet, DemonessAttack.SweepRadius * DemonicPower.SweepReachMultiplier,
                    Mathf.Min(360f, DemonessAttack.SweepCone * DemonicPower.SweepConeMultiplier), false);
                yield return Wait(0.9f);
            }
            yield return Wait(1f);
        }

        /// <summary>Her father's power floods in; every click is a vital stab, and Tail Sweep strikes twice, paralysing and cursing.</summary>
        private IEnumerator ArchdemonTechnique()
        {
            Set(4f, new Vector2(-2.2f, 0f), new Vector2(-0.1f, 0.5f), new Vector2(0f, -0.9f), new Vector2(-0.6f, 1.4f));
            yield return Wait(0.6f);
            Cast();
            Color color = HeroBuffs.AscendColor;
            TintHero(color, 4.6f);
            HeroVfx.Pulse(fx, HeroAt, 2.2f, color, 0.5f);
            HeroVfx.Motes(fx, HeroAt, 1f, DemonessAttack.Abyss, 18, 0.9f);
            HeroVfx.Sparks(fx, HeroAt, color, 20, 5f, 0.45f);
            CombatVfx.Ring(fx, HeroAt, 1.5f, DemonessAttack.Pale, 0.45f);
            yield return Wait(0.8f);
            for (int i = 0; i < 3; i++)
            {
                VitalStab(dummies[i], color);
                yield return Wait(0.45f);
            }
            yield return Wait(0.3f);
            for (int sweep = 0; sweep < 2; sweep++)
            {
                TailSweep(color, DemonessAttack.SweepRadius, DemonessAttack.SweepCone, true);
                yield return Wait(DemonessAttack.TwinSweepDelay);
            }
            yield return Wait(2.4f);
        }

        /// <summary>A portal opens over an enemy walking in, tracks it, and the paw comes down on it and its neighbour.</summary>
        private IEnumerator DemonPaw()
        {
            Set(5f, new Vector2(-3.6f, -1.2f), new Vector2(3.4f, -1f), new Vector2(4.2f, -1.7f));
            Vector2 heroAt = HeroAt;
            yield return Approach(0.9f, 1.5f);
            var target = dummies[0];
            Face(target.Position - heroAt);
            Cast();
            float radius = DemonessAttack.PawRadius, windup = DemonessAttack.PortalWindup;
            Vector2 center = target.Position;
            var paw = DemonPawVfx.Play(fx, center, radius, windup);
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                Advance(heroAt, 1.5f);
                // The paw tracks its prey while the portal opens, then commits to that spot.
                if (t < windup * 0.6f) center = target.Position;
                if (paw != null) paw.Center = center;
                yield return null;
            }
            CombatVfx.Ring(fx, center, radius, DemonessAttack.Pale, 0.4f);
            HeroVfx.Pulse(fx, center, radius * 1.2f, DemonessAttack.Violet, 0.4f);
            HeroVfx.Sparks(fx, center, new Color(0.55f, 0.5f, 0.6f), 22, 5.5f, 0.45f);
            foreach (var dummy in dummies)
            {
                if (Vector2.Distance(center, dummy.Position) > radius + DummyRadius) continue;
                dummy.Stun(1.6f);
            }
            yield return Wait(2.2f);
        }

        /// <summary>A pentagram flares under a pack: everything on it is paralysed and cursed.</summary>
        private IEnumerator DemonCurse()
        {
            Vector2 center = new Vector2(2f, 0f);
            Set(7f, new Vector2(-5.6f, 0f), center + new Vector2(-1.4f, 0.8f), center + new Vector2(1.1f, 1.3f), center + new Vector2(0.4f, -1.2f), center + new Vector2(-1.9f, -1.6f));
            Vector2 heroAt = HeroAt;
            yield return Approach(0.8f, 0.9f);
            Face(Vector2.right);
            Cast();
            // The brand lands in the middle of the pack as it stands now.
            Vector2 brand = Vector2.zero;
            foreach (var dummy in dummies) brand += dummy.Position / dummies.Count;
            PentagramVfx.Play(fx, brand, DemonessAttack.CurseRadius, DemonessAttack.CurseWindup);
            for (float t = 0f; t < DemonessAttack.CurseWindup; t += Time.deltaTime) { Advance(heroAt, 0.9f); yield return null; }
            foreach (var dummy in dummies)
            {
                if (Vector2.Distance(brand, dummy.Position) > DemonessAttack.CurseRadius + DummyRadius) continue;
                dummy.Paralyze(DemonessAttack.CurseParalysis);
                dummy.Curse(DemonessAttack.CurseDuration);
            }
            yield return Wait(DemonessAttack.CurseParalysis + 0.4f);
        }

        /// <summary>Her wings beat and she darts through a line of enemies, leaving each one paralysed.</summary>
        private IEnumerator WingDash()
        {
            Set(4.5f, new Vector2(-2.6f, 0f), new Vector2(-1.2f, 0.25f), new Vector2(0f, -0.25f), new Vector2(1.1f, 0.2f));
            yield return Wait(0.7f);
            const float Speed = 16f;
            float distance = DemonessAttack.WingDashDistance;
            Vector2 aim = Vector2.right;
            Cast();
            HeroVfx.Pulse(fx, HeroAt, 1.2f, DemonessAttack.Violet, 0.3f);
            WingFlapVfx.Play(fx, hero.transform, distance / Speed + 0.1f, aim);
            var struck = new HashSet<Dummy>();
            yield return MoveHero(HeroAt + aim * distance, Speed, at =>
            {
                HeroVfx.Sparks(fx, at, DemonessAttack.Abyss, 2, 1.5f, 0.3f, -aim, 90f, 0.8f);
                foreach (var dummy in dummies)
                {
                    if (struck.Contains(dummy) || Vector2.Distance(at, dummy.Position) > DummyRadius + 0.7f) continue;
                    struck.Add(dummy);
                    dummy.Paralyze(DemonessAttack.WingDashParalysis);
                }
            });
            yield return Wait(DemonessAttack.WingDashParalysis + 0.3f);
        }

        /// <summary>The dummies that are held, as the Soul Siphon effect wants them: position, with the hit radius in z.</summary>
        private IEnumerable<Vector3> HeldDummies()
        {
            foreach (var dummy in dummies)
                if (dummy.Body != null && !dummy.Dead && dummy.IsHeld) yield return new Vector3(dummy.Position.x, dummy.Position.y, DummyRadius);
        }

        /// <summary>Three enemies stand paralysed around her; she drains them, and souls stream into her every second.</summary>
        private IEnumerator SoulSiphon()
        {
            Set(4.5f, new Vector2(-0.4f, 0f), new Vector2(1.7f, 0.5f), new Vector2(-2.2f, -0.8f), new Vector2(1.4f, -1.5f));
            yield return Wait(0.5f);
            const float Duration = 3f;
            foreach (var dummy in dummies)
            {
                dummy.Paralyze(Duration + 1.2f);
                HeroVfx.Pulse(fx, dummy.Position, 0.6f, DemonessAttack.Violet, 0.3f);
            }
            yield return Wait(0.7f);
            Cast();
            var siphon = SoulSiphonVfx.Play(fx, hero.transform, Duration, DemonessAttack.SiphonRadius, HeldDummies);
            for (float t = 0f; t < Duration; t += 1f)
            {
                yield return Wait(1f);
                foreach (var dummy in dummies)
                {
                    dummy.Flash();
                    HeroVfx.Sparks(fx, dummy.Position, DemonessAttack.Pale, 6, 2.5f, 0.3f, HeroAt - dummy.Position, 60f);
                }
                if (siphon != null) siphon.Flare(dummies.Count);
            }
            yield return Wait(1.2f);
        }

        /// <summary>Three enemies are held; the snap breaks every hold at once and throws them back.</summary>
        private IEnumerator NightmareSnap()
        {
            Set(4.5f, new Vector2(-1f, 0f), new Vector2(1.6f, 0.9f), new Vector2(2.2f, -0.9f), new Vector2(0f, -1.6f));
            yield return Wait(0.5f);
            foreach (var dummy in dummies)
            {
                HeroVfx.Pulse(fx, dummy.Position, 0.6f, DemonessAttack.Violet, 0.3f);
                dummy.Paralyze(3f);
            }
            yield return Wait(1.1f);
            Vector2 at = HeroAt;
            Cast();
            NightmareSnapVfx.Snap(fx, at, DemonessAttack.SnapRadius);
            foreach (var dummy in dummies)
            {
                NightmareSnapVfx.Tether(fx, at, dummy.Position, 0.8f);
                HeroVfx.Sparks(fx, dummy.Position, DemonessAttack.Pale, 12, 4.5f, 0.35f);
                dummy.Release();
                dummy.Hit(at, 0.8f);
            }
            yield return Wait(2f);
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Reaper's demos: his scythe and soul skulls, the Army of the Dead and his six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static Color Soul => ReaperAttack.Soul;
        private static Color Bone => ReaperAttack.Bone;
        private static Color Shade => ReaperAttack.Shade;

        private IEnumerator Reaper() => kind switch
        {
            DemoKind.Charged => Harvest(),
            DemoKind.Heavy => SoulSkulls(),
            DemoKind.Mechanic => ArmyOfTheDeadDemo(),
            _ => ability.Type switch
            {
                AbilityType.ShadeWalk => ShadeWalk(),
                AbilityType.FearIncarnate => FearIncarnate(),
                AbilityType.Feast => Feast(),
                AbilityType.Sow => Sow(),
                AbilityType.Reap => Reap(),
                AbilityType.ReapersTechnique => ReapersTechnique(),
                _ => null
            }
        };

        /// <summary>One sweep of the scythe through a wide cone.</summary>
        private void Scythe(Vector2 aim, float cone, float reach, float duration, Color color, float knockback = 0.5f, bool reverse = false)
        {
            Face(aim);
            ScytheSwingVfx.Play(fx, hero.transform, aim, cone, reach, duration, color, reverse);
            HeroVfx.Slash(fx, HeroAt, aim, reach, cone, FlameMesh.Alpha(color, 0.55f), 0.18f);
            foreach (var dummy in InCone(HeroAt, aim, reach, cone)) dummy.Hit(HeroAt, knockback);
        }

        /// <summary>A soul leaves <paramref name="from"/> and drifts to the Reaper, who takes it.</summary>
        private IEnumerator SoulTo(Vector2 from)
        {
            Transform stage = fx;
            var soul = DungeonVisuals.Create("Soul", stage, from, Vector2.one * 0.42f, Color.white, 6);
            soul.sprite = SoulWisp.Sprite;
            // It rises out of the body, hangs a moment, then is drawn to him.
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                if (soul == null) yield break;
                soul.transform.position = from + Vector2.up * 0.5f * (t / 0.3f);
                yield return null;
            }
            yield return Wait(0.25f);
            while (soul != null && hero != null && Vector2.Distance(soul.transform.position, HeroAt) > 0.3f)
            {
                soul.transform.position = Vector2.MoveTowards(soul.transform.position, HeroAt, 9f * Time.deltaTime);
                yield return null;
            }
            if (soul == null || stage == null) yield break;
            HeroVfx.Pulse(stage, soul.transform.position, 0.5f, Soul, 0.2f);
            HeroVfx.Sparks(stage, soul.transform.position, Soul, 5, 2f, 0.25f);
            Destroy(soul.gameObject);
        }

        /// <summary>A quick cut, then a full charge: the harvest sweeps wider and pulls a soul out of everything it cuts.</summary>
        private IEnumerator Harvest()
        {
            Set(5f, new Vector2(-1.4f, 0f), new Vector2(0.8f, 0f), new Vector2(0.2f, 1.7f), new Vector2(0.2f, -1.7f));
            yield return Wait(0.5f);
            Scythe(Vector2.right, ReaperAttack.TapCone, ReaperAttack.Reach, 0.28f, Bone);
            yield return Wait(1f);
            foreach (var dummy in dummies) { dummy.Velocity = Vector2.zero; }
            yield return Charge(ReaperAttack.ChargeDuration, (mesh, charge) => Cone(mesh, HeroAt, Vector2.right, ReaperAttack.Reach,
                Mathf.Lerp(ReaperAttack.TapCone, ReaperAttack.ChargedCone, charge), FlameMesh.Alpha(Color.Lerp(Shade, Soul, charge), charge >= 1f ? 0.26f + 0.08f * Mathf.Sin(Time.time * 14f) : 0.08f + 0.14f * charge)));
            var cut = InCone(HeroAt, Vector2.right, ReaperAttack.Reach, ReaperAttack.ChargedCone);
            Scythe(Vector2.right, ReaperAttack.ChargedCone, ReaperAttack.Reach, 0.34f, Soul, 0.9f);
            foreach (var dummy in cut) StartCoroutine(SoulTo(dummy.Position));
            yield return Wait(2f);
        }

        /// <summary>A skull thrown from his hand: it turns after its prey, and a fearsome one leaves it frozen with fright.</summary>
        private void Skull(Vector2 origin, Vector2 direction, Dummy target, float fear)
        {
            var body = Sprite("Reaper skull", ReaperSkull.Sprite, origin, Vector2.one * ReaperSkull.Size, Color.white, 7);
            CombatVfx.Trail(body.gameObject, FlameMesh.Alpha(Soul, fear > 0f ? 0.8f : 0.5f), 0.14f, 0.18f);
            var shot = Shoot(body, direction, ReaperSkull.Speed, ReaperAttack.SkullRange, 0.14f, false, (dummy, at) =>
            {
                dummy.Hit(at - direction, 0.4f);
                if (fear > 0f) dummy.Fear(fear + 0.8f);
                HeroVfx.Sparks(fx, at, Bone, 7, 3.2f, 0.28f, -direction, 160f, 0.9f);
                HeroVfx.Pulse(fx, at, 0.45f, Soul, 0.2f);
            });
            shot.Target = target;
            shot.TurnRate = ReaperSkull.TurnRate;
        }

        /// <summary>Right click: he spends souls on skulls. A tap throws one; held to the full, three fearsome ones fan out and hunt.</summary>
        private IEnumerator SoulSkulls()
        {
            Set(6f, new Vector2(-4.4f, 0f), new Vector2(1.4f, 1.9f), new Vector2(2.4f, 0f), new Vector2(1.4f, -1.9f));
            yield return Wait(0.6f);
            Face(Vector2.right);
            Vector2 hand = HeroAt + Vector2.right * 0.45f;
            Skull(hand, Vector2.right, dummies[1], 0f);
            HeroVfx.Sparks(fx, hand, Soul, 9, 3.5f, 0.3f, Vector2.right, 70f);
            yield return Wait(1.4f);
            // Held: the souls gather in his hand, one ping for each skull.
            for (int ping = 1; ping <= 3; ping++)
            {
                yield return Wait(ReaperAttack.SkullChargeTime / 3f);
                HeroVfx.Pulse(fx, hand, 0.35f + 0.15f * ping / 3f, Soul, 0.25f);
                HeroVfx.Motes(fx, HeroAt, 0.5f, Soul, 2, 0.5f);
            }
            for (int i = -1; i <= 1; i++)
                Skull(hand, Quaternion.Euler(0, 0, i * ReaperAttack.SkullSpread) * Vector2.right, dummies[i + 1], ReaperAttack.SkullFear);
            HeroVfx.Sparks(fx, hand, Soul, 15, 3.5f, 0.3f, Vector2.right, 70f);
            yield return Wait(2.4f);
        }

        /// <summary>R: three souls raise a skeleton that fights beside him; its blows strike fear.</summary>
        private IEnumerator ArmyOfTheDeadDemo()
        {
            Set(5f, new Vector2(-2.6f, 0f), new Vector2(2.2f, 0.7f), new Vector2(2.6f, -0.9f));
            yield return Wait(0.6f);
            var skeletons = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                Vector2 spot = HeroAt + new Vector2(1f, i == 0 ? 0.8f : -0.8f);
                skeletons[i] = Sprite("Skeleton", SkeletonMinion.Sprite, spot, Vector2.one * SkeletonMinion.Size, Color.white, 4);
                HeroVfx.Pulse(fx, spot, 0.9f, Soul, 0.4f);
                HeroVfx.Sparks(fx, spot, Bone, 12, 3.5f, 0.4f, Vector2.up, 120f);
                yield return Wait(0.5f);
            }
            // Each walks to its own enemy and cuts at it until it turns and cowers.
            float[] nextCut = { 0f, 0.4f };
            for (float t = 0f; t < 3.2f; t += Time.deltaTime)
            {
                for (int i = 0; i < 2; i++)
                {
                    var prey = dummies[i];
                    Vector2 at = skeletons[i].transform.position, toward = prey.Position - at;
                    if (toward.magnitude > SkeletonMinion.Reach + DummyRadius) skeletons[i].transform.position = at + toward.normalized * 2.8f * Time.deltaTime;
                    else if (t >= nextCut[i])
                    {
                        nextCut[i] = t + SkeletonMinion.AttackInterval;
                        HeroVfx.Slash(fx, at, toward.normalized, SkeletonMinion.Reach + 0.3f, 90f, Bone, 0.15f);
                        prey.Hit(at, 0.15f);
                        prey.Fear(SkeletonMinion.HitFear);
                    }
                }
                if (Random.value < 0.08f) HeroVfx.Motes(fx, HeroAt, 0.7f, Random.value < 0.5f ? Soul : Bone, 3, 0.6f);
                yield return null;
            }
            yield return Wait(0.6f);
        }

        /// <summary>His body turns to shade and he walks straight through the pack; their touch cannot hurt him.</summary>
        private IEnumerator ShadeWalk()
        {
            Set(5f, new Vector2(-3.4f, 0f), new Vector2(-0.6f, 0.3f), new Vector2(0.4f, -0.3f), new Vector2(1.4f, 0.4f));
            yield return Wait(0.5f);
            Cast();
            HeroVfx.Motes(fx, HeroAt, 0.8f, Shade, 16, 0.9f);
            Veil(ReaperAttack.ShadeWalkTime);
            TintHero(Shade, ReaperAttack.ShadeWalkTime);
            Face(Vector2.right);
            float nextMote = 0f;
            for (float t = 0f; t < ReaperAttack.ShadeWalkTime; t += Time.deltaTime)
            {
                HeroAt += Vector2.right * 2.2f * Time.deltaTime;
                if (t >= nextMote) { nextMote = t + 0.15f; HeroVfx.Motes(fx, HeroAt, 0.5f, Shade, 2, 0.5f); }
                yield return null;
            }
            yield return Wait(0.8f);
        }

        /// <summary>Every enemy is struck with fear at once: frozen, its back turned to him.</summary>
        private IEnumerator FearIncarnate()
        {
            Set(6.5f, new Vector2(0f, 0f), new Vector2(3f, 1f), new Vector2(-3.2f, 0.6f), new Vector2(2.2f, -2.2f), new Vector2(-2.4f, -2f), new Vector2(0.4f, 2.8f));
            yield return Approach(0.9f, 1.4f);
            Cast();
            foreach (var dummy in dummies)
            {
                dummy.Fear(ReaperAttack.FearIncarnateTime);
                HeroVfx.Sparks(fx, dummy.Position, Soul, 8, 3f, 0.35f, dummy.Position - HeroAt, 90f);
            }
            HeroVfx.Pulse(fx, HeroAt, 9f, Shade, 0.6f);
            yield return Wait(ReaperAttack.FearIncarnateTime);
            yield return Approach(0.9f, 1.4f);
        }

        /// <summary>He eats the souls he carries and his wounds close.</summary>
        private IEnumerator Feast()
        {
            Set(4.5f, new Vector2(0f, -0.3f));
            TintHero(new Color(0.8f, 0.3f, 0.3f), 1.2f);
            yield return Wait(0.9f);
            Cast();
            FeastVfx.Play(fx, hero.transform, 3, 2);
            yield return Wait(2.8f);
        }

        /// <summary>The seed of fear on an enemy: a ring of soul-light turning under it.</summary>
        private void SowMark(Dummy dummy, float seconds)
        {
            dummy.Fear(seconds);
            CombatVfx.Ring(fx, dummy.Position, 0.8f, Soul, 0.5f);
            Draw(seconds, 6, (mesh, age) =>
            {
                if (dummy.Dead || dummy.Body == null) return;
                float fade = Mathf.Clamp01((seconds - age) / 0.3f);
                Vector2 at = dummy.Position;
                mesh.Ring(at, DummyRadius + 0.22f, 0.04f, FlameMesh.Alpha(Soul, 0.7f * fade), 24);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = at + FlameMesh.Polar(age * 2.5f + i * Mathf.PI * 0.5f, DummyRadius + 0.22f);
                    mesh.Diamond(p, 0.07f, FlameMesh.Alpha(Color.white, fade));
                }
            });
        }

        /// <summary>He sows fear in one enemy and cuts it down while it cowers: the fear leaps to everything near it.</summary>
        private IEnumerator Sow()
        {
            Set(5f, new Vector2(-2f, 0f), new Vector2(0.4f, 0f), new Vector2(2f, 1.2f), new Vector2(2.2f, -1f), new Vector2(0.8f, -2.2f));
            yield return Wait(0.6f);
            Cast();
            var target = dummies[0];
            Face(target.Position - HeroAt);
            CombatVfx.Bolt(fx, HeroAt, target.Position, Shade);
            SowMark(target, ReaperAttack.SowTime);
            yield return Wait(0.8f);
            Scythe(Vector2.right, ReaperAttack.TapCone, ReaperAttack.Reach, 0.28f, Bone, 0f);
            Vector2 at = target.Position;
            Kill(target);
            // It died afraid: the seed travels on.
            foreach (var dummy in Within(at, ReaperAttack.SowSpreadRadius))
            {
                CombatVfx.Bolt(fx, at, dummy.Position, Soul);
                SowMark(dummy, ReaperAttack.SowTime);
            }
            HeroVfx.Pulse(fx, at, ReaperAttack.SowSpreadRadius, Shade, 0.4f);
            yield return Wait(ReaperAttack.SowTime + 0.4f);
        }

        /// <summary>The pack is frozen with fear; he reaps it out of them, and every second of it comes home to him as a soul.</summary>
        private IEnumerator Reap()
        {
            Set(5.5f, new Vector2(-1f, 0f), new Vector2(1.8f, 0.9f), new Vector2(2.4f, -0.8f), new Vector2(-0.4f, -2.2f));
            yield return Wait(0.5f);
            foreach (var dummy in dummies)
            {
                dummy.Fear(4f);
                HeroVfx.Sparks(fx, dummy.Position, Soul, 8, 3f, 0.35f, dummy.Position - HeroAt, 90f);
            }
            HeroVfx.Pulse(fx, HeroAt, 6f, Shade, 0.5f);
            yield return Wait(1.1f);
            Cast();
            var reap = ReapVfx.Begin(fx, hero.transform);
            foreach (var dummy in dummies)
            {
                if (reap != null) reap.Add(dummy.Position, DummyRadius, 2);
                dummy.Release();
                dummy.Hit(HeroAt, 0.3f);
            }
            yield return Wait(2.4f);
        }

        /// <summary>For a while his quick cuts become three scythe arts: a wide cut, a back-cut and a full spin.</summary>
        private IEnumerator ReapersTechnique()
        {
            Set(5.5f, new Vector2(0f, 0f), new Vector2(1.9f, 0.5f), new Vector2(-2.1f, 0.3f), new Vector2(0.8f, -2f), new Vector2(-1f, 2.1f));
            yield return Wait(0.5f);
            Cast();
            HeroVfx.Pulse(fx, HeroAt, 1.4f, Soul, 0.45f);
            yield return Wait(0.6f);
            for (int round = 0; round < 2; round++)
            {
                Scythe(Vector2.right, 170f, ReaperAttack.Reach, 0.22f, Soul, 0.3f);
                yield return Wait(0.45f);
                Scythe(Vector2.right, 170f, ReaperAttack.Reach + 0.3f, 0.22f, Bone, 0.4f, true);
                yield return Wait(0.45f);
                Scythe(Vector2.right, 360f, ReaperAttack.Reach + 0.4f, 0.36f, Soul, 0.8f);
                yield return Wait(0.7f);
                // They close in again for the next string.
                yield return Approach(0.5f, 2.4f);
            }
            yield return Wait(0.8f);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Wizard's demos: her fireballs and lightning, Wild Storm and her six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static Color Shock => CombatDamage.ShockColor;

        private IEnumerator Wizard() => kind switch
        {
            DemoKind.Charged => WizardCharged(),
            DemoKind.Heavy => WizardLightning(),
            DemoKind.Mechanic => WildStormDemo(),
            _ => ability.Type switch
            {
                AbilityType.Fireball => InfernoOrb(),
                AbilityType.FrostNova => FrostNova(),
                AbilityType.Blink => ArcaneBlink(),
                AbilityType.IceWall => IceWallDemo(),
                AbilityType.BallLightning => BallLightningDemo(),
                AbilityType.LightningStorm => LightningStormDemo(),
                _ => null
            }
        };

        /// <summary>
        /// A ball of fire wreathed in flame. A plain one bursts on the first enemy it meets; with a <paramref name="blast"/>
        /// radius it is the Inferno Orb and burns everything around where it lands.
        /// </summary>
        private DemoShot Fireball(Vector2 from, Vector2 direction, float range, float scale = 1.2f, float blast = 0f, Color? tint = null)
        {
            var orb = DungeonVisuals.CreateEmberBolt(fx, from);
            if (tint.HasValue) orb.color = tint.Value;
            orb.transform.localScale = Vector3.one * scale;
            Color glow = tint ?? new Color(1f, 0.55f, 0.18f);
            CombatVfx.Trail(orb.gameObject, glow, blast > 0f ? 0.45f : 0.18f, blast > 0f ? 0.28f : 0.16f);
            if (!tint.HasValue) InfernoVfx.Wreathe(fx, orb.transform, direction, blast > 0f ? 0.22f : 0.13f * scale / 1.2f);
            HeroVfx.Sparks(fx, from + direction.normalized * 0.4f, tint ?? FlameMesh.Orange, blast > 0f ? 10 : 5, 2.5f, 0.2f, direction, 50f, 0.8f);
            Transform stage = fx;
            return Shoot(orb, direction, 10f, range, 0.1f, false, (dummy, at) =>
            {
                if (blast > 0f) return;
                dummy.Hit(at - direction, 0.4f * scale);
                if (!tint.HasValue) dummy.Burn(2.5f); else dummy.Chill(2f);
            }, at =>
            {
                if (blast > 0f)
                {
                    InfernoVfx.Blast(stage, at, blast);
                    HeroVfx.Sparks(stage, at, FlameMesh.Yellow, 24, 6.5f, 0.5f, null, 360f, 1.4f);
                    CombatVfx.Ring(stage, at, blast, glow);
                    HeroVfx.Pulse(stage, at, blast, glow, 0.4f);
                    foreach (var dummy in Within(at, blast)) { dummy.Hit(at, 0.6f); dummy.Burn(3f); }
                }
                else if (!tint.HasValue)
                {
                    InfernoVfx.Blast(stage, at, 0.45f * scale / 1.2f);
                    HeroVfx.Sparks(stage, at, glow, 10, 3.5f, 0.3f);
                }
                else
                {
                    CombatVfx.Ring(stage, at, 0.25f, glow, 0.18f);
                    HeroVfx.Sparks(stage, at, glow, 8, 3f, 0.28f);
                }
            });
        }

        /// <summary>A quick fireball, then one she holds: it swells as it charges and hits far harder.</summary>
        private IEnumerator WizardCharged()
        {
            Set(4.5f, new Vector2(-3.2f, 0f), new Vector2(1.2f, 0f));
            yield return Wait(0.5f);
            Face(Vector2.right);
            Fireball(HeroAt, Vector2.right, PlayerProjectile.MaxRange);
            yield return Wait(1f);
            // The charge gathers in her hand: a growing ember.
            float held = 1.5f;
            Draw(held, 7, (mesh, age) =>
            {
                float charge = Mathf.Clamp01(age / (held * 0.85f));
                Vector2 hand = HeroAt + new Vector2(0.45f, 0.1f);
                mesh.Disc(hand, 0.12f + 0.22f * charge, FlameMesh.Alpha(FlameMesh.Yellow, 0.9f), FlameMesh.Alpha(FlameMesh.Orange, 0f), 20);
                mesh.Disc(hand, 0.05f + 0.1f * charge, Color.white, FlameMesh.Alpha(FlameMesh.Yellow, 0.8f), 14);
            });
            for (float t = 0f; t < held; t += Time.deltaTime)
            {
                if (Random.value < 0.3f) HeroVfx.Sparks(fx, HeroAt + new Vector2(0.45f, 0.1f), FlameMesh.Orange, 1, 1.5f, 0.25f, Vector2.up, 120f, 0.7f);
                yield return null;
            }
            Fireball(HeroAt, Vector2.right, PlayerProjectile.MaxRange, 1.9f);
            yield return Wait(1.6f);
        }

        /// <summary>Right click: a bolt of lightning snaps to the nearest enemy she is facing.</summary>
        private IEnumerator WizardLightning()
        {
            Set(4.5f, new Vector2(-2.8f, 0f), new Vector2(1.6f, 0.6f), new Vector2(-0.4f, -1.6f));
            for (int i = 0; i < 3; i++)
            {
                yield return Wait(0.8f);
                var target = dummies[i % dummies.Count];
                Face(target.Position - HeroAt);
                HeroVfx.Pulse(fx, HeroAt, 0.7f, AbilityCatalog.Ice, 0.25f);
                CombatVfx.GlowBolt(fx, HeroAt, target.Position, AbilityCatalog.Ice);
                HeroVfx.Sparks(fx, target.Position, Color.Lerp(AbilityCatalog.Ice, Color.white, 0.4f), 7, 4f, 0.25f);
                target.Hit(HeroAt, 0.3f);
            }
            yield return Wait(1.2f);
        }

        /// <summary>R: a storm cloud gathers over her and takes turns hurling fire, lightning and ice at whoever is near.</summary>
        private IEnumerator WildStormDemo()
        {
            Set(5.5f, new Vector2(0f, -0.6f), new Vector2(3.2f, 0.4f), new Vector2(-3.4f, -0.2f), new Vector2(2.2f, -2f), new Vector2(-2.4f, 1.6f));
            yield return Wait(0.5f);
            Vector2 anchor = HeroAt + Vector2.up * WildStorm.Height;
            var cloud = Sprite("Wild storm", WildStorm.CloudSprite, anchor, Vector2.one, Color.white, 8);
            HeroVfx.Pulse(fx, anchor, 1.6f, WildStorm.CloudColor, 0.4f);
            float nextStrike = 0.3f;
            int turn = 0;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                cloud.transform.position = anchor + new Vector2(Mathf.Sin(Time.time * 2f) * 0.08f, Mathf.Sin(Time.time * 3.1f) * 0.05f);
                // A flicker of inner lightning keeps the cloud alive between strikes.
                cloud.color = Color.Lerp(Color.white, new Color(0.8f, 0.9f, 1.4f), Random.value < 0.06f ? 1f : 0f);
                Advance(HeroAt, 0.5f);
                if (t >= nextStrike)
                {
                    nextStrike += WildStorm.Interval;
                    var target = dummies[turn % dummies.Count];
                    Vector2 aim = (target.Position - anchor).normalized;
                    switch (turn++ % 3)
                    {
                        case 0: Fireball(anchor, aim, WildStorm.Range); break;
                        case 1:
                            CombatVfx.GlowBolt(fx, anchor, target.Position, AbilityCatalog.Ice);
                            HeroVfx.Sparks(fx, target.Position, Color.Lerp(AbilityCatalog.Ice, Color.white, 0.4f), 7, 4f, 0.25f);
                            target.Stun(0.8f);
                            break;
                        default: Fireball(anchor, aim, WildStorm.Range, 1f, 0f, AbilityCatalog.Ice); break;
                    }
                }
                yield return null;
            }
            HeroVfx.Motes(fx, cloud.transform.position, 0.8f, WildStorm.CloudColor, 12, 0.6f);
            Destroy(cloud.gameObject);
            yield return Wait(1f);
        }

        /// <summary>A roaring ball of fire flies into the pack and bursts, setting everything around it alight.</summary>
        private IEnumerator InfernoOrb()
        {
            Set(5.5f, new Vector2(-4.2f, 0f), new Vector2(1.4f, 0f), new Vector2(2.4f, 1f), new Vector2(2.3f, -1.1f), new Vector2(0.6f, 1.2f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            Fireball(HeroAt, Vector2.right, 7f, 1.7f, 1.7f);
            yield return Wait(2.8f);
        }

        /// <summary>A ring of ice bursts out from her: everything it reaches is struck and frozen solid.</summary>
        private IEnumerator FrostNova()
        {
            Set(7.5f, new Vector2(0f, 0f), new Vector2(1.6f, 0.7f), new Vector2(-2.6f, 1.2f), new Vector2(3.6f, -1.4f), new Vector2(-3.4f, -2f), new Vector2(0.6f, -2.8f));
            yield return Approach(0.7f, 1.2f);
            Cast();
            Vector2 center = HeroAt;
            Color color = AbilityCatalog.Ice;
            var frozen = new HashSet<Dummy>();
            float nextRing = 0f;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(t / PlayerAbilities.FrostNovaExpandTime), r = Mathf.Lerp(0.3f, PlayerAbilities.FrostNovaRadius, progress);
                if (t >= nextRing || progress >= 1f)
                {
                    nextRing = t + 0.08f;
                    CombatVfx.Ring(fx, center, r, color, 0.25f);
                    Vector2 edge = center + FlameMesh.Polar(Random.value * Mathf.PI * 2f, r);
                    HeroVfx.Sparks(fx, edge, Color.Lerp(color, Color.white, 0.4f), 3, 2f, 0.25f, edge - center, 70f, 0.8f);
                }
                foreach (var dummy in Within(center, r))
                    if (frozen.Add(dummy)) { dummy.Hit(center, 0.2f); dummy.Freeze(PlayerAbilities.FrostNovaFreeze); }
                if (progress >= 1f) break;
                yield return null;
            }
            HeroVfx.Pulse(fx, center, PlayerAbilities.FrostNovaRadius, color, 0.3f);
            yield return Wait(PlayerAbilities.FrostNovaFreeze + 0.3f);
        }

        /// <summary>The pack has her against a wall; she is suddenly on the other side of it.</summary>
        private IEnumerator ArcaneBlink()
        {
            Set(5.5f, new Vector2(-1.4f, 0f), new Vector2(-3.8f, 0.6f), new Vector2(-4f, -0.7f));
            // A wall across her way.
            Sprite("Demo wall", null, At(0f, 0f), new Vector2(0.9f, 5f), new Color(0.2f, 0.24f, 0.32f), 1);
            Sprite("Demo wall top", null, At(0f, 0f), new Vector2(0.7f, 4.8f), new Color(0.27f, 0.32f, 0.42f), 1);
            yield return Approach(1.1f, 1.6f);
            Cast();
            Vector2 from = HeroAt, landing = from + Vector2.right * 4.4f;
            Color color = AbilityCatalog.Violet;
            HeroAt = landing;
            Face(Vector2.right);
            CombatVfx.GlowBolt(fx, from, landing, color);
            HeroVfx.Sparks(fx, from, color, 8, 2.6f, 0.3f);
            HeroVfx.Sparks(fx, landing, color, 8, 3f, 0.3f, landing - from, 120f);
            // They can only mill about where she was.
            for (float t = 0f; t < 1.8f; t += Time.deltaTime) { Advance(from, 1.6f); yield return null; }
        }

        /// <summary>A row of ice blocks standing across the aim, each a little prism seen from above and in front.</summary>
        private static void DrawIceWall(FlameMesh mesh, Vector2 center, float rise, float alpha, HashSet<int> broken)
        {
            Color deep = new Color(0.4f, 0.66f, 0.95f, 0.85f), body = new Color(0.68f, 0.9f, 1f, 0.9f), top = new Color(0.9f, 0.97f, 1f, 0.95f);
            int count = Mathf.RoundToInt(IceWall.HalfLength * 2f / IceWall.SegmentLength);
            // Farther blocks (higher on screen) first, so nearer ones cover them.
            for (int i = count - 1; i >= 0; i--)
            {
                if (broken.Contains(i)) continue;
                Vector2 at = center + Vector2.up * (-IceWall.HalfLength + IceWall.SegmentLength * (i + 0.5f));
                float half = IceWall.SegmentLength * 0.47f, height = IceWall.BlockHeight * rise * (0.4f + 0.6f * alpha);
                Vector2 a = at + new Vector2(-IceWall.Thickness, -half), b = at + new Vector2(IceWall.Thickness, -half), up = Vector2.up * height;
                mesh.Ellipse(at + Vector2.down * 0.05f, IceWall.Thickness * 1.3f, 0.2f, new Color(0f, 0f, 0f, 0.25f * alpha), new Color(0f, 0f, 0f, 0f), 12);
                // The front face, then the top.
                mesh.Quad(a, a + up, b + up, b, FlameMesh.Alpha(deep, alpha), FlameMesh.Alpha(body, alpha), FlameMesh.Alpha(body, alpha), FlameMesh.Alpha(deep, alpha));
                Vector2 back = Vector2.up * half * 2f;
                mesh.Quad(a + up, a + up + back, b + up + back, b + up, FlameMesh.Alpha(top, alpha), FlameMesh.Alpha(top, alpha), FlameMesh.Alpha(top, alpha), FlameMesh.Alpha(top, alpha));
                mesh.Bar(a + up, Vector2.right, IceWall.Thickness * 2f, 0.02f, FlameMesh.Alpha(Color.white, 0.8f * alpha), FlameMesh.Alpha(Color.white, 0.8f * alpha));
                mesh.Bar(Vector2.Lerp(a, b, 0.25f) + up * 0.15f, Vector2.up, height * 0.7f, 0.035f, FlameMesh.Alpha(Color.white, 0.6f * alpha), FlameMesh.Alpha(Color.white, 0.6f * alpha));
            }
        }

        /// <summary>A wall of ice rises across the pack's path; they batter at it, and a block's shards freeze whoever broke it.</summary>
        private IEnumerator IceWallDemo()
        {
            Set(5.5f, new Vector2(-3.4f, 0f), new Vector2(3.2f, 0.7f), new Vector2(3.6f, -0.8f));
            yield return Approach(0.7f, 1.6f);
            Cast();
            Face(Vector2.right);
            Vector2 center = HeroAt + Vector2.right * IceWall.Distance;
            var broken = new HashSet<int>();
            const float Life = 4.2f, Melt = 0.5f;
            Draw(Life + Melt, 5, (mesh, age) => DrawIceWall(mesh, center, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.25f)), age >= Life ? 1f - (age - Life) / Melt : 1f, broken));
            HeroVfx.Sparks(fx, center, new Color(0.8f, 0.95f, 1f), 16, 4f, 0.4f, null, 360f, 1.2f);
            bool shattered = false;
            for (float t = 0f; t < Life; t += Time.deltaTime)
            {
                // They walk up to the wall and no farther.
                foreach (var dummy in dummies)
                    if (!dummy.IsHeld && dummy.Position.x > center.x + IceWall.Thickness + DummyRadius)
                        dummy.Position += Vector2.left * 1.6f * Time.deltaTime;
                if (!shattered && t > 2.4f)
                {
                    shattered = true;
                    // One block gives way under their blows, and its shards freeze the one beside it.
                    int block = Mathf.Clamp(Mathf.FloorToInt((dummies[0].Position.y - center.y + IceWall.HalfLength) / IceWall.SegmentLength), 0, 7);
                    broken.Add(block);
                    Vector2 at = center + Vector2.up * (-IceWall.HalfLength + IceWall.SegmentLength * (block + 0.5f));
                    HeroVfx.Sparks(fx, at, new Color(0.8f, 0.95f, 1f), 16, 5f, 0.45f, null, 360f, 1.3f);
                    HeroVfx.Sparks(fx, at, Color.white, 6, 3f, 0.3f, Vector2.up, 140f, 0.8f);
                    HeroVfx.Pulse(fx, at, IceWall.ShatterReach, FlameMesh.Alpha(new Color(0.8f, 0.95f, 1f), 0.5f), 0.3f);
                    foreach (var dummy in Within(at, IceWall.ShatterReach)) dummy.Freeze(IceWall.ShatterFreeze);
                }
                yield return null;
            }
            yield return Wait(Melt + 0.4f);
        }

        /// <summary>A slow orb of lightning drifts through the pack, zapping everything it passes.</summary>
        private IEnumerator BallLightningDemo()
        {
            Set(6f, new Vector2(-4.8f, 0f), new Vector2(-1.4f, 0.9f), new Vector2(0.4f, -0.8f), new Vector2(2.2f, 0.7f), new Vector2(3.6f, -0.6f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            Vector2 at = HeroAt;
            HeroVfx.Sparks(fx, at, Shock, 8, 3f, 0.25f);
            const float Life = 3.2f;
            var orb = Draw(Life, 7, (mesh, age) =>
            {
                float time = Time.time, fadeIn = Mathf.Clamp01(age / 0.15f);
                float radius = BallLightning.OrbRadius * fadeIn * (1f + 0.06f * Mathf.Sin(time * 31f) + 0.04f * Mathf.Sin(time * 53f));
                // A faint wake, a flickering halo, then the sphere: white-hot heart out to electric blue with a bright rim.
                mesh.Quad(at - Vector2.up * radius * 0.8f, at + Vector2.up * radius * 0.8f, at - Vector2.right * radius * 3f, at - Vector2.right * radius * 3f,
                    FlameMesh.Alpha(Shock, 0.25f * fadeIn), FlameMesh.Alpha(Shock, 0.25f * fadeIn), FlameMesh.Alpha(Shock, 0f), FlameMesh.Alpha(Shock, 0f));
                mesh.Disc(at, radius * (2.6f + 0.3f * Mathf.Sin(time * 23f)), FlameMesh.Alpha(Shock, 0.28f * fadeIn), FlameMesh.Alpha(Shock, 0f), 32);
                mesh.Disc(at, radius, FlameMesh.Alpha(Color.white, fadeIn), FlameMesh.Alpha(Color.Lerp(Shock, new Color(0.2f, 0.35f, 0.9f), 0.35f), fadeIn), 32);
                mesh.Ring(at, radius, 0.04f, FlameMesh.Alpha(Color.Lerp(Shock, Color.white, 0.5f), fadeIn), 32);
                mesh.Ellipse(at + new Vector2(-0.35f, 0.4f) * radius, radius * 0.3f, radius * 0.2f, FlameMesh.Alpha(Color.white, 0.8f * fadeIn), FlameMesh.Alpha(Color.white, 0f), 12);
                // Forks crawl over it, a new set every few frames.
                int seed = Mathf.FloorToInt(time / 0.06f);
                for (int arc = 0; arc < 4; arc++)
                {
                    float start = FlameMesh.Hash(arc, seed) * Mathf.PI * 2f;
                    Vector2 last = at + FlameMesh.Polar(start, radius * 0.4f);
                    for (int p = 1; p < 5; p++)
                    {
                        float reach = arc == 3 ? radius * (0.8f + p * 0.4f) : radius * (0.35f + 0.6f * FlameMesh.Hash(arc * 7 + p, seed));
                        Vector2 next = at + FlameMesh.Polar(start + (arc == 3 ? 0.1f : 0.6f) * p * (FlameMesh.Hash(p, seed + arc) - 0.3f), reach);
                        if ((next - last).sqrMagnitude > 0.000001f)
                        {
                            mesh.Bar(last, (next - last).normalized, Vector2.Distance(last, next), 0.05f, FlameMesh.Alpha(Shock, 0.6f * fadeIn), FlameMesh.Alpha(Shock, 0.6f * fadeIn));
                            mesh.Bar(last, (next - last).normalized, Vector2.Distance(last, next), 0.02f, FlameMesh.Alpha(Color.white, fadeIn), FlameMesh.Alpha(Color.white, fadeIn));
                        }
                        last = next;
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    float a = time * (5f + i * 1.3f) + i * 2.1f;
                    mesh.Diamond(at + new Vector2(Mathf.Cos(a) * radius * 1.7f, Mathf.Sin(a) * radius * (0.6f + 0.4f * i)), 0.045f, FlameMesh.Alpha(Color.white, 0.9f * fadeIn));
                }
            });
            float nextZap = 0f;
            for (float t = 0f; t < Life; t += Time.deltaTime)
            {
                at += Vector2.right * BallLightning.Speed * Time.deltaTime;
                if (t >= nextZap)
                {
                    nextZap = t + BallLightning.ZapInterval;
                    foreach (var dummy in Within(at, BallLightning.Reach))
                    {
                        CombatVfx.Bolt(fx, at, dummy.Position, Shock);
                        dummy.Hit(at, 0.1f);
                    }
                }
                yield return null;
            }
            HeroVfx.Pulse(fx, at, 0.8f, Shock, 0.2f);
            HeroVfx.Sparks(fx, at, Color.white, 10, 4f, 0.25f);
            yield return Wait(1f);
        }

        /// <summary>For three seconds bolts strike down from above onto the enemies around her.</summary>
        private IEnumerator LightningStormDemo()
        {
            Set(6f, new Vector2(0f, -0.4f), new Vector2(3f, 0.6f), new Vector2(-3.2f, 0.2f), new Vector2(2f, -2f), new Vector2(-2f, 1.9f), new Vector2(-1.4f, -2.2f));
            yield return Wait(0.5f);
            Cast();
            int turn = 0;
            for (float t = 0f; t < LightningStorm.Duration; t += LightningStorm.Interval)
            {
                StartCoroutine(SkyBolt(dummies[turn++ % dummies.Count].Position));
                for (float wait = 0f; wait < LightningStorm.Interval; wait += Time.deltaTime) { Advance(HeroAt, 0.6f); yield return null; }
            }
            yield return Wait(1.2f);
        }

        /// <summary>A ring warns of the strike, then the bolt comes down on it.</summary>
        private IEnumerator SkyBolt(Vector2 spot)
        {
            Transform stage = fx;
            CombatVfx.Ring(stage, spot, LightningStorm.StrikeRadius, FlameMesh.Alpha(Shock, 0.6f), LightningStorm.Warning);
            yield return Wait(LightningStorm.Warning);
            if (stage == null) yield break;
            CombatVfx.GlowBolt(stage, spot + Vector2.up * 7f, spot, Shock);
            HeroVfx.Sparks(stage, spot, Shock, 12, 4.5f, 0.3f, null, 360f, 1.2f);
            foreach (var dummy in Within(spot, LightningStorm.StrikeRadius)) dummy.Hit(spot, 0.3f);
        }
    }
}

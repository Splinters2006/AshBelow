using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Assassin's demos: her dagger and Shadowstep, the Sharpened Dagger and her six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static readonly Color DaggerColor = new Color(0.8f, 0.5f, 1f);

        private IEnumerator Assassin() => kind switch
        {
            DemoKind.Charged => AssassinCharged(),
            DemoKind.Heavy => ShadowstepDemo(),
            DemoKind.Mechanic => SharpenedDaggerDemo(),
            _ => ability.Type switch
            {
                AbilityType.FanOfKnives => FanOfKnives(),
                AbilityType.VenomVial => VenomVialDemo(),
                AbilityType.ShadowVeil => ShadowVeil(),
                AbilityType.SmokeBomb => SmokeBomb(),
                AbilityType.DeathMark => DeathMark(),
                AbilityType.ShadowClone => ShadowCloneDemo(),
                _ => null
            }
        };

        /// <summary>A straight thrust of the dagger down a narrow wedge.</summary>
        private void Stab(Vector2 aim, float cone = SwordAttack.StabAngle, float knockback = 0.3f)
        {
            Face(aim);
            StabVfx.Play(fx, HeroAt, aim, SwordAttack.StabReach, DaggerColor);
            foreach (var dummy in InCone(HeroAt, aim, SwordAttack.StabReach, cone)) dummy.Hit(HeroAt, knockback);
        }

        /// <summary>She blinks through the shadows to <paramref name="landing"/>, cutting whatever stood in her way.</summary>
        private void Shadowstep(Vector2 landing)
        {
            Vector2 from = HeroAt, travel = landing - from;
            HeroAt = landing;
            ShadowstepVfx.Play(fx, from, landing);
            foreach (var dummy in InLane(from, travel, travel.magnitude, 0.14f))
            {
                HeroVfx.Slash(fx, dummy.Position - travel.normalized * 0.4f, travel, 0.7f, 70f, ShadowstepVfx.Violet, 0.18f);
                dummy.Hit(from, 0.2f);
            }
        }

        /// <summary>A quick thrust, then a held one: the wedge narrows to a needle as the charge fills, and it hits far harder.</summary>
        private IEnumerator AssassinCharged()
        {
            Set(4.5f, new Vector2(-2.6f, 0f), new Vector2(0.4f, 0f), new Vector2(1f, 0.1f));
            yield return Wait(0.5f);
            Stab(Vector2.right);
            yield return Wait(0.9f);
            yield return Charge(1.2f / 1.5f, (mesh, charge) => Cone(mesh, HeroAt, Vector2.right, SwordAttack.StabReach, Mathf.Lerp(SwordAttack.StabAngle, 22f, charge),
                Color.Lerp(new Color(0.6f, 0.3f, 1f, 0.12f), new Color(0.9f, 0.65f, 1f, 0.5f), charge)));
            Stab(Vector2.right, 22f, 0.9f);
            foreach (var dummy in InCone(HeroAt, Vector2.right, SwordAttack.StabReach, 22f)) HeroVfx.Sparks(fx, dummy.Position, DaggerColor, 10, 4f, 0.3f, Vector2.right, 90f);
            yield return Wait(1.6f);
        }

        /// <summary>Right click: she steps through the shadows, through the enemy in her way, and stabs it in the back.</summary>
        private IEnumerator ShadowstepDemo()
        {
            Set(4.5f, new Vector2(-2.4f, 0f), new Vector2(0f, 0f));
            for (int i = 0; i < 2; i++)
            {
                yield return Wait(0.7f);
                Vector2 through = i == 0 ? Vector2.right : Vector2.left;
                Face(through);
                Shadowstep(HeroAt + through * 4.2f);
                yield return Wait(0.35f);
                // Behind it now: a backstab.
                Stab(-through, SwordAttack.StabAngle, 0.5f);
                RearHitMarker.Draw(fx, dummies[0].Position, through, DummyRadius);
                dummies[0].Position = At(0f, 0f);
                dummies[0].Velocity = Vector2.zero;
                yield return Wait(0.5f);
            }
            yield return Wait(1f);
        }

        /// <summary>R: a whetstone scrapes her edge keen for a while; every backstab keeps it sharp and makes it sharper.</summary>
        private IEnumerator SharpenedDaggerDemo()
        {
            Set(4.5f, new Vector2(-2.4f, 0f), new Vector2(0f, 0f));
            yield return Wait(0.5f);
            int bonus = 1;
            SharpenVfx.Play(fx, hero.transform, true);
            KeenEdgeAura.Show(fx, hero.transform, bonus, SharpenedDagger.Duration);
            HeroVfx.Sparks(fx, HeroAt, SharpenedDagger.EdgeColor, 10, 3.5f, 0.3f);
            yield return Wait(1f);
            for (int i = 0; i < 2; i++)
            {
                Vector2 through = i == 0 ? Vector2.right : Vector2.left;
                Shadowstep(HeroAt + through * 4.2f);
                yield return Wait(0.3f);
                Stab(-through, SwordAttack.StabAngle, 0f);
                RearHitMarker.Draw(fx, dummies[0].Position, through, DummyRadius);
                // The backstab hones it further.
                SharpenVfx.Play(fx, hero.transform, false);
                KeenEdgeAura.Show(fx, hero.transform, ++bonus, SharpenedDagger.Duration);
                HeroVfx.Sparks(fx, HeroAt, SharpenedDagger.EdgeColor, 6, 3.5f, 0.3f);
                yield return Wait(1.1f);
            }
            yield return Wait(0.8f);
        }

        /// <summary>A ring of knives flies out through the pack, hangs spinning, then streaks back to her hand through them again.</summary>
        private IEnumerator FanOfKnives()
        {
            Set(6.5f, new Vector2(0f, 0f), new Vector2(2.4f, 0.4f), new Vector2(-2.6f, 1f), new Vector2(1.2f, -2.2f), new Vector2(-1.8f, -2f), new Vector2(0.4f, 2.6f));
            yield return Wait(0.6f);
            Cast();
            const int Knives = 12;
            var knives = new List<SpriteRenderer>();
            var struck = new HashSet<(int, Dummy)>();
            for (int i = 0; i < Knives; i++)
            {
                var knife = Sprite("Returning knife", null, HeroAt, new Vector2(0.42f, 0.09f), ReturningKnife.Blade, 6);
                CombatVfx.Trail(knife.gameObject, new Color(ReturningKnife.Blade.r, ReturningKnife.Blade.g, ReturningKnife.Blade.b, 0.8f), 0.07f, 0.12f);
                knife.transform.rotation = Quaternion.Euler(0f, 0f, i * 360f / Knives);
                knives.Add(knife);
            }
            // Out: each along its own spoke until its range runs out, then it hangs there spinning.
            float flown = 0f;
            for (float t = 0f; t < ReturningKnife.ReturnDelay; t += Time.deltaTime)
            {
                float step = Mathf.Min(ReturningKnife.OutwardSpeed * Time.deltaTime, PlayerProjectile.MaxRange - flown);
                flown += step;
                for (int i = 0; i < Knives; i++)
                {
                    if (step > 0f) knives[i].transform.position += (Vector3)(FlameMesh.Polar(i * Mathf.PI * 2f / Knives, 1f) * step);
                    else knives[i].transform.Rotate(0f, 0f, 900f * Time.deltaTime);
                    foreach (var dummy in Within(knives[i].transform.position, 0f))
                        if (struck.Add((i, dummy))) dummy.Hit(HeroAt, 0.15f);
                }
                yield return null;
            }
            foreach (var knife in knives) HeroVfx.Sparks(fx, knife.transform.position, ReturningKnife.Blade, 4, 2f, 0.2f);
            struck.Clear();
            // Home: straight back to her hand, cutting everything on the way.
            bool flying = true;
            while (flying)
            {
                flying = false;
                for (int i = 0; i < Knives; i++)
                {
                    if (knives[i] == null) continue;
                    Vector2 offset = HeroAt - (Vector2)knives[i].transform.position;
                    if (offset.magnitude <= 0.35f)
                    {
                        HeroVfx.Sparks(fx, HeroAt, ReturningKnife.Blade, 3, 1.6f, 0.15f);
                        Destroy(knives[i].gameObject);
                        knives[i] = null;
                        continue;
                    }
                    flying = true;
                    knives[i].transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
                    knives[i].transform.position = Vector2.MoveTowards(knives[i].transform.position, HeroAt, ReturningKnife.ReturnSpeed * Time.deltaTime);
                    foreach (var dummy in Within(knives[i].transform.position, 0.1f))
                        if (struck.Add((i, dummy))) dummy.Hit(dummy.Position - offset, 0.15f);
                }
                yield return null;
            }
            yield return Wait(1.4f);
        }

        /// <summary>A vial arcs into the pack and shatters into a pool: whoever stands in it is poisoned.</summary>
        private IEnumerator VenomVialDemo()
        {
            Set(5.5f, new Vector2(-4f, 0f), new Vector2(0.8f, 0.4f), new Vector2(1.6f, -0.5f), new Vector2(0.4f, -1f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            Vector2 from = HeroAt, landing = At(1f, -0.2f);
            Color venom = VenomVial.Venom, murk = new Color(0.12f, 0.3f, 0.08f);
            const float Pool = 3f, Fade = 0.4f;
            float flight = VenomVial.FlightTime, radius = VenomVial.Radius;
            Draw(flight + Pool + Fade, 4, (mesh, age) =>
            {
                if (age < flight)
                {
                    // The spinning bottle: a round flask with a neck and cork, over its shadow.
                    float t = age / flight;
                    Vector2 ground = Vector2.Lerp(from, landing, t), p = ground + Vector2.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                    mesh.Ellipse(ground + Vector2.down * 0.2f, 0.18f, 0.07f, FlameMesh.Alpha(Color.black, 0.35f), FlameMesh.Alpha(Color.black, 0f), 14);
                    Vector2 up = FlameMesh.Polar(age * 16f, 1f), side = Vector2.Perpendicular(up);
                    Color glass = new Color(0.8f, 0.95f, 0.9f);
                    mesh.Disc(p, 0.17f, venom, murk, 16);
                    mesh.Quad(p + up * 0.12f - side * 0.05f, p + up * 0.12f + side * 0.05f, p + up * 0.3f + side * 0.05f, p + up * 0.3f - side * 0.05f, glass, glass, glass, glass);
                    mesh.Diamond(p + up * 0.33f, 0.05f, new Color(0.55f, 0.36f, 0.2f));
                    return;
                }
                float poolAge = age - flight, grow = 1f - (1f - Mathf.Clamp01(poolAge / 0.2f)) * (1f - Mathf.Clamp01(poolAge / 0.2f));
                float fade = Mathf.Clamp01((flight + Pool + Fade - age) / Fade), r = radius * grow;
                mesh.Disc(landing, r, FlameMesh.Alpha(murk, 0.55f * fade), FlameMesh.Alpha(venom, 0.3f * fade), 40);
                mesh.Ring(landing, r, 0.08f, FlameMesh.Alpha(venom, 0.75f * fade), 48);
                // Bubbles swell and pop across the pool, and fumes drift up out of it.
                for (int i = 0; i < 12; i++)
                {
                    float seed = FlameMesh.Hash(i, 8.3f), cycle = Mathf.Repeat(poolAge * (0.9f + seed) + seed, 1f);
                    mesh.Ring(landing + FlameMesh.Polar(seed * 40f, r * 0.8f * Mathf.Sqrt(FlameMesh.Hash(i, 2.2f))), 0.04f + 0.1f * cycle, 0.025f, FlameMesh.Alpha(venom, fade * (1f - cycle * 0.7f)), 12);
                }
                for (int i = 0; i < 8; i++)
                {
                    float seed = FlameMesh.Hash(i, 4.9f), rise = Mathf.Repeat(poolAge * 0.6f + seed, 1f);
                    Vector2 p = landing + new Vector2((seed - 0.5f) * r * 1.4f + Mathf.Sin(poolAge * 3f + i) * 0.1f, rise * 1.1f);
                    mesh.Disc(p, 0.12f + 0.15f * rise, FlameMesh.Alpha(venom, 0.25f * fade * Mathf.Sin(rise * Mathf.PI)), FlameMesh.Alpha(venom, 0f), 12);
                }
            });
            yield return Wait(flight);
            HeroVfx.Sparks(fx, landing, venom, 16, 4.5f, 0.4f, null, 360f, 0.9f);
            HeroVfx.Sparks(fx, landing, new Color(0.85f, 1f, 0.9f), 8, 3.5f, 0.3f, Vector2.up, 160f, 0.6f);
            HeroVfx.Pulse(fx, landing, radius, venom, 0.35f);
            foreach (var dummy in Within(landing, radius)) dummy.Hit(landing, 0.1f);
            for (float t = 0f; t < Pool; t += 0.5f)
            {
                // The poison shows as a green burn on everyone in the pool.
                foreach (var dummy in Within(landing, radius))
                {
                    dummy.Flame.color = AbilityCatalog.Green;
                    dummy.Burn(0.7f);
                    HeroVfx.Sparks(fx, dummy.Position, DungeonEnemy.PoisonColor, 4, 1.4f, 0.4f, Vector2.up, 60f, 0.8f);
                }
                yield return Wait(0.5f);
            }
            yield return Wait(1f);
        }

        /// <summary>She fades into shadow: the pack loses her and walks on to where she was.</summary>
        private IEnumerator ShadowVeil()
        {
            Set(5f, new Vector2(-0.6f, 0f), new Vector2(3.4f, 0.6f), new Vector2(3.6f, -0.7f));
            yield return Approach(0.9f, 1.8f);
            Cast();
            Vector2 lost = HeroAt;
            ShadowstepVfx.Puff(fx, HeroAt);
            Veil(PlayerAbilities.VeilDuration);
            // She walks clear while they close on empty air.
            for (float t = 0f; t < PlayerAbilities.VeilDuration; t += Time.deltaTime)
            {
                HeroAt += new Vector2(-0.9f, 0.75f) * Time.deltaTime;
                Advance(lost, 1.8f);
                yield return null;
            }
            yield return Wait(0.8f);
        }

        /// <summary>A cloud of smoke: inside it they lose her, and every stab she lands on them counts as a backstab.</summary>
        private IEnumerator SmokeBomb()
        {
            Set(5.5f, new Vector2(-0.4f, 0f), new Vector2(1.4f, 0.3f), new Vector2(1.5f, -1f));
            yield return Wait(0.5f);
            Cast();
            Vector2 center = HeroAt;
            Color smoke = new Color(0.55f, 0.55f, 0.62f);
            ShadowstepVfx.Puff(fx, center);
            const float Life = 3.6f;
            Veil(Life);
            // The cloud itself: a soft grey pall that hangs over the ground.
            Draw(Life + 0.4f, 6, (mesh, age) =>
            {
                float fade = Mathf.Clamp01(age / 0.3f) * Mathf.Clamp01((Life + 0.4f - age) / 0.4f);
                mesh.Disc(center, SmokeCloud.Radius, FlameMesh.Alpha(smoke, 0.32f * fade), FlameMesh.Alpha(smoke, 0.08f * fade), 40);
                for (int i = 0; i < 9; i++)
                {
                    float seed = FlameMesh.Hash(i, 3.3f), drift = age * (0.2f + 0.3f * seed) + seed * 6f;
                    mesh.Disc(center + FlameMesh.Polar(drift, SmokeCloud.Radius * (0.25f + 0.6f * FlameMesh.Hash(i, 7.7f))), 0.7f + 0.5f * seed, FlameMesh.Alpha(smoke, 0.22f * fade), FlameMesh.Alpha(smoke, 0f), 16);
                }
            });
            float nextPuff = 0f, nextStab = 0.7f;
            for (float t = 0f; t < Life; t += Time.deltaTime)
            {
                if (t >= nextPuff)
                {
                    nextPuff = t + 0.3f;
                    HeroVfx.Motes(fx, center, SmokeCloud.Radius * 0.9f, FlameMesh.Alpha(smoke, 0.8f), 10, 0.9f);
                    CombatVfx.Ring(fx, center, SmokeCloud.Radius, FlameMesh.Alpha(smoke, 0.5f), 0.35f);
                }
                if (t >= nextStab)
                {
                    nextStab = t + 0.6f;
                    var victim = Nearest(HeroAt);
                    Vector2 aim = (victim.Position - HeroAt).normalized;
                    Stab(aim, SwordAttack.StabAngle, 0.1f);
                    RearHitMarker.Draw(fx, victim.Position, aim, DummyRadius);
                }
                yield return null;
            }
            yield return Wait(1f);
        }

        /// <summary>She marks an enemy and cuts it; when the mark runs out, everything it took lands on it again and bursts over its neighbours.</summary>
        private IEnumerator DeathMark()
        {
            Set(5f, new Vector2(-2.6f, 0f), new Vector2(0.6f, 0f), new Vector2(1.9f, 1f), new Vector2(1.8f, -1.1f));
            yield return Wait(0.5f);
            Cast();
            var marked = dummies[0];
            Color blood = new Color(0.85f, 0.2f, 0.3f);
            const float Life = 3f;
            HeroVfx.Pulse(fx, marked.Position, 0.9f, blood, 0.3f);
            // The skull hangs over it with its clock running down round it.
            var skull = Sprite("Death mark", DungeonVisuals.SkullSprite, marked.Position + Vector2.up * 1.3f, Vector2.one * 0.42f, blood, 10);
            Draw(Life, 9, (mesh, age) =>
            {
                Vector2 anchor = marked.Position + Vector2.up * (1.3f + 0.05f * Mathf.Sin(age * 5f));
                if (skull != null) skull.transform.position = anchor;
                float left = 1f - age / Life;
                const int Segments = 28;
                for (int i = 0; i < Mathf.CeilToInt(Segments * left); i++)
                {
                    float a0 = Mathf.PI * 0.5f - i * Mathf.PI * 2f / Segments, a1 = a0 - Mathf.PI * 2f / Segments;
                    mesh.Quad(anchor + FlameMesh.Polar(a0, 0.3f), anchor + FlameMesh.Polar(a0, 0.36f), anchor + FlameMesh.Polar(a1, 0.36f), anchor + FlameMesh.Polar(a1, 0.3f), blood, blood, blood, blood);
                }
                mesh.Ring(marked.Position, DummyRadius + 0.25f, 0.04f, FlameMesh.Alpha(blood, 0.5f + 0.3f * Mathf.Sin(age * 8f)), 24);
            });
            yield return Wait(0.5f);
            for (int i = 0; i < 4; i++)
            {
                Stab((marked.Position - HeroAt).normalized, SwordAttack.StabAngle, 0.05f);
                yield return Wait(0.45f);
            }
            yield return Wait(Life - 0.5f - 4 * 0.45f);
            if (skull != null) Destroy(skull.gameObject);
            // The mark ends: it all comes due.
            Vector2 at = marked.Position;
            HeroVfx.Pulse(fx, at, DungeonEnemy.DeathMarkBurstRadius, FlameMesh.Alpha(blood, 0.7f), 0.4f);
            HeroVfx.Sparks(fx, at, blood, 24, 6f, 0.45f, null, 360f, 1.3f);
            HeroVfx.Sparks(fx, at + Vector2.up * 1.3f, new Color(1f, 0.6f, 0.6f), 10, 4f, 0.35f, Vector2.up, 160f, 1f);
            Kill(marked);
            foreach (var dummy in Within(at, DungeonEnemy.DeathMarkBurstRadius)) dummy.Hit(at, 0.9f);
            yield return Wait(1.6f);
        }

        /// <summary>While it lasts, every backstab calls a shadow of her out behind the victim to stab it again.</summary>
        private IEnumerator ShadowCloneDemo()
        {
            Set(4.5f, new Vector2(-2.4f, 0f), new Vector2(0f, 0f));
            yield return Wait(0.5f);
            Cast();
            HeroVfx.Pulse(fx, HeroAt, 1.2f, ShadowstepVfx.Violet, 0.4f);
            yield return Wait(0.6f);
            for (int i = 0; i < 2; i++)
            {
                Vector2 through = i == 0 ? Vector2.right : Vector2.left;
                Shadowstep(HeroAt + through * 4.2f);
                yield return Wait(0.3f);
                Stab(-through, SwordAttack.StabAngle, 0f);
                var victim = dummies[0];
                RearHitMarker.Draw(fx, victim.Position, through, DummyRadius);
                // The clone steps out of the smoke on the far side of it and lunges in.
                ShadowCloneVfx.Play(fx, hero.transform, victim.Position + Vector2.up * 0.9f, victim.Position, ShadowClone.Delay);
                yield return Wait(ShadowClone.Delay);
                victim.Hit(victim.Position + Vector2.up, 0f);
                yield return Wait(1.1f);
            }
            yield return Wait(0.8f);
        }
    }
}

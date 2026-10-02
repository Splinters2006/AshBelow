using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Archer's demos: his bow, the Elemental Quiver and his six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static readonly Color PlainArrow = new Color(0.95f, 1f, 0.65f);

        private IEnumerator Archer() => kind switch
        {
            DemoKind.Charged => ArcherCharged(),
            DemoKind.Heavy => TripleShot(),
            DemoKind.Mechanic => ElementalQuiverDemo(),
            _ => ability.Type switch
            {
                AbilityType.Volley => ArrowVolley(),
                AbilityType.PiercingShot => PiercingShot(),
                AbilityType.Windstep => WindstepDemo(),
                AbilityType.NetShot => NetShot(),
                AbilityType.RicochetArrow => RicochetArrowDemo(),
                AbilityType.BearTrap => BearTrapDemo(),
                _ => null
            }
        };

        /// <summary>A quick arrow, then a drawn one: it flies farther and hits harder.</summary>
        private IEnumerator ArcherCharged()
        {
            Set(5f, new Vector2(-3.6f, 0f), new Vector2(0.6f, 0f), new Vector2(2.2f, 0f));
            yield return Wait(0.5f);
            Face(Vector2.right);
            Arrow(HeroAt, Vector2.right, PlayerProjectile.MaxRange, PlainArrow);
            yield return Wait(0.9f);
            yield return Charge(1.2f, (mesh, charge) => Lane(mesh, HeroAt, Vector2.right, BowAttack.RangeForCharge(charge), 0.06f, ChargeFill(charge, character.Color)));
            Arrow(HeroAt, Vector2.right, BowAttack.RangeForCharge(1f), AbilityCatalog.Gold, (dummy, at) =>
            {
                dummy.Hit(at + Vector2.left, 0.9f);
                HeroVfx.Pulse(fx, at, 0.5f, AbilityCatalog.Gold, 0.2f);
                HeroVfx.Sparks(fx, at, AbilityCatalog.Gold, 10, 4f, 0.3f, Vector2.right, 120f);
            });
            yield return Wait(1.6f);
        }

        /// <summary>Right click: three arrows in a tight, long-range spread.</summary>
        private IEnumerator TripleShot()
        {
            Set(5.5f, new Vector2(-4.4f, 0f), new Vector2(1.6f, 0f), new Vector2(2.4f, 0.75f), new Vector2(2.4f, -0.75f));
            for (int volley = 0; volley < 2; volley++)
            {
                yield return Wait(0.7f);
                Face(Vector2.right);
                for (int i = -1; i <= 1; i++) Arrow(HeroAt, Quaternion.Euler(0, 0, BowAttack.SpreadAngle * i) * Vector2.right, BowAttack.HeavyRange, PlainArrow);
            }
            yield return Wait(1.4f);
        }

        /// <summary>R: each press loads the next element; fire arrows burn, ice arrows freeze and lightning arrows shock.</summary>
        private IEnumerator ElementalQuiverDemo()
        {
            Set(5f, new Vector2(-3.6f, 0f), new Vector2(1.2f, 1.2f), new Vector2(1.6f, 0f), new Vector2(1.2f, -1.2f));
            yield return Wait(0.5f);
            var elements = new[] { DamageElement.Fire, DamageElement.Ice, DamageElement.Lightning };
            for (int i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                Color color = CombatDamage.ElementColor(element);
                HeroVfx.Pulse(fx, HeroAt, 0.9f, color, 0.25f);
                HeroVfx.Sparks(fx, HeroAt, color, 8, 2.5f, 0.3f);
                yield return Wait(0.45f);
                var target = dummies[i];
                Vector2 aim = (target.Position - HeroAt).normalized;
                Face(aim);
                Arrow(HeroAt, aim, 7f, ElementalQuiver.ShotColor(element, PlainArrow), (dummy, at) =>
                {
                    HeroVfx.Sparks(fx, at, color, 10, 3.5f, 0.3f);
                    HeroVfx.Pulse(fx, at, 0.6f, color, 0.25f);
                    if (element == DamageElement.Fire) dummy.Burn(3f);
                    else if (element == DamageElement.Ice) dummy.Freeze(2f);
                    else
                    {
                        dummy.Stun(1f);
                        // The shock leaps to whoever stands nearest.
                        foreach (var other in Within(dummy.Position, 2f))
                            if (other != dummy) { CombatVfx.Bolt(fx, dummy.Position, other.Position, color); other.Flash(); }
                    }
                });
                yield return Wait(0.9f);
            }
            yield return Wait(1.4f);
        }

        /// <summary>A circle is marked around the pack and sixteen arrows patter down into it.</summary>
        private IEnumerator ArrowVolley()
        {
            Vector2 center = At(2f, -0.3f);
            Set(6f, new Vector2(-4.6f, -0.3f), new Vector2(1.2f, 0.4f), new Vector2(2.8f, -0.2f), new Vector2(1.8f, -1.4f), new Vector2(3f, 1.2f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            const float FallTime = 0.22f, FallHeight = 6f;
            float total = FallTime + ArrowRain.Duration;
            Draw(total + 0.25f, 2, (mesh, age) =>
            {
                float fade = Mathf.Clamp01((total + 0.25f - age) / 0.25f), pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                mesh.Disc(center, ArrowRain.Radius, FlameMesh.Alpha(PlainArrow, (0.06f + 0.05f * pulse) * fade), FlameMesh.Alpha(PlainArrow, 0.02f * fade));
                mesh.Ring(center, ArrowRain.Radius, 0.07f, FlameMesh.Alpha(PlainArrow, (0.45f + 0.3f * pulse) * fade), 56);
            });
            Vector2 slant = new Vector2(-0.35f, 1f).normalized;
            for (int i = 0; i < ArrowRain.BaseArrows; i++)
            {
                // The first lands dead on the mark, the rest scatter; a few are sure to find someone.
                Vector2 landing = i == 0 ? center : i % 3 == 0 ? dummies[i / 3 % dummies.Count].Position : center + Random.insideUnitCircle * ArrowRain.Radius;
                StartCoroutine(FallingArrow(landing, slant, FallTime, FallHeight));
                yield return Wait(ArrowRain.Duration / ArrowRain.BaseArrows);
            }
            yield return Wait(1.6f);
        }

        private IEnumerator FallingArrow(Vector2 landing, Vector2 slant, float fallTime, float fallHeight)
        {
            Transform stage = fx;
            Vector2 sky = landing + slant * fallHeight;
            var arrow = DungeonVisuals.CreateArrow("Falling arrow", stage, sky, 0.7f, PlainArrow, 7);
            arrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(-slant.y, -slant.x) * Mathf.Rad2Deg);
            var trail = CombatVfx.Trail(arrow.gameObject, FlameMesh.Alpha(PlainArrow, 0.6f), 0.06f, 0.08f);
            for (float t = 0f; t < fallTime; t += Time.deltaTime)
            {
                if (arrow == null) yield break;
                float progress = t / fallTime;
                arrow.transform.position = Vector2.Lerp(sky, landing, progress * progress) + slant * 0.3f;
                yield return null;
            }
            if (arrow == null || stage == null) yield break;
            // It sticks in the floor, head buried and fletching up, for a moment before fading.
            arrow.transform.localScale *= 0.8f;
            arrow.transform.position = landing + slant * 0.24f;
            if (trail != null) trail.emitting = false;
            arrow.gameObject.AddComponent<FadingSprite>().Duration = 0.9f;
            HeroVfx.Sparks(stage, landing, new Color(0.75f, 0.7f, 0.6f, 0.7f), 5, 2f, 0.22f, Vector2.up, 160f, 0.6f);
            HeroVfx.Pulse(stage, landing, 0.3f, FlameMesh.Alpha(PlainArrow, 0.5f), 0.15f);
            foreach (var dummy in Within(landing, ArrowRain.ImpactRadius)) dummy.Hit(landing + Vector2.up * 0.3f, 0.1f);
        }

        /// <summary>One thundering arrow tears through a whole line of enemies.</summary>
        private IEnumerator PiercingShot()
        {
            Set(6.5f, new Vector2(-5.4f, 0f), new Vector2(-2f, 0.1f), new Vector2(0f, -0.15f), new Vector2(2f, 0.15f), new Vector2(4f, -0.05f));
            yield return Wait(0.7f);
            Cast();
            Face(Vector2.right);
            Color color = AbilityCatalog.Gold, core = new Color(1f, 0.97f, 0.85f);
            Vector2 origin = HeroAt, direction = Vector2.right;
            var body = Sprite("Piercing shot", null, origin, new Vector2(1.1f, 0.16f), color, 7);
            // A soft halo and a white-hot core ride along with the shaft.
            DungeonVisuals.Create("Halo", body.transform, origin, new Vector2(1.35f, 3.2f), FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.2f), 0.3f), 6).transform.localPosition = Vector2.zero;
            DungeonVisuals.Create("Core", body.transform, origin, new Vector2(0.9f, 0.4f), core, 8).transform.localPosition = new Vector2(0.06f, 0f);
            DungeonVisuals.Create("Tip", body.transform, origin, new Vector2(0.2f, 1.7f), Color.white, 8).transform.localPosition = new Vector2(0.52f, 0f);
            CombatVfx.Trail(body.gameObject, color, 0.34f, 0.28f);
            HeroVfx.Pulse(fx, origin + direction * 0.4f, 1.3f, color, 0.3f);
            HeroVfx.Sparks(fx, origin, core, 14, 6f, 0.3f, -direction, 70f, 1.2f);
            CombatVfx.GlowBolt(fx, origin, origin + direction * 1.6f, color);
            Shoot(body, direction, PiercingArrow.Speed, PiercingArrow.Range, 0.3f, true, (dummy, at) =>
            {
                dummy.Hit(at - direction + Vector2.up * (dummy.Position.y > at.y ? -0.5f : 0.5f), 0.8f);
                HeroVfx.Pulse(fx, at, 0.55f, FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.3f), 0.8f), 0.22f);
                HeroVfx.Sparks(fx, at, core, 12, 5.5f, 0.3f, direction, 80f, 1.3f);
                CombatVfx.Ring(fx, at, 0.5f, color, 0.25f);
            });
            yield return Wait(2f);
        }

        /// <summary>A gust carries him aside as the pack closes in, and three arrows answer.</summary>
        private IEnumerator WindstepDemo()
        {
            Set(5.5f, new Vector2(-1f, -1.4f), new Vector2(2.2f, -1.2f), new Vector2(3f, -0.4f), new Vector2(3.2f, -1.9f));
            yield return Approach(1.1f, 1.8f);
            Cast();
            Vector2 from = HeroAt, aim = new Vector2(-0.75f, 0.66f).normalized, to = from + aim * 3.5f;
            HeroAt = to;
            WindstepVfx.Play(fx, from, to);
            // The counterattack flies back at whoever was chasing him.
            Vector2 back = (dummies[0].Position - to).normalized;
            Face(back);
            for (int i = -1; i <= 1; i++) Arrow(to, Quaternion.Euler(0, 0, BowAttack.SpreadAngle * i) * back, BowAttack.HeavyRange, PlainArrow);
            yield return Approach(1.6f, 1.2f);
        }

        /// <summary>A round casting net: ropes from a centre knot out to lead weights, crossed by rings of rope.</summary>
        private static void DrawNet(FlameMesh mesh, Vector2 center, float radius, float spinDegrees, float alpha, bool flying)
        {
            Color rope = FlameMesh.Alpha(new Color(0.8f, 0.72f, 0.5f), alpha), knot = FlameMesh.Alpha(new Color(0.55f, 0.45f, 0.3f), alpha), weight = FlameMesh.Alpha(new Color(0.3f, 0.3f, 0.34f), alpha);
            float width = flying ? 0.045f : 0.035f;
            if (flying) mesh.Ellipse(center + Vector2.down * 0.25f, radius * 0.8f, radius * 0.35f, new Color(0f, 0f, 0f, 0.18f), new Color(0f, 0f, 0f, 0f), 16);
            const int Spokes = 8;
            var rim = new Vector2[Spokes];
            for (int i = 0; i < Spokes; i++)
            {
                rim[i] = center + FlameMesh.Polar((spinDegrees + i * 360f / Spokes) * Mathf.Deg2Rad, radius);
                mesh.Bar(center, (rim[i] - center).normalized, radius, width, rope, rope);
            }
            foreach (float ring in new[] { 0.45f, 0.8f, 1f })
                for (int i = 0; i < Spokes; i++)
                {
                    Vector2 a = Vector2.Lerp(center, rim[i], ring), b = Vector2.Lerp(center, rim[(i + 1) % Spokes], ring);
                    Vector2 sag = Vector2.Lerp(a, b, 0.5f);
                    sag += (center - sag) * 0.12f;
                    foreach (var (from, to) in new[] { (a, sag), (sag, b) })
                        if ((to - from).sqrMagnitude > 0.000001f) mesh.Bar(from, (to - from).normalized, Vector2.Distance(from, to), width * 0.85f, rope, rope);
                    mesh.Diamond(a, 0.03f, knot);
                }
            mesh.Disc(center, 0.07f, knot, knot, 8);
            foreach (var lead in rim) mesh.Disc(lead, 0.06f, weight, weight, 8);
        }

        /// <summary>A weighted net spins out, drops open on the first enemy it reaches and roots everything under it.</summary>
        private IEnumerator NetShot()
        {
            Set(5f, new Vector2(-3.6f, 0f), new Vector2(0.9f, 0.2f), new Vector2(1.6f, -0.6f), new Vector2(1.7f, 0.9f));
            yield return Approach(0.8f, 1.2f);
            Cast();
            Face(Vector2.right);
            Vector2 at = HeroAt;
            const float Hold = 2f;
            float flown = 0f, landedAt = -1f;
            HeroVfx.Sparks(fx, at, new Color(0.8f, 0.72f, 0.5f), 6, 3f, 0.2f, Vector2.right, 60f, 0.7f);
            var net = Draw(10f, 6, (mesh, age) =>
            {
                bool landed = landedAt >= 0f;
                float since = age - landedAt, radius = landed ? ThrownNet.OpenRadius * (1f + 0.08f * Mathf.Exp(-since * 12f) * Mathf.Sin(since * 40f))
                    : Mathf.Lerp(ThrownNet.StartRadius, ThrownNet.OpenRadius, Mathf.Clamp01(flown / PlayerAbilities.NetRange));
                DrawNet(mesh, at, radius, landed ? 0f : age * 540f, landed ? Mathf.Clamp01((Hold + 0.3f - since) / 0.3f) : 1f, !landed);
            });
            float age = 0f;
            while (flown < PlayerAbilities.NetRange && Within(at, ThrownNet.OpenRadius * 0.5f).Count == 0)
            {
                float step = ThrownNet.Speed * Time.deltaTime;
                at += Vector2.right * step;
                flown += step;
                age += Time.deltaTime;
                Advance(HeroAt, 1.2f);
                yield return null;
            }
            landedAt = age;
            HeroVfx.Pulse(fx, at, ThrownNet.OpenRadius, FlameMesh.Alpha(new Color(0.8f, 0.72f, 0.5f), 0.35f), 0.2f);
            HeroVfx.Sparks(fx, at, new Color(0.8f, 0.72f, 0.5f), 10, 2.5f, 0.25f);
            foreach (var dummy in Within(at, ThrownNet.OpenRadius)) dummy.Root(Hold);
            yield return Wait(Hold + 0.3f);
            if (net != null) Destroy(net.gameObject);
            yield return Approach(0.8f, 1.2f);
        }

        /// <summary>One arrow, four enemies: it leaps from each to the next, hitting harder every time.</summary>
        private IEnumerator RicochetArrowDemo()
        {
            Set(5.5f, new Vector2(-4.4f, -0.8f), new Vector2(-0.6f, -0.6f), new Vector2(1.6f, 1.4f), new Vector2(3.4f, -0.4f), new Vector2(1f, -1.9f));
            yield return Wait(0.6f);
            Cast();
            Color tint = new Color(1f, 0.85f, 0.45f), hot = new Color(1f, 0.97f, 0.82f);
            var arrow = DungeonVisuals.CreateArrow("Ricochet arrow", fx, HeroAt, 0.75f, tint, 7);
            var glow = DungeonVisuals.Create("Ricochet glow", arrow.transform, HeroAt, Vector2.one, FlameMesh.Alpha(tint, 0.3f), 6);
            glow.sprite = DungeonVisuals.GlowSprite;
            glow.transform.localPosition = Vector2.zero;
            glow.transform.localScale = new Vector3(0.9f, 0.55f, 1f);
            CombatVfx.Trail(arrow.gameObject, FlameMesh.Alpha(tint, 0.85f), 0.12f, 0.22f);
            Face(dummies[0].Position - HeroAt);
            HeroVfx.Sparks(fx, HeroAt, tint, 6, 3f, 0.2f, dummies[0].Position - HeroAt, 50f, 0.8f);
            for (int i = 0; i < dummies.Count; i++)
            {
                Vector2 goal = dummies[i].Position, heading = (goal - (Vector2)arrow.transform.position).normalized;
                arrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
                while (Vector2.Distance(arrow.transform.position, goal) > DummyRadius)
                {
                    arrow.transform.position = Vector2.MoveTowards(arrow.transform.position, goal, RicochetArrow.Speed * Time.deltaTime);
                    yield return null;
                }
                Vector2 at = arrow.transform.position;
                dummies[i].Hit(at - heading, 0.3f + 0.2f * i);
                // The ricochet's flash: a gold burst sprayed along the new heading and a white glint where it turned.
                Vector2 next = i + 1 < dummies.Count ? (dummies[i + 1].Position - at).normalized : heading;
                HeroVfx.Pulse(fx, at, 0.55f, FlameMesh.Alpha(tint, 0.8f), 0.18f);
                HeroVfx.Sparks(fx, at, tint, 9, 4f, 0.25f, next, 70f, 0.9f);
                HeroVfx.Sparks(fx, at, hot, 4, 2.5f, 0.2f, -heading, 90f, 0.6f);
                CombatVfx.Ring(fx, at, 0.35f, hot, 0.15f);
            }
            Destroy(arrow.gameObject);
            yield return Wait(1.6f);
        }

        /// <summary>A toothed trap seen from above: two half-ring jaws that fold shut toward the hinge as <paramref name="open"/> falls to 0.</summary>
        private static void DrawTrap(FlameMesh mesh, Vector2 center, float open, float alpha)
        {
            const float JawRadius = 0.4f;
            Color iron = FlameMesh.Alpha(new Color(0.62f, 0.62f, 0.68f), alpha), dark = FlameMesh.Alpha(new Color(0.28f, 0.28f, 0.32f), alpha),
                steel = FlameMesh.Alpha(new Color(0.88f, 0.9f, 0.95f), alpha), plate = FlameMesh.Alpha(new Color(0.45f, 0.4f, 0.36f), alpha);
            mesh.Ellipse(center + Vector2.down * 0.05f, JawRadius + 0.12f, (JawRadius + 0.12f) * 0.8f, new Color(0f, 0f, 0f, 0.3f * alpha), new Color(0f, 0f, 0f, 0f), 24);
            mesh.Disc(center, JawRadius * 0.5f, plate, dark, 20);
            mesh.Bar(center + Vector2.down * (JawRadius + 0.05f), Vector2.up, JawRadius * 2f + 0.1f, 0.06f, dark, dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 Point(float angle, float radius) => center + new Vector2(side * Mathf.Sin(angle) * radius * open, Mathf.Cos(angle) * radius);
                const int Segments = 14, Teeth = 7;
                for (int i = 0; i < Segments; i++)
                {
                    float a0 = Mathf.PI * i / Segments, a1 = Mathf.PI * (i + 1) / Segments;
                    mesh.Quad(Point(a0, JawRadius - 0.04f), Point(a0, JawRadius + 0.04f), Point(a1, JawRadius + 0.04f), Point(a1, JawRadius - 0.04f), dark, iron, iron, dark);
                }
                for (int i = 0; i < Teeth; i++)
                {
                    float angle = Mathf.PI * (i + 0.5f) / Teeth, half = Mathf.PI * 0.35f / Teeth;
                    mesh.Triangle(Point(angle - half, JawRadius - 0.03f), Point(angle, JawRadius * 0.62f), Point(angle + half, JawRadius - 0.03f), iron, steel, iron);
                }
            }
            mesh.Disc(center + Vector2.up * JawRadius, 0.05f, steel, iron, 10);
            mesh.Disc(center + Vector2.down * JawRadius, 0.05f, steel, iron, 10);
        }

        /// <summary>He sets a trap in the pack's path; the first enemy to step in is held fast.</summary>
        private IEnumerator BearTrapDemo()
        {
            Set(5f, new Vector2(-3.6f, 0f), new Vector2(3.4f, 0.1f), new Vector2(4.2f, -0.7f));
            yield return Wait(0.5f);
            Cast();
            Face(Vector2.right);
            Vector2 at = At(0.2f, 0f);
            float sprungAt = -1f, clock = 0f;
            HeroVfx.Sparks(fx, at, new Color(0.6f, 0.55f, 0.45f, 0.7f), 6, 1.8f, 0.2f, Vector2.up, 160f, 0.6f);
            Draw(12f, 2, (mesh, age) =>
            {
                clock = age;
                float arm = Mathf.Clamp01(age / BearTrap.ArmTime);
                float open = sprungAt >= 0f ? Mathf.Lerp(1f, 0.08f, Mathf.Clamp01((age - sprungAt) / 0.07f)) : arm < 1f ? 1f - Mathf.Pow(1f - arm, 3f) * Mathf.Cos(arm * 9f) : 1f;
                DrawTrap(mesh, at, open, sprungAt >= 0f ? Mathf.Clamp01((sprungAt + 2.6f - age) / 0.3f) : 1f);
            });
            Dummy caught = null;
            while (caught == null)
            {
                Advance(HeroAt, 1.9f);
                foreach (var dummy in Within(at, BearTrap.TriggerRadius - DummyRadius * 0.5f)) caught = dummy;
                yield return null;
            }
            sprungAt = clock;
            caught.Root(2f);
            HeroVfx.Sparks(fx, at, new Color(0.88f, 0.9f, 0.95f), 12, 4f, 0.3f);
            CombatVfx.Ring(fx, at, 0.7f, Color.white, 0.25f);
            // Held fast, it is an easy mark.
            for (int i = 0; i < 3; i++)
            {
                yield return Wait(0.5f);
                Arrow(HeroAt, (caught.Position - HeroAt).normalized, 8f, PlainArrow);
            }
            yield return Wait(1.2f);
        }
    }
}

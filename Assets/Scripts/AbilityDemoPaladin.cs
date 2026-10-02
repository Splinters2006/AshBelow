using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Paladin's demos: his hammer and blessings, the Heavenly Host and his six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static Color Gold => AbilityCatalog.Gold;

        private IEnumerator Paladin() => kind switch
        {
            DemoKind.Charged => PaladinBlessing(),
            DemoKind.Heavy => HolySwords(),
            DemoKind.Mechanic => HeavenlyHostDemo(),
            _ => ability.Type switch
            {
                AbilityType.HealingLight => HealingLight(),
                AbilityType.Judgment => Judgment(),
                AbilityType.Sanctuary => Sanctuary(),
                AbilityType.HolyLance => HolyLance(),
                AbilityType.Consecration => ConsecrationDemo(),
                AbilityType.DivineIntervention => DivineIntervention(),
                _ => null
            }
        };

        /// <summary>A full charge is not a blow but a blessing: he and every ally near him strike harder for a while.</summary>
        private IEnumerator PaladinBlessing()
        {
            Set(5.5f, new Vector2(-1.4f, 0f), new Vector2(1f, 0.3f), new Vector2(1.2f, -0.8f));
            var ally = AddAlly(new Vector2(-2.6f, 1.2f));
            yield return Wait(0.5f);
            Swing(Vector2.right, PaladinAttack.SwingReach, PaladinAttack.SwingCone, Gold, 0.3f);
            yield return Wait(0.9f);
            // The ring of the blessing's reach fills as he holds the charge.
            yield return Charge(PaladinAttack.ChargeDuration, (mesh, charge) =>
            {
                mesh.Ring(HeroAt, PaladinAttack.BlessingRadius, 0.05f, FlameMesh.Alpha(Gold, 0.25f + 0.4f * charge), 56);
                mesh.Ring(HeroAt, PaladinAttack.BlessingRadius * charge, 0.08f, FlameMesh.Alpha(Gold, 0.8f), 56);
            });
            foreach (var blessed in new[] { hero.transform, ally.transform })
            {
                CombatVfx.Ring(fx, blessed.position, 0.6f, Gold);
                HeroVfx.Motes(fx, blessed.position, 0.6f, Gold, 14, 1f);
                BlessingSparkles.Attach(blessed, () => true);
            }
            CombatVfx.Ring(fx, HeroAt, PaladinAttack.BlessingRadius, Gold, 0.6f);
            HeroVfx.Pulse(fx, HeroAt, PaladinAttack.BlessingRadius, Gold, 0.55f);
            yield return Wait(0.9f);
            // Blessed, his blows land harder.
            for (int i = 0; i < 3; i++)
            {
                Swing(Vector2.right, PaladinAttack.SwingReach, PaladinAttack.SwingCone, Gold, 0.6f);
                foreach (var dummy in InCone(HeroAt, Vector2.right, PaladinAttack.SwingReach, PaladinAttack.SwingCone)) HeroVfx.Sparks(fx, dummy.Position, Gold, 6, 3f, 0.25f);
                yield return Wait(0.5f);
            }
            yield return Wait(1.2f);
        }

        /// <summary>Right click: a holy sword falls from the sky onto every enemy near him, one after another.</summary>
        private IEnumerator HolySwords()
        {
            Set(5f, new Vector2(0f, -0.5f), new Vector2(2f, 0.4f), new Vector2(-2.2f, 0f), new Vector2(1f, -2.2f), new Vector2(-1.2f, 1.6f));
            yield return Wait(0.6f);
            HeroVfx.Pulse(fx, HeroAt, 1.2f, Gold, 0.35f);
            HeroVfx.Motes(fx, HeroAt, 0.7f, Gold, 12, 0.8f);
            CombatVfx.Ring(fx, HeroAt, PaladinAttack.HolySwordRadius, Gold, 0.45f);
            for (int i = 0; i < dummies.Count; i++) StartCoroutine(HolySword(dummies[i], i * 0.07f));
            yield return Wait(2.4f);
        }

        private IEnumerator HolySword(Dummy target, float delay)
        {
            Transform stage = fx;
            if (delay > 0f) yield return Wait(delay);
            if (stage == null || target.Body == null) yield break;
            // The sword tracks its target while the sigil forms, then commits to that spot.
            HolySwordVfx.Play(stage, target.Position, target.Body.transform);
            yield return Wait(HolySwordVfx.ImpactDelay);
            if (stage == null || target.Body == null) yield break;
            target.Hit(target.Position + Vector2.up, 0.2f);
            target.Chill(0.8f);
        }

        /// <summary>R: an angel comes down on the ally who has fallen and raises them to their feet.</summary>
        private IEnumerator HeavenlyHostDemo()
        {
            Set(5f, new Vector2(-1.6f, -0.3f), new Vector2(3.2f, 1f));
            var ally = AddAlly(new Vector2(1.2f, -0.3f));
            // The ally lies where they fell.
            ally.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            ally.color = new Color(0.4f, 0.42f, 0.48f);
            yield return Wait(1f);
            Vector2 target = ally.transform.position;
            HolyLightVfx.Play(fx, target, 1.4f, 0.35f);
            AngelVfx.Play(fx, target);
            HeroVfx.Motes(fx, target, 1.1f, Gold, 30, 1.4f);
            HeroVfx.Pulse(fx, target, 1.8f, Color.white, 0.5f);
            CombatVfx.Ring(fx, HeroAt, 1.2f, Gold, 0.45f);
            yield return Wait(0.8f);
            ally.transform.rotation = Quaternion.identity;
            ally.color = new Color(0.45f, 0.72f, 1f);
            HealVfx.Play(fx, ally.transform);
            yield return Wait(2.2f);
        }

        /// <summary>A soft green light: he and the ally beside him are healed.</summary>
        private IEnumerator HealingLight()
        {
            Set(5.5f, new Vector2(-0.8f, 0f));
            var ally = AddAlly(new Vector2(1.4f, 0.6f));
            // Both are hurt.
            ally.color = new Color(0.6f, 0.45f, 0.5f);
            TintHero(new Color(0.8f, 0.3f, 0.3f), 1f);
            yield return Wait(1f);
            Cast();
            HealVfx.Play(fx, hero.transform);
            HealVfx.Play(fx, ally.transform);
            CombatVfx.Ring(fx, HeroAt, 4f, FlameMesh.Alpha(HealVfx.Mint, 0.7f), 0.5f);
            ally.color = new Color(0.45f, 0.72f, 1f);
            yield return Wait(2.2f);
        }

        /// <summary>He marks the ground under the pack; after a breath a column of light smites and slows everything on it.</summary>
        private IEnumerator Judgment()
        {
            Vector2 mark = At(2f, 0f);
            Set(6f, new Vector2(-4.2f, 0f), new Vector2(1.4f, 0.6f), new Vector2(2.8f, -0.4f), new Vector2(1.8f, -1.5f), new Vector2(3.2f, 1.4f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            HolyLightVfx.Play(fx, mark, PaladinRelics.JudgmentRadius, PaladinRelics.JudgmentWindup);
            yield return Wait(PaladinRelics.JudgmentWindup);
            foreach (var dummy in Within(mark, PaladinRelics.JudgmentRadius))
            {
                dummy.Hit(mark, 0.5f);
                dummy.Chill(2f);
            }
            yield return Wait(2.2f);
        }

        /// <summary>A bubble of holy light forms round him: enemies are shoved out and every bolt that enters it is destroyed.</summary>
        private IEnumerator Sanctuary()
        {
            Set(6f, new Vector2(0f, 0f), new Vector2(1.2f, 0.5f), new Vector2(-1.4f, -0.6f), new Vector2(4.6f, 1.4f), new Vector2(-4.6f, -1.2f));
            yield return Wait(0.6f);
            Cast();
            const float Life = 3.4f;
            float radius = PaladinRelics.SanctuaryRadius;
            HolyBubble.Sanctuary(fx, HeroAt, radius, Life, hero.transform);
            // Whoever stood inside is thrown out to its edge.
            foreach (var dummy in Within(HeroAt, radius))
            {
                Vector2 away = (dummy.Position - HeroAt).normalized;
                dummy.Position = HeroAt + away * (radius + DummyRadius + 0.15f);
                HeroVfx.Sparks(fx, dummy.Position, HolyBubble.Holy, 6, 3f, 0.3f, away, 70f);
            }
            float nextBolt = 0.5f;
            int thrower = 2;
            for (float t = 0f; t < Life; t += Time.deltaTime)
            {
                if (t >= nextBolt)
                {
                    nextBolt = t + 0.5f;
                    var shooter = dummies[2 + thrower++ % 2];
                    shooter.Flash();
                    StartCoroutine(StoppedBolt(DungeonVisuals.CreateEmberBolt(fx, shooter.Position), shooter.Position, radius));
                }
                yield return null;
            }
            yield return Wait(1f);
        }

        /// <summary>The lance of light: a long tapering cone of gold from a round guard to a blazing point, trailing its own streak.</summary>
        private static void DrawLance(FlameMesh mesh, Vector2 origin, Vector2 point, Vector2 direction, float fade)
        {
            Color gold = new Color(1f, 0.82f, 0.3f), pale = new Color(1f, 0.96f, 0.8f);
            Vector2 side = Vector2.Perpendicular(direction), guard = point - direction * ThrownLance.Length * 0.72f, butt = point - direction * ThrownLance.Length;
            float shimmer = 1f + 0.1f * Mathf.Sin(Time.time * 40f), width = ThrownLance.Width;
            Vector2 tail = origin + direction * Mathf.Max(0f, Vector2.Distance(origin, point) - 4f);
            mesh.Quad(tail - side * width * 0.2f, tail + side * width * 0.2f, point + side * width, point - side * width,
                FlameMesh.Alpha(gold, 0f), FlameMesh.Alpha(gold, 0f), FlameMesh.Alpha(gold, 0.3f * fade), FlameMesh.Alpha(gold, 0.3f * fade));
            mesh.Bar(butt, direction, ThrownLance.Length * 0.28f, 0.1f, FlameMesh.Alpha(new Color(0.55f, 0.4f, 0.2f), fade), FlameMesh.Alpha(new Color(0.75f, 0.55f, 0.25f), fade));
            mesh.Disc(butt, 0.07f, FlameMesh.Alpha(gold, fade), FlameMesh.Alpha(gold, fade), 10);
            mesh.Quad(guard - side * 0.2f, guard + side * 0.2f, point, point, FlameMesh.Alpha(gold, fade), FlameMesh.Alpha(gold, fade), FlameMesh.Alpha(pale, fade), FlameMesh.Alpha(pale, fade));
            mesh.Quad(guard - side * 0.07f, guard + side * 0.07f, point, point, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade));
            // The vamplate, seen edge-on, and the blazing point.
            mesh.Bar(guard - side * 0.3f, side, 0.6f, 0.14f, FlameMesh.Alpha(gold, fade), FlameMesh.Alpha(gold, fade));
            mesh.Disc(point, 0.2f * shimmer, FlameMesh.Alpha(Color.white, 0.9f * fade), FlameMesh.Alpha(pale, 0f), 16);
        }

        /// <summary>He hurls a lance of light down a line of enemies; it runs every one of them through and stuns the first.</summary>
        private IEnumerator HolyLance()
        {
            Set(6.5f, new Vector2(-5.2f, 0f), new Vector2(-1.6f, 0.1f), new Vector2(0.6f, -0.15f), new Vector2(2.8f, 0.15f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            Color gold = new Color(1f, 0.82f, 0.3f), pale = new Color(1f, 0.96f, 0.8f);
            Vector2 origin = HeroAt, direction = Vector2.right, point = origin;
            HeroVfx.Pulse(fx, origin + direction * 0.4f, 0.8f, FlameMesh.Alpha(pale, 0.8f), 0.2f);
            HeroVfx.Sparks(fx, origin, gold, 10, 4f, 0.25f, direction, 50f, 0.9f);
            float flight = ThrownLance.Range / ThrownLance.Speed, endedAt = -1f;
            var lance = Draw(flight + 0.25f, 8, (mesh, age) => DrawLance(mesh, origin, point, direction, endedAt >= 0f ? 1f - (age - endedAt) / 0.25f : 1f));
            var struck = new HashSet<Dummy>();
            float clock = 0f;
            while (Vector2.Distance(origin, point) < ThrownLance.Range)
            {
                point += direction * ThrownLance.Speed * Time.deltaTime;
                clock += Time.deltaTime;
                foreach (var dummy in InLane(origin, direction, Vector2.Distance(origin, point), ThrownLance.Width))
                {
                    if (!struck.Add(dummy)) continue;
                    dummy.Hit(dummy.Position - direction, 0.4f);
                    // Only the first enemy it meets is stunned.
                    if (struck.Count == 1) dummy.Stun(1f);
                    HeroVfx.Sparks(fx, dummy.Position, pale, 8, 4f, 0.25f, direction, 70f, 0.9f);
                    HeroVfx.Pulse(fx, dummy.Position, 0.6f, FlameMesh.Alpha(gold, 0.7f), 0.15f);
                }
                yield return null;
            }
            endedAt = clock;
            HeroVfx.Pulse(fx, point, 0.9f, FlameMesh.Alpha(pale, 0.8f), 0.25f);
            HeroVfx.Sparks(fx, point, gold, 12, 4f, 0.3f, -direction, 160f, 1f);
            yield return Wait(1.8f);
        }

        /// <summary>The spots where enemies stand in the consecrated ground, for its pillars of light.</summary>
        private IEnumerable<Vector2> Consecrated(Vector2 center)
        {
            foreach (var dummy in Within(center, Consecration.Radius)) yield return dummy.Position;
        }

        /// <summary>The ground around him is sanctified: every second it smites the enemies standing on it, and blesses him.</summary>
        private IEnumerator ConsecrationDemo()
        {
            Set(5.5f, new Vector2(0f, 0f), new Vector2(3.6f, 0.8f), new Vector2(-3.8f, -0.4f), new Vector2(2.6f, -2f), new Vector2(-2.4f, 2f));
            yield return Wait(0.5f);
            Cast();
            Vector2 center = HeroAt;
            const float Life = 4f;
            ConsecrationVfx.Play(fx, center, Life, Consecration.Radius, () => Consecrated(center));
            BlessingSparkles.Attach(hero.transform, () => true);
            float nextTick = 0f;
            for (float t = 0f; t < Life; t += Time.deltaTime)
            {
                // They keep coming, into the light.
                Advance(center, 1.1f);
                if (t >= nextTick)
                {
                    nextTick += Consecration.Interval;
                    foreach (var dummy in Within(center, Consecration.Radius)) dummy.Hit(dummy.Position + Vector2.up, 0f);
                }
                yield return null;
            }
            yield return Wait(1f);
        }

        /// <summary>Guardian angels circle the ally he marks; when the blow that would have killed them lands, a pillar of light saves them.</summary>
        private IEnumerator DivineIntervention()
        {
            Set(5f, new Vector2(-2.6f, 0f), new Vector2(2.6f, 0.2f), new Vector2(2.4f, -1f));
            var ally = AddAlly(new Vector2(0.6f, -0.2f));
            yield return Wait(0.5f);
            Cast();
            CombatVfx.GlowBolt(fx, HeroAt, ally.transform.position, Gold);
            GuardianAngelsVfx.Play(fx, ally.transform, 2.2f);
            // The pack falls on the ally...
            for (float t = 0f; t < 1.6f; t += Time.deltaTime) { Advance(ally.transform.position, 1.6f); yield return null; }
            ally.color = Color.white;
            HeroVfx.Sparks(fx, ally.transform.position, new Color(1f, 0.3f, 0.25f), 12, 4f, 0.3f);
            yield return Wait(0.2f);
            // ...and the angels catch them.
            GuardianAngelsVfx.Rescued(fx, ally.transform);
            ally.color = new Color(0.45f, 0.72f, 1f);
            foreach (var dummy in Within(ally.transform.position, 2f)) dummy.Hit(ally.transform.position, 1f);
            yield return Wait(2.4f);
        }
    }
}

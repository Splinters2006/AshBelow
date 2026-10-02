using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Augment's demos: his plasma ray and arm cannon, Overclock and his six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static Color Plasma => CyborgAttack.Plasma;
        private static Color PlasmaCore => CyborgAttack.Core;

        private IEnumerator Augment() => kind switch
        {
            DemoKind.Charged => AugmentCharged(),
            DemoKind.Heavy => PlasmaCannon(),
            DemoKind.Mechanic => OverclockDemo(),
            _ => ability.Type switch
            {
                AbilityType.MicroMissiles => MicroMissiles(),
                AbilityType.RocketBoost => RocketBoost(),
                AbilityType.SentryTurret => SentryTurretDemo(),
                AbilityType.EmpPulse => EmpPulse(),
                AbilityType.GrappleArm => GrappleArm(),
                AbilityType.OrbitalLaser => OrbitalLaserDemo(),
                _ => null
            }
        };

        /// <summary>Where shots leave the arm cannon.</summary>
        private Vector2 Muzzle(Vector2 aim) => HeroAt + aim * 0.45f + Vector2.down * 0.05f;

        /// <summary>A plasma ray from <paramref name="origin"/>: it pierces every enemy in its line.</summary>
        private void Ray(Vector2 origin, Vector2 aim, float range, float width, float knockback = 0.35f)
        {
            CyborgVfx.Ray(fx, origin, origin + aim * range, Plasma, width);
            foreach (var dummy in InLane(origin, aim, range, width * 0.5f))
            {
                dummy.Hit(origin, knockback);
                HeroVfx.Sparks(fx, dummy.Position, PlasmaCore, 6, 4f, 0.22f, aim, 70f);
            }
        }

        /// <summary>A quick ray, then a held one: it reaches farther and grows nearly three times as wide.</summary>
        private IEnumerator AugmentCharged()
        {
            Set(5.5f, new Vector2(-4.2f, 0f), new Vector2(-0.6f, 0.1f), new Vector2(1f, -0.15f), new Vector2(2.6f, 0.12f));
            yield return Wait(0.5f);
            Face(Vector2.right);
            Ray(Muzzle(Vector2.right), Vector2.right, CyborgAttack.RayRange, CyborgAttack.RayWidth);
            yield return Wait(0.9f);
            yield return Charge(CyborgAttack.ChargeDuration, (mesh, charge) => Lane(mesh, Muzzle(Vector2.right), Vector2.right,
                Mathf.Lerp(CyborgAttack.RayRange, CyborgAttack.ChargedRayRange, charge), Mathf.Lerp(CyborgAttack.RayWidth, CyborgAttack.ChargedRayWidth, charge) * 0.5f, ChargeFill(charge, Plasma)));
            Ray(Muzzle(Vector2.right), Vector2.right, CyborgAttack.ChargedRayRange, CyborgAttack.ChargedRayWidth, 0.7f);
            yield return Wait(1.6f);
        }

        /// <summary>The arm cannon's orb: it bursts in flame on the first enemy it meets, burning everything around.</summary>
        private void FireOrb(Vector2 origin, Vector2 direction, float charge)
        {
            float size = 0.3f + 0.25f * charge, radius = 1.1f + 0.9f * charge;
            var body = Sprite("Plasma orb", null, origin, Vector2.one * size, Plasma, 7);
            DungeonVisuals.Create("Core", body.transform, origin, Vector2.one * 0.55f, PlasmaCore, 8).transform.localPosition = Vector2.zero;
            CombatVfx.Trail(body.gameObject, new Color(Plasma.r, Plasma.g, Plasma.b, 0.8f), size * 0.8f, 0.18f);
            HeroVfx.Pulse(fx, origin, 0.6f + 0.4f * charge, Plasma, 0.25f);
            HeroVfx.Sparks(fx, origin, PlasmaCore, 10, 5f, 0.25f, direction, 60f);
            Transform stage = fx;
            Color fire = CombatDamage.ElementColor(DamageElement.Fire);
            var shot = Shoot(body, direction, PlasmaOrb.Speed, PlasmaOrb.Range, size * 0.5f, false, null, at =>
            {
                HeroVfx.Pulse(stage, at, radius, Plasma, 0.35f);
                CombatVfx.Ring(stage, at, radius, fire, 0.35f);
                HeroVfx.Sparks(stage, at, Color.Lerp(fire, PlasmaCore, 0.4f), 18, 5.5f, 0.4f, null, 360f, 1.2f);
                HeroVfx.Sparks(stage, at, Plasma, 10, 3.5f, 0.35f);
                foreach (var dummy in Within(at, radius)) { dummy.Hit(at, 1f); dummy.Burn(2.5f); }
            });
            shot.Turn = false;
            shot.Spin = 540f;
        }

        /// <summary>Right click: he holds the cannon while it charges, then looses an orb that bursts in flame.</summary>
        private IEnumerator PlasmaCannon()
        {
            Set(5.5f, new Vector2(-4.2f, 0f), new Vector2(1.6f, 0f), new Vector2(2.6f, 1f), new Vector2(2.5f, -1.1f));
            yield return Wait(0.6f);
            Face(Vector2.right);
            Vector2 muzzle = Muzzle(Vector2.right);
            HeroVfx.Pulse(fx, muzzle, 0.5f, Plasma, 0.25f);
            // The charge gathers at the muzzle: a diamond of plasma that swells and whitens.
            var glow = Sprite("Cannon charge", null, muzzle, Vector2.one * 0.1f, Plasma, 8);
            glow.transform.rotation = Quaternion.Euler(0, 0, 45f);
            float nextHum = 0f;
            for (float t = 0f; t < CyborgAttack.CannonChargeTime + 0.3f; t += Time.deltaTime)
            {
                float charge = Mathf.Clamp01(t / CyborgAttack.CannonChargeTime);
                glow.transform.localScale = Vector2.one * (0.12f + 0.3f * charge + (charge >= 1f ? 0.04f * Mathf.Sin(Time.time * 30f) : 0f));
                glow.color = Color.Lerp(Plasma, PlasmaCore, charge >= 1f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 20f) : charge * 0.4f);
                if (t >= nextHum) { nextHum = t + 0.08f; HeroVfx.Motes(fx, muzzle, 0.3f + 0.4f * (1f - charge), Plasma, 3, 0.25f); }
                yield return null;
            }
            Destroy(glow.gameObject);
            FireOrb(muzzle, Vector2.right, 1f);
            yield return Wait(2.4f);
        }

        /// <summary>R: every ray is fully charged, and they come twice as fast.</summary>
        private IEnumerator OverclockDemo()
        {
            Set(5.5f, new Vector2(-4.2f, 0f), new Vector2(-0.6f, 0.6f), new Vector2(1f, -0.5f), new Vector2(2.6f, 0.3f));
            yield return Wait(0.5f);
            Face(Vector2.right);
            Ray(Muzzle(Vector2.right), Vector2.right, CyborgAttack.RayRange, CyborgAttack.RayWidth);
            yield return Wait(0.7f);
            HeroVfx.Pulse(fx, HeroAt, 2.2f, Plasma, 0.45f);
            HeroVfx.Sparks(fx, HeroAt, PlasmaCore, 24, 6f, 0.45f);
            HeroVfx.Motes(fx, HeroAt, 1f, Plasma, 20, 0.8f);
            CombatVfx.Ring(fx, HeroAt, 1.6f, Plasma, 0.4f);
            TintHero(Plasma, 3.4f);
            yield return Wait(0.7f);
            for (int i = 0; i < 9; i++)
            {
                Vector2 aim = Quaternion.Euler(0, 0, (i % 3 - 1) * 7f) * Vector2.right;
                Ray(Muzzle(aim), aim, CyborgAttack.ChargedRayRange, CyborgAttack.ChargedRayWidth, 0.15f);
                HeroVfx.Motes(fx, HeroAt, 0.45f, Plasma, 2, 0.4f);
                yield return Wait(CyborgAttack.RayInterval * 0.5f);
            }
            yield return Wait(1.2f);
        }

        /// <summary>One homing mini missile: it curves in on its target and bursts.</summary>
        private void Missile(Vector2 origin, Vector2 direction, Dummy target)
        {
            var body = Sprite("Micro-missile", null, origin, new Vector2(0.26f, 0.09f), new Color(0.85f, 0.88f, 0.92f), 7);
            DungeonVisuals.Create("Tip", body.transform, origin, new Vector2(0.3f, 1f), CyborgAttack.MissileColor, 8).transform.localPosition = new Vector2(0.45f, 0f);
            CombatVfx.Trail(body.gameObject, new Color(1f, 0.7f, 0.4f, 0.7f), 0.08f, 0.2f);
            Transform stage = fx;
            var shot = Shoot(body, direction, MicroMissile.Speed, MicroMissile.Speed * MicroMissile.Lifetime, 0.1f, false, null, at =>
            {
                HeroVfx.Pulse(stage, at, MicroMissile.BlastRadius, CyborgAttack.MissileColor, 0.25f);
                HeroVfx.Sparks(stage, at, Color.Lerp(CyborgAttack.MissileColor, Color.white, 0.3f), 8, 3.5f, 0.28f);
                foreach (var dummy in Within(at, MicroMissile.BlastRadius)) dummy.Hit(at, 0.4f);
            });
            shot.Target = target;
            shot.TurnRate = MicroMissile.TurnRate;
        }

        /// <summary>His shoulder pod fires a fan of six missiles that seek out the nearest enemies.</summary>
        private IEnumerator MicroMissiles()
        {
            Set(6f, new Vector2(-4.4f, 0f), new Vector2(0.6f, 1.8f), new Vector2(2f, 0.2f), new Vector2(0.8f, -1.9f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            const int Count = CyborgAttack.BaseMissiles;
            for (int i = 0; i < Count; i++)
            {
                Vector2 direction = Quaternion.Euler(0, 0, -60f + 120f * i / (Count - 1)) * Vector2.right;
                Missile(HeroAt + direction * 0.3f, direction, dummies[i % dummies.Count]);
            }
            HeroVfx.Sparks(fx, HeroAt, CyborgAttack.MissileColor, 12, 4f, 0.3f, Vector2.right, 140f);
            yield return Wait(2.6f);
        }

        /// <summary>He blasts forward on his leg thrusters, ramming through the pack, and lands in a burst of flame.</summary>
        private IEnumerator RocketBoost()
        {
            Set(5.5f, new Vector2(-3.6f, 0f), new Vector2(-1.4f, 0.2f), new Vector2(0f, -0.3f), new Vector2(1.4f, 0.7f), new Vector2(1.6f, -0.9f));
            yield return Wait(0.7f);
            Cast();
            Vector2 aim = Vector2.right, from = HeroAt, landing = from + aim * CyborgAttack.BoostDistance;
            Face(aim);
            HeroAt = landing;
            foreach (var dummy in InLane(from, aim, CyborgAttack.BoostDistance, CyborgAttack.BoostHitRadius)) dummy.Hit(from, 1.2f);
            RocketBoostVfx.Play(fx, from, landing, CyborgAttack.BoostBlastRadius);
            foreach (var dummy in Within(landing, CyborgAttack.BoostBlastRadius))
            {
                dummy.Hit(landing, 0.6f);
                dummy.Burn(2.5f);
            }
            yield return Wait(2.2f);
        }

        /// <summary>He drops a turret among the pack; it turns on the nearest enemy and fires a piercing ray again and again.</summary>
        private IEnumerator SentryTurretDemo()
        {
            Set(5.5f, new Vector2(-4f, 0f), new Vector2(2.6f, 1.4f), new Vector2(3.2f, -0.6f), new Vector2(-0.4f, -2f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            Vector2 at = At(-0.4f, 0f);
            var body = CyborgVfx.TurretBody(fx, at, out var barrel);
            HeroVfx.Pulse(fx, at, 0.9f, Plasma, 0.3f);
            HeroVfx.Sparks(fx, at + Vector2.down * 0.2f, new Color(0.7f, 0.75f, 0.8f), 10, 3f, 0.3f, Vector2.up, 160f);
            for (int shot = 0; shot < 6; shot++)
            {
                for (float t = 0f; t < SentryTurret.Interval; t += Time.deltaTime) { Advance(at, 0.8f); yield return null; }
                var target = Nearest(at);
                Vector2 aim = (target.Position - at).normalized;
                if (barrel != null) barrel.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                Ray(at + aim * 0.15f + Vector2.up * 0.08f, aim, SentryTurret.Range, SentryTurret.RayWidth, 0.6f);
            }
            HeroVfx.Motes(fx, at, 0.5f, Plasma, 10, 0.5f);
            HeroVfx.Sparks(fx, at, new Color(0.7f, 0.75f, 0.8f), 8, 2.5f, 0.3f);
            Destroy(body.gameObject);
            yield return Wait(1f);
        }

        /// <summary>A pulse rolls out from him: every enemy near is stunned, and the bolts in the air around him are fried.</summary>
        private IEnumerator EmpPulse()
        {
            Set(6.5f, new Vector2(0f, 0f), new Vector2(2.6f, 1f), new Vector2(-2.8f, 0.4f), new Vector2(1.6f, -2.2f), new Vector2(-5.4f, -1.6f), new Vector2(5.4f, 1.8f));
            // The two far ones are throwing bolts at him.
            var bolts = new List<SpriteRenderer>();
            for (int i = 3; i < 5; i++)
            {
                dummies[i].Flash();
                var bolt = DungeonVisuals.CreateEmberBolt(fx, dummies[i].Position);
                bolts.Add(bolt);
                StartCoroutine(Bolt(bolt, dummies[i].Position, 0f, 4f));
            }
            for (float t = 0f; t < 0.75f; t += Time.deltaTime)
            {
                for (int i = 0; i < 3; i++)
                    if (Vector2.Distance(dummies[i].Position, HeroAt) > 1.6f) dummies[i].Position = Vector2.MoveTowards(dummies[i].Position, HeroAt, 1.4f * Time.deltaTime);
                yield return null;
            }
            Cast();
            float radius = PlayerAbilities.EmpRadius;
            HeroVfx.Pulse(fx, HeroAt, radius, WorldCatalog.Neon, 0.4f);
            CombatVfx.Ring(fx, HeroAt, radius, WorldCatalog.Neon, 0.4f);
            foreach (var dummy in Within(HeroAt, radius)) dummy.Stun(PlayerAbilities.EmpStun);
            foreach (var bolt in bolts)
            {
                if (bolt == null) continue;
                HeroVfx.Sparks(fx, bolt.transform.position, WorldCatalog.Neon, 4, 2f, 0.2f);
                Destroy(bolt.gameObject);
            }
            yield return Wait(PlayerAbilities.EmpStun + 0.6f);
        }

        /// <summary>The chain as alternating links from the arm to the hook, then the hook's hub and prongs.</summary>
        private static void DrawGrapple(FlameMesh mesh, Vector2 arm, Vector2 hook, Vector2 facing, bool taut)
        {
            Color steel = new Color(0.7f, 0.75f, 0.85f), dark = new Color(0.35f, 0.38f, 0.46f), glow = new Color(0.35f, 1f, 0.78f);
            Vector2 span = hook - arm;
            float length = span.magnitude;
            Vector2 dir = length > 0.001f ? span / length : facing;
            int links = Mathf.FloorToInt(length / 0.16f);
            for (int i = 0; i < links; i++)
            {
                Vector2 at = arm + dir * (i + 0.5f) * 0.16f;
                if (i % 2 == 0) mesh.Ellipse(at, 0.09f, 0.045f, steel, dark, 8);
                else mesh.Bar(at - dir * 0.07f, dir, 0.14f, 0.03f, dark, steel);
            }
            if (taut && length > 0.1f) mesh.Bar(arm, dir, length, 0.02f, FlameMesh.Alpha(glow, 0.5f), FlameMesh.Alpha(glow, 0.1f));
            Vector2 across = Vector2.Perpendicular(facing);
            mesh.Disc(hook, 0.08f, steel, dark, 10);
            mesh.Triangle(hook + across * 0.05f, hook + facing * 0.22f, hook - across * 0.05f, steel, Color.white, steel);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 root = hook + facing * 0.05f + across * s * 0.05f, elbow = root + (facing * 0.1f + across * s * 0.12f), tip = elbow - facing * 0.08f + across * s * 0.02f;
                mesh.Bar(root, (elbow - root).normalized, Vector2.Distance(root, elbow), 0.04f, steel, steel);
                mesh.Bar(elbow, (tip - elbow).normalized, Vector2.Distance(elbow, tip), 0.035f, steel, Color.white);
            }
        }

        /// <summary>His hook flies out on its chain, snags the first enemy in line and hauls it to his feet.</summary>
        private IEnumerator GrappleArm()
        {
            Set(5.5f, new Vector2(-3.6f, 0f), new Vector2(2.4f, 0f), new Vector2(3.4f, 1.1f));
            yield return Wait(0.6f);
            Cast();
            Vector2 aim = Vector2.right, arm = HeroAt + aim * 0.3f, hook = arm;
            Face(aim);
            bool retracting = false;
            HeroVfx.Sparks(fx, HeroAt, new Color(0.35f, 1f, 0.78f), 5, 3f, 0.2f, aim, 50f, 0.7f);
            var chain = Draw(10f, 7, (mesh, age) => DrawGrapple(mesh, arm, hook, retracting ? -aim : aim, !retracting));
            var caught = dummies[0];
            // Out, until it bites.
            while (Vector2.Distance(hook, caught.Position) > GrappleHook.HookRadius + DummyRadius)
            {
                hook += aim * GrappleHook.Speed * Time.deltaTime;
                yield return null;
            }
            HeroVfx.Pulse(fx, hook, 0.45f, new Color(0.35f, 1f, 0.78f), 0.15f);
            HeroVfx.Sparks(fx, hook, new Color(0.7f, 0.75f, 0.85f), 8, 3.5f, 0.2f, -aim, 140f, 0.8f);
            caught.Flash();
            // Reeled in to a step in front of him.
            Vector2 goal = arm + aim * 0.8f;
            while (Vector2.Distance(caught.Position, goal) > 0.1f)
            {
                caught.Position = Vector2.MoveTowards(caught.Position, goal, GrappleHook.ReelSpeed * Time.deltaTime);
                hook = caught.Position;
                yield return null;
            }
            caught.Stun(1.2f);
            retracting = true;
            while (Vector2.Distance(hook, arm) > 0.1f)
            {
                hook = Vector2.MoveTowards(hook, arm, GrappleHook.RetractSpeed * Time.deltaTime);
                yield return null;
            }
            if (chain != null) Destroy(chain.gameObject);
            // Dragged into reach, it is his.
            yield return Wait(0.2f);
            Ray(Muzzle(aim), aim, CyborgAttack.RayRange, CyborgAttack.RayWidth);
            yield return Wait(1.6f);
        }

        /// <summary>A laser comes down from the sky and follows his aim across the pack, burning whatever it touches.</summary>
        private IEnumerator OrbitalLaserDemo()
        {
            Set(6f, new Vector2(-4.6f, -1.6f), new Vector2(-1f, -0.4f), new Vector2(1f, 0.6f), new Vector2(2.8f, -0.8f), new Vector2(0.6f, -2f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            Vector2 at = At(-2.6f, -0.8f);
            const float Life = 3f, SkyHeight = 9f;
            float radius = OrbitalLaser.Radius;
            Draw(Life + 0.25f, 12, (mesh, age) =>
            {
                float strength = age > Life ? Mathf.Clamp01(1f - (age - Life) / 0.25f) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.2f));
                float time = Time.time, shimmer = 1f + 0.08f * Mathf.Sin(time * 47f) + 0.05f * Mathf.Sin(time * 83f);
                // The burn spot on the floor, then three layers of one straight column: a soft glow, the plasma body and a white-hot core.
                mesh.Disc(at, radius * 1.25f, FlameMesh.Alpha(Plasma, 0.35f * strength), FlameMesh.Alpha(Plasma, 0f), 32);
                mesh.Ring(at, radius, 0.05f, FlameMesh.Alpha(PlasmaCore, 0.8f * strength), 40);
                Vector2 sky = at + Vector2.up * SkyHeight;
                foreach (var (half, centre, edge) in new[]
                {
                    (radius * 1.1f, FlameMesh.Alpha(Plasma, 0.22f * strength), FlameMesh.Alpha(Plasma, 0f)),
                    (radius * 0.55f, FlameMesh.Alpha(Plasma, 0.75f * strength), FlameMesh.Alpha(Plasma, 0.1f * strength)),
                    (radius * 0.22f, FlameMesh.Alpha(Color.white, strength), FlameMesh.Alpha(PlasmaCore, 0.5f * strength))
                })
                {
                    Vector2 side = Vector2.right * half * shimmer * strength;
                    mesh.Quad(at - side, sky - side, sky, at, edge, edge, centre, centre);
                    mesh.Quad(at, sky, sky + side, at + side, centre, centre, edge, edge);
                    mesh.Ellipse(at, side.x, side.x * 0.45f, centre, edge, 16);
                }
                // Energy rippling down the beam.
                for (int i = 0; i < 4; i++)
                {
                    Vector2 c = at + Vector2.up * Mathf.Repeat(-time * 14f + i * SkyHeight / 4f, SkyHeight);
                    float w = radius * 0.6f * strength;
                    mesh.Quad(c + new Vector2(-w, 0f), c + new Vector2(0f, 0.09f), c + new Vector2(w, 0f), c + new Vector2(0f, -0.09f),
                        FlameMesh.Alpha(PlasmaCore, 0f), FlameMesh.Alpha(Color.white, 0.5f * strength), FlameMesh.Alpha(PlasmaCore, 0f), FlameMesh.Alpha(Color.white, 0.5f * strength));
                }
            });
            float nextTick = 0f;
            int chasing = 0;
            for (float t = 0f; t < Life; t += Time.deltaTime)
            {
                // It sweeps from one enemy to the next, as his cursor would.
                Vector2 goal = dummies[chasing % dummies.Count].Position;
                at = Vector2.MoveTowards(at, goal, OrbitalLaser.Speed * 0.6f * Time.deltaTime);
                if (Vector2.Distance(at, goal) < 0.1f && t >= nextTick - 0.05f) chasing++;
                if (t >= nextTick)
                {
                    nextTick = t + OrbitalLaser.Tick;
                    HeroVfx.Sparks(fx, at, Color.Lerp(Plasma, Color.white, 0.4f), 2, 3.5f, 0.2f, null, 360f, 0.7f);
                    foreach (var dummy in Within(at, radius))
                    {
                        dummy.Hit(at, 0f);
                        dummy.Burn(1.5f);
                        HeroVfx.Sparks(fx, dummy.Position, PlasmaCore, 4, 3f, 0.2f, Vector2.up, 120f, 0.8f);
                    }
                }
                yield return null;
            }
            yield return Wait(1.2f);
        }
    }
}

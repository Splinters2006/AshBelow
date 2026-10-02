using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Knight's demos: his sword and shield, Shield Taunt and his six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static readonly Color KnightSlash = new Color(0.5f, 1f, 0.9f), ShieldBlue = new Color(0.4f, 0.75f, 1f);

        private IEnumerator Knight() => kind switch
        {
            DemoKind.Charged => KnightCharged(),
            DemoKind.Heavy => KnightParry(),
            DemoKind.Mechanic => ShieldTauntDemo(),
            _ => ability.Type switch
            {
                AbilityType.ShieldRush => ShieldRush(),
                AbilityType.Earthshatter => Earthshatter(),
                AbilityType.Aegis => Aegis(),
                AbilityType.ShieldThrow => ShieldThrow(),
                AbilityType.Whirlwind => WhirlwindDemo(),
                AbilityType.WarBanner => WarBannerDemo(),
                _ => null
            }
        };

        /// <summary>A sweep of the sword (or the Paladin's hammer): everything in the cone is struck.</summary>
        private void Swing(Vector2 aim, float reach, float cone, Color color, float knockback = 0.4f)
        {
            Face(aim);
            HeroVfx.Slash(fx, HeroAt, aim, reach, cone, color);
            foreach (var dummy in InCone(HeroAt, aim, reach, cone)) dummy.Hit(HeroAt, knockback);
        }

        /// <summary>A quick cut, then a held one: the cone widens to twice the sweep as the charge fills.</summary>
        private IEnumerator KnightCharged()
        {
            Set(4f, new Vector2(-1.2f, 0f), new Vector2(0.6f, 0f), new Vector2(0.2f, 1.3f), new Vector2(0.2f, -1.3f));
            yield return Wait(0.5f);
            Swing(Vector2.right, SwordAttack.Reach, SwordAttack.ConeAngle, KnightSlash);
            yield return Wait(0.9f);
            yield return Charge(AttackCharge.KnightChargeDuration, (mesh, charge) => Cone(mesh, HeroAt, Vector2.right, SwordAttack.Reach,
                Mathf.Lerp(SwordAttack.ConeAngle, 120f, charge), Color.Lerp(new Color(0.4f, 1f, 0.85f, 0.12f), new Color(1f, 0.8f, 0.25f, 0.3f), charge)));
            Swing(Vector2.right, SwordAttack.Reach, 120f, KnightSlash, 0.8f);
            yield return Wait(1.6f);
        }

        /// <summary>An enemy bolt flying at the hero from <paramref name="from"/>, stopping <paramref name="shortBy"/> units short of him.</summary>
        private IEnumerator Bolt(SpriteRenderer bolt, Vector2 from, float shortBy, float speed = 6.5f)
        {
            Vector2 toward = (HeroAt - from).normalized, goal = HeroAt - toward * shortBy;
            bolt.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg);
            while (bolt != null && Vector2.Distance(bolt.transform.position, goal) > 0.02f)
            {
                bolt.transform.position = Vector2.MoveTowards(bolt.transform.position, goal, speed * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>Right click: for a quarter of a second his shield turns a bolt straight back at whoever threw it.</summary>
        private IEnumerator KnightParry()
        {
            Set(4.5f, new Vector2(-2.2f, 0f), new Vector2(2.6f, 0f));
            yield return Wait(0.5f);
            for (int i = 0; i < 2; i++)
            {
                var shooter = dummies[0];
                shooter.Flash();
                var bolt = DungeonVisuals.CreateEmberBolt(fx, shooter.Position);
                CombatVfx.Trail(bolt.gameObject, new Color(1f, 0.5f, 0.15f, 0.7f), 0.18f, 0.16f);
                yield return Bolt(bolt, shooter.Position, 0.9f);
                // The shield comes up just as it arrives.
                HeroVfx.Pulse(fx, HeroAt, 0.95f, ShieldBlue, 0.25f);
                Draw(KnightShield.Duration, 5, (mesh, age) => Cone(mesh, HeroAt, Vector2.right, 0.9f, 120f, new Color(0.4f, 0.75f, 1f, 0.65f)));
                HeroVfx.Sparks(fx, bolt.transform.position, ShieldBlue, 8, 3.5f, 0.25f, Vector2.right, 100f);
                bolt.color = new Color(0.6f, 0.9f, 1f);
                Shoot(bolt, Vector2.right, 9f, 8f, 0.15f, false, (dummy, at) =>
                {
                    dummy.Hit(at + Vector2.left, 0.6f);
                    HeroVfx.Sparks(fx, at, ShieldBlue, 10, 4f, 0.3f);
                    HeroVfx.Pulse(fx, at, 0.6f, ShieldBlue, 0.2f);
                });
                yield return Wait(1.5f);
            }
        }

        /// <summary>R: a furious bellow raises a ward of rage; every enemy turns on him, and the bolts they throw are stopped.</summary>
        private IEnumerator ShieldTauntDemo()
        {
            Set(5f, new Vector2(0f, 0f), new Vector2(3.4f, 1f), new Vector2(-3.6f, 0.6f), new Vector2(2.6f, -2f), new Vector2(-2.8f, -1.8f));
            yield return Wait(0.5f);
            ShieldTauntVfx.Play(fx, hero.transform, ShieldTaunt.Duration, ShieldTaunt.Reach);
            HeroVfx.Sparks(fx, HeroAt, HeroBuffs.TauntColor, 18, 5f, 0.4f);
            TintHero(HeroBuffs.TauntColor, ShieldTaunt.Duration);
            float nextBolt = 0.3f;
            int thrower = 0;
            for (float t = 0f; t < ShieldTaunt.Duration; t += Time.deltaTime)
            {
                Advance(HeroAt, 1.4f);
                if (t >= nextBolt)
                {
                    nextBolt = t + 0.45f;
                    var shooter = dummies[thrower++ % dummies.Count];
                    var bolt = DungeonVisuals.CreateEmberBolt(fx, shooter.Position);
                    StartCoroutine(StoppedBolt(bolt, shooter.Position));
                }
                yield return null;
            }
            yield return Wait(1.2f);
        }

        /// <summary>A bolt that flies at the hero and dies on his ward (or bubble) <paramref name="reach"/> units out.</summary>
        private IEnumerator StoppedBolt(SpriteRenderer bolt, Vector2 from, float reach = ShieldTaunt.Reach * 0.8f)
        {
            Transform stage = fx;
            yield return Bolt(bolt, from, reach, 7.5f);
            if (bolt == null || stage == null) yield break;
            HeroVfx.Sparks(stage, bolt.transform.position, ShieldBlue, 8, 3.5f, 0.25f, (Vector2)bolt.transform.position - HeroAt, 100f);
            Destroy(bolt.gameObject);
        }

        /// <summary>He braces behind his shield and charges, smashing through everything in his path.</summary>
        private IEnumerator ShieldRush()
        {
            Set(5.5f, new Vector2(-4f, 0f), new Vector2(-1.6f, 0.2f), new Vector2(0f, -0.3f), new Vector2(1.4f, 0.25f));
            yield return Wait(0.6f);
            Cast();
            Vector2 aim = Vector2.right;
            var shield = Sprite("Rush shield", null, HeroAt, new Vector2(0.2f, 1.15f), AbilityCatalog.Ice, 7);
            var glow = DungeonVisuals.Create("Rush glow", shield.transform, HeroAt, new Vector2(2.4f, 1.3f), new Color(0.6f, 0.9f, 1f, 0.35f), 6);
            glow.transform.localPosition = Vector2.zero;
            HeroVfx.Pulse(fx, HeroAt, 1.2f, AbilityCatalog.Ice, 0.3f);
            var struck = new HashSet<Dummy>();
            float nextDust = 0f, travelled = 0f;
            yield return MoveHero(HeroAt + aim * KnightRelics.RushDistance, KnightRelics.RushDistance / KnightRelics.RushDuration, at =>
            {
                shield.transform.position = at + aim * 0.55f;
                travelled += Time.deltaTime;
                if (travelled >= nextDust)
                {
                    nextDust = travelled + 0.06f;
                    HeroVfx.Sparks(fx, at - aim * 0.3f + Vector2.down * 0.3f, Dust, 4, 2.2f, 0.3f, -aim, 110f, 0.8f);
                }
                foreach (var dummy in dummies)
                {
                    if (struck.Contains(dummy) || Vector2.Distance(at + aim * 0.3f, dummy.Position) > KnightRelics.RushWidth + DummyRadius) continue;
                    struck.Add(dummy);
                    dummy.Hit(at - aim + Vector2.up * (dummy.Position.y > at.y ? -0.6f : 0.6f), 1.3f);
                    dummy.Chill(1.2f);
                    HeroVfx.Pulse(fx, dummy.Position, 1f, AbilityCatalog.Ice, 0.25f);
                }
            });
            Destroy(shield.gameObject);
            CombatVfx.Ring(fx, HeroAt, 1.1f, AbilityCatalog.Ice, 0.35f);
            yield return Wait(1.6f);
        }

        /// <summary>He stomps: five rings crack the ground outward, striking and slowing each enemy as they reach it.</summary>
        private IEnumerator Earthshatter()
        {
            Set(5f, new Vector2(0f, 0f), new Vector2(1.3f, 0.5f), new Vector2(-2f, 0.9f), new Vector2(2.6f, -1.2f), new Vector2(-1.2f, -1.8f));
            yield return Wait(0.6f);
            Cast();
            float radius = PlayerAbilities.EarthshatterRadius;
            QuakeVfx.Play(fx, HeroAt, radius, KnightRelics.QuakeRings, KnightRelics.QuakeInterval, AbilityCatalog.Gold);
            var struck = new HashSet<Dummy>();
            for (int ring = 1; ring <= KnightRelics.QuakeRings; ring++)
            {
                float r = radius * ring / KnightRelics.QuakeRings;
                for (int i = 0; i < 5; i++)
                    HeroVfx.Sparks(fx, HeroAt + FlameMesh.Polar(Random.value * Mathf.PI * 2f, r), Dust, 3, 2.4f, 0.3f, Vector2.up, 90f, 0.8f);
                foreach (var dummy in Within(HeroAt, r))
                {
                    if (!struck.Add(dummy)) continue;
                    dummy.Hit(HeroAt, 0.4f);
                    dummy.Chill(2f);
                }
                yield return Wait(KnightRelics.QuakeInterval);
            }
            yield return Wait(2f);
        }

        /// <summary>A shimmering bubble closes round him: for two seconds nothing that is thrown at him gets through.</summary>
        private IEnumerator Aegis()
        {
            Set(4.5f, new Vector2(0f, 0f), new Vector2(3.2f, 0.8f), new Vector2(-3.2f, -0.6f));
            yield return Wait(0.5f);
            Cast();
            HolyBubble.Wrap(fx, hero.transform, 2f, AbilityCatalog.Ice);
            for (int i = 0; i < 4; i++)
            {
                yield return Wait(0.4f);
                var shooter = dummies[i % dummies.Count];
                shooter.Flash();
                StartCoroutine(StoppedBolt(DungeonVisuals.CreateEmberBolt(fx, shooter.Position), shooter.Position, 0.85f));
            }
            yield return Wait(1.4f);
        }

        /// <summary>His shield spins out, rings off three enemies in turn and flies back to his arm.</summary>
        private IEnumerator ShieldThrow()
        {
            Set(5f, new Vector2(-3.4f, -0.6f), new Vector2(0.4f, 0f), new Vector2(2.6f, 1.6f), new Vector2(3.2f, -1.4f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            var shield = Sprite("Thrown shield", ThrownShield.ShieldSprite, HeroAt, Vector2.one * 0.62f, Color.white, 7);
            var glow = DungeonVisuals.Create("Shield glow", shield.transform, HeroAt, Vector2.one * 1.8f, FlameMesh.Alpha(AbilityCatalog.Ice, 0.35f), 6);
            glow.sprite = DungeonVisuals.GlowSprite;
            CombatVfx.Trail(shield.gameObject, new Color(0.5f, 0.85f, 1f, 0.6f), 0.3f, 0.18f);
            HeroVfx.Sparks(fx, HeroAt, AbilityCatalog.Ice, 6, 3f, 0.2f, Vector2.right, 60f, 0.8f);
            float age = 0f;
            for (int stop = 0; stop <= dummies.Count; stop++)
            {
                bool home = stop == dummies.Count;
                Vector2 goal = home ? HeroAt : dummies[stop].Position, before = shield.transform.position;
                while (Vector2.Distance(shield.transform.position, goal) > (home ? 0.4f : DummyRadius))
                {
                    age += Time.deltaTime;
                    shield.transform.rotation = Quaternion.Euler(0f, 0f, age * 720f);
                    shield.transform.position = Vector2.MoveTowards(shield.transform.position, goal, ThrownShield.Speed * Time.deltaTime);
                    yield return null;
                }
                Vector2 at = shield.transform.position;
                if (home) break;
                // It rings off the target: a steel flash, sparks thrown back along its path and a bright clang ring.
                dummies[stop].Hit(before, 0.6f);
                HeroVfx.Pulse(fx, at, 0.7f, FlameMesh.Alpha(AbilityCatalog.Ice, 0.8f), 0.18f);
                HeroVfx.Sparks(fx, at, Color.white, 6, 4.5f, 0.2f, before - at, 120f, 0.8f);
                HeroVfx.Sparks(fx, at, AbilityCatalog.Ice, 10, 4f, 0.3f);
                CombatVfx.Ring(fx, at, 0.45f, Color.white, 0.15f);
            }
            HeroVfx.Pulse(fx, HeroAt, 0.8f, AbilityCatalog.Ice, 0.2f);
            HeroVfx.Sparks(fx, HeroAt, AbilityCatalog.Ice, 5, 2f, 0.2f);
            Destroy(shield.gameObject);
            yield return Wait(1.4f);
        }

        /// <summary>He spins with his sword for two seconds, walking through the pack as he goes.</summary>
        private IEnumerator WhirlwindDemo()
        {
            Set(5f, new Vector2(-3f, 0f), new Vector2(-0.8f, 0.9f), new Vector2(0.2f, -0.8f), new Vector2(1.6f, 0.6f), new Vector2(2.4f, -0.9f));
            yield return Wait(0.5f);
            Cast();
            WhirlwindVfx.Play(fx, hero.transform, Whirlwind.Duration, Whirlwind.Radius);
            float nextTick = 0f;
            for (float t = 0f; t < Whirlwind.Duration; t += Time.deltaTime)
            {
                HeroAt += Vector2.right * 2.6f * Time.deltaTime;
                if (t >= nextTick)
                {
                    nextTick += Whirlwind.Tick;
                    foreach (var dummy in Within(HeroAt, Whirlwind.Radius))
                    {
                        dummy.Hit(HeroAt, 0.2f);
                        HeroVfx.Sparks(fx, dummy.Position, Color.white, 3, 3f, 0.15f, dummy.Position - HeroAt, 90f, 0.6f);
                    }
                }
                yield return null;
            }
            yield return Wait(1.2f);
        }

        /// <summary>He plants a banner: inside its wide circle his blows land harder.</summary>
        private IEnumerator WarBannerDemo()
        {
            Set(6.5f, new Vector2(-0.6f, -0.3f), new Vector2(1.3f, 0f), new Vector2(1.1f, 1.4f), new Vector2(1.2f, -1.5f));
            yield return Wait(0.5f);
            Cast();
            WarBannerVfx.Play(fx, HeroAt, 4.2f, WarBanner.Radius);
            TintHero(new Color(0.9f, 0.2f, 0.25f), 4f);
            yield return Wait(1.1f);
            for (int i = 0; i < 4; i++)
            {
                Swing(Quaternion.Euler(0, 0, i % 2 == 0 ? 25f : -25f) * Vector2.right, SwordAttack.Reach, SwordAttack.ConeAngle, KnightSlash, 0.2f);
                foreach (var dummy in InCone(HeroAt, Vector2.right, SwordAttack.Reach, 120f)) HeroVfx.Sparks(fx, dummy.Position, AbilityCatalog.Gold, 5, 3f, 0.25f);
                yield return Wait(0.5f);
            }
            yield return Wait(1.4f);
        }
    }
}

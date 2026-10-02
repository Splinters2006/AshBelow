using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Brawler's demos: her fists and Empower, Super Angry and her six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static Color Glove => BrawlerAttack.Glove;

        private IEnumerator Brawler() => kind switch
        {
            DemoKind.Charged => Barrage(),
            DemoKind.Heavy => Empower(),
            DemoKind.Mechanic => SuperAngryDemo(),
            _ => ability.Type switch
            {
                AbilityType.KnuckleSandwich => KnuckleSandwich(),
                AbilityType.WildLeap => WildLeap(),
                AbilityType.PrimalRage => PrimalRage(),
                AbilityType.ThunderClap => ThunderClap(),
                AbilityType.HaymakerDash => HaymakerDash(),
                AbilityType.Suplex => Suplex(),
                _ => null
            }
        };

        /// <summary>One punch down a rectangle ahead of her.</summary>
        private void Punch(Vector2 aim, float length, float halfWidth, Color color, float knockback = 0.3f, float duration = 0.16f, Vector2 lane = default)
        {
            Face(aim);
            BrawlerVfx.Punch(fx, HeroAt + lane, aim, length, halfWidth, color, duration);
            foreach (var dummy in InLane(HeroAt, aim, length, halfWidth + lane.magnitude)) dummy.Hit(HeroAt, knockback);
        }

        /// <summary>A string of quick jabs at <paramref name="scale"/> times her usual size.</summary>
        private IEnumerator Jabs(int count, Color color, float scale = 1f, float interval = 0.2f)
        {
            for (int i = 0; i < count; i++)
            {
                Punch(Vector2.right, BrawlerAttack.JabLength * scale, BrawlerAttack.JabHalfWidth * scale, color, 0.1f);
                yield return Wait(interval);
            }
        }

        /// <summary>A slow full charge: the rectangle swells as it fills, then a barrage of punches and a finisher that sends them flying.</summary>
        private IEnumerator Barrage()
        {
            Set(4.5f, new Vector2(-1.6f, 0f), new Vector2(0f, 0.3f), new Vector2(0.2f, -0.5f));
            yield return Wait(0.4f);
            yield return Charge(BrawlerAttack.ChargeDuration, (mesh, charge) => Lane(mesh, HeroAt, Vector2.right,
                Mathf.Lerp(BrawlerAttack.JabLength, BrawlerAttack.BarrageLength, charge), Mathf.Lerp(BrawlerAttack.JabHalfWidth, BrawlerAttack.BarrageHalfWidth, charge), ChargeFill(charge, Glove)));
            int count = BrawlerAttack.BarragePunches;
            float length = BrawlerAttack.BarrageLength, halfWidth = BrawlerAttack.BarrageHalfWidth;
            var flurry = BrawlerVfx.Flurry(fx, hero.transform, () => Vector2.right, length, halfWidth, Glove, count * 0.07f + 0.15f);
            for (int i = 0; i < count; i++)
            {
                bool finisher = i == count - 1;
                float fist = finisher ? halfWidth : 0.28f;
                Vector2 lane = Vector2.up * (finisher ? 0f : Random.Range(-1f, 1f) * (halfWidth - fist));
                Punch(Vector2.right, length, fist, finisher ? Color.Lerp(Glove, Color.white, 0.3f) : Glove, finisher ? 1.4f : 0.02f, finisher ? 0.22f : 0.1f, lane);
                if (finisher)
                {
                    HeroVfx.Pulse(fx, HeroAt + Vector2.right * length, halfWidth, Glove, 0.3f);
                    HeroVfx.Sparks(fx, HeroAt + Vector2.right * length, Color.Lerp(Glove, Color.white, 0.4f), 14, 5f, 0.35f, Vector2.right, 120f, 1.2f);
                }
                yield return Wait(0.07f);
            }
            if (flurry != null) flurry.Stop();
            yield return Wait(1.6f);
        }

        /// <summary>Right click: for a few seconds she hits harder, faster and wider.</summary>
        private IEnumerator Empower()
        {
            Set(4.5f, new Vector2(-1.4f, 0f), new Vector2(0f, 0.2f), new Vector2(0.6f, -0.4f));
            yield return Wait(0.4f);
            yield return Jabs(3, Glove);
            yield return Wait(0.4f);
            Color color = HeroBuffs.EmpowerColor;
            HeroVfx.Pulse(fx, HeroAt, 1.3f, color, 0.4f);
            HeroVfx.Sparks(fx, HeroAt, color, 12, 4f, 0.35f);
            CombatVfx.Ring(fx, HeroAt, 0.8f, color);
            TintHero(color, 2.6f);
            yield return Wait(0.5f);
            yield return Jabs(9, color, 1.1f, 0.14f);
            yield return Wait(1.2f);
        }

        /// <summary>R: fury takes her; her punches reach half as far again and come faster than she can count.</summary>
        private IEnumerator SuperAngryDemo()
        {
            Set(5f, new Vector2(-1.6f, 0f), new Vector2(0.4f, 0.3f), new Vector2(0.9f, -0.6f), new Vector2(1.4f, 0.9f));
            yield return Wait(0.5f);
            const float Life = 3.4f;
            SuperAngryVfx.Play(fx, hero.transform, Life);
            HeroVfx.Pulse(fx, HeroAt, 2.6f, HeroBuffs.FuryColor, 0.5f);
            HeroVfx.Sparks(fx, HeroAt, HeroBuffs.FuryColor, 26, 6f, 0.5f);
            CombatVfx.Ring(fx, HeroAt, 1.8f, Color.white, 0.4f);
            TintHero(HeroBuffs.FuryColor, Life);
            yield return Wait(0.7f);
            yield return Jabs(16, HeroBuffs.FuryColor, 1.5f, 0.14f);
            yield return Wait(1.2f);
        }

        /// <summary>She draws the fist back while the target area glows, then lunges into one crushing blow.</summary>
        private IEnumerator KnuckleSandwich()
        {
            Set(5.5f, new Vector2(-2.6f, 0f), new Vector2(-0.6f, 0.5f), new Vector2(-0.2f, -0.6f), new Vector2(0.4f, 0.1f));
            yield return Wait(0.5f);
            Cast();
            Vector2 aim = Vector2.right;
            Face(aim);
            const float Length = 3.2f, HalfWidth = 1.1f;
            Color color = Color.Lerp(Glove, AbilityCatalog.Gold, 0.35f);
            var windup = BrawlerVfx.Windup(fx, hero.transform, aim, Length, HalfWidth, color, BrawlerAttack.SandwichWindup + 0.05f);
            for (float t = 0f; t < BrawlerAttack.SandwichWindup; t += Time.deltaTime)
            {
                if (Random.value < 0.35f) HeroVfx.Sparks(fx, HeroAt - aim * 0.5f, color, 2, 1.6f, 0.2f, aim, 120f, 0.7f);
                yield return null;
            }
            if (windup != null) windup.Stop();
            // A short lunge into the blow.
            HeroAt += aim * 0.4f;
            Vector2 from = HeroAt, impact = from + aim * Length;
            foreach (var dummy in InLane(from, aim, Length, HalfWidth)) dummy.Hit(from, 2.2f);
            BrawlerVfx.HeavyPunch(fx, from, aim, Length, HalfWidth, color);
            HeroVfx.Pulse(fx, impact, HalfWidth * 1.6f, color, 0.45f);
            HeroVfx.Pulse(fx, from + aim * Length * 0.5f, Length * 0.6f, Color.white, 0.2f);
            CombatVfx.Ring(fx, impact, HalfWidth * 1.4f, Color.white, 0.35f);
            HeroVfx.Sparks(fx, impact, color, 30, 8f, 0.5f, aim, 100f, 1.6f);
            for (int i = 1; i <= 3; i++)
                HeroVfx.Sparks(fx, from + aim * Length * i / 4f, Dust, 4, 3f, 0.35f, Vector2.up, 360f, 0.9f);
            yield return Wait(2f);
        }

        /// <summary>She pounces across the room onto an enemy and slams down; the farther the leap, the wider the slam.</summary>
        private IEnumerator WildLeap()
        {
            Set(6f, new Vector2(-4.4f, -0.6f), new Vector2(2.2f, -0.4f), new Vector2(3.2f, 0.6f), new Vector2(3f, -1.5f));
            yield return Wait(0.6f);
            Cast();
            Vector2 from = HeroAt, landing = dummies[0].Position + Vector2.left * (DummyRadius + 0.3f);
            Face(landing - from);
            HeroVfx.Sparks(fx, from + Vector2.down * 0.3f, new Color(0.7f, 0.66f, 0.6f, 0.8f), 8, 2.6f, 0.3f, from - landing, 120f, 0.9f);
            yield return Arc(hero.transform, from, landing, BrawlerAttack.LeapDuration, BrawlerAttack.LeapHeight);
            float radius = BrawlerAttack.SlamRadius(Vector2.Distance(from, landing), 0);
            CombatVfx.Ring(fx, landing, radius, AbilityCatalog.Gold, 0.5f);
            HeroVfx.Pulse(fx, landing, radius, AbilityCatalog.Gold, 0.45f);
            HeroVfx.Sparks(fx, landing, Dust, 20, 5f, 0.45f);
            foreach (var dummy in Within(landing, radius))
            {
                dummy.Hit(landing, 1.2f);
                dummy.Chill(1f);
            }
            yield return Wait(2f);
        }

        /// <summary>Ten seconds of rage: bigger, faster, harder punches. Then she is spent.</summary>
        private IEnumerator PrimalRage()
        {
            Set(4.5f, new Vector2(-1.4f, 0f), new Vector2(0f, 0.2f), new Vector2(0.6f, -0.4f));
            yield return Wait(0.5f);
            Cast();
            Color rage = HeroBuffs.RageColor;
            HeroVfx.Pulse(fx, HeroAt, 2f, rage, 0.5f);
            HeroVfx.Sparks(fx, HeroAt, rage, 22, 5f, 0.45f);
            CombatVfx.Ring(fx, HeroAt, 1.4f, rage, 0.5f);
            PrimalRageVfx.Play(fx, hero.transform, 2.8f);
            TintHero(rage, 2.8f);
            yield return Wait(0.6f);
            yield return Jabs(12, rage, 1f, 0.12f);
            yield return Wait(0.3f);
            // Tired: grey, and slow.
            TintHero(HeroBuffs.TiredColor, 1.6f);
            yield return Jabs(2, HeroBuffs.TiredColor, 1f, 0.6f);
            yield return Wait(0.6f);
        }

        /// <summary>She claps: a shockwave rolls far out in a narrow cone, knocking back and stunning what it passes.</summary>
        private IEnumerator ThunderClap()
        {
            Set(6f, new Vector2(-4.4f, 0f), new Vector2(-2.2f, 0.3f), new Vector2(-0.6f, -0.6f), new Vector2(0.4f, 0.9f));
            yield return Wait(0.6f);
            Cast();
            Vector2 origin = HeroAt, aim = Vector2.right;
            Face(aim);
            Color clap = new Color(0.8f, 0.9f, 1f);
            BrawlerVfx.Move(fx, PunchVfx.Style.Clap, origin, aim, BrawlerAttack.ClapRange, BrawlerAttack.ClapCone * 0.5f, clap);
            // The same timing and easing as the wave drawn by the effect, so enemies are struck as the front passes them.
            float travel = BrawlerVfx.ClapTime * PunchVfx.ClapTravel;
            var struck = new HashSet<Dummy>();
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(t / travel), front = BrawlerAttack.ClapRange * (1f - (1f - progress) * (1f - progress));
                foreach (var dummy in InCone(origin, aim, front, BrawlerAttack.ClapCone))
                {
                    if (!struck.Add(dummy)) continue;
                    dummy.Hit(origin, 1.8f);
                    dummy.Stun(BrawlerAttack.ClapStun);
                    HeroVfx.Sparks(fx, dummy.Position, clap, 6, 3f, 0.2f, dummy.Position - origin, 80f, 0.8f);
                }
                if (progress >= 1f) break;
                yield return null;
            }
            yield return Wait(1.8f);
        }

        /// <summary>She dashes in and uppercuts the first enemy: it sails over in an arc and crashes into the ones behind.</summary>
        private IEnumerator HaymakerDash()
        {
            Set(6f, new Vector2(-4.4f, 0f), new Vector2(-1f, 0f), new Vector2(1.9f, 0.4f), new Vector2(2.2f, -0.5f));
            yield return Wait(0.6f);
            Cast();
            Vector2 aim = Vector2.right, from = HeroAt;
            var victim = dummies[0];
            yield return MoveHero(victim.Position - aim * (DummyRadius + 0.6f), 18f,
                at => HeroVfx.Sparks(fx, at + Vector2.down * 0.3f, Dust, 1, 1.5f, 0.3f, -aim, 70f, 0.8f));
            BrawlerVfx.Move(fx, PunchVfx.Style.Dash, from, aim, Vector2.Distance(from, HeroAt), 0.35f, Glove);
            // The uppercut: the glove scoops up through the victim; the blow lands when it connects.
            BrawlerVfx.Move(fx, PunchVfx.Style.Uppercut, victim.Position, aim, 1f, 0.6f, Glove);
            yield return Wait(BrawlerVfx.UppercutTime * PunchVfx.UppercutImpact);
            victim.Stun(BrawlerAttack.LaunchTime + 0.4f);
            Vector2 start = victim.Position, landing = start + aim * BrawlerAttack.HaymakerLaunch;
            yield return Arc(victim.Body.transform, start, landing, BrawlerAttack.LaunchTime, BrawlerAttack.LaunchHeight);
            HeroVfx.Pulse(fx, landing, BrawlerAttack.HaymakerSplash, Glove, 0.3f);
            HeroVfx.Sparks(fx, landing, Dust, 14, 4f, 0.35f, null, 360f, 1f);
            foreach (var dummy in Within(landing, BrawlerAttack.HaymakerSplash))
                if (dummy != victim) dummy.Hit(landing, 1.2f);
            yield return Wait(1.8f);
        }

        /// <summary>She grabs the nearest enemy, heaves it over and slams it down on the others.</summary>
        private IEnumerator Suplex()
        {
            Set(5f, new Vector2(-0.6f, 0f), new Vector2(-1.5f, 0.2f), new Vector2(1.4f, 0.4f), new Vector2(1.8f, -0.5f));
            yield return Wait(0.6f);
            Cast();
            var victim = dummies[0];
            Face(Vector2.right);
            Vector2 start = victim.Position, landing = HeroAt + Vector2.right * BrawlerAttack.SuplexThrow;
            yield return Arc(victim.Body.transform, start, landing, BrawlerAttack.SuplexTime, 1.4f);
            Color color = new Color(0.85f, 0.7f, 0.5f);
            CombatVfx.Ring(fx, landing, BrawlerAttack.SuplexRadius, color, 0.4f);
            HeroVfx.Sparks(fx, landing, color, 20, 5f, 0.4f);
            foreach (var dummy in Within(landing, BrawlerAttack.SuplexRadius)) dummy.Hit(landing, dummy == victim ? 0f : 1.2f);
            victim.Stun(0.5f);
            yield return Wait(1.8f);
        }
    }
}

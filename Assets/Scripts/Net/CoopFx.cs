using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Mirrors a local hero's attack effects to teammates. Call sites keep drawing their own effect as before and
    /// add one of these calls; teammates replay a cosmetic copy that never deals damage.
    /// </summary>
    public static class CoopFx
    {
        private static void Send(DungeonRun run, FxKind kind, Vector2 a, Vector2 b = default, Color? color = null, float f1 = 0f, float f2 = 0f, int n = 0)
        {
            if (run == null || !run.IsNetworked) return;
            run.Coop.SendFx(new FxMessage { Kind = kind, A = a, B = b, Color = color ?? Color.white, F1 = f1, F2 = f2, N = n });
        }

        public static void Arrow(DungeonRun run, Vector2 position, Vector2 direction, float range) => Send(run, FxKind.Arrow, position, direction, null, range);
        public static void Coin(DungeonRun run, Vector2 position, Vector2 direction, float range) => Send(run, FxKind.Coin, position, direction, null, range);
        public static void Spell(DungeonRun run, Vector2 position, Vector2 direction, Color color, float range, float radius, int pierces)
            => Send(run, FxKind.Spell, position, direction, color, range, radius, pierces);
        public static void Slash(DungeonRun run, Vector2 center, Vector2 aim, float reach, float cone, Color color)
            => Send(run, FxKind.Slash, center, aim, color, reach, cone);
        public static void Bolt(DungeonRun run, Vector2 from, Vector2 to, Color color, bool glow = false)
            => Send(run, glow ? FxKind.GlowBolt : FxKind.Bolt, from, to, color);
        public static void Ring(DungeonRun run, Vector2 center, float radius, Color color, float duration = 0.4f)
            => Send(run, FxKind.Ring, center, default, color, radius, duration);
        public static void Pulse(DungeonRun run, Vector2 center, float radius, Color color, float duration = 0.35f)
            => Send(run, FxKind.Pulse, center, default, color, radius, duration);
        public static void Rift(DungeonRun run, Vector2 from, Vector2 to, float strength) => Send(run, FxKind.Rift, from, to, null, strength);
        public static void Execution(DungeonRun run, Vector2 center, float radius) => Send(run, FxKind.Execution, center, default, null, radius);
        public static void Singularity(DungeonRun run, Vector2 center, float radius, float duration = 1.1f)
            => Send(run, FxKind.Singularity, center, default, null, radius, duration);

        public static void Punch(DungeonRun run, Vector2 origin, Vector2 aim, float length, float halfWidth, Color color)
            => Send(run, FxKind.Punch, origin, aim, color, length, halfWidth);

        public static void Knife(DungeonRun run, Vector2 position, Vector2 direction, float range) => Send(run, FxKind.Knife, position, direction, null, range);
        public static void PiercingShot(DungeonRun run, Vector2 origin, Vector2 direction, DamageElement infusion)
            => Send(run, FxKind.PiercingShot, origin, direction, null, 0f, 0f, (int)infusion);
        public static void Aegis(DungeonRun run, float duration) => Send(run, FxKind.Aegis, default, default, null, duration);
        public static void Holy(DungeonRun run, Vector2 center, float radius, float windup) => Send(run, FxKind.Holy, center, default, null, radius, windup);
        /// <summary>
        /// Sanctuary is more than a picture: every machine plants a real bubble that follows the Paladin, since each
        /// judges its own bolts.
        /// </summary>
        public static void Sanctuary(DungeonRun run, Vector2 center, float radius, float duration) => Send(run, FxKind.Sanctuary, center, default, null, radius, duration);
        public static void SanctuaryEnd(DungeonRun run) => Send(run, FxKind.SanctuaryEnd, default);
        public static void Windstep(DungeonRun run, Vector2 from, Vector2 to) => Send(run, FxKind.Windstep, from, to);
        public static void Shadowstep(DungeonRun run, Vector2 from, Vector2 to) => Send(run, FxKind.Shadowstep, from, to);
        public static void Stab(DungeonRun run, Vector2 origin, Vector2 aim, float reach, Color color) => Send(run, FxKind.Stab, origin, aim, color, reach);
        public static void Quake(DungeonRun run, Vector2 center, float radius, int rings, float interval, Color color)
            => Send(run, FxKind.Quake, center, default, color, radius, interval, rings);
        public static void HolySword(DungeonRun run, Vector2 target) => Send(run, FxKind.HolySword, target);
        public static void Venom(DungeonRun run, Vector2 from, Vector2 landing, float radius, float duration)
            => Send(run, FxKind.Venom, from, landing, null, radius, duration);
        public static void HeavyPunch(DungeonRun run, Vector2 origin, Vector2 aim, float length, float halfWidth, Color color)
            => Send(run, FxKind.HeavyPunch, origin, aim, color, length, halfWidth);
        /// <summary>The barrage blur follows the teammate who threw it; its duration travels in milliseconds.</summary>
        public static void Flurry(DungeonRun run, float length, float halfWidth, Color color, float duration)
            => Send(run, FxKind.Flurry, default, default, color, length, halfWidth, Mathf.RoundToInt(duration * 1000f));

        public static void TailStab(DungeonRun run, Vector2 origin, Vector2 aim, float reach, Color color)
            => Send(run, FxKind.TailStab, origin, aim, color, reach);
        public static void TailSweep(DungeonRun run, Vector2 origin, Vector2 aim, float reach, float cone, Color color)
            => Send(run, FxKind.TailSweep, origin, aim, color, reach, cone);
        public static void Pentagram(DungeonRun run, Vector2 center, float radius, float windup)
            => Send(run, FxKind.Pentagram, center, default, null, radius, windup);
        /// <summary>Teammates see the paw land where it was aimed when cast (their copy does not track the target).</summary>
        public static void DemonPaw(DungeonRun run, Vector2 center, float radius, float windup)
            => Send(run, FxKind.DemonPaw, center, default, null, radius, windup);
        public static void DemonHead(DungeonRun run, Vector2 center, float radius) => Send(run, FxKind.DemonHead, center, default, null, radius);
        public static void Sharpen(DungeonRun run, int bonus, float duration, bool full)
            => Send(run, FxKind.Sharpen, default, default, null, duration, full ? 1f : 0f, bonus);
        public static void CoinFlip(DungeonRun run, bool won) => Send(run, FxKind.CoinFlip, default, default, null, 0f, 0f, won ? 1 : 0);
        public static void CoinRain(DungeonRun run, Vector2 center) => Send(run, FxKind.CoinRain, center);
        public static void Angel(DungeonRun run, Vector2 target) => Send(run, FxKind.Angel, target);
        public static void Jackpot(DungeonRun run, GamblerAttack.JackpotPrize prize) => Send(run, FxKind.Jackpot, default, default, null, 0f, 0f, (int)prize);

        public static void PlasmaRay(DungeonRun run, Vector2 from, Vector2 to, Color color, float width)
            => Send(run, FxKind.PlasmaRay, from, to, color, width);
        public static void CannonShot(DungeonRun run, Vector2 origin, Vector2 direction, float radius, float charge)
            => Send(run, FxKind.PlasmaOrb, origin, direction, null, radius, charge);
        public static void Turret(DungeonRun run, Vector2 position, float duration) => Send(run, FxKind.SentryTurret, position, default, null, duration);
        /// <summary>Teammates see the missile home in on where its target stood at launch.</summary>
        public static void Missile(DungeonRun run, Vector2 origin, Vector2 direction, Vector2 goal)
            => Send(run, FxKind.MicroMissile, origin, direction, null, goal.x, goal.y);

        private static RemoteHero FindHero(DungeonRun run, ulong id)
        {
            foreach (var hero in run.Coop.RemoteHeroes) if (hero != null && hero.Id == id) return hero;
            return null;
        }

        public static void RearHit(DungeonRun run, Vector2 position, Vector2 facing, float hitRadius)
            => Send(run, FxKind.RearHit, position, facing, null, hitRadius);

        public static void Play(DungeonRun run, FxMessage fx)
        {
            var root = run.ProjectileRoot;
            Color color = fx.Color;
            switch (fx.Kind)
            {
                case FxKind.Arrow: PlayerProjectile.SpawnGhost(run, fx.A, fx.B, fx.F1); break;
                case FxKind.Coin: PlayerProjectile.SpawnGhost(run, fx.A, fx.B, fx.F1, ProjectileStyle.Coin); break;
                case FxKind.Spell: SpellProjectile.SpawnGhost(run, fx.A, fx.B, color, fx.F1, fx.F2, fx.N); break;
                case FxKind.Slash: HeroVfx.Slash(root, fx.A, fx.B, fx.F1, fx.F2, color); break;
                case FxKind.Bolt: CombatVfx.Bolt(root, fx.A, fx.B, color); break;
                case FxKind.GlowBolt: CombatVfx.GlowBolt(root, fx.A, fx.B, color); break;
                case FxKind.Ring: CombatVfx.Ring(root, fx.A, fx.F1, color, fx.F2); break;
                case FxKind.Pulse: HeroVfx.Pulse(root, fx.A, fx.F1, color, fx.F2); break;
                case FxKind.Rift: ShadowVfx.Rift(root, fx.A, fx.B, fx.F1); break;
                case FxKind.Execution: ShadowVfx.Execution(root, fx.A, fx.F1); break;
                case FxKind.Singularity: ShadowVfx.Singularity(root, fx.A, fx.F1, fx.F2); break;
                case FxKind.Punch: BrawlerVfx.Punch(root, fx.A, fx.B, fx.F1, fx.F2, color); break;
                case FxKind.RearHit: RearHitMarker.Draw(root, fx.A, fx.B, fx.F1); break;
                case FxKind.Knife:
                    // The ghost blade flies home to the teammate who threw it.
                    var thrower = FindHero(run, fx.Origin);
                    if (thrower != null) ReturningKnife.SpawnGhost(run, thrower.transform, fx.A, fx.B, fx.F1);
                    break;
                case FxKind.PiercingShot: PiercingArrow.SpawnGhost(run, fx.A, fx.B, (DamageElement)fx.N); break;
                case FxKind.Aegis:
                    var knight = FindHero(run, fx.Origin);
                    if (knight != null) HolyBubble.Wrap(root, knight.transform, fx.F1, AbilityCatalog.Ice);
                    break;
                case FxKind.Holy: HolyLightVfx.Play(root, fx.A, fx.F1, fx.F2); break;
                case FxKind.Sanctuary: HolyBubble.Sanctuary(run, fx.A, fx.F1, fx.F2, FindHero(run, fx.Origin)?.transform); break;
                case FxKind.SanctuaryEnd:
                    var paladin = FindHero(run, fx.Origin);
                    if (paladin != null) HolyBubble.EndFollowing(paladin.transform);
                    break;
                case FxKind.Windstep: WindstepVfx.Play(root, fx.A, fx.B); break;
                case FxKind.Shadowstep: ShadowstepVfx.Play(root, fx.A, fx.B); break;
                case FxKind.Stab: StabVfx.Play(root, fx.A, fx.B, fx.F1, color); break;
                case FxKind.Quake: QuakeVfx.Play(root, fx.A, fx.F1, fx.N, fx.F2, color); break;
                case FxKind.HolySword: HolySwordVfx.Play(root, fx.A); break;
                case FxKind.Venom: VenomVial.SpawnGhost(run, fx.A, fx.B, fx.F1, fx.F2); break;
                case FxKind.HeavyPunch: BrawlerVfx.HeavyPunch(root, fx.A, fx.B, fx.F1, fx.F2, color); break;
                case FxKind.TailStab: TailVfx.Stab(root, fx.A, fx.B, fx.F1, color); break;
                case FxKind.TailSweep: TailVfx.Sweep(root, fx.A, fx.B, fx.F1, fx.F2, color); break;
                case FxKind.Pentagram: PentagramVfx.Play(root, fx.A, fx.F1, fx.F2); break;
                case FxKind.DemonPaw: DemonPawVfx.Play(root, fx.A, fx.F1, fx.F2); break;
                case FxKind.DemonHead: DemonHeadVfx.Play(root, fx.A, fx.F1); break;
                case FxKind.CoinFlip:
                    var gambler = FindHero(run, fx.Origin);
                    if (gambler != null) CoinFlipVfx.Play(root, gambler.transform, fx.N == 1);
                    break;
                case FxKind.CoinRain: CoinRainVfx.Play(root, fx.A); break;
                case FxKind.Angel: AngelVfx.Play(root, fx.A); break;
                case FxKind.Jackpot:
                    var winner = FindHero(run, fx.Origin);
                    if (winner != null) JackpotVfx.Play(root, winner.transform, (GamblerAttack.JackpotPrize)fx.N);
                    break;
                case FxKind.Sharpen:
                    var assassin = FindHero(run, fx.Origin);
                    if (assassin != null)
                    {
                        SharpenVfx.Play(root, assassin.transform, fx.F2 > 0.5f);
                        KeenEdgeAura.Show(root, assassin.transform, fx.N, fx.F1);
                    }
                    break;
                case FxKind.PlasmaRay: CyborgVfx.Ray(root, fx.A, fx.B, color, fx.F1); break;
                case FxKind.PlasmaOrb: PlasmaOrb.SpawnGhost(run, fx.A, fx.B, fx.F1, fx.F2); break;
                case FxKind.SentryTurret: SentryTurret.SpawnGhost(run, fx.A, fx.F1); break;
                case FxKind.MicroMissile: MicroMissile.SpawnGhost(run, fx.A, fx.B, new Vector2(fx.F1, fx.F2)); break;
                case FxKind.Flurry:
                    var brawler = FindHero(run, fx.Origin);
                    if (brawler != null) BrawlerVfx.Flurry(root, brawler.transform, () => brawler != null ? brawler.Aim : Vector2.zero, fx.F1, fx.F2, color, fx.N / 1000f);
                    break;
            }
        }
    }
}

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

        public static void Arrow(DungeonRun run, Vector2 position, Vector2 direction, float range, DamageElement infusion = DamageElement.Physical)
            => Send(run, FxKind.Arrow, position, direction, null, range, 0f, (int)infusion);
        public static void Ricochet(DungeonRun run, Vector2 position, Vector2 direction, DamageElement infusion)
            => Send(run, FxKind.Ricochet, position, direction, null, 0f, 0f, (int)infusion);
        public static void Net(DungeonRun run, Vector2 position, Vector2 direction, float range, float hold, DamageElement infusion)
            => Send(run, FxKind.Net, position, direction, null, range, hold, (int)infusion);
        /// <summary>Teammates see the Assassin's shadow clone step out and lunge.</summary>
        public static void ShadowClone(DungeonRun run, Vector2 from, Vector2 target, float lunge) => Send(run, FxKind.ShadowClone, from, target, null, lunge);
        /// <summary>Teammates see the Death Mark's skull and clock over the enemy nearest where it was cast.</summary>
        public static void DeathMark(DungeonRun run, Vector2 at, float duration) => Send(run, FxKind.DeathMark, at, default, null, duration);
        public static void RocketBoost(DungeonRun run, Vector2 from, Vector2 to, float blastRadius) => Send(run, FxKind.RocketBoost, from, to, null, blastRadius);
        /// <summary>Starts or steers a teammate's copy of the Orbital Laser, keeping it alive a little longer.</summary>
        public static void OrbitalLaser(DungeonRun run, Vector2 at, float keepAlive) => Send(run, FxKind.OrbitalLaser, at, default, null, keepAlive);
        public static void Grapple(DungeonRun run, Vector2 aim, float range) => Send(run, FxKind.Grapple, default, aim, null, range);
        public static void BrawlerMove(DungeonRun run, PunchVfx.Style style, Vector2 origin, Vector2 aim, float length, float halfWidth, Color color)
            => Send(run, FxKind.BrawlerMove, origin, aim, color, length, halfWidth, (int)style);
        /// <summary>Teammates see the Gambler's Insurance aura, and each claim paid (or denied) against it.</summary>
        public static void Insurance(DungeonRun run, float duration) => Send(run, FxKind.Insurance, default, default, null, duration);
        public static void InsuranceClaim(DungeonRun run, bool paid) => Send(run, FxKind.InsuranceClaim, default, default, null, 0f, 0f, paid ? 1 : 0);
        public static void Card(DungeonRun run, Vector2 position, Vector2 direction, int suit) => Send(run, FxKind.Card, position, direction, null, 0f, 0f, suit);
        public static void Dice(DungeonRun run, Vector2 from, Vector2 landing, int face, float delay) => Send(run, FxKind.Dice, from, landing, null, delay, 0f, face);
        public static void Shield(DungeonRun run, Vector2 aim) => Send(run, FxKind.Shield, default, aim);
        public static void Whirlwind(DungeonRun run, float duration, float radius) => Send(run, FxKind.Whirlwind, default, default, null, duration, radius);
        public static void WarBanner(DungeonRun run, Vector2 at, float duration, float radius) => Send(run, FxKind.WarBanner, at, default, null, duration, radius);
        public static void Consecration(DungeonRun run, Vector2 at, float duration, float radius) => Send(run, FxKind.Consecration, at, default, null, duration, radius);
        public static void Heal(DungeonRun run, Vector2 center, float radius) => Send(run, FxKind.Heal, center, default, null, radius);
        public static void Lance(DungeonRun run, Vector2 origin, Vector2 aim) => Send(run, FxKind.Lance, origin, aim);
        /// <summary>Teammates see guardian angels circle the watched hero: the caster, or the teammate <paramref name="target"/>.</summary>
        public static void Intervention(DungeonRun run, Vector2 at, float duration, ulong? target)
            => Send(run, FxKind.Intervention, at, default, null, duration, 0f, target.HasValue ? (int)target.Value : -1);
        /// <summary>Teammates see this hero's Divine Intervention trigger and save them.</summary>
        public static void InterventionSaved(DungeonRun run) => Send(run, FxKind.InterventionSaved, default);
        public static void BallLightning(DungeonRun run, Vector2 origin, Vector2 aim, float duration) => Send(run, FxKind.BallLightning, origin, aim, null, duration);
        /// <summary>An Ice Wall block broke here; every machine breaks its copy.</summary>
        public static void IceBreak(DungeonRun run, Vector2 at) => Send(run, FxKind.IceBreak, at);
        public static void NightmareSnap(DungeonRun run, Vector2 at, float radius) => Send(run, FxKind.NightmareSnap, at, default, null, radius);
        public static void SnapTether(DungeonRun run, Vector2 from, Vector2 to, float strength) => Send(run, FxKind.SnapTether, from, to, null, strength);
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
        /// <summary>The Specimen's Boulder Toss leaves a rock wall that blocks bolts on every machine.</summary>
        public static void RockCover(DungeonRun run, Vector2 center, Vector2 facing, float width, float duration)
            => Send(run, FxKind.RockCover, center, facing, null, width, duration);

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

        public static void TailStab(DungeonRun run, Vector2 origin, Vector2 aim, float reach, Color color, bool twin = false)
            => Send(run, FxKind.TailStab, origin, aim, color, reach, twin ? 1f : 0f);
        public static void TailSweep(DungeonRun run, Vector2 origin, Vector2 aim, float reach, float cone, Color color)
            => Send(run, FxKind.TailSweep, origin, aim, color, reach, cone);
        public static void Pentagram(DungeonRun run, Vector2 center, float radius, float windup)
            => Send(run, FxKind.Pentagram, center, default, null, radius, windup);
        /// <summary>Teammates see the paw land where it was aimed when cast (their copy does not track the target).</summary>
        public static void DemonPaw(DungeonRun run, Vector2 center, float radius, float windup)
            => Send(run, FxKind.DemonPaw, center, default, null, radius, windup);
        public static void DemonHead(DungeonRun run, Vector2 center, float radius) => Send(run, FxKind.DemonHead, center, default, null, radius);
        public static void AvatarOfDeath(DungeonRun run, float duration) => Send(run, FxKind.AvatarOfDeath, default, default, null, duration);
        /// <summary>Teammates see Demonic Power's rune circles on her (and Dread Presence's ring, when <paramref name="auraRadius"/> is above zero).</summary>
        public static void DemonicPower(DungeonRun run, float duration, float auraRadius) => Send(run, FxKind.DemonicPower, default, default, null, duration, auraRadius);
        /// <summary>Teammates see the Brawler's fury burning on her.</summary>
        public static void SuperAngry(DungeonRun run, float duration) => Send(run, FxKind.SuperAngry, default, default, null, duration);
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

        private static DungeonEnemy NearestEnemy(DungeonRun run, Vector2 at, float within)
        {
            DungeonEnemy best = null;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                float distance = Vector2.Distance(at, enemy.transform.position);
                if (distance <= within) { within = distance; best = enemy; }
            }
            return best;
        }

        /// <summary>The hero on this machine (the local one or a teammate) standing nearest <paramref name="at"/>.</summary>
        private static Transform NearestHero(DungeonRun run, Vector2 at)
        {
            Transform best = run.Player != null ? run.Player.transform : null;
            float bestDistance = best != null ? Vector2.Distance(at, best.position) : float.MaxValue;
            foreach (var hero in run.Coop.RemoteHeroes)
            {
                if (hero == null || !hero.IsAlive) continue;
                float distance = Vector2.Distance(at, hero.transform.position);
                if (distance < bestDistance) { bestDistance = distance; best = hero.transform; }
            }
            return best;
        }

        private static RemoteHero FindHero(DungeonRun run, ulong id)
        {
            foreach (var hero in run.Coop.RemoteHeroes) if (hero != null && hero.Id == id) return hero;
            return null;
        }

        /// <summary>Every machine raises its own Ice Wall, so the host's enemies are really held back.</summary>
        public static void IceWall(DungeonRun run, Vector2 center, Vector2 along, float halfLength, float duration, int health)
            => Send(run, FxKind.IceWall, center, along, null, halfLength, duration, health);

        /// <summary>Teammates see the Demoness's wings beat on her as she dashes.</summary>
        public static void Wings(DungeonRun run, float duration, Vector2 heading) => Send(run, FxKind.Wings, default, heading, null, duration);
        /// <summary>Teammates see Soul Siphon's aura and streams on her.</summary>
        public static void SoulSiphon(DungeonRun run, float duration, float radius) => Send(run, FxKind.SoulSiphon, default, default, null, duration, radius);

        public static void RearHit(DungeonRun run, Vector2 position, Vector2 facing, float hitRadius)
            => Send(run, FxKind.RearHit, position, facing, null, hitRadius);

        public static void Play(DungeonRun run, FxMessage fx)
        {
            var root = run.ProjectileRoot;
            Color color = fx.Color;
            switch (fx.Kind)
            {
                case FxKind.Arrow: PlayerProjectile.SpawnGhost(run, fx.A, fx.B, fx.F1, ProjectileStyle.Arrow, (DamageElement)fx.N); break;
                case FxKind.IceWall: Slopgame.IceWall.Create(run, fx.A, fx.B, fx.F1, fx.F2, fx.N); break;
                case FxKind.Wings:
                    var flyer = FindHero(run, fx.Origin);
                    if (flyer != null) WingFlapVfx.Play(root, flyer.transform, fx.F1, fx.B);
                    break;
                case FxKind.SoulSiphon:
                    var siphoner = FindHero(run, fx.Origin);
                    if (siphoner != null) SoulSiphonVfx.Play(run, siphoner.transform, fx.F1, fx.F2);
                    break;
                case FxKind.Ricochet: RicochetArrow.SpawnGhost(run, fx.A, fx.B, (DamageElement)fx.N); break;
                case FxKind.Net: ThrownNet.SpawnGhost(run, fx.A, fx.B, fx.F1, fx.F2, (DamageElement)fx.N); break;
                case FxKind.ShadowClone:
                    var caster = FindHero(run, fx.Origin);
                    if (caster != null) ShadowCloneVfx.Play(root, caster.transform, fx.A, fx.B, fx.F1);
                    break;
                case FxKind.DeathMark:
                    var markedEnemy = NearestEnemy(run, fx.A, 1.5f);
                    if (markedEnemy != null) DeathMarkVfx.Play(root, markedEnemy, fx.F1);
                    break;
                case FxKind.RocketBoost: RocketBoostVfx.Play(root, fx.A, fx.B, fx.F1); break;
                case FxKind.OrbitalLaser: Slopgame.OrbitalLaser.Ghost(run, fx.Origin, fx.A, fx.F1); break;
                case FxKind.Grapple:
                    var grappler = FindHero(run, fx.Origin);
                    if (grappler != null) GrappleHook.SpawnGhost(run, grappler.transform, fx.B, fx.F1);
                    break;
                case FxKind.BrawlerMove: BrawlerVfx.Move(root, (PunchVfx.Style)fx.N, fx.A, fx.B, fx.F1, fx.F2, color); break;
                case FxKind.Insurance:
                    var insured = FindHero(run, fx.Origin);
                    if (insured != null) InsuranceVfx.Play(root, insured.transform, fx.F1);
                    break;
                case FxKind.InsuranceClaim:
                    var claimant = FindHero(run, fx.Origin);
                    if (claimant != null) InsuranceVfx.Claim(root, claimant.transform, fx.N == 1);
                    break;
                case FxKind.Card: ThrownCard.SpawnGhost(run, fx.A, fx.B, (ThrownCard.Suit)Mathf.Clamp(fx.N, 0, 3)); break;
                case FxKind.Dice: DiceBomb.SpawnGhost(run, fx.A, fx.B, fx.N, fx.F1); break;
                case FxKind.Shield:
                    var knightThrower = FindHero(run, fx.Origin);
                    if (knightThrower != null) ThrownShield.SpawnGhost(run, knightThrower.transform, fx.B);
                    break;
                case FxKind.Whirlwind:
                    var spinner = FindHero(run, fx.Origin);
                    if (spinner != null) WhirlwindVfx.Play(root, spinner.transform, fx.F1, fx.F2);
                    break;
                case FxKind.WarBanner: WarBannerVfx.Play(root, fx.A, fx.F1, fx.F2); break;
                case FxKind.Consecration: ConsecrationVfx.Play(run, fx.A, fx.F1, fx.F2); break;
                case FxKind.Heal: HealVfx.PlayAround(run, fx.A, fx.F1); break;
                case FxKind.Lance: ThrownLance.SpawnGhost(run, fx.A, fx.B); break;
                case FxKind.Intervention:
                    // N names the watched hero (-1: the caster); fall back to position for anything unexpected.
                    Transform watched = null;
                    if (fx.N >= 0 && (ulong)fx.N == run.Coop.LocalId) { if (run.Player != null) watched = run.Player.transform; }
                    else
                    {
                        var watchedHero = FindHero(run, fx.N < 0 ? fx.Origin : (ulong)fx.N);
                        if (watchedHero != null) watched = watchedHero.transform;
                    }
                    if (watched == null) watched = NearestHero(run, fx.A);
                    if (watched != null) GuardianAngelsVfx.Play(root, watched, fx.F1);
                    break;
                case FxKind.InterventionSaved:
                    var saved = FindHero(run, fx.Origin);
                    if (saved != null) GuardianAngelsVfx.Rescued(root, saved.transform);
                    break;
                case FxKind.BallLightning: Slopgame.BallLightning.SpawnGhost(run, fx.A, fx.B, fx.F1); break;
                case FxKind.IceBreak: Slopgame.IceWall.BreakAt(run, fx.A); break;
                case FxKind.NightmareSnap: NightmareSnapVfx.Snap(root, fx.A, fx.F1); break;
                case FxKind.SnapTether: NightmareSnapVfx.Tether(root, fx.A, fx.B, fx.F1); break;
                case FxKind.Coin: PlayerProjectile.SpawnGhost(run, fx.A, fx.B, fx.F1, ProjectileStyle.Coin); break;
                case FxKind.Spell: SpellProjectile.SpawnGhost(run, fx.A, fx.B, color, fx.F1, fx.F2, fx.N); break;
                case FxKind.Slash: HeroVfx.Slash(root, fx.A, fx.B, fx.F1, fx.F2, color); break;
                case FxKind.Bolt: CombatVfx.Bolt(root, fx.A, fx.B, color); break;
                case FxKind.GlowBolt: CombatVfx.GlowBolt(root, fx.A, fx.B, color); break;
                case FxKind.Ring: CombatVfx.Ring(root, fx.A, fx.F1, color, fx.F2); break;
                case FxKind.Pulse: HeroVfx.Pulse(root, fx.A, fx.F1, color, fx.F2); break;
                case FxKind.RockCover: Slopgame.RockCover.Raise(run, fx.A, fx.B, fx.F1, fx.F2, true); break;
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
                case FxKind.TailStab: TailVfx.Stab(root, fx.A, fx.B, fx.F1, color, fx.F2 > 0.5f); break;
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
                case FxKind.AvatarOfDeath:
                    var reaper = FindHero(run, fx.Origin);
                    if (reaper != null) AvatarOfDeathVfx.Play(root, reaper.transform, fx.F1);
                    break;
                case FxKind.DemonicPower:
                    var demoness = FindHero(run, fx.Origin);
                    if (demoness != null) DemonicPowerVfx.Play(root, demoness.transform, fx.F1, fx.F2);
                    break;
                case FxKind.SuperAngry:
                    var furious = FindHero(run, fx.Origin);
                    if (furious != null) SuperAngryVfx.Play(root, furious.transform, fx.F1);
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

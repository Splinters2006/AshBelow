using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class PlayerAbilities : MonoBehaviour, IRunPersistent
    {
        public DungeonPlayer Player { get; set; }
        public const int SlotCount = 2;
        public const int MaxRank = 3;
        public const float ShadowstepDistance = 5f;
        public const float FrostNovaRadius = 5f, FrostNovaExpandTime = 0.5f, FrostNovaFreeze = 3f;
        /// <summary>Frost Nova's damage, in multiples of the hero's (other artifacts deal 3).</summary>
        public const int FrostNovaDamage = 5;
        public const float BlinkDistance = 6f;
        /// <summary>Earthshatter is 25% bigger than its original 2.6-unit reach.</summary>
        public const float EarthshatterRadius = 3.25f;
        public const float VeilDuration = 3f, VeilProtection = 1.5f;
        private readonly AbilityType[] equipped = new AbilityType[SlotCount];
        // Cooldowns belong to the ability, not the key, so swapping Q and E never refreshes one.
        private readonly Dictionary<AbilityType, float> readyAt = new Dictionary<AbilityType, float>();
        private readonly Dictionary<AbilityType, int> ranks = new Dictionary<AbilityType, int>();
        private float castReadyAt;
        public AbilityType Equipped(int slot) => slot >= 0 && slot < SlotCount ? equipped[slot] : AbilityType.None;
        public int Rank(AbilityType type) => ranks.TryGetValue(type, out int value) ? value : 0;
        public bool IsEquipped(AbilityType type) => type != AbilityType.None && (equipped[0] == type || equipped[1] == type);
        /// <summary>Learned this run (whether or not it sits on Q or E); hybrid talents need their ability learned.</summary>
        public bool IsLearned(AbilityType type) => type != AbilityType.None && Rank(type) > 0;
        /// <summary>Every ability learned this run, in catalog order (the abilities page).</summary>
        public IEnumerable<AbilityDefinition> Learned
        {
            get { foreach (var ability in AbilityCatalog.All) if (IsLearned(ability.Type)) yield return ability; }
        }
        /// <summary>Kill talents: takes time off every relic ability's cooldown.</summary>
        public void ReduceCooldowns(float seconds)
        {
            foreach (var type in new List<AbilityType>(readyAt.Keys)) readyAt[type] = Cooldowns.Shorten(readyAt[type], seconds);
        }

        // Capped at the ability's own cooldown, so a delayed start (Primal Rage, Archdemon's Technique) reads as a paused timer.
        public float CooldownRemaining(int slot) => CooldownRemaining(Equipped(slot));
        public float CooldownRemaining(AbilityType type) => type == AbilityType.None ? 0f
            : DebugMode.Cooldown(Mathf.Min(Mathf.Max(0f, (readyAt.TryGetValue(type, out float ready) ? ready : 0f) - Time.time),
                AbilityCatalog.Get(type)?.Cooldown ?? float.MaxValue));
        public void SaveRun(HeroSnapshot hero)
        {
            foreach (var pair in ranks) hero.abilities.Add(new SavedCount(pair.Key.ToString(), pair.Value));
            hero.abilityQ = equipped[0].ToString();
            hero.abilityE = equipped[1].ToString();
        }

        /// <summary>Learned abilities, their ranks and keys come back; every cooldown starts ready.</summary>
        public void LoadRun(HeroSnapshot hero)
        {
            ranks.Clear();
            readyAt.Clear();
            foreach (var ability in hero.abilities)
                if (RunSnapshot.TryParse(ability.id, out AbilityType type) && type != AbilityType.None) ranks[type] = Mathf.Clamp(ability.value, 1, MaxRank);
            equipped[0] = SavedSlot(hero.abilityQ);
            equipped[1] = SavedSlot(hero.abilityE);
        }

        private AbilityType SavedSlot(string name) => RunSnapshot.TryParse(name, out AbilityType type) && IsLearned(type) ? type : AbilityType.None;

        public int EmptySlot => equipped[0] == AbilityType.None ? 0 : equipped[1] == AbilityType.None ? 1 : -1;

        /// <summary>
        /// A guardian's ability pick: learns the ability at rank 1 (putting it on an empty key if there is one), or raises
        /// the rank of one already learned.
        /// </summary>
        public bool Learn(AbilityType type)
        {
            var definition = AbilityCatalog.Get(type);
            if (definition == null || definition.ClassWeapon != Player.ClassWeapon) return false;
            if (IsLearned(type))
            {
                if (Rank(type) >= MaxRank) return false;
                ranks[type] = Rank(type) + 1;
                return true;
            }
            ranks[type] = 1;
            if (EmptySlot >= 0) equipped[EmptySlot] = type;
            return true;
        }

        /// <summary>
        /// The Specimen's transformation trades an ability from the path that lost him for one of his own: the new one
        /// takes the old one's rank and key, and starts ready.
        /// </summary>
        public void Replace(AbilityType from, AbilityType to, int rank)
        {
            if (!IsLearned(from) || to == AbilityType.None || IsLearned(to)) return;
            ranks.Remove(from);
            readyAt.Remove(from);
            ranks[to] = Mathf.Clamp(rank, 1, MaxRank);
            for (int slot = 0; slot < SlotCount; slot++) if (equipped[slot] == from) equipped[slot] = to;
        }

        /// <summary>Unlearns an ability (taking it off its key).</summary>
        public void Forget(AbilityType type)
        {
            ranks.Remove(type);
            readyAt.Remove(type);
            for (int slot = 0; slot < SlotCount; slot++) if (equipped[slot] == type) equipped[slot] = AbilityType.None;
        }

        /// <summary>Puts a learned ability on Q or E; if it already sits on the other key, the two swap.</summary>
        public bool Equip(AbilityType type, int slot)
        {
            if (!IsLearned(type) || slot < 0 || slot >= SlotCount) return false;
            int other = 1 - slot;
            if (equipped[other] == type) equipped[other] = equipped[slot];
            equipped[slot] = type;
            return true;
        }

        /// <summary>Learns (or ranks up) an ability and puts it on <paramref name="slot"/>; an equipped ability just ranks up.</summary>
        public bool Claim(AbilityType type, int slot)
        {
            var definition = AbilityCatalog.Get(type);
            if (definition == null || definition.ClassWeapon != Player.ClassWeapon || slot < 0 || slot >= SlotCount) return false;
            if (IsEquipped(type))
            {
                if (Rank(type) >= MaxRank) return false;
                ranks[type] = Rank(type) + 1;
                return true;
            }
            if (Rank(type) == 0) ranks[type] = 1;
            equipped[slot] = type;
            return true;
        }

        /// <summary>
        /// Casts the ability in <paramref name="slot"/>. <paramref name="aim"/> points from the hero to the cursor;
        /// its length is how far away the cursor is, which cursor-targeted abilities such as Venom Vial use.
        /// </summary>
        public bool TryUse(int slot, Vector2 aim)
        {
            // Storm-Charged Orb: Inferno Orb charges while its key is held and flies on release.
            if (slot >= 0 && slot < SlotCount && equipped[slot] == AbilityType.Fireball && Player.Powerups.Count(PowerupType.StormChargedOrb) > 0
                && orbChargeSlot < 0 && orbCharge < 0f && Player.Run.IsPlaying && CooldownRemaining(slot) <= 0f)
            {
                orbChargeSlot = slot;
                orbChargeStarted = Time.time;
                return true;
            }
            // Recasting an active Sanctuary drops it; its cooldown keeps running from the original cast.
            if (slot >= 0 && slot < SlotCount && equipped[slot] == AbilityType.Sanctuary && Player.Run.IsPlaying)
            {
                var holder = Player.GetComponent<PaladinRelics>();
                if (holder != null && holder.IsSanctuaryActive) { holder.EndSanctuary(); return true; }
            }
            // Slice Dice Chunk: once a slash has hit, the same key throws the next one while the cooldown already runs.
            if (slot >= 0 && slot < SlotCount && equipped[slot] == AbilityType.SliceDiceChunk && Player.Run.IsPlaying
                && Player.Weapon is SamuraiAttack combo && combo.CanContinueCombo)
                return aim.sqrMagnitude >= 0.001f && combo.ContinueCombo(aim, Rank(AbilityType.SliceDiceChunk));
            if (slot < 0 || slot >= SlotCount || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy
                || Time.time < castReadyAt || CooldownRemaining(slot) > 0f || aim.sqrMagnitude < 0.001f) return false;
            var definition = AbilityCatalog.Get(equipped[slot]);
            if (definition == null) return false;
            // Every ability, dashes and blinks included, is aimed at the cursor.
            float cursorDistance = aim.magnitude;
            aim.Normalize();
            // Checked before anything is spent: Arcane Blink needs somewhere to land.
            Vector2 blinkLanding = default;
            if (definition.Type == AbilityType.Blink && !FindShadowstepLanding(Player.Run.Map, transform.position, aim,
                    Mathf.Min(BlinkDistance + Player.Powerups.Count(PowerupType.BlinkDistance) * 0.5f, cursorDistance), out blinkLanding)) return false;
            // A Specimen ability asleep in his current form does nothing, so it mustn't drop his guard or charge either.
            if (Player.Weapon is SpecimenAttack sleeper && !sleeper.CanUseAbility(definition.Type)) return false;
            Player.Weapon?.Hide();
            int rank = Rank(definition.Type);
            int damage = Player.Damage * 3 + rank - 1;
            var powers = Player.Powerups;
            switch (definition.Type)
            {
                case AbilityType.ShieldRush:
                case AbilityType.Earthshatter:
                case AbilityType.Aegis:
                    var knight = Player.GetComponent<KnightRelics>();
                    if (knight == null || knight.IsRushing) return false;
                    if (definition.Type == AbilityType.ShieldRush) knight.ShieldRush(aim, damage * 2 + powers.Count(PowerupType.RushPower) * 2, cursorDistance);
                    else if (definition.Type == AbilityType.Earthshatter)
                        knight.Earthshatter(EarthshatterRadius + powers.Count(PowerupType.ShatterRadius) * 0.5f, damage, definition.Color, 2f);
                    else knight.Aegis(2f + (rank - 1) * 0.3f + powers.Count(PowerupType.AegisDuration) * 0.4f);
                    break;
                case AbilityType.ShieldThrow:
                    if (!ThrownShield.Throw(Player, aim, Player.Damage * 2 + rank)) return false;
                    break;
                case AbilityType.Whirlwind:
                    Whirlwind.Spin(Player, Whirlwind.Duration + (rank - 1) * 0.4f, Player.Damage + rank - 1); break;
                case AbilityType.WarBanner:
                    WarBanner.Plant(Player, WarBanner.Duration + (rank - 1) * 1.5f); break;
                case AbilityType.IceWall:
                    IceWall.Raise(Player, aim, IceWall.Duration + (rank - 1) * 0.75f, IceWall.BaseHealth + (rank - 1)); break;
                case AbilityType.BallLightning:
                    BallLightning.Launch(Player, aim, BallLightning.Duration + (rank - 1) * 0.5f, Player.Damage + rank - 1); break;
                case AbilityType.LightningStorm:
                    LightningStorm.Call(Player, LightningStorm.Duration + (rank - 1) * 0.75f, Player.Damage * 2 + rank - 1); break;
                case AbilityType.SmokeBomb:
                    SmokeCloud.Drop(Player, SmokeCloud.Duration + (rank - 1) * 0.75f); break;
                case AbilityType.DeathMark:
                    var marked = NearestEnemy((Vector2)transform.position + aim * Mathf.Min(cursorDistance, 8f), 3f);
                    if (marked == null) return false;
                    marked.DeathMark(DungeonEnemy.DeathMarkTime);
                    if (!marked.IsInvulnerable)
                    {
                        DeathMarkVfx.Play(Player.Run.ProjectileRoot, marked, DungeonEnemy.DeathMarkTime);
                        CoopFx.DeathMark(Player.Run, marked.transform.position, DungeonEnemy.DeathMarkTime);
                    }
                    break;
                case AbilityType.ShadowClone:
                    ShadowClone.Activate(Player, ShadowClone.Duration + (rank - 1) * 1.5f); break;
                case AbilityType.HolyLance:
                    HolyLance(aim, Player.Damage * 2 + rank - 1); break;
                case AbilityType.Consecration:
                    Consecration.Sanctify(Player, Consecration.Duration + (rank - 1) * 1f, Mathf.Max(1, Player.Damage / 2) + rank - 1); break;
                case AbilityType.DivineIntervention:
                    Intervene((Vector2)transform.position + aim * cursorDistance); break;
                case AbilityType.CardToss:
                    ThrownCard.Toss(Player, aim, 3, Player.Damage + rank); break;
                case AbilityType.DiceBomb:
                    DiceBomb.Toss(Player, FindGroundLanding(Player.Run.Map, transform.position, aim, Mathf.Min(DiceBomb.Range, cursorDistance)), Player.Damage + rank - 1); break;
                case AbilityType.Insurance:
                    Player.Insure(DungeonPlayer.InsuranceTime + (rank - 1) * 1.5f); break;
                case AbilityType.EmpPulse:
                    EmpPulse(EmpRadius, EmpStun + (rank - 1) * 0.3f); break;
                case AbilityType.GrappleArm:
                    Grapple(aim, Player.Damage + rank); break;
                case AbilityType.OrbitalLaser:
                    OrbitalLaser.Call(Player, (Vector2)transform.position + aim * Mathf.Min(cursorDistance, 6f), OrbitalLaser.Duration + (rank - 1) * 0.75f, Player.Damage + rank - 1); break;
                case AbilityType.NetShot:
                    NetShot(aim, 2f + (rank - 1) * 0.5f, BowAttack.SteadyHandAbilityDamage(Player, Player.Damage + rank - 1)); break;
                case AbilityType.RicochetArrow:
                    RicochetArrow.Fire(Player, aim, BowAttack.SteadyHandAbilityDamage(Player, Player.Damage * 2 + rank - 1), Player.Damage); break;
                case AbilityType.BearTrap:
                    BearTrap.Set(Player, FindGroundLanding(Player.Run.Map, transform.position, aim, Mathf.Min(BearTrap.Range, cursorDistance)),
                        Player.Damage * 3 + rank - 1, 2f + (rank - 1) * 0.5f); break;
                case AbilityType.Volley:
                    // Arrows fall from the sky, so the volley can be called down on the far side of a wall.
                    ArrowRain.Cast(Player, FindOpenLanding(Player.Run.Map, transform.position, aim, Mathf.Min(ArrowRain.Range, cursorDistance)),
                        ArrowRain.BaseArrows + powers.Count(PowerupType.VolleyCount) * ArrowRain.ArrowsPerRank, BowAttack.SteadyHandAbilityDamage(Player, Player.Damage + rank - 1)); break;
                case AbilityType.PiercingShot:
                    PiercingArrow.Fire(Player, aim, BowAttack.SteadyHandAbilityDamage(Player, damage + 1 + powers.Count(PowerupType.PiercingPower) * 2)); break;
                case AbilityType.Windstep:
                    Dash(aim, Mathf.Min(3.5f + powers.Count(PowerupType.WindstepDistance) * 0.5f, cursorDistance), 0, true);
                    Fan(aim, 3, BowAttack.SpreadAngle, BowAttack.SteadyHandAbilityDamage(Player, Player.Damage + rank - 1), BowAttack.HeavyRange); break;
                case AbilityType.Fireball:
                    var orb = SpellProjectile.Spawn(Player, aim, damage, DamageElement.Fire, definition.Color, 7f,
                        1.7f + powers.Count(PowerupType.FireballRadius) * 0.4f);
                    // Storm-Charged Orb: held to full charge, the blast shocks everything it burns.
                    if (orbCharge >= 1f) { orb.StormCharged = true; HeroVfx.Pulse(Player.Run.ProjectileRoot, transform.position, 1f, CombatDamage.ShockColor, 0.3f); }
                    break;
                case AbilityType.FrostNova:
                    StartCoroutine(ExpandingNova(transform.position, FrostNovaRadius, FrostNovaExpandTime, Player.Damage * FrostNovaDamage + rank - 1, definition.Color,
                        FrostNovaFreeze + powers.Count(PowerupType.FrostDuration) * 0.5f + (rank - 1) * 0.25f)); break;
                case AbilityType.Blink:
                    Vector2 blinkFrom = transform.position;
                    Blink(blinkLanding, definition.Color);
                    // Frostblink: a Frost Nova bursts where the Wizard vanished.
                    if (powers.Count(PowerupType.Frostblink) > 0)
                        StartCoroutine(ExpandingNova(blinkFrom, FrostNovaRadius, FrostNovaExpandTime, Player.Damage * 2, AbilityCatalog.Ice, FrostNovaFreeze));
                    break;
                case AbilityType.FanOfKnives:
                    int knives = 12 + powers.Count(PowerupType.KnifeCount) * 2;
                    for (int i = 0; i < knives; i++)
                        ReturningKnife.Throw(Player, Quaternion.Euler(0, 0, i * 360f / knives) * aim, Player.Damage + rank);
                    break;
                case AbilityType.VenomVial:
                    VenomVial.Throw(Player, VenomVial.FindLanding(Player.Run.Map, transform.position, aim, cursorDistance),
                        VenomVial.Radius + (rank - 1) * 0.2f, VenomVial.PoolDuration + powers.Count(PowerupType.VenomDuration),
                        Player.Damage + rank - 1, Player.Damage + rank); break;
                case AbilityType.ShadowVeil:
                    float veil = VeilDuration + (rank - 1) * 0.5f + powers.Count(PowerupType.VeilDuration) * 0.4f;
                    Player.Veil(veil);
                    Player.Protect(VeilProtection + (rank - 1) * 0.3f);
                    ShadowstepVfx.Puff(Player.Run.ProjectileRoot, transform.position);
                    CoopFx.Shadowstep(Player.Run, transform.position, transform.position); break;
                case AbilityType.HealingLight:
                    HealAllies(2 + rank - 1 + powers.Count(PowerupType.HealingPower)); break;
                case AbilityType.Judgment:
                case AbilityType.Sanctuary:
                    var paladin = Player.GetComponent<PaladinRelics>();
                    if (paladin == null) return false;
                    if (definition.Type == AbilityType.Judgment)
                        paladin.Judgment(FindGroundLanding(Player.Run.Map, transform.position, aim, Mathf.Min(PaladinRelics.JudgmentRange, cursorDistance)),
                            damage + powers.Count(PowerupType.JudgmentPower) * 2, 2f);
                    else paladin.Sanctuary(PaladinRelics.SanctuaryRadius + powers.Count(PowerupType.SanctuarySize) * 0.5f,
                        PaladinRelics.SanctuaryDuration + (rank - 1) * 0.5f);
                    break;
                case AbilityType.Heartbeat:
                case AbilityType.FightOrFlight:
                case AbilityType.Bulldoze:
                case AbilityType.BoulderToss:
                case AbilityType.IronSkin:
                case AbilityType.GiantSwing:
                case AbilityType.SwingLine:
                case AbilityType.AnkleWrap:
                case AbilityType.Bind:
                case AbilityType.RoundUp:
                    var specimen = Player.Weapon as SpecimenAttack;
                    if (specimen == null || !SpecimenArts.Cast(specimen, definition.Type, aim, rank, cursorDistance)) return false;
                    break;
                case AbilityType.KnuckleSandwich:
                case AbilityType.WildLeap:
                case AbilityType.PrimalRage:
                case AbilityType.ThunderClap:
                case AbilityType.HaymakerDash:
                case AbilityType.Suplex:
                    var brawler = Player.GetComponent<BrawlerAttack>();
                    if (brawler == null || !brawler.CastArtifact(definition.Type, aim, rank)) return false;
                    break;
                case AbilityType.ArchdemonTechnique:
                case AbilityType.DemonPaw:
                case AbilityType.DemonCurse:
                case AbilityType.WingDash:
                case AbilityType.SoulSiphon:
                case AbilityType.NightmareSnap:
                    var demoness = Player.GetComponent<DemonessAttack>();
                    if (demoness == null || !demoness.CastArtifact(definition.Type, aim, rank, cursorDistance)) return false;
                    break;
                case AbilityType.Windfall:
                case AbilityType.AllIn:
                case AbilityType.Jackpot:
                    var gambler = Player.GetComponent<GamblerAttack>();
                    if (gambler == null || !gambler.CastArtifact(definition.Type, rank)) return false;
                    break;
                case AbilityType.ShadeWalk:
                case AbilityType.FearIncarnate:
                case AbilityType.Feast:
                case AbilityType.Sow:
                case AbilityType.Reap:
                case AbilityType.ReapersTechnique:
                    var reaper = Player.GetComponent<ReaperAttack>();
                    if (reaper == null || !reaper.CastArtifact(definition.Type, aim, rank, cursorDistance)) return false;
                    break;
                case AbilityType.SliceDiceChunk:
                case AbilityType.Bloodscent:
                case AbilityType.BloodShallFlow:
                case AbilityType.MaestrosTechnique:
                case AbilityType.SwiftAsTheWind:
                case AbilityType.Bloodpop:
                    var samurai = Player.GetComponent<SamuraiAttack>();
                    if (samurai == null || !samurai.CastArtifact(definition.Type, aim, rank)) return false;
                    break;
                case AbilityType.MicroMissiles:
                case AbilityType.RocketBoost:
                case AbilityType.SentryTurret:
                    var augment = Player.GetComponent<CyborgAttack>();
                    if (augment == null || !augment.CastArtifact(definition.Type, aim, rank, cursorDistance)) return false;
                    break;
            }
            CombatVfx.Ring(Player.Run.ProjectileRoot, transform.position, 0.65f, definition.Color);
            CoopFx.Ring(Player.Run, transform.position, 0.65f, definition.Color);
            HeroVfx.Pulse(Player.Run.ProjectileRoot, transform.position, 1.1f, definition.Color, 0.35f);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, definition.Color, 10, 3.5f, 0.35f);
            // Primal Rage's and Archdemon's Technique's cooldowns only start once their effects have ended.
            readyAt[definition.Type] = Time.time + definition.Cooldown * Player.Powerups.RelicCooldownMultiplier
                + (definition.Type == AbilityType.PrimalRage ? Player.Buffs.RageCycleRemaining
                    : definition.Type == AbilityType.ArchdemonTechnique ? Player.Buffs.AscendRemaining : 0f);
            // Zipline: a killing Swing Line is ready again at once.
            if (Player.Weapon is SpecimenAttack refunded && refunded.ConsumeRefund(definition.Type)) readyAt[definition.Type] = Time.time;
            castReadyAt = Time.time + 0.2f;
            Player.Powerups.OnAbilityUsed();
            return true;
        }

        /// <summary>The living enemy nearest <paramref name="point"/> within <paramref name="radius"/>.</summary>
        public DungeonEnemy NearestEnemy(Vector2 point, float radius)
        {
            DungeonEnemy best = null;
            float bestDistance = radius;
            foreach (var enemy in Player.Run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                float distance = Vector2.Distance(point, enemy.transform.position);
                if (distance <= bestDistance) { best = enemy; bestDistance = distance; }
            }
            return best;
        }

        public const float InterventionTime = 5f;

        /// <summary>Holy Lance: a lance of light flies along a wide line, piercing with holy damage; the first enemy it meets is stunned.</summary>
        private void HolyLance(Vector2 aim, int damage) => ThrownLance.Throw(Player, aim, damage);

        /// <summary>Hovering this close to themselves, the Paladin watches over themselves instead of an ally.</summary>
        public const float InterventionSelfRadius = 1f;

        /// <summary>
        /// Divine Intervention: the living ally nearest the cursor is watched over for a few seconds. The Paladin
        /// only picks themselves when no ally is alive or the cursor is right on top of them.
        /// </summary>
        private void Intervene(Vector2 cursor)
        {
            var run = Player.Run;
            RemoteHero chosen = null;
            if (run.IsNetworked && Vector2.Distance(cursor, transform.position) > InterventionSelfRadius)
            {
                float best = float.MaxValue;
                foreach (var hero in run.Coop.RemoteHeroes)
                {
                    if (hero == null || !hero.IsAlive) continue;
                    float distance = Vector2.Distance(cursor, hero.transform.position);
                    if (distance < best) { best = distance; chosen = hero; }
                }
            }
            if (chosen == null) Player.Intercede(InterventionTime);
            else
            {
                run.Coop.SendSupport(chosen.Id, SupportKind.Intervention, 0, InterventionTime);
                // A golden tether shows the caster exactly who was chosen.
                CombatVfx.GlowBolt(run.ProjectileRoot, transform.position, chosen.transform.position, AbilityCatalog.Gold);
                CoopFx.Bolt(run, transform.position, chosen.transform.position, AbilityCatalog.Gold, true);
            }
            // Guardian angels circle whoever is watched over, on every machine.
            var watched = chosen != null ? chosen.transform : transform;
            GuardianAngelsVfx.Play(run.ProjectileRoot, watched, InterventionTime);
            CoopFx.Intervention(run, watched.position, InterventionTime, chosen != null ? chosen.Id : (ulong?)null);
        }

        public const float EmpRadius = 4f, EmpStun = 1.5f, GrappleRange = 7f;

        /// <summary>EMP Pulse: stuns every enemy nearby and fries the enemy bolts around the Augment.</summary>
        private void EmpPulse(float radius, float stun)
        {
            var run = Player.Run;
            Vector2 at = transform.position;
            HeroVfx.Pulse(run.ProjectileRoot, at, radius, WorldCatalog.Neon, 0.4f);
            CombatVfx.Ring(run.ProjectileRoot, at, radius, WorldCatalog.Neon, 0.4f);
            CoopFx.Pulse(run, at, radius, WorldCatalog.Neon, 0.4f);
            ScreenFx.Flash(FlameMesh.Alpha(WorldCatalog.Neon, 0.15f), 0.2f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= radius + enemy.HitRadius) enemy.Stun(stun);
            foreach (var bolt in run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>())
            {
                if (bolt.IsSpent || bolt.IsReflected || Vector2.Distance(at, bolt.transform.position) > radius) continue;
                HeroVfx.Sparks(run.ProjectileRoot, bolt.transform.position, WorldCatalog.Neon, 4, 2f, 0.2f);
                if (run.IsNetworked) run.Coop.ReportBolt(bolt, CoopBoltEventKind.Consumed);
                bolt.Consume();
            }
        }

        /// <summary>Grapple Arm: the hook flies out on its chain, snags the first enemy it touches and hauls it in (guardians only take the hit).</summary>
        private void Grapple(Vector2 aim, int damage) => GrappleHook.Fire(Player, aim, GrappleRange, damage);

        public const float NetRange = 5f;

        /// <summary>Net Shot: a weighted net flies out and drops on the first enemy it reaches, rooting everything under it.</summary>
        private void NetShot(Vector2 aim, float hold, int damage) => ThrownNet.Fire(Player, aim, NetRange, hold, damage);

        public const float OrbChargeTime = 1f;
        private int orbChargeSlot = -1;
        private float orbChargeStarted, orbCharge = -1f;
        /// <summary>How full Storm-Charged Orb's charge is (0-1), or -1 when not charging.</summary>
        public float OrbCharge => orbChargeSlot >= 0 ? Mathf.Clamp01((Time.time - orbChargeStarted) / OrbChargeTime) : -1f;

        private void Update()
        {
            if (orbChargeSlot < 0) return;
            if (!Player.Run.IsPlaying || Player.Health <= 0 || equipped[orbChargeSlot] != AbilityType.Fireball) { orbChargeSlot = -1; return; }
            if (OrbCharge >= 1f && Time.frameCount % 6 == 0)
                HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, CombatDamage.ShockColor, 2, 2f, 0.2f);
            if (KeyBindings.IsHeld(orbChargeSlot == 0 ? GameAction.AbilityQ : GameAction.AbilityE)) return;
            // Released: the orb flies with whatever charge it built.
            int slot = orbChargeSlot;
            orbCharge = OrbCharge;
            orbChargeSlot = -1;
            try { TryUse(slot, Player.AimDirection * 6f); }
            finally { orbCharge = -1f; }
        }

        /// <summary>Healing Light: heals every hero within 4 units in a soft green light, kept distinct from the Paladin's gold.</summary>
        private void HealAllies(int amount)
        {
            const float Reach = 4f;
            var run = Player.Run;
            foreach (var ally in FindObjectsByType<DungeonPlayer>())
                if (ally.Run == run && ally.Health > 0 && Vector2.Distance(transform.position, ally.transform.position) <= Reach)
                    ally.ApplySupport(SupportKind.Heal, amount, 0f);
            run.Coop?.SupportAllies(transform.position, Reach, SupportKind.Heal, amount, 0f);
            HealVfx.PlayAround(run, transform.position, Reach);
            CombatVfx.Ring(run.ProjectileRoot, transform.position, Reach, FlameMesh.Alpha(HealVfx.Mint, 0.7f), 0.5f);
            CoopFx.Heal(run, transform.position, Reach);
        }

        private void Fan(Vector2 aim, int count, float spacing, int damage, float range = PlayerProjectile.MaxRange)
        {
            for (int i = 0; i < count; i++)
                PlayerProjectile.Spawn(Player.Run, transform.position,
                    Quaternion.Euler(0, 0, (i - (count - 1) * 0.5f) * spacing) * aim, damage, range);
        }

        /// <summary>The farthest point up to <paramref name="distance"/> along <paramref name="aim"/> before the first wall.</summary>
        public static Vector2 FindGroundLanding(DungeonMap map, Vector2 from, Vector2 aim, float distance)
        {
            if (aim.sqrMagnitude < 0.0001f) return from;
            aim.Normalize();
            Vector2 landing = from;
            for (float travel = 0.1f; travel <= distance + 0.001f; travel += 0.1f)
            {
                Vector2 next = from + aim * Mathf.Min(travel, distance);
                if (!map.CanStand(next, 0.1f)) break;
                landing = next;
            }
            return landing;
        }

        /// <summary>
        /// The farthest standable point up to <paramref name="distance"/> along <paramref name="aim"/>, walls in between or
        /// not: for whatever drops in from above (Arrow Volley).
        /// </summary>
        public static Vector2 FindOpenLanding(DungeonMap map, Vector2 from, Vector2 aim, float distance)
        {
            if (aim.sqrMagnitude < 0.0001f) return from;
            aim.Normalize();
            for (float travel = distance; travel > 0.05f; travel -= 0.1f)
            {
                Vector2 spot = from + aim * travel;
                if (map.CanStand(spot, 0.1f)) return spot;
            }
            return from;
        }

        public static bool FindShadowstepLanding(DungeonMap map, Vector2 from, Vector2 aim, float distance, out Vector2 landing)
        {
            landing = from;
            if (map == null || aim.sqrMagnitude < 0.001f || distance < 0.15f) return false;
            aim.Normalize();
            int steps = Mathf.CeilToInt(distance / 0.1f);
            // Only the destination must be clear: intervening walls are intentionally ignored.
            for (int i = steps; i > 0; i--)
            {
                float travel = distance * i / steps;
                if (travel < 0.15f) break;
                Vector2 candidate = from + aim * travel;
                if (!map.CanStand(candidate)) continue;
                landing = candidate;
                return true;
            }
            return false;
        }

        public bool Shadowstep(Vector2 aim, float distance = ShadowstepDistance)
        {
            if (Player.ClassWeapon != WeaponType.Daggers || !Player.Run.IsPlaying) return false;
            Vector2 from = transform.position;
            if (!FindShadowstepLanding(Player.Run.Map, from, aim, distance, out Vector2 landing)) return false;
            transform.position = landing;
            Player.Protect(0.35f);
            Vector2 travel = landing - from;
            var color = ShadowstepVfx.Violet;
            ShadowstepVfx.Play(Player.Run.ProjectileRoot, from, landing);
            CoopFx.Shadowstep(Player.Run, from, landing);
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                Vector2 position = enemy.transform.position;
                float along = Mathf.Clamp01(Vector2.Dot(position - from, travel) / travel.sqrMagnitude);
                Vector2 closest = from + travel * along;
                float radius = enemy.HitRadius + 0.14f;
                if (enemy.Health <= 0 || (position - closest).sqrMagnitude > radius * radius) continue;
                HeroVfx.Slash(Player.Run.ProjectileRoot, position - travel.normalized * 0.4f, travel, 0.7f, 70f, color, 0.18f);
                CombatDamage.ApplyShadowstep(Player, enemy);
            }
            return true;
        }

        /// <summary>Dashes along the ground, stopping at walls. A <paramref name="windy"/> dash is drawn as a gust of wind (Windstep).</summary>
        public void Dash(Vector2 aim, float distance, int damage = 0, bool windy = false)
        {
            Vector2 from = transform.position;
            var hit = new HashSet<DungeonEnemy>();
            int steps = Mathf.CeilToInt(distance / 0.15f);
            for (int i = 0; i < steps; i++)
            {
                transform.position = Player.Run.Map.Move(transform.position, aim.normalized * (distance / steps));
                if (damage <= 0) continue;
                foreach (var enemy in Player.Run.Enemies.ToArray())
                    if (!hit.Contains(enemy) && InArea(enemy, transform.position, 0.9f))
                    {
                        hit.Add(enemy);
                        CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, from);
                    }
            }
            Player.Protect(0.35f);
            if (windy)
            {
                WindstepVfx.Play(Player.Run.ProjectileRoot, from, transform.position);
                CoopFx.Windstep(Player.Run, from, transform.position);
                return;
            }
            CombatVfx.GlowBolt(Player.Run.ProjectileRoot, from, transform.position, AbilityCatalog.Ice);
            CoopFx.Bolt(Player.Run, from, transform.position, AbilityCatalog.Ice, true);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, AbilityCatalog.Ice, 8, 3f, 0.3f, (Vector2)transform.position - from, 120f);
        }

        /// <summary>Arcane Blink: an instant jump to a spot that may lie beyond walls. It deals no damage.</summary>
        private void Blink(Vector2 landing, Color color)
        {
            Vector2 from = transform.position;
            transform.position = landing;
            Player.Protect(0.35f);
            CombatVfx.GlowBolt(Player.Run.ProjectileRoot, from, landing, color);
            CoopFx.Bolt(Player.Run, from, landing, color, true);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, from, color, 8, 2.6f, 0.3f);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, landing, color, 8, 3f, 0.3f, landing - from, 120f);
        }

        /// <summary>Frost Nova: a ring of ice that grows outward, striking and freezing each enemy once as it reaches them.</summary>
        private IEnumerator ExpandingNova(Vector2 center, float radius, float duration, int damage, Color color, float freeze)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            var hit = new HashSet<DungeonEnemy>();
            float nextRing = 0f;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot) yield break;
                float progress = Mathf.Clamp01(t / duration);
                float r = Mathf.Lerp(0.3f, radius, progress);
                if (t >= nextRing || progress >= 1f)
                {
                    nextRing = t + 0.08f;
                    CombatVfx.Ring(root, center, r, color, 0.25f);
                    CoopFx.Ring(run, center, r, color, 0.25f);
                    float angle = Random.value * Mathf.PI * 2f;
                    Vector2 edge = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    HeroVfx.Sparks(root, edge, Color.Lerp(color, Color.white, 0.4f), 3, 2f, 0.25f, edge - center, 70f, 0.8f);
                }
                foreach (var enemy in run.Enemies.ToArray())
                    if (enemy != null && !hit.Contains(enemy) && InArea(enemy, center, r))
                    {
                        hit.Add(enemy);
                        CombatDamage.Apply(Player, enemy, damage, DamageElement.Ice, center);
                        if (enemy.Health > 0) enemy.Freeze(freeze * CombatDamage.FreezeScale(Player));
                    }
                if (progress >= 1f)
                {
                    HeroVfx.Pulse(root, center, radius, color, 0.3f);
                    yield break;
                }
                yield return null;
            }
        }

        private bool InArea(DungeonEnemy enemy, Vector2 center, float radius) => enemy.Health > 0
            && Vector2.Distance(center, enemy.transform.position) <= radius + enemy.HitRadius
            && Player.Run.HasLineOfSight(center, enemy.transform.position);

        public void AreaAttack(Vector2 center, float radius, int damage, DamageElement element, Color color, float slow = 0f,
            bool guaranteedEffect = false)
        {
            CombatVfx.Ring(Player.Run.ProjectileRoot, center, radius, color);
            HeroVfx.Pulse(Player.Run.ProjectileRoot, center, radius, color, 0.4f);
            CoopFx.Ring(Player.Run, center, radius, color);
            CoopFx.Pulse(Player.Run, center, radius, color, 0.4f);
            foreach (var enemy in Player.Run.Enemies.ToArray())
                if (InArea(enemy, center, radius))
                {
                    CombatDamage.Apply(Player, enemy, damage, element, center, guaranteedEffect: guaranteedEffect);
                    if (enemy.Health > 0 && slow > 0f) enemy.Chill(slow);
                }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class PlayerAbilities : MonoBehaviour
    {
        public DungeonPlayer Player { get; set; }
        public const int SlotCount = 2;
        public const int MaxRank = 3;
        public const float ShadowstepDistance = 5f;
        public const float FrostNovaRadius = 3.1f, FrostNovaExpandTime = 0.75f, FrostNovaFreeze = 2f;
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
            // Recasting an active Sanctuary drops it; its cooldown keeps running from the original cast.
            if (slot >= 0 && slot < SlotCount && equipped[slot] == AbilityType.Sanctuary && Player.Run.IsPlaying)
            {
                var holder = Player.GetComponent<PaladinRelics>();
                if (holder != null && holder.IsSanctuaryActive) { holder.EndSanctuary(); return true; }
            }
            if (slot < 0 || slot >= SlotCount || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy
                || Time.time < castReadyAt || CooldownRemaining(slot) > 0f || aim.sqrMagnitude < 0.001f) return false;
            var definition = AbilityCatalog.Get(equipped[slot]);
            if (definition == null) return false;
            if (IsMovementAbility(definition.Type)) aim = Player.MobilityAim(aim);
            float cursorDistance = aim.magnitude;
            aim.Normalize();
            // Checked before anything is spent: Arcane Blink needs somewhere to land.
            Vector2 blinkLanding = default;
            if (definition.Type == AbilityType.Blink && !FindShadowstepLanding(Player.Run.Map, transform.position, aim,
                    Mathf.Min(BlinkDistance + Player.Powerups.Count(PowerupType.BlinkDistance) * 0.5f, cursorDistance), out blinkLanding)) return false;
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
                case AbilityType.NetShot:
                    NetShot(aim, 2f + (rank - 1) * 0.5f, Player.Damage + rank - 1); break;
                case AbilityType.RicochetArrow:
                    RicochetArrow.Fire(Player, aim, Player.Damage * 2 + rank - 1, Player.Damage); break;
                case AbilityType.BearTrap:
                    BearTrap.Set(Player, FindGroundLanding(Player.Run.Map, transform.position, aim, Mathf.Min(BearTrap.Range, cursorDistance)),
                        Player.Damage * 3 + rank - 1, 2f + (rank - 1) * 0.5f); break;
                case AbilityType.Volley:
                    ArrowRain.Cast(Player, FindGroundLanding(Player.Run.Map, transform.position, aim, Mathf.Min(ArrowRain.Range, cursorDistance)),
                        ArrowRain.BaseArrows + powers.Count(PowerupType.VolleyCount) * ArrowRain.ArrowsPerRank, Player.Damage + rank - 1); break;
                case AbilityType.PiercingShot:
                    PiercingArrow.Fire(Player, aim, damage + 1 + powers.Count(PowerupType.PiercingPower) * 2); break;
                case AbilityType.Windstep:
                    Dash(aim, Mathf.Min(3.5f + powers.Count(PowerupType.WindstepDistance) * 0.5f, cursorDistance), 0, true);
                    Fan(aim, 3, BowAttack.SpreadAngle, Player.Damage + rank - 1, BowAttack.HeavyRange); break;
                case AbilityType.Fireball:
                    SpellProjectile.Spawn(Player, aim, damage, DamageElement.Fire, definition.Color, 7f,
                        1.7f + powers.Count(PowerupType.FireballRadius) * 0.4f, guaranteedEffect: true); break;
                case AbilityType.FrostNova:
                    StartCoroutine(ExpandingNova(transform.position, FrostNovaRadius, FrostNovaExpandTime, damage, definition.Color,
                        FrostNovaFreeze + powers.Count(PowerupType.FrostDuration) * 0.5f + (rank - 1) * 0.25f)); break;
                case AbilityType.Blink:
                    Blink(blinkLanding, definition.Color); break;
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
                    ForAllies(SupportKind.Heal, 2 + rank - 1 + powers.Count(PowerupType.HealingPower), 0f); break;
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
                case AbilityType.Eclipse:
                case AbilityType.SoulRend:
                case AbilityType.ShadowReign:
                    var admin = Player.GetComponent<AdminAttack>();
                    if (admin == null || !admin.CastRelic(definition.Type, aim, rank)) return false;
                    break;
                case AbilityType.KnuckleSandwich:
                case AbilityType.WildLeap:
                case AbilityType.PrimalRage:
                    var brawler = Player.GetComponent<BrawlerAttack>();
                    if (brawler == null || !brawler.CastArtifact(definition.Type, aim, rank)) return false;
                    break;
                case AbilityType.ArchdemonTechnique:
                case AbilityType.DemonPaw:
                case AbilityType.DemonCurse:
                    var demoness = Player.GetComponent<DemonessAttack>();
                    if (demoness == null || !demoness.CastArtifact(definition.Type, aim, rank, cursorDistance)) return false;
                    break;
                case AbilityType.Windfall:
                case AbilityType.AllIn:
                case AbilityType.Jackpot:
                    var gambler = Player.GetComponent<GamblerAttack>();
                    if (gambler == null || !gambler.CastArtifact(definition.Type, rank)) return false;
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
            if (Player.ClassWeapon != WeaponType.Shadow)
            {
                HeroVfx.Pulse(Player.Run.ProjectileRoot, transform.position, 1.1f, definition.Color, 0.35f);
                HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, definition.Color, 10, 3.5f, 0.35f);
            }
            // Primal Rage's and Archdemon's Technique's cooldowns only start once their effects have ended.
            readyAt[definition.Type] = Time.time + definition.Cooldown * Player.Powerups.RelicCooldownMultiplier
                + (definition.Type == AbilityType.PrimalRage ? Player.Buffs.RageCycleRemaining
                    : definition.Type == AbilityType.ArchdemonTechnique ? Player.Buffs.AscendRemaining : 0f);
            castReadyAt = Time.time + 0.2f;
            Player.Powerups.OnAbilityUsed();
            return true;
        }

        public const float NetRange = 5f, NetCone = 70f;

        /// <summary>Net Shot: a weighted net fans out ahead, roots everything it catches and nicks it.</summary>
        private void NetShot(Vector2 aim, float hold, int damage)
        {
            var run = Player.Run;
            var color = new Color(0.8f, 0.75f, 0.55f);
            HeroVfx.Slash(run.ProjectileRoot, transform.position, aim, NetRange, NetCone, color, 0.3f);
            CoopFx.Slash(run, transform.position, aim, NetRange, NetCone, color);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || !SwordAttack.ContainsTarget(enemy.transform.position - transform.position, aim, NetRange + enemy.HitRadius, NetCone)
                    || !run.HasLineOfSight(transform.position, enemy.transform.position)) continue;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, transform.position, 0f);
                if (enemy.Health > 0) enemy.Root(hold);
                HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, color, 6, 2f, 0.3f);
            }
        }

        /// <summary>Dashes and blinks travel the way the hero is moving (see <see cref="DungeonPlayer.MobilityAim"/>).</summary>
        public static bool IsMovementAbility(AbilityType type)
            => type == AbilityType.ShieldRush || type == AbilityType.Windstep || type == AbilityType.Blink || type == AbilityType.RocketBoost;

        private void ForAllies(SupportKind kind, int amount, float duration)
        {
            foreach (var ally in FindObjectsByType<DungeonPlayer>())
                if (ally.Run == Player.Run && ally.Health > 0 && Vector2.Distance(transform.position, ally.transform.position) <= 4f)
                {
                    ally.ApplySupport(kind, amount, duration);
                    HeroVfx.Motes(Player.Run.ProjectileRoot, ally.transform.position, 0.7f, AbilityCatalog.Gold, 14, 1f);
                }
            Player.Run.Coop?.SupportAllies(transform.position, 4f, kind, amount, duration);
            CombatVfx.Ring(Player.Run.ProjectileRoot, transform.position, 4f, AbilityCatalog.Gold);
            CoopFx.Ring(Player.Run, transform.position, 4f, AbilityCatalog.Gold);
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
                        CombatDamage.Apply(Player, enemy, damage, DamageElement.Ice, center, guaranteedEffect: true);
                        if (enemy.Health > 0) enemy.Freeze(freeze);
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

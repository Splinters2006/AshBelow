using UnityEngine;

namespace Slopgame
{
    public enum BossKind { AshWarden, Duelist, Archdemon, GridOverseer, SiegeEngine, SingularityCore, HexMatriarch, BrimstoneHound, InfernalJudge, Spellblade, RimeArchmage, GrandMagister }

    /// <summary>
    /// The arena guardian's shared state (health, title, invulnerability, co-op state). Its fighting style lives in
    /// a <see cref="BossBehaviour"/> component chosen by world and floor.
    /// </summary>
    [RequireComponent(typeof(DungeonEnemy))]
    public sealed class DungeonBoss : MonoBehaviour
    {
        public DungeonEnemy Enemy { get; private set; }
        public BossBehaviour Behaviour { get; private set; }
        public BossKind Kind { get; private set; }
        public int MaxHealth { get; private set; }
        public string Title => Behaviour.Title;
        public string Tell => Behaviour.Tell;
        public bool IsEnraged => Enemy.Health <= MaxHealth / 2;
        public bool IsCharging => Behaviour.IsCharging;
        public bool IsInvulnerable => Behaviour.IsInvulnerable;
        public float HitRadius => Behaviour.HitRadius * Behaviour.SizeMultiplier;
        public bool DealsContactDamage => Behaviour.DealsContactDamage;
        public float ContactReach => Behaviour.ContactReach * Behaviour.SizeMultiplier;
        /// <summary>Four bits of attack state mirrored to co-op guests.</summary>
        public byte NetState => (byte)(Behaviour.NetState & 0x0F);
        private SpriteRenderer body;
        private bool dropped, hostMaxHealthLogged;
        private float nextDeflect;

        /// <summary>Guardians have this many times their style's base health, so a fight lasts through several attack cycles.</summary>
        public const float HealthMultiplier = 6f;

        /// <summary>
        /// Guardians fight aggressively: their attack clock runs this much faster than a normal enemy's, so windups,
        /// cooldowns and recoveries all come round sooner. Hazard warnings still last their full time.
        /// </summary>
        public const float AttackPace = 1.3f;
        /// <summary>
        /// What every guardian blow costs the hero in the first world: its touch, its bolts and its hazards (everything else
        /// deals 1). Later worlds add to it, see <see cref="WorldDefinition.GuardianHitDamage"/>.
        /// </summary>
        public const int HitDamage = 2;
        /// <summary>Guardians also close in this much faster than their style's base speed.</summary>
        public const float ChaseSpeedBoost = 1.15f;

        /// <summary>
        /// A guardian's health for a party: each extra hero adds 150% of solo health (normal enemies add 100%),
        /// so a bigger party still has to work through every attack.
        /// </summary>
        public static int ScaledHealth(int baseHealth, int partySize) => Mathf.CeilToInt(baseHealth * HealthMultiplier * (1f + 1.5f * (Mathf.Max(1, partySize) - 1)));

        /// <summary>
        /// Each world's three guardians in order from its <see cref="WorldDefinition.FirstGuardian"/>: the ash guardians on
        /// floors 5/10/15 (and in worlds without their own), the Arcology's machines on 20/25/30, the Infernal Court's on 35/40/45
        /// and the Arcane Spire's mages on 50/55/60.
        /// </summary>
        public static BossKind KindForFloor(int floor) => (BossKind)(Mathf.Max(0, floor / 5 - 1) % 3 + (int)WorldCatalog.ForFloor(floor).FirstGuardian);

        /// <summary>The name a guardian of this kind bears, without spawning it.</summary>
        public static string TitleFor(BossKind kind, bool highTech) => kind switch
        {
            BossKind.HexMatriarch => HexMatriarchBoss.FixedTitle,
            BossKind.BrimstoneHound => BrimstoneHoundBoss.FixedTitle,
            BossKind.InfernalJudge => InfernalJudgeBoss.FixedTitle,
            BossKind.Spellblade => SpellbladeBoss.FixedTitle,
            BossKind.RimeArchmage => RimeArchmageBoss.FixedTitle,
            BossKind.GrandMagister => GrandMagisterBoss.FixedTitle,
            BossKind.GridOverseer => GridOverseerBoss.FixedTitle,
            BossKind.SiegeEngine => SiegeEngineBoss.FixedTitle,
            BossKind.SingularityCore => SingularityCoreBoss.FixedTitle,
            BossKind.Duelist => DuelistBoss.TitleFor(highTech),
            BossKind.Archdemon => ArchdemonBoss.TitleFor(highTech),
            _ => AshWardenBoss.TitleFor(highTech)
        };

        public void Initialize(DungeonRun run)
        {
            Enemy = GetComponent<DungeonEnemy>();
            Enemy.Run = run;
            Enemy.Boss = this;
            body = GetComponent<SpriteRenderer>();
            Kind = KindForFloor(run.Floor);
            Behaviour = Kind == BossKind.HexMatriarch ? gameObject.AddComponent<HexMatriarchBoss>()
                : Kind == BossKind.BrimstoneHound ? (BossBehaviour)gameObject.AddComponent<BrimstoneHoundBoss>()
                : Kind == BossKind.InfernalJudge ? gameObject.AddComponent<InfernalJudgeBoss>()
                : Kind == BossKind.Spellblade ? gameObject.AddComponent<SpellbladeBoss>()
                : Kind == BossKind.RimeArchmage ? (BossBehaviour)gameObject.AddComponent<RimeArchmageBoss>()
                : Kind == BossKind.GrandMagister ? gameObject.AddComponent<GrandMagisterBoss>()
                : Kind == BossKind.GridOverseer ? gameObject.AddComponent<GridOverseerBoss>()
                : Kind == BossKind.SiegeEngine ? (BossBehaviour)gameObject.AddComponent<SiegeEngineBoss>()
                : Kind == BossKind.SingularityCore ? gameObject.AddComponent<SingularityCoreBoss>()
                : Kind == BossKind.Duelist ? gameObject.AddComponent<DuelistBoss>()
                : Kind == BossKind.Archdemon ? (BossBehaviour)gameObject.AddComponent<ArchdemonBoss>()
                : gameObject.AddComponent<AshWardenBoss>();
            // Every world past the first fields tougher guardians, deadlier worlds (the Infernal Court) tougher still.
            MaxHealth = Mathf.CeilToInt(ScaledHealth(Behaviour.BaseHealth(run.Floor), run.PartySize) * run.World.GuardianHealthMultiplier);
            Enemy.Health = MaxHealth;
            Behaviour.Setup(this);
            Enemy.Speed *= ChaseSpeedBoost;
            gameObject.name = Title;
        }

        private void Update()
        {
            if (!Enemy.Run.IsPlaying || Enemy.Health <= 0) return;
            body.color = Behaviour.BodyColor();
            Behaviour.VisualTick();
            if (Enemy.Run.IsGuest) return;
            Vector2 offset = Enemy.Run.NearestHero(transform.position) - (Vector2)transform.position;
            Behaviour.HostTick(offset);
            if (DealsContactDamage) Enemy.TryContactHit(ContactReach);
        }

        /// <summary>
        /// Co-op guest: take the host's maximum health, so the health bar measures against the same total the host
        /// uses. If the machines worked it out differently, the guest's bar started part-empty on every guardian.
        /// </summary>
        public void SyncMaxHealth(int hostMaxHealth)
        {
            if (hostMaxHealth <= 0 || hostMaxHealth == MaxHealth) return;
            if (!hostMaxHealthLogged)
            {
                hostMaxHealthLogged = true;
                Debug.LogWarning($"{Title}: host max health {hostMaxHealth} differs from this machine's {MaxHealth} (party {Enemy.Run.PartySize}). Are both players on the same build?");
            }
            MaxHealth = hostMaxHealth;
        }

        /// <summary>Co-op guest: mirror the host's attack state so tells, flight and invulnerability match.</summary>
        public void ApplySnapshot(bool isCharging, byte state) => Behaviour.ApplyNetState(isCharging, state);

        /// <summary>A blow glanced off while the boss is untouchable.</summary>
        public void Deflect(Vector2 source)
        {
            if (Time.time < nextDeflect) return;
            nextDeflect = Time.time + 0.12f;
            HeroVfx.Sparks(Enemy.Run.ProjectileRoot, transform.position, AbilityCatalog.Gold, 6, 3f, 0.25f, source - (Vector2)transform.position, 90f);
        }

        public void Defeated()
        {
            if (dropped) return;
            dropped = true;
            foreach (var bolt in Enemy.Run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>())
            {
                bolt.gameObject.SetActive(false);
                Destroy(bolt.gameObject);
            }
            foreach (var zone in Enemy.Run.ProjectileRoot.GetComponentsInChildren<HellfireZone>())
            {
                zone.gameObject.SetActive(false);
                Destroy(zone.gameObject);
            }
            // The guardian's summoned court dissolves with it (every machine does this itself).
            foreach (var minion in Enemy.Run.Enemies.ToArray())
                if (minion != null && minion.IsMinion && minion.Health > 0)
                {
                    HeroVfx.Pulse(Enemy.Run.ProjectileRoot, minion.transform.position, 0.8f, new Color(0.9f, 0.4f, 1f), 0.4f);
                    minion.Die(false);
                }
            Behaviour.OnDefeated();
            Enemy.Run.DropArtifact(Behaviour.GroundPosition);
            // Soul Tithe: every guardian gives up a heart worth 2 HP.
            if (Enemy.Run.Progress.Rank(PermanentUpgradeCatalog.SoulTitheId) > 0)
                HealthPickup.Drop(Enemy.Run, Behaviour.GroundPosition + Vector2.down * 1.2f, 2);
        }
    }
}

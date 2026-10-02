using UnityEngine;

namespace Slopgame
{
    public enum ProjectileStyle { Arrow, Coin }

    /// <summary>A hero's arrow or thrown coin. Flies straight and strikes the first enemy it meets.</summary>
    public sealed class PlayerProjectile : MonoBehaviour
    {
        private DungeonRun run;
        private int damage;
        /// <summary>Elemental Quiver: the element a critical hit sets off.</summary>
        private DamageElement infusion;
        public const float MaxRange = 5f, CoinSpeed = 12f, ArrowSpeed = 15f;
        private float remainingRange = MaxRange;
        public float RemainingRange => remainingRange;
        public Vector2 Direction { get; private set; }
        public bool IsSpent { get; private set; }
        /// <summary>A fully charged arrow (Hunter's Mark, Point Blank).</summary>
        public bool FullyCharged { get; set; }
        private bool ghost;
        private float travelled;
        // The throw or shot this arrow (or coin) belongs to: everything one volley kills counts together for Massacre.
        private int attack;

        /// <summary>The local hero's shot. The Archer's arrows take the element loaded in her Elemental Quiver.</summary>
        public static PlayerProjectile Spawn(DungeonRun run, Vector2 position, Vector2 direction, int damage, float range = MaxRange,
            ProjectileStyle style = ProjectileStyle.Arrow)
        {
            var infusion = style == ProjectileStyle.Arrow && run.Player != null && run.Player.Mechanic is ElementalQuiver quiver
                ? quiver.Element : DamageElement.Physical;
            if (style == ProjectileStyle.Coin) CoopFx.Coin(run, position, direction, range);
            else CoopFx.Arrow(run, position, direction, range, infusion);
            var shot = Create(run, position, direction, damage, range, style, infusion);
            if (run.Player != null && run.Player.Powerups != null) shot.attack = run.Player.Powerups.ActiveAttack;
            return shot;
        }

        /// <summary>A teammate's arrow or coin: flies and stops like theirs, but their machine deals the damage.</summary>
        public static PlayerProjectile SpawnGhost(DungeonRun run, Vector2 position, Vector2 direction, float range,
            ProjectileStyle style = ProjectileStyle.Arrow, DamageElement infusion = DamageElement.Physical)
        {
            var arrow = Create(run, position, direction, 0, range, style, infusion);
            arrow.ghost = true;
            return arrow;
        }

        private static PlayerProjectile Create(DungeonRun run, Vector2 position, Vector2 direction, int damage, float range,
            ProjectileStyle style, DamageElement infusion)
        {
            bool coin = style == ProjectileStyle.Coin;
            Color color = coin ? GamblerAttack.Gold : infusion != DamageElement.Physical
                ? Color.Lerp(CombatDamage.ElementColor(infusion), Color.white, 0.25f) : new Color(0.95f, 1f, 0.65f);
            var arrow = (coin ? DungeonVisuals.CreateCoin("Coin", run.ProjectileRoot, position, CoinSize, 6)
                : DungeonVisuals.CreateArrow("Arrow", run.ProjectileRoot, position, 0.6f, color, 6)).gameObject.AddComponent<PlayerProjectile>();
            arrow.run = run;
            arrow.damage = damage;
            arrow.infusion = infusion;
            arrow.style = style;
            arrow.remainingRange = Mathf.Max(0f, range);
            arrow.Direction = direction.normalized;
            arrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            CombatVfx.Trail(arrow.gameObject, new Color(color.r, color.g, color.b, 0.8f), coin ? 0.14f : 0.07f, coin ? 0.16f : 0.1f);
            return arrow;
        }

        private ProjectileStyle style;
        private const float CoinSize = 0.3f;
        private bool IsCoin => style == ProjectileStyle.Coin;

        private void Update()
        {
            Advance(Time.deltaTime);
            // A thrown coin flips end over end: its face narrows to an edge and back, flashing as it turns.
            if (IsSpent || !IsCoin) return;
            float flip = Mathf.Cos(Time.time * 28f);
            transform.localScale = new Vector3(CoinSize, CoinSize * (0.15f + 0.85f * Mathf.Abs(flip)), 1f);
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.6f), 1f - Mathf.Abs(flip));
        }

        public void Advance(float deltaTime)
        {
            if (IsSpent || !run.IsPlaying || deltaTime <= 0) return;
            float distance = Mathf.Min((IsCoin ? CoinSpeed : ArrowSpeed) * deltaTime, remainingRange);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + Direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.08f) || HolyBubble.Blocks(transform.position, next) || IceWall.StopsShot(transform.position, next, !ghost))
                {
                    if (IsCoin) HeroVfx.Sparks(run.ProjectileRoot, transform.position, GamblerAttack.Gold, 7, 3f, 0.25f, -Direction, 140f, 0.8f);
                    else HeroVfx.Sparks(run.ProjectileRoot, transform.position, new Color(0.85f, 0.85f, 0.7f), 5, 2.5f, 0.2f, -Direction, 140f, 0.7f);
                    Consume();
                    return;
                }
                transform.position = next;
                if (!ghost) Breakable.SmashAt(run, next, 0.1f);
                for (int j = run.Enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = run.Enemies[j];
                    if (Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                    if (enemy == lastStruck) continue;
                    if (!ghost) Strike(enemy, next);
                    if (IsCoin) CoinImpact(next);
                    // Ricochet: the coin bounces on to one more enemy.
                    if (IsCoin && !ghost && !bounced && run.Player.Powerups.Count(PowerupType.Ricochet) > 0 && Rebound(enemy)) return;
                    Consume();
                    return;
                }
            }
            remainingRange -= distance;
            travelled += distance;
            if (remainingRange <= 0) Consume();
        }

        public const float SniperStep = 4f, PointBlankRange = 3f, HuntersMarkTime = 4f, RicochetRange = 5f;
        private bool bounced;
        private DungeonEnemy lastStruck;

        /// <summary>Turns the coin toward the nearest other enemy in range; false when there is none.</summary>
        private bool Rebound(DungeonEnemy from)
        {
            DungeonEnemy next = null;
            float best = RicochetRange;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy == from || enemy.Health <= 0) continue;
                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance < best && run.HasLineOfSight(transform.position, enemy.transform.position)) { best = distance; next = enemy; }
            }
            if (next == null) return false;
            bounced = true;
            lastStruck = from;
            Direction = ((Vector2)next.transform.position - (Vector2)transform.position).normalized;
            remainingRange = RicochetRange + 0.5f;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
            return true;
        }

        /// <summary>The local hero's hit, with the Archer's arrow talents.</summary>
        private void Strike(DungeonEnemy enemy, Vector2 at)
        {
            var player = run.Player;
            bool arrow = !IsCoin && player.ClassWeapon == WeaponType.Bow;
            int dealt = damage + (arrow && player.Powerups.Count(PowerupType.Sniper) > 0 ? Mathf.FloorToInt(travelled / SniperStep) : 0);
            // Heads or Tails: every coin hit is a flip for double or half.
            if (IsCoin && player.Powerups.Count(PowerupType.HeadsOrTails) > 0) dealt = Random.value < 0.5f ? dealt * 2 : Mathf.Max(1, dealt / 2);
            bool pointBlank = arrow && FullyCharged && travelled <= PointBlankRange && player.Powerups.Count(PowerupType.PointBlank) > 0;
            using (player.Powerups.ResumeAttack(attack)) CombatDamage.Apply(player, enemy, dealt, DamageElement.Physical, at - Direction, pointBlank ? 3f : 1f, infusion);
            if (enemy == null || enemy.Health <= 0) return;
            if (pointBlank) enemy.Stun(0.5f);
            if (arrow && FullyCharged && player.Powerups.Count(PowerupType.HuntersMark) > 0) enemy.Mark(HuntersMarkTime);
        }

        /// <summary>A coin strike rings out: a gold flash, a spray of glitter thrown onward and a white glint.</summary>
        private void CoinImpact(Vector2 point)
        {
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, point, 0.45f, new Color(1f, 0.9f, 0.5f, 0.8f), 0.18f);
            HeroVfx.Sparks(root, point, GamblerAttack.Gold, 9, 4f, 0.28f, Direction, 110f, 0.9f);
            HeroVfx.Sparks(root, point, Color.white, 4, 2.5f, 0.18f, -Direction, 160f, 0.6f);
        }

        private void Consume()
        {
            IsSpent = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}

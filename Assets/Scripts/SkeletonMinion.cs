using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A skeleton raised by the Reaper's <see cref="ArmyOfTheDead"/>. It chases the nearest enemy and hacks at it,
    /// falling back to its master's side when nothing is in sight. Its blows are dealt in the Reaper's name, so his
    /// talents apply to them, and its health and damage are fractions of his. Enemies it stands against wear it down.
    /// It lasts until it is destroyed or the floor ends.
    /// </summary>
    public sealed class SkeletonMinion : MonoBehaviour
    {
        public const float Sight = 9f, Reach = 0.75f, AttackInterval = 0.9f, FollowDistance = 1.6f, HurtInterval = 1f, Size = 0.8f;
        private DungeonPlayer master;
        private DungeonRun run;
        private SpriteRenderer body;
        private float readyAt, nextHurtAt, flashUntil, raisedAt, struckAt = float.NegativeInfinity;
        private SpriteRenderer shadow;
        private const float RiseTime = 0.35f;
        private int wounds;
        private static Sprite sprite;

        /// <summary>A third of the Reaper's maximum health, rounded up.</summary>
        public int MaxHealth => Mathf.Max(1, Mathf.CeilToInt(master.MaxHealth / 3f));
        public int Health => MaxHealth - wounds;
        /// <summary>Half the Reaper's damage, rounded up.</summary>
        public int Damage => Mathf.Max(1, Mathf.CeilToInt(master.Damage * 0.5f));

        // W bone, w shaded bone, D outline, G soul-fire in the eye sockets, K rusted blade, B hilt.
        public static Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite("Skeleton", new[]
        {
            "....DDDDDD......", "...DWWWWWWD.....", "..DWWWWWWWwD....", "..DWGGWWGGwD....",
            "..DWGGWWGGwD..K.", "..DWWWDDWWwD..K.", "...DWWWWWWD...K.", "...DWDWDWDD...K.",
            "....DDDDDD...DKD", "..DWDwWWwDWD.DKD", ".DWDDWDDWDDWDBBB", ".DWD.DWWD.DWWDB.",
            "..D..DwwD..DD...", "....DWDDWD......", "....DWD.DWD.....", "...DWWD.DWWD...."
        }, key => key == 'W' ? ReaperAttack.Bone : key == 'w' ? new Color(0.68f, 0.66f, 0.58f) : key == 'D' ? new Color(0.1f, 0.11f, 0.14f)
            : key == 'G' ? ReaperAttack.Soul : key == 'K' ? new Color(0.66f, 0.7f, 0.74f) : key == 'B' ? new Color(0.45f, 0.3f, 0.18f) : Color.clear);

        public static SkeletonMinion Raise(DungeonPlayer master, Vector2 position)
        {
            var renderer = DungeonVisuals.Create("Skeleton", master.Run.ProjectileRoot, position, Vector2.one * Size, Color.white, 4);
            renderer.sprite = Sprite;
            var skeleton = renderer.gameObject.AddComponent<SkeletonMinion>();
            skeleton.master = master;
            skeleton.run = master.Run;
            skeleton.body = renderer;
            skeleton.readyAt = Time.time + 0.4f;
            skeleton.nextHurtAt = Time.time + HurtInterval;
            skeleton.raisedAt = Time.time;
            // The shadow lives beside it, so the skeleton's own stretch and squash leave it alone.
            skeleton.shadow = DungeonVisuals.Create("Skeleton shadow", master.Run.ProjectileRoot, position, new Vector2(0.5f, 0.14f), new Color(0.01f, 0.02f, 0.04f, 0.35f), 3);
            renderer.transform.localScale = new Vector3(Size, 0f, 1f);
            return skeleton;
        }

        private void Update()
        {
            if (run == null || master == null || !run.IsPlaying) return;
            if (master.Health <= 0 || Health <= 0) { Crumble(); return; }
            Vector2 position = transform.position;
            var target = Nearest(position);
            Vector2 goal = target != null ? (Vector2)target.transform.position : (Vector2)master.transform.position;
            float stop = target != null ? Reach + target.HitRadius - 0.1f : FollowDistance;
            Vector2 toGoal = goal - position;
            if (toGoal.magnitude > stop)
            {
                float speed = master.Speed * 0.9f * master.Buffs.MoveMultiplier;
                transform.position = position = run.Map.Move(position, toGoal.normalized * speed * Time.deltaTime, 0.25f);
            }
            if (Mathf.Abs(toGoal.x) > 0.05f) body.flipX = toGoal.x < 0f;
            // It climbs up out of the ground, sways as it walks and leans into each blow.
            float rise = Mathf.Clamp01((Time.time - raisedAt) / RiseTime), strike = 1f - Mathf.Clamp01((Time.time - struckAt) / 0.18f);
            bool walking = toGoal.magnitude > stop;
            float sway = walking ? Mathf.Sin(Time.time * 13f) : 0f;
            transform.localScale = new Vector3(Size * (1f + 0.18f * strike), Size * (rise * rise * (3f - 2f * rise)) * (1f - 0.12f * strike + 0.04f * Mathf.Abs(sway)), 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, sway * 5f - (body.flipX ? -1f : 1f) * 14f * strike);
            if (shadow != null) shadow.transform.position = position + Vector2.down * Size * 0.5f;
            body.color = Time.time < flashUntil ? PlayerHurt : Color.Lerp(new Color(0.6f, 0.6f, 0.6f), Color.white, Health / (float)MaxHealth);
            if (target != null && Time.time >= readyAt && Vector2.Distance(position, target.transform.position) <= Reach + target.HitRadius)
            {
                readyAt = Time.time + AttackInterval * master.Powerups.AttackIntervalMultiplier;
                struckAt = Time.time;
                Vector2 aim = ((Vector2)target.transform.position - position).normalized;
                HeroVfx.Slash(run.ProjectileRoot, position, aim, Reach + 0.3f, 90f, ReaperAttack.Bone, 0.15f);
                CombatDamage.Apply(master, target, Damage, DamageElement.Physical, position, 0.3f);
            }
            // Whatever it stands toe to toe with hits back, once a second.
            if (Time.time < nextHurtAt) return;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.IsHeld || enemy.IsRanged
                    || Vector2.Distance(position, enemy.transform.position) > enemy.HitRadius + 0.5f) continue;
                nextHurtAt = Time.time + HurtInterval;
                wounds++;
                flashUntil = Time.time + 0.15f;
                HeroVfx.Sparks(run.ProjectileRoot, position, ReaperAttack.Bone, 6, 3f, 0.25f, position - (Vector2)enemy.transform.position, 120f);
                break;
            }
        }

        private void OnDestroy() { if (shadow != null) Destroy(shadow.gameObject); }

        private static readonly Color PlayerHurt = new Color(1f, 0.35f, 0.35f);

        private DungeonEnemy Nearest(Vector2 from)
        {
            DungeonEnemy best = null;
            float bestDistance = Sight;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.IsInvulnerable) continue;
                float distance = Vector2.Distance(from, enemy.transform.position);
                if (distance < bestDistance && run.HasLineOfSight(from, enemy.transform.position)) { bestDistance = distance; best = enemy; }
            }
            return best;
        }

        private void Crumble()
        {
            HeroVfx.Sparks(run.ProjectileRoot, transform.position, ReaperAttack.Bone, 12, 3.5f, 0.4f);
            HeroVfx.Pulse(run.ProjectileRoot, transform.position, 0.6f, ReaperAttack.Shade, 0.3f);
            if (shadow != null) Destroy(shadow.gameObject);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}

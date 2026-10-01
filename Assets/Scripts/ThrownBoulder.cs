using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Boulder Toss: a chunk of floor the Behemoth rips up and hurls at the cursor. It shatters on the first enemy (or
    /// wall) it meets, showering the enemies around it with rubble, and leaves a <see cref="RockCover"/> behind.
    /// </summary>
    public sealed class ThrownBoulder : MonoBehaviour
    {
        public const float Speed = 13f, Radius = 0.32f, ShatterRadius = 1.8f;
        private SpecimenAttack owner;
        private DungeonRun run;
        private Vector2 origin, direction, position;
        private float range, travelled, spin, coverTime, coverWidth;
        private int damage, shardDamage;
        private FlameMesh mesh;

        public static void Throw(SpecimenAttack owner, Vector2 aim, float range, int damage, int shardDamage, float coverTime, float coverWidth)
        {
            var player = owner.Player;
            var boulder = new GameObject("Boulder").AddComponent<ThrownBoulder>();
            boulder.transform.SetParent(player.Run.ProjectileRoot, false);
            boulder.owner = owner;
            boulder.run = player.Run;
            boulder.direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            boulder.origin = boulder.position = (Vector2)player.transform.position + boulder.direction * 0.4f * owner.BodyScale;
            boulder.range = range;
            boulder.damage = damage;
            boulder.shardDamage = shardDamage;
            boulder.coverTime = coverTime;
            boulder.coverWidth = coverWidth;
            boulder.mesh = new FlameMesh(boulder.gameObject, 8);
            // Torn out of the floor at his feet.
            HeroVfx.Sparks(player.Run.ProjectileRoot, player.transform.position, SpecimenCatalog.Stone, 10, 3f, 0.35f, Vector2.up, 120f);
            ScreenFx.Shake(0.08f, 0.1f);
        }

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null || owner == null) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            float distance = Mathf.Min(Speed * Time.deltaTime, range - travelled);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.1f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = position + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.1f)) { Impact(position, null); return; }
                position = next;
                travelled += distance / steps;
                foreach (var enemy in run.Enemies)
                {
                    if (enemy == null || enemy.Health <= 0 || Vector2.Distance(position, enemy.transform.position) > enemy.HitRadius + Radius) continue;
                    Impact(position, enemy);
                    return;
                }
            }
            if (travelled >= range - 0.001f) { Impact(position, null); return; }
            spin += Time.deltaTime * 9f;
            Draw();
        }

        private void Impact(Vector2 at, DungeonEnemy struck)
        {
            var player = owner.Player;
            if (struck != null) owner.HitAndShove(struck, damage, at - direction, 2f, true);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy == struck || enemy.Health <= 0) continue;
                if (Vector2.Distance(at, enemy.transform.position) > ShatterRadius + enemy.HitRadius || !run.HasLineOfSight(at, enemy.transform.position)) continue;
                CombatDamage.Apply(player, enemy, struck != null ? shardDamage : damage, DamageElement.Physical, at, 1f);
            }
            HeroVfx.Sparks(run.ProjectileRoot, at, SpecimenCatalog.Stone, 22, 5f, 0.45f);
            HeroVfx.Pulse(run.ProjectileRoot, at, ShatterRadius, FlameMesh.Alpha(SpecimenCatalog.Stone, 0.6f), 0.3f);
            CoopFx.Bolt(run, origin, at, SpecimenCatalog.Stone, true);
            CoopFx.Pulse(run, at, ShatterRadius, SpecimenCatalog.Stone, 0.3f);
            ScreenFx.Shake(0.14f, 0.18f);
            // The rubble settles into a wall across the throw, a step short of where it broke.
            Vector2 cover = at - direction * 0.45f;
            if (!run.Map.CanStand(cover, 0.1f)) cover = at;
            RockCover.Raise(run, cover, direction, coverWidth, coverTime, false);
            Destroy(gameObject);
        }

        private void Draw()
        {
            mesh.Begin();
            Color light = new Color(0.78f, 0.76f, 0.72f), mid = SpecimenCatalog.Stone, dark = new Color(0.36f, 0.35f, 0.34f);
            mesh.Ellipse(position + new Vector2(0.04f, -0.06f), Radius * 1.05f, Radius * 0.55f, new Color(0f, 0f, 0f, 0.25f), new Color(0f, 0f, 0f, 0f), 18);
            // A lumpy rock: a few overlapping discs tumbling around its middle.
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = FlameMesh.Polar(spin + i * Mathf.PI * 0.5f, Radius * 0.35f);
                mesh.Disc(position + offset, Radius * (0.62f + 0.08f * (i % 2)), i == 0 ? light : mid, dark, 14);
            }
            mesh.Disc(position + FlameMesh.Polar(spin + 0.8f, Radius * 0.3f), Radius * 0.25f, light, mid, 10);
            mesh.Commit();
        }

        private void OnDestroy() => mesh?.Release();
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>Holy Ground: where a holy sword struck, the ground glows for a while and slows enemies crossing it.</summary>
    public sealed class HallowedGround : MonoBehaviour
    {
        public const float Radius = 1.2f, Duration = 3f;
        private DungeonRun run;
        private float until, nextSlow;

        public static void Leave(DungeonRun run, Vector2 at)
        {
            var ground = new GameObject("Hallowed ground").AddComponent<HallowedGround>();
            ground.transform.SetParent(run.ProjectileRoot, false);
            ground.transform.position = at;
            ground.run = run;
            ground.until = Time.time + Duration;
            CombatVfx.Ring(run.ProjectileRoot, at, Radius, FlameMesh.Alpha(AbilityCatalog.Gold, 0.6f), Duration);
            CoopFx.Ring(run, at, Radius, FlameMesh.Alpha(AbilityCatalog.Gold, 0.6f), Duration);
        }

        private void Update()
        {
            if (run == null || Time.time >= until) { Destroy(gameObject); return; }
            if (!run.IsPlaying || Time.time < nextSlow) return;
            nextSlow = Time.time + 0.3f;
            Vector2 at = transform.position;
            foreach (var enemy in run.Enemies)
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= Radius + enemy.HitRadius) enemy.Chill(0.4f);
        }
    }
}

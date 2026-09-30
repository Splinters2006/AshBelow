using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Smoke Bomb: a cloud at the Assassin's feet. While she stands in it enemies lose track of her, and every hit she
    /// lands on an enemy inside counts as a backstab.
    /// </summary>
    public sealed class SmokeCloud : MonoBehaviour
    {
        public const float Radius = 2.6f, Duration = 4f;
        private static readonly List<SmokeCloud> clouds = new List<SmokeCloud>();
        private static readonly Color Smoke = new Color(0.55f, 0.55f, 0.62f);
        private DungeonPlayer player;
        private float until, nextPuff;

        public static void Drop(DungeonPlayer player, float duration)
        {
            var cloud = new GameObject("Smoke cloud").AddComponent<SmokeCloud>();
            cloud.transform.SetParent(player.Run.ProjectileRoot, false);
            cloud.transform.position = player.transform.position;
            cloud.player = player;
            cloud.until = Time.time + duration;
            ShadowstepVfx.Puff(player.Run.ProjectileRoot, player.transform.position);
            CoopFx.Shadowstep(player.Run, player.transform.position, player.transform.position);
        }

        /// <summary>True when <paramref name="position"/> is inside one of the local hero's smoke clouds.</summary>
        public static bool Covers(Vector2 position)
        {
            foreach (var cloud in clouds)
                if (cloud != null && Time.time < cloud.until && Vector2.Distance(position, cloud.transform.position) <= Radius) return true;
            return false;
        }

        private void OnEnable() => clouds.Add(this);
        private void OnDisable() => clouds.Remove(this);

        private void Update()
        {
            if (player == null || player.Run == null || Time.time >= until) { Destroy(gameObject); return; }
            if (!player.Run.IsPlaying) return;
            Vector2 center = transform.position;
            if (Vector2.Distance(player.transform.position, center) <= Radius) player.Veil(0.15f);
            if (Time.time < nextPuff) return;
            nextPuff = Time.time + 0.3f;
            HeroVfx.Motes(player.Run.ProjectileRoot, center, Radius * 0.9f, FlameMesh.Alpha(Smoke, 0.8f), 10, 0.9f);
            CoopFx.Ring(player.Run, center, Radius, FlameMesh.Alpha(Smoke, 0.5f), 0.35f);
            CombatVfx.Ring(player.Run.ProjectileRoot, center, Radius, FlameMesh.Alpha(Smoke, 0.5f), 0.35f);
        }
    }
}

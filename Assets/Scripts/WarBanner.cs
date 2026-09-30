using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// War Banner: planted at the Knight's feet for a few seconds. Heroes inside deal +1 damage, and planting it gives
    /// everyone inside a ward. Teammates on other machines are blessed for the bonus instead.
    /// </summary>
    public sealed class WarBanner : MonoBehaviour
    {
        public const float Radius = 3f, Duration = 6f;
        public const int Bonus = 1;
        private static readonly List<WarBanner> active = new List<WarBanner>();
        private static readonly Color Crimson = new Color(0.9f, 0.2f, 0.25f);
        private DungeonPlayer player;
        private float until, nextPulse;

        /// <summary>The damage bonus for standing at <paramref name="position"/>.</summary>
        public static int BonusAt(Vector2 position)
        {
            foreach (var banner in active)
                if (banner != null && Time.time < banner.until && Vector2.Distance(position, banner.transform.position) <= Radius) return Bonus;
            return 0;
        }

        public static void Plant(DungeonPlayer player, float duration)
        {
            var run = player.Run;
            Vector2 at = player.transform.position;
            var pole = DungeonVisuals.Create("War banner", run.ProjectileRoot, at + Vector2.up * 0.45f, new Vector2(0.08f, 1.3f), new Color(0.55f, 0.45f, 0.35f), 4);
            var flag = DungeonVisuals.Create("Banner cloth", pole.transform, at + new Vector2(0.24f, 0.8f), new Vector2(0.45f, 0.35f), Crimson, 5);
            flag.transform.localScale = new Vector3(5.6f, 0.27f, 1f);
            var banner = pole.gameObject.AddComponent<WarBanner>();
            banner.player = player;
            banner.until = Time.time + duration;
            player.Powerups.AddWard();
            run.Coop?.SupportAllies(at, Radius, SupportKind.Ward, 1, 0f);
            HeroVfx.Pulse(run.ProjectileRoot, at, Radius, Crimson, 0.5f);
            CoopFx.Pulse(run, at, Radius, Crimson, 0.5f);
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        private void Update()
        {
            if (player == null || player.Run == null) { Destroy(gameObject); return; }
            if (Time.time >= until) { Destroy(gameObject); return; }
            if (!player.Run.IsPlaying || Time.time < nextPulse) return;
            nextPulse = Time.time + 0.5f;
            Vector2 at = transform.position;
            CombatVfx.Ring(player.Run.ProjectileRoot, at + Vector2.down * 0.45f, Radius, FlameMesh.Alpha(Crimson, 0.5f), 0.5f);
            // Teammates keep their bonus refreshed while they stand inside.
            player.Run.Coop?.SupportAllies(at, Radius, SupportKind.Bless, Bonus, 0.7f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// War Banner: planted at the Knight's feet for a few seconds. Heroes inside deal +1 damage, and planting it gives
    /// everyone inside two wards. Teammates on other machines are blessed for the bonus instead.
    /// </summary>
    public sealed class WarBanner : MonoBehaviour
    {
        public const float Radius = 4f, Duration = 6f;
        public const int Bonus = 1, Wards = 2;
        private static readonly List<WarBanner> active = new List<WarBanner>();
        public static readonly Color Crimson = new Color(0.9f, 0.2f, 0.25f);
        private DungeonPlayer player;
        private Vector2 at;
        private float until, nextPulse;

        /// <summary>The damage bonus for standing at <paramref name="position"/>.</summary>
        public static int BonusAt(Vector2 position)
        {
            foreach (var banner in active)
                if (banner != null && Time.time < banner.until && Vector2.Distance(position, banner.at) <= Radius) return Bonus;
            return 0;
        }

        public static void Plant(DungeonPlayer player, float duration)
        {
            var run = player.Run;
            Vector2 at = player.transform.position;
            var banner = new GameObject("War banner").AddComponent<WarBanner>();
            banner.transform.SetParent(run.ProjectileRoot, false);
            banner.player = player;
            banner.at = at;
            banner.until = Time.time + duration;
            for (int i = 0; i < Wards; i++) player.Powerups.AddWard();
            run.Coop?.SupportAllies(at, Radius, SupportKind.Ward, Wards, 0f);
            WarBannerVfx.Play(run.ProjectileRoot, at, duration, Radius);
            CoopFx.WarBanner(run, at, duration, Radius);
            ScreenFx.Shake(0.1f, 0.12f);
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        private void Update()
        {
            if (player == null || player.Run == null) { Destroy(gameObject); return; }
            if (Time.time >= until) { Destroy(gameObject); return; }
            if (!player.Run.IsPlaying || Time.time < nextPulse) return;
            nextPulse = Time.time + 0.5f;
            // Teammates keep their bonus refreshed while they stand inside.
            player.Run.Coop?.SupportAllies(at, Radius, SupportKind.Bless, Bonus, 0.7f);
        }
    }

    /// <summary>
    /// The planted banner: a wooden pole with a gold spear-tip finial and crossbar, a crimson flag with a gold border,
    /// fringe and a gold sword emblem rippling in the wind, over a heraldic circle on the ground whose rim turns
    /// slowly and pulses. Planting it slams down with a ring of dust. Purely visual.
    /// </summary>
    public sealed class WarBannerVfx : MonoBehaviour
    {
        private const float PlantTime = 0.18f, FadeTime = 0.3f, PoleHeight = 1.7f, FlagWidth = 0.7f, FlagHeight = 0.85f;
        private static readonly Color Wood = new Color(0.45f, 0.3f, 0.18f), WoodDark = new Color(0.28f, 0.18f, 0.1f),
            Cloth = new Color(0.78f, 0.12f, 0.18f), ClothDark = new Color(0.5f, 0.06f, 0.1f), Gold = new Color(1f, 0.82f, 0.3f);
        private Vector2 at;
        private float age, duration, radius;
        private FlameMesh ground, banner;
        private bool landed;
        private Transform root;

        public static void Play(Transform root, Vector2 at, float duration, float radius)
        {
            if (root == null) return;
            var vfx = new GameObject("War banner art").AddComponent<WarBannerVfx>();
            // The owner stays at the origin: FlameMesh draws in its local space.
            vfx.transform.SetParent(root, false);
            vfx.root = root;
            vfx.at = at;
            vfx.duration = duration;
            vfx.radius = radius;
            vfx.ground = new FlameMesh(vfx.gameObject, 2);
            var upright = new GameObject("Banner");
            upright.transform.SetParent(vfx.transform, false);
            vfx.banner = new FlameMesh(upright, 6);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= duration + FadeTime) { Destroy(gameObject); return; }
            float alpha = Mathf.Clamp01((duration + FadeTime - age) / FadeTime);
            float plant = Mathf.Clamp01(age / PlantTime);
            if (!landed && plant >= 1f)
            {
                landed = true;
                HeroVfx.Sparks(root, at, new Color(0.7f, 0.62f, 0.5f, 0.8f), 14, 3.5f, 0.35f, Vector2.up, 200f, 1f);
                HeroVfx.Pulse(root, at, radius, FlameMesh.Alpha(WarBanner.Crimson, 0.6f), 0.45f);
            }
            float time = Time.time;
            DrawGround(alpha, plant, time);
            // Drops in from above and plants.
            Vector2 foot = at + Vector2.up * (1f - plant * plant) * 2.5f;
            DrawBanner(foot, alpha, time);
        }

        private void DrawGround(float alpha, float plant, float time)
        {
            Color crimson = WarBanner.Crimson;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f);
            float grow = landed ? 1f - Mathf.Pow(1f - Mathf.Clamp01((age - PlantTime) / 0.3f), 3f) : 0f;
            float r = radius * grow;
            ground.Begin();
            if (r > 0.05f)
            {
                ground.Disc(at, r, FlameMesh.Alpha(crimson, 0.14f * alpha), FlameMesh.Alpha(crimson, (0.05f + 0.04f * pulse) * alpha), 48);
                ground.Ring(at, r, 0.09f, FlameMesh.Alpha(Color.Lerp(crimson, Gold, 0.3f), (0.6f + 0.3f * pulse) * alpha), 64);
                ground.Ring(at, r * 0.92f, 0.03f, FlameMesh.Alpha(Gold, 0.45f * alpha), 64);
                // Turning rim: gold studs between the two rings, like a shield's edge.
                for (int i = 0; i < 16; i++)
                {
                    float a = time * 0.4f + i * Mathf.PI / 8f;
                    ground.Diamond(at + FlameMesh.Polar(a, r * 0.96f), 0.07f, FlameMesh.Alpha(Gold, 0.8f * alpha));
                }
                // Four heraldic rays from the banner out to the rim.
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                    Vector2 dir = FlameMesh.Polar(a, 1f), side = Vector2.Perpendicular(dir);
                    ground.Quad(at + side * 0.15f, at - side * 0.15f, at + dir * r * 0.88f, at + dir * r * 0.88f,
                        FlameMesh.Alpha(crimson, 0.25f * alpha), FlameMesh.Alpha(crimson, 0.25f * alpha), FlameMesh.Alpha(crimson, 0f), FlameMesh.Alpha(crimson, 0f));
                }
            }
            ground.Ellipse(at, 0.35f, 0.14f, new Color(0f, 0f, 0f, 0.4f * alpha * plant), new Color(0f, 0f, 0f, 0f), 16);
            ground.Commit();
        }

        private void DrawBanner(Vector2 foot, float alpha, float time)
        {
            Color C(Color c) => FlameMesh.Alpha(c, alpha);
            Vector2 top = foot + Vector2.up * PoleHeight;
            banner.Begin();
            // Pole with a darker edge, crossbar and a gold spear-tip finial.
            banner.Bar(foot, Vector2.up, PoleHeight, 0.1f, C(Wood), C(Wood));
            banner.Bar(foot + Vector2.right * 0.03f, Vector2.up, PoleHeight, 0.035f, C(WoodDark), C(WoodDark));
            Vector2 bar = top + Vector2.down * 0.12f;
            banner.Bar(bar + Vector2.left * 0.05f, Vector2.right, FlagWidth + 0.12f, 0.07f, C(Gold), C(Gold));
            banner.Disc(bar + Vector2.right * (FlagWidth + 0.07f), 0.05f, C(Gold), C(Gold), 8);
            banner.Triangle(top + new Vector2(-0.09f, 0f), top + Vector2.up * 0.3f, top + new Vector2(0.09f, 0f), C(Gold), C(Color.white), C(Gold));
            banner.Disc(top, 0.07f, C(Gold), C(new Color(0.8f, 0.6f, 0.2f)), 10);

            // The flag hangs from the crossbar and ripples: each column sways a little more the farther from the pole.
            const int Columns = 8, Rows = 6;
            var grid = new Vector2[Columns + 1, Rows + 1];
            for (int x = 0; x <= Columns; x++)
                for (int y = 0; y <= Rows; y++)
                {
                    float u = x / (float)Columns, v = y / (float)Rows;
                    float wave = Mathf.Sin(time * 5f - u * 4f + v * 1.5f) * 0.06f * u;
                    // A swallow-tail notch cut up into the bottom edge.
                    float drop = FlagHeight * v - (y == Rows ? Mathf.Max(0f, 0.18f - Mathf.Abs(u - 0.5f) * 0.6f) : 0f);
                    grid[x, y] = bar + new Vector2(0.02f + FlagWidth * u + wave * 0.5f, -drop + wave);
                }
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                {
                    float shade = 0.5f + 0.5f * Mathf.Sin(time * 5f - (x + 0.5f) / Columns * 4f);
                    bool border = x == 0 || x == Columns - 1 || y == 0;
                    Color c = border ? Gold : Color.Lerp(ClothDark, Cloth, 0.55f + 0.45f * shade);
                    banner.Quad(grid[x, y], grid[x + 1, y], grid[x + 1, y + 1], grid[x, y + 1], C(c), C(c), C(c), C(c));
                }
            // Fringe along the bottom edge.
            for (int x = 0; x <= Columns; x++)
                banner.Bar(grid[x, Rows], Vector2.down, 0.08f, 0.025f, C(Gold), C(new Color(0.8f, 0.6f, 0.2f)));
            // Emblem: a gold sword pointing down the middle of the flag.
            Vector2 hilt = Vector2.Lerp(grid[Columns / 2, 1], grid[Columns / 2, 2], 0.5f);
            Vector2 point = grid[Columns / 2, Rows - 1];
            Vector2 blade = point - hilt;
            float length = blade.magnitude;
            if (length > 0.01f)
            {
                Vector2 dir = blade / length, side = Vector2.Perpendicular(dir);
                banner.Quad(hilt - side * 0.035f, hilt + side * 0.035f, point, point, C(Color.white), C(Color.white), C(Gold), C(Gold));
                banner.Bar(hilt - side * 0.1f, side, 0.2f, 0.04f, C(Gold), C(Gold));
                banner.Disc(hilt - dir * 0.05f, 0.035f, C(Gold), C(Gold), 8);
            }
            banner.Commit();
        }

        private void OnDestroy()
        {
            ground?.Release();
            banner?.Release();
        }
    }
}

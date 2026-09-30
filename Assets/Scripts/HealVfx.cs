using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Healing Light, kept distinct from the Paladin's gold holy effects: a soft column of mint-green light settles on
    /// the healed hero, a gentle ring spreads at their feet, and little green-white crosses float up around them.
    /// </summary>
    public sealed class HealVfx : MeshEffect
    {
        public static readonly Color Mint = new Color(0.5f, 1f, 0.7f);
        private const float Life = 1f;
        private Transform follow;
        private Vector2 at;

        /// <summary>Plays on <paramref name="hero"/>, following them as they move.</summary>
        public static void Play(Transform root, Transform hero)
        {
            if (hero == null) return;
            var effect = Spawn<HealVfx>(root, Life, 9);
            if (effect == null) return;
            effect.follow = hero;
            effect.at = hero.position;
            effect.Redraw();
            HeroVfx.Sparks(root, hero.position, Color.white, 5, 1.5f, 0.4f, Vector2.up, 100f, 0.6f);
        }

        /// <summary>Heals landing on every hero (this machine's and teammates') within <paramref name="radius"/> of <paramref name="center"/>.</summary>
        public static void PlayAround(DungeonRun run, Vector2 center, float radius)
        {
            if (run == null || run.ProjectileRoot == null) return;
            foreach (var ally in Object.FindObjectsByType<DungeonPlayer>())
                if (ally.Run == run && ally.Health > 0 && Vector2.Distance(center, ally.transform.position) <= radius) Play(run.ProjectileRoot, ally.transform);
            if (run.IsNetworked)
                foreach (var hero in run.Coop.RemoteHeroes)
                    if (hero != null && hero.IsAlive && Vector2.Distance(center, hero.transform.position) <= radius) Play(run.ProjectileRoot, hero.transform);
        }

        protected override void LateUpdate()
        {
            if (follow != null) at = follow.position;
            base.LateUpdate();
        }

        protected override void Draw(float t)
        {
            float fade = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            Vector2 feet = at + Vector2.down * 0.45f;
            // The column of light settling down onto them, narrowing as it fades.
            float width = 0.5f * (1f - 0.3f * t), settle = EaseOut(Mathf.Clamp01(t / 0.3f));
            Vector2 top = feet + Vector2.up * (4f - 2.5f * settle);
            Mesh.Quad(feet - Vector2.right * width, top - Vector2.right * width * 0.5f, top + Vector2.right * width * 0.5f, feet + Vector2.right * width,
                FlameMesh.Alpha(Mint, 0.45f * fade), FlameMesh.Alpha(Mint, 0f), FlameMesh.Alpha(Mint, 0f), FlameMesh.Alpha(Mint, 0.45f * fade));
            Mesh.Quad(feet - Vector2.right * width * 0.3f, top - Vector2.right * width * 0.15f, top + Vector2.right * width * 0.15f, feet + Vector2.right * width * 0.3f,
                FlameMesh.Alpha(Color.white, 0.5f * fade), FlameMesh.Alpha(Color.white, 0f), FlameMesh.Alpha(Color.white, 0f), FlameMesh.Alpha(Color.white, 0.5f * fade));
            // A soft ring spreading on the floor.
            float ring = EaseOut(t);
            Mesh.Ellipse(feet, 0.3f + 0.6f * ring, (0.3f + 0.6f * ring) * 0.4f, FlameMesh.Alpha(Mint, 0.35f * fade), FlameMesh.Alpha(Mint, 0f), 24);
            // Crosses floating up around them, each on its own little delay.
            for (int i = 0; i < 6; i++)
            {
                float seed = FlameMesh.Hash(i, 6.6f), local = Mathf.Clamp01((t - seed * 0.35f) / 0.6f);
                if (local <= 0f || local >= 1f) continue;
                Vector2 p = at + new Vector2((seed - 0.5f) * 1f + Mathf.Sin(local * 6f + i) * 0.05f, -0.2f + local * 1.1f);
                float size = 0.07f + 0.03f * seed, a = Mathf.Sin(local * Mathf.PI) * fade;
                Color c = FlameMesh.Alpha(Color.Lerp(Mint, Color.white, 0.5f), a);
                Mesh.Bar(p + Vector2.down * size, Vector2.up, size * 2f, size * 0.7f, c, c);
                Mesh.Bar(p + Vector2.left * size, Vector2.right, size * 2f, size * 0.7f, c, c);
            }
        }
    }
}

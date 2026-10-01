using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Feast!: the souls the Reaper is about to eat appear in a ring around him and in a row over his head, then are
    /// dragged into his jaws one after another, each one struck off the row as it goes. Once the last is swallowed a
    /// mint-green cross rises from him for every hit point it restored.
    /// </summary>
    public sealed class FeastVfx : MonoBehaviour
    {
        private const float EatStart = 0.12f, EatSpan = 0.5f, Gulp = 0.2f, HealTime = 0.75f, OrbitRadius = 1.15f;
        private static readonly Color Spent = new Color(0.95f, 0.25f, 0.3f);
        private Transform hero, root;
        private FlameMesh mesh;
        private float age, spin;
        private int souls, healed, eaten;
        private bool burst;

        public static void Play(Transform root, Transform hero, int souls, int healed)
        {
            if (root == null || hero == null || souls <= 0) return;
            var vfx = new GameObject("Feast").AddComponent<FeastVfx>();
            vfx.transform.SetParent(root, false);
            vfx.root = root;
            vfx.hero = hero;
            vfx.souls = souls;
            vfx.healed = healed;
            vfx.spin = Random.value * Mathf.PI * 2f;
            vfx.mesh = new FlameMesh(vfx.gameObject, 9);
        }

        /// <summary>When soul <paramref name="index"/> reaches his jaws.</summary>
        private float EatenAt(int index) => EatStart + Gulp + EatSpan * index / Mathf.Max(1, souls);
        private float FeastEnd => EatenAt(souls - 1);

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (hero == null || age >= FeastEnd + HealTime) { Destroy(gameObject); return; }
            Vector2 body = hero.position, jaws = body + Vector2.up * 0.3f;
            mesh.Begin();
            for (int i = 0; i < souls; i++)
            {
                float eatenAt = EatenAt(i);
                Vector2 slot = body + new Vector2((i - (souls - 1) * 0.5f) * 0.26f, 1.2f);
                if (age >= eatenAt)
                {
                    if (i >= eaten)
                    {
                        eaten = i + 1;
                        HeroVfx.Sparks(root, jaws, ReaperAttack.Soul, 8, 3f, 0.25f, Vector2.down, 150f, 0.9f);
                        HeroVfx.Pulse(root, jaws, 0.4f, ReaperAttack.Soul, 0.18f);
                    }
                    // Struck off the row: a grey husk with a red line through it, sinking away.
                    float gone = Mathf.Clamp01((age - eatenAt) / 0.45f);
                    Vector2 husk = slot + Vector2.down * 0.25f * gone;
                    mesh.Diamond(husk, 0.11f, FlameMesh.Alpha(new Color(0.35f, 0.38f, 0.4f), 1f - gone));
                    mesh.Bar(husk + new Vector2(-0.12f, -0.12f), new Vector2(1f, 1f).normalized, 0.34f, 0.05f, FlameMesh.Alpha(Spent, 1f - gone), FlameMesh.Alpha(Spent, 1f - gone));
                    continue;
                }
                mesh.Diamond(slot, 0.12f + 0.02f * Mathf.Sin(Time.time * 10f + i), ReaperAttack.Soul);
                // It circles him until its turn, then is dragged in to his jaws.
                Vector2 orbit = body + FlameMesh.Polar(spin + age * 3.5f + i * Mathf.PI * 2f / souls, OrbitRadius);
                float pull = Mathf.Clamp01((age - (eatenAt - Gulp)) / Gulp);
                Vector2 at = Vector2.Lerp(orbit, jaws, pull * pull);
                if (pull > 0f)
                {
                    Vector2 back = orbit - at;
                    if (back.sqrMagnitude > 0.001f) mesh.Bar(at, back.normalized, Mathf.Min(0.6f, back.magnitude), 0.14f, FlameMesh.Alpha(ReaperAttack.Soul, 0.8f), FlameMesh.Alpha(ReaperAttack.Soul, 0f));
                }
                mesh.Disc(at, 0.16f * (1f - 0.4f * pull), Color.white, FlameMesh.Alpha(ReaperAttack.Soul, 0.5f), 12);
            }
            float heal = age - FeastEnd;
            if (heal >= 0f)
            {
                if (!burst)
                {
                    burst = true;
                    HealVfx.Play(root, hero);
                    HeroVfx.Pulse(root, body, 1.3f, HealVfx.Mint, 0.45f);
                }
                // One cross per hit point, rising out of him side by side.
                for (int i = 0; i < healed; i++)
                {
                    float t = Mathf.Clamp01((heal - i * 0.1f) / (HealTime - 0.2f));
                    if (t <= 0f) continue;
                    float fade = 1f - t * t, size = 0.2f + 0.08f * Mathf.Sin(t * Mathf.PI);
                    Vector2 at = body + new Vector2((i - (healed - 1) * 0.5f) * 0.5f, 0.5f + 1.1f * (1f - (1f - t) * (1f - t)));
                    Color mint = FlameMesh.Alpha(HealVfx.Mint, fade), white = FlameMesh.Alpha(Color.white, fade);
                    mesh.Bar(at + Vector2.left * size, Vector2.right, size * 2f, size * 0.75f, mint, mint);
                    mesh.Bar(at + Vector2.down * size, Vector2.up, size * 2f, size * 0.75f, mint, mint);
                    mesh.Bar(at + Vector2.left * size * 0.7f, Vector2.right, size * 1.4f, size * 0.3f, white, white);
                    mesh.Bar(at + Vector2.down * size * 0.7f, Vector2.up, size * 1.4f, size * 0.3f, white, white);
                }
            }
            mesh.Commit();
        }

        private void OnDestroy() { mesh?.Release(); }
    }
}

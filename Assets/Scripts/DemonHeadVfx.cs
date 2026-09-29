using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Demonic Power: the Demon Lord's massive horned head rises out of the ground behind the Demoness, its eyes blaze,
    /// its jaw drops and it roars, sending shockwaves of terror out to the edge of the fear, then it sinks away.
    /// Purely visual; <see cref="DemonicPower"/> applies the fear.
    /// </summary>
    public sealed class DemonHeadVfx : MonoBehaviour
    {
        private const float RiseTime = 0.35f, RoarStart = 0.45f, RoarEnd = 1.3f, Lifetime = 1.9f;
        private const float PixelsPerUnit = 24f;
        private static readonly Color Hellfire = new Color(1f, 0.16f, 0.08f);

        // Pixel art drawn as left halves and mirrored. N outline, H/h horn bone and shade, R blood-red skin, r dark skin,
        // L skin highlight, E/e blazing eyes, B the black maw, T fangs.
        private static readonly string[] HeadHalf =
        {
            "NH..............", "NHh.............", ".NHh............", ".NHhh...........", "..NHhh.....NNNNN",
            "..NHHhNNNNNrrrrr", "...NHhrRRRRRRRRR", "...NNrRRRLRRRRRR", "....NrRRRRRRRRRR", "....NrRLRRRRRRNN",
            "...NrRRRNNNRRRRR", "...NrRRNeEEeNRRR", "...NrRRNEEEeNRRR", "...NrRRRNNNNRRRR", "...NrRRRRRRRRNrR",
            "....NrRRRRRRRNrN", "....NrRRRRRRRRRR", "....NrRBBBBBBBBB", "....NrRBTBTBTBTB", ".....NrBBBBBBBBB",
        };
        private static readonly string[] JawHalf =
        {
            ".....NrBBBBBBBBB", ".....NrBTBTBTBTB", ".....NrRRRRRRRRR", "......NrRRRRRRRR", ".......NNrRRRRRR", ".........NNNNNNN",
        };
        private static Sprite headSprite, jawSprite;
        private static Sprite HeadSprite => headSprite != null ? headSprite : headSprite = Mirrored("Demon head", HeadHalf, new Vector2(0.5f, 0f));
        private static Sprite JawSprite => jawSprite != null ? jawSprite : jawSprite = Mirrored("Demon jaw", JawHalf, new Vector2(0.5f, 1f));

        private FlameMesh mesh;
        private SpriteRenderer head, jaw;
        private Vector2 center;
        private float radius, age;

        public static DemonHeadVfx Play(Transform root, Vector2 center, float radius)
        {
            if (root == null) return null;
            var effect = new GameObject("Demon head").AddComponent<DemonHeadVfx>();
            effect.transform.SetParent(root, false);
            effect.center = center;
            effect.radius = radius;
            effect.mesh = new FlameMesh(effect.gameObject, 10);
            effect.head = effect.CreateLayer("Head", HeadSprite, 12);
            effect.jaw = effect.CreateLayer("Jaw", JawSprite, 11);
            return effect;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            float rise = EaseOut(age / RiseTime);
            float roar = Mathf.Clamp01((age - RoarStart) / (RoarEnd - RoarStart));
            float fade = age < RoarEnd ? 1f : 1f - (age - RoarEnd) / (Lifetime - RoarEnd);
            // Wide enough to loom over the whole area of the fear.
            float scale = radius * 1.05f * Mathf.Lerp(0.55f, 1f, rise);
            bool roaring = age >= RoarStart && age < RoarEnd;
            Vector2 shake = roaring ? new Vector2(Mathf.Sin(age * 70f), Mathf.Cos(age * 57f)) * 0.06f : Vector2.zero;
            float sink = age < RoarEnd ? 0f : (1f - fade) * 1.4f;
            Vector2 baseAt = center + Vector2.up * (Mathf.Lerp(-1.2f, 0.9f, rise) - sink) + shake;

            // The jaw drops open for the roar and snaps shut as the head sinks.
            float open = age < RoarStart ? Mathf.Clamp01((age - RiseTime) / (RoarStart - RiseTime)) * 0.4f
                : age < RoarEnd ? Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(roar * 4f)) : Mathf.Clamp01(1f - (age - RoarEnd) * 5f);
            float px = scale / PixelsPerUnit;
            Vector2 jawAt = baseAt + Vector2.up * (1f - open * 5f) * px;

            mesh.Begin();
            // A pool of shadow and hellfire the head rises out of.
            mesh.Ellipse(center + Vector2.up * 0.2f, radius * 0.85f * rise, radius * 0.3f * rise,
                FlameMesh.Alpha(DemonessAttack.Abyss, 0.75f * fade), FlameMesh.Alpha(DemonessAttack.Violet, 0f));
            // A violet aura behind the head.
            Vector2 middle = baseAt + Vector2.up * 10f * px;
            mesh.Ellipse(middle, 20f * px * (1f + 0.05f * Mathf.Sin(age * 9f)), 15f * px,
                FlameMesh.Alpha(HeroBuffs.AscendColor, 0.4f * fade * rise), FlameMesh.Alpha(DemonessAttack.Violet, 0f), 40);
            // The glowing throat between the jaws.
            if (open > 0.05f)
                mesh.Ellipse(baseAt + Vector2.up * (1.5f - open * 2f) * px, 7f * px, (1.5f + open * 3f) * px,
                    FlameMesh.Alpha(FlameMesh.Yellow, 0.9f * fade), FlameMesh.Alpha(Hellfire, 0.6f * fade), 20);
            DrawEyes(baseAt, px, fade * Mathf.Clamp01((age - RiseTime * 0.6f) * 6f));
            if (roaring) DrawRoar(baseAt, roar);
            mesh.Commit();

            var tint = new Color(1f, 1f, 1f, 0.95f * fade * Mathf.Clamp01(age / (RiseTime * 0.5f)));
            Place(head, baseAt, scale, tint);
            Place(jaw, jawAt, scale, tint);
        }

        private static void Place(SpriteRenderer layer, Vector2 at, float scale, Color tint)
        {
            layer.enabled = tint.a > 0f;
            layer.transform.localPosition = at;
            layer.transform.localScale = Vector3.one * scale;
            layer.color = tint;
        }

        /// <summary>Blazing eye glows over the sprite's eyes, flaring hardest as the roar begins.</summary>
        private void DrawEyes(Vector2 baseAt, float px, float alpha)
        {
            if (alpha <= 0f) return;
            float flare = 1f + 1.2f * Mathf.Clamp01(1f - Mathf.Abs(age - RoarStart) * 5f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = baseAt + new Vector2(side * 6f, 8.5f) * px;
                mesh.Ellipse(eye, 4f * px * flare, 2.2f * px * flare, FlameMesh.Alpha(Hellfire, 0.6f * alpha), FlameMesh.Alpha(Hellfire, 0f));
                mesh.Ellipse(eye, 1.6f * px, 0.9f * px, FlameMesh.Alpha(FlameMesh.Yellow, alpha), FlameMesh.Alpha(FlameMesh.Yellow, 0.4f * alpha), 12);
            }
        }

        /// <summary>Shockwaves of terror rolling out from the maw to the edge of the fear.</summary>
        private void DrawRoar(Vector2 baseAt, float roar)
        {
            for (int i = 0; i < 3; i++)
            {
                float t = Mathf.Repeat(roar * 1.8f + i / 3f, 1f);
                float r = Mathf.Lerp(0.6f, radius, t);
                mesh.Ring(center, r, 0.16f * (1f - t) + 0.03f, FlameMesh.Alpha(i == 1 ? DemonessAttack.Pale : DemonessAttack.Violet, 0.75f * (1f - t)), 64);
            }
            // Streaks of hellfire blasting from the maw.
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI * 0.5f + (i - 4.5f) * 0.32f + Mathf.Sin(age * 20f + i) * 0.05f;
                float length = radius * (0.35f + 0.5f * FlameMesh.Hash(i, age * 3f));
                mesh.Bar(baseAt, FlameMesh.Polar(a, 1f), length, 0.09f, FlameMesh.Alpha(FlameMesh.Yellow, 0.6f * (1f - roar)), FlameMesh.Alpha(Hellfire, 0f));
            }
        }

        private SpriteRenderer CreateLayer(string name, Sprite sprite, int order)
        {
            var layer = new GameObject(name).AddComponent<SpriteRenderer>();
            layer.transform.SetParent(transform, false);
            layer.sprite = sprite;
            layer.sortingOrder = order;
            layer.enabled = false;
            return layer;
        }

        private static Sprite Mirrored(string name, string[] halfRows, Vector2 pivot)
        {
            int half = halfRows[0].Length, width = half * 2, height = halfRows.Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[(height - y - 1) * width + x] = PixelColor(halfRows[y][x < half ? x : width - 1 - x]);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit);
        }

        private static Color PixelColor(char c)
        {
            switch (c)
            {
                case 'N': return new Color(0.08f, 0.01f, 0.02f);
                case 'H': return new Color(0.9f, 0.84f, 0.7f);
                case 'h': return new Color(0.55f, 0.48f, 0.4f);
                case 'R': return new Color(0.62f, 0.04f, 0.06f);
                case 'r': return new Color(0.34f, 0.02f, 0.04f);
                case 'L': return new Color(0.86f, 0.16f, 0.1f);
                case 'E': return FlameMesh.Yellow;
                case 'e': return new Color(1f, 0.45f, 0.08f);
                case 'B': return new Color(0.03f, 0f, 0.02f);
                case 'T': return new Color(0.95f, 0.92f, 0.85f);
                default: return Color.clear;
            }
        }

        private static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

        private void OnDestroy() { mesh?.Release(); }
    }
}

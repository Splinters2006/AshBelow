using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Reaper's scythe itself, swung through the arc of a cut: it pivots at his hands and sweeps from one edge of
    /// the cone to the other, blade leading, with a glowing trail off its point. It follows him as he moves and fades
    /// as the cut finishes. For the swing his detail layer swaps to a copy without the scythe (the way the Demoness's
    /// wings leave her sprite to beat), so there is only ever one scythe; it settles back in his hands afterwards.
    /// </summary>
    public sealed class ScytheSwingVfx : MonoBehaviour
    {
        // The scythe is drawn at the hero sprite's own pixel size: a slim shaft with a steel socket, and a long, nearly
        // straight blade standing up from its end at a sharp angle, leaning back only toward its point.
        private const int Width = 48, Height = 19, ShaftRow = 2, ShaftEnd = 43, Socket = 39;
        // The blade's centre line, in pixels above the shaft: from its heel on the socket to its point, with only a slight bow.
        private static readonly Vector2 Heel = new Vector2(43f, 2f), Bend = new Vector2(42.5f, 10f), Point = new Vector2(34.5f, 15f);
        private Transform hero;
        private SpriteRenderer blade, details;
        // Swings in flight per hero: quick cuts overlap, and the scythe only returns to his hands after the last one.
        private static readonly System.Collections.Generic.Dictionary<Transform, int> swinging = new System.Collections.Generic.Dictionary<Transform, int>();
        private float age, duration, from, sweep;
        private static Sprite sprite;

        /// <summary>
        /// One unit long with its pivot on the butt of the shaft, which runs along +x. The blade is a long wedge, almost
        /// straight: broad where its heel sits on the socket at the shaft's far end, standing up from it at a sharp
        /// angle and tapering to a point that leans back, with a dark spine outside and the bright cutting edge inside.
        /// </summary>
        private static Sprite Scythe
        {
            get
            {
                if (sprite != null) return sprite;
                var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = "Scythe", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[Width * Height];
                Color edge = new Color(0.95f, 0.97f, 1f), steel = new Color(0.78f, 0.84f, 0.92f), spine = new Color(0.45f, 0.52f, 0.63f);
                Color wood = new Color(0.56f, 0.37f, 0.2f), darkWood = new Color(0.34f, 0.22f, 0.12f);
                const int Samples = 120;
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        int up = y - ShaftRow;
                        Color color = Color.clear;
                        if (x <= ShaftEnd && (up == 0 || up == -1)) color = x >= Socket ? (up == 0 ? steel : spine) : up == 0 ? wood : darkWood;
                        if (up > 0)
                        {
                            // The nearest point of the centre line decides how wide the blade is here and which side this pixel is on.
                            var pixel = new Vector2(x, up);
                            float nearest = float.MaxValue, at = 0f;
                            for (int i = 0; i <= Samples; i++)
                            {
                                float t = (float)i / Samples, distance = (pixel - Along(t)).sqrMagnitude;
                                if (distance < nearest) { nearest = distance; at = t; }
                            }
                            float half = 3.1f * Mathf.Pow(1f - at, 0.75f) + 0.5f;
                            if (Mathf.Sqrt(nearest) <= half)
                            {
                                Vector2 tangent = (2f * (1f - at) * (Bend - Heel) + 2f * at * (Point - Bend)).normalized, offset = pixel - Along(at);
                                float side = tangent.x * offset.y - tangent.y * offset.x;
                                color = side > half - 1.3f ? edge : side < -(half - 1.1f) ? spine : steel;
                            }
                        }
                        pixels[y * Width + x] = color;
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(0f, (float)ShaftRow / Height), Width);
            }
        }

        private static Vector2 Along(float t) => (1f - t) * (1f - t) * Heel + 2f * t * (1f - t) * Bend + t * t * Point;

        /// <param name="cone">Degrees swept, centred on <paramref name="aim"/>; 360 is a full spin.</param>
        /// <param name="reverse">Sweeps clockwise (a back-cut) instead of counter-clockwise.</param>
        public static void Play(Transform parent, Transform hero, Vector2 aim, float cone, float reach, float duration, Color glow, bool reverse = false)
        {
            if (parent == null || hero == null || aim.sqrMagnitude < 0.0001f) return;
            var pivot = new GameObject("Scythe swing");
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = hero.position;
            var swing = pivot.AddComponent<ScytheSwingVfx>();
            swing.hero = hero;
            swing.duration = Mathf.Max(0.05f, duration);
            float direction = reverse ? -1f : 1f, middle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            swing.sweep = cone * direction;
            swing.from = middle - swing.sweep * 0.5f;

            // The sprite is one unit long and pivots on the butt of its shaft; a back-cut mirrors it so the blade still leads.
            swing.blade = DungeonVisuals.Create("Scythe", pivot.transform, hero.position, Vector2.one * reach, Color.white, 8);
            swing.blade.sprite = Scythe;
            swing.blade.flipY = reverse;
            swing.blade.transform.localPosition = Vector3.zero;
            swing.blade.transform.localRotation = Quaternion.identity;

            // A single fine trail off the blade's heel, tracing the rim of the cut.
            var heel = new GameObject("Scythe heel");
            heel.transform.SetParent(pivot.transform, false);
            heel.transform.localPosition = new Vector3(reach * 45f / Width, reach * 5f / Width * direction, 0f);
            CombatVfx.Trail(heel, FlameMesh.Alpha(glow, 0.7f), 0.07f, Mathf.Min(0.14f, duration * 0.5f));
            swing.TakeScythe();
            swing.Pose();
        }

        /// <summary>Lifts the scythe off his sprite for the length of the swing.</summary>
        private void TakeScythe()
        {
            var held = HeroSprites.Accent(WeaponType.Scythe);
            foreach (var part in hero.GetComponentsInChildren<SpriteRenderer>())
                if (part.sprite == held || part.sprite == HeroSprites.ScythelessAccent) { details = part; break; }
            if (details == null) return;
            details.sprite = HeroSprites.ScythelessAccent;
            swinging[hero] = (swinging.TryGetValue(hero, out int count) ? count : 0) + 1;
        }

        private void OnDestroy()
        {
            if (details == null || hero == null) return;
            int left = (swinging.TryGetValue(hero, out int count) ? count : 1) - 1;
            if (left > 0) { swinging[hero] = left; return; }
            swinging.Remove(hero);
            details.sprite = HeroSprites.Accent(WeaponType.Scythe);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= duration + 0.08f || hero == null) { Destroy(gameObject); return; }
            Pose();
        }

        private void Pose()
        {
            transform.position = hero.position;
            // A quick draw back, then the cut whips through and slows as it finishes.
            float t = Mathf.Clamp01(age / duration);
            float swung = t < 0.15f ? -0.06f * Mathf.Sin(t / 0.15f * Mathf.PI * 0.5f) : Mathf.Lerp(-0.06f, 1f, 1f - Mathf.Pow(1f - (t - 0.15f) / 0.85f, 3f));
            transform.rotation = Quaternion.Euler(0f, 0f, from + sweep * swung);
            if (blade != null) blade.color = new Color(1f, 1f, 1f, Mathf.Clamp01((duration + 0.08f - age) / 0.1f));
        }
    }
}

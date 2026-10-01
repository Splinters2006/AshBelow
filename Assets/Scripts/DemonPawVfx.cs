using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// HEEEELP: a portal rips open in the air above the target while a violet warning circle fills on the ground,
    /// red eyes glare out of it, then the Demoness's giant pet shoves its pixel-art paw through and slams it down: a
    /// red-furred mitt with four fanned toes, a big trefoil palm pad and bean-shaped toe pads, each toe tipped with a
    /// hooked claw glowing with hellfire. Purely visual; she applies the damage.
    /// </summary>
    public sealed class DemonPawVfx : MonoBehaviour
    {
        private const float SlamTime = 0.5f, PortalHeight = 2.6f;
        /// <summary>Where the paw lands; the caster keeps it on the target until the paw commits.</summary>
        public Vector2 Center { get; set; }
        private FlameMesh mesh;
        private float radius, windup, age;
        private SpriteRenderer pawRenderer, armRenderer;
        private static readonly Color Hellfire = new Color(1f, 0.16f, 0.08f);

        // The pet's paw is pixel art drawn from shapes (see BuildPaw); PixelsPerUnit matches the forearm tile.
        private const float PixelsPerUnit = 32f;
        private const int PawWidth = 40, PawHeight = 50;
        // One tile of the forearm, repeated up to the portal; bristling tufts stick out of its edges.
        private static readonly string[] ArmHalf = { "....NrRRRLRRRRRR", "...NrRRRRRRRRRRR", "..NNrRRLRRRRReRR", "....NrRRRRRRLRRR" };
        private static Sprite pawSprite, armSprite;
        private static Sprite PawSprite => pawSprite != null ? pawSprite : pawSprite = FromGrid("Demon paw", BuildPaw(), new Vector2(0.5f, 0f));
        private static Sprite ArmSprite => armSprite != null ? armSprite : armSprite = Mirrored("Demon forearm", ArmHalf, new Vector2(0.5f, 0f));

        public static DemonPawVfx Play(Transform root, Vector2 center, float radius, float windup)
        {
            if (root == null) return null;
            var effect = new GameObject("Demon paw").AddComponent<DemonPawVfx>();
            effect.transform.SetParent(root, false);
            effect.Center = center;
            effect.radius = radius;
            effect.windup = Mathf.Max(0.05f, windup);
            effect.mesh = new FlameMesh(effect.gameObject, 10);
            effect.armRenderer = effect.CreateLayer("Forearm", ArmSprite, 11);
            effect.armRenderer.drawMode = SpriteDrawMode.Tiled;
            effect.pawRenderer = effect.CreateLayer("Paw", PawSprite, 12);
            return effect;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= windup + SlamTime) { Destroy(gameObject); return; }
            mesh.Begin();
            float size = radius * 0.62f;
            // Where the landed paw rests: centred on the slam circle rather than standing on its middle.
            float rest = size * 0.35f - PawHeight / PixelsPerUnit * size;
            if (age < windup)
            {
                float t = age / windup;
                DrawWarning(t);
                float open = EaseOut(Mathf.Clamp01(t / 0.4f));
                DrawPortal(Center + Vector2.up * PortalHeight, open, 1f);
                DrawEyes(Center + Vector2.up * PortalHeight, open * Mathf.Clamp01((0.4f - t) * 8f));
                // The paw pushes through the portal, then plunges.
                float drop = Mathf.Clamp01((t - 0.35f) / 0.65f);
                drop *= drop;
                Paw(Center + Vector2.up * Mathf.Lerp(PortalHeight, rest, drop), size, t > 0.35f ? Mathf.Clamp01((t - 0.35f) * 6f) : 0f);
            }
            else
            {
                float t = (age - windup) / SlamTime, fade = 1f - t;
                DrawPortal(Center + Vector2.up * PortalHeight, 1f - EaseOut(t), fade);
                mesh.Ring(Center, radius * (1f + 0.4f * t), 0.14f * fade, FlameMesh.Alpha(Hellfire, 0.85f * fade), 56);
                DrawCracks(fade);
                // The paw lifts slightly as it withdraws.
                Paw(Center + Vector2.up * (rest + t * t * 1.2f), size, fade);
            }
            mesh.Commit();
        }

        private void DrawWarning(float t)
        {
            Color violet = DemonessAttack.Violet;
            mesh.Disc(Center, radius * t, FlameMesh.Alpha(violet, 0.08f + 0.18f * t), FlameMesh.Alpha(violet, 0.3f * t), 40);
            mesh.Ring(Center, radius, 0.06f, FlameMesh.Alpha(violet, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(age * 14f))), 56);
            // The paw's shadow grows as it closes in.
            mesh.Ellipse(Center, radius * 0.7f * t, radius * 0.35f * t, FlameMesh.Alpha(DemonessAttack.Abyss, 0.5f * t), FlameMesh.Alpha(DemonessAttack.Abyss, 0f));
        }

        private void DrawPortal(Vector2 at, float open, float alpha)
        {
            if (open <= 0.01f || alpha <= 0f) return;
            float rx = radius * 0.95f * open, ry = radius * 0.42f * open;
            mesh.Ellipse(at, rx * 1.25f, ry * 1.4f, FlameMesh.Alpha(DemonessAttack.Violet, 0.5f * alpha), FlameMesh.Alpha(DemonessAttack.Violet, 0f));
            mesh.Ellipse(at, rx, ry, FlameMesh.Alpha(Color.black, 0.95f * alpha), FlameMesh.Alpha(DemonessAttack.Abyss, 0.9f * alpha));
            // Swirling motes on the rim.
            for (int i = 0; i < 8; i++)
            {
                float a = age * 3f + i * Mathf.PI / 4f;
                mesh.Diamond(at + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry), 0.07f, FlameMesh.Alpha(DemonessAttack.Pale, 0.8f * alpha));
            }
        }

        /// <summary>
        /// Places the pixel-art paw so its claw tips sit just under <paramref name="at"/>, with the forearm tiled up to
        /// the portal it reaches out of.
        /// </summary>
        private void Paw(Vector2 at, float size, float alpha)
        {
            bool visible = alpha > 0f;
            pawRenderer.enabled = armRenderer.enabled = visible;
            if (!visible) return;
            float scale = size * 2f;
            var tint = new Color(1f, 1f, 1f, alpha);
            Vector2 tips = at + Vector2.down * size * 0.35f;
            pawRenderer.transform.localPosition = tips;
            pawRenderer.transform.localScale = Vector3.one * scale;
            pawRenderer.color = tint;
            float wristY = tips.y + PawSprite.rect.height / PixelsPerUnit * scale;
            float armLength = Center.y + PortalHeight - wristY;
            armRenderer.enabled = armLength > 0.01f;
            armRenderer.transform.localPosition = new Vector2(tips.x, wristY);
            armRenderer.transform.localScale = Vector3.one * scale;
            armRenderer.size = new Vector2(ArmSprite.rect.width / PixelsPerUnit, Mathf.Max(0.01f, armLength) / scale);
            armRenderer.color = tint;
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

        /// <summary>Two slit eyes glaring out of the dark before the paw comes through.</summary>
        private void DrawEyes(Vector2 at, float alpha)
        {
            if (alpha <= 0f) return;
            float gap = radius * 0.24f, w = radius * 0.16f, h = radius * 0.05f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = at + new Vector2(side * gap, 0f);
                mesh.Ellipse(eye, w * 1.8f, h * 3f, FlameMesh.Alpha(Hellfire, 0.45f * alpha), FlameMesh.Alpha(Hellfire, 0f));
                // Slanted inward into a scowl.
                mesh.Triangle(eye + new Vector2(-side * w, h * 1.4f), eye + new Vector2(side * w, -h * 0.2f), eye + new Vector2(-side * w * 0.2f, -h * 1.2f),
                    FlameMesh.Alpha(FlameMesh.Yellow, alpha), FlameMesh.Alpha(Hellfire, alpha), FlameMesh.Alpha(Hellfire, alpha));
                mesh.Ellipse(eye + new Vector2(-side * w * 0.2f, h * 0.1f), h * 0.35f, h * 1.2f, FlameMesh.Alpha(Color.black, alpha), FlameMesh.Alpha(Color.black, alpha), 10);
            }
        }

        private void DrawCracks(float fade)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + FlameMesh.Hash(i, 5.3f);
                float length = radius * (0.7f + FlameMesh.Hash(i, 1.7f) * 0.6f);
                mesh.Bar(Center, FlameMesh.Polar(a, 1f), length, 0.1f, FlameMesh.Alpha(FlameMesh.Yellow, 0.9f * fade), FlameMesh.Alpha(Hellfire, 0f));
            }
        }

        /// <summary>
        /// Draws the paw into a grid of palette keys (row 0 at the top): a furry wrist widening into a rounded palm,
        /// four toes fanned along its lower edge, a trefoil palm pad and a bean-shaped pad on each toe, and a hooked
        /// claw curling out of each toe, glowing toward its tip. Everything gets a dark outline, the toes are creased
        /// apart, and the fur is shaded dark at its edges with a highlight up and to the left.
        /// </summary>
        private static char[,] BuildPaw()
        {
            var grid = new char[PawWidth, PawHeight];
            var owner = new int[PawWidth, PawHeight];
            Vector2[] toes = { new Vector2(6.5f, 30.5f), new Vector2(14.5f, 35.5f), new Vector2(25.5f, 35.5f), new Vector2(33.5f, 30.5f) };
            bool Ellipse(Vector2 p, Vector2 c, float rx, float ry) => Mathf.Pow((p.x - c.x) / rx, 2f) + Mathf.Pow((p.y - c.y) / ry, 2f) <= 1f;
            for (int y = 0; y < PawHeight; y++)
                for (int x = 0; x < PawWidth; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // Wrist, with a ragged fur edge.
                    float wristHalf = 11f + (y % 3 == 0 ? 1f : 0f);
                    bool palm = (y < 14 && Mathf.Abs(p.x - 20f) <= wristHalf) || Ellipse(p, new Vector2(20f, 19f), 15.5f, 10.5f);
                    owner[x, y] = -1;
                    if (palm) { grid[x, y] = 'R'; owner[x, y] = 0; continue; }
                    for (int i = 0; i < toes.Length; i++)
                        if (Ellipse(p, toes[i], 5.2f, 6f)) { grid[x, y] = 'R'; owner[x, y] = i + 1; break; }
                }
            // Claws: a tapering hook from under each toe, curling outward, glowing toward the tip.
            for (int i = 0; i < toes.Length; i++)
            {
                float outward = Mathf.Sign(toes[i].x - 20f) * (i == 0 || i == 3 ? 1f : 0.4f);
                Vector2 a = toes[i] + new Vector2(0f, 4.5f), b = a + new Vector2(outward * 1.5f, 4.5f), c = a + new Vector2(outward * 3.5f, 9.5f);
                for (float t = 0f; t <= 1f; t += 0.02f)
                {
                    Vector2 point = Vector2.Lerp(Vector2.Lerp(a, b, t), Vector2.Lerp(b, c, t), t);
                    float half = Mathf.Lerp(1.4f, 0.35f, t);
                    for (int y = Mathf.FloorToInt(point.y - half); y <= Mathf.CeilToInt(point.y + half); y++)
                        for (int x = Mathf.FloorToInt(point.x - half); x <= Mathf.CeilToInt(point.x + half); x++)
                        {
                            if (x < 0 || y < 0 || x >= PawWidth || y >= PawHeight || owner[x, y] >= 0) continue;
                            if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), point) > half + 0.25f) continue;
                            if (grid[x, y] != 'G') grid[x, y] = t > 0.55f ? 'G' : 'C';
                        }
                }
            }
            // Fur shading and creases, worked out on a copy so each pixel sees the original shape.
            var shaded = (char[,])grid.Clone();
            for (int y = 0; y < PawHeight; y++)
                for (int x = 0; x < PawWidth; x++)
                {
                    if (owner[x, y] < 0) continue;
                    bool edge = false, crease = false;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            // Above the top row the forearm carries on, so that is not an edge.
                            if (ny < 0) continue;
                            if (nx < 0 || nx >= PawWidth || ny >= PawHeight || owner[nx, ny] < 0) { edge = true; continue; }
                            if (owner[nx, ny] != owner[x, y] && (dx == 0 || dy == 0)) crease = true;
                        }
                    if (crease && owner[x, y] > 0) shaded[x, y] = 'N';
                    else if (edge) shaded[x, y] = 'r';
                    else if (Ellipse(new Vector2(x + 0.5f, y + 0.5f), new Vector2(13.5f, 12f), 5.5f, 3.5f) && (x + 2 * y) % 4 == 0) shaded[x, y] = 'L';
                }
            // Pads: a trefoil on the palm, a bean on each toe, each with a sheen.
            for (int y = 0; y < PawHeight; y++)
                for (int x = 0; x < PawWidth; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    bool palmPad = Ellipse(p, new Vector2(20f, 20.5f), 5.5f, 3.6f) || Ellipse(p, new Vector2(15.5f, 23.5f), 2.8f, 2.6f)
                        || Ellipse(p, new Vector2(20f, 25f), 2.8f, 2.4f) || Ellipse(p, new Vector2(24.5f, 23.5f), 2.8f, 2.6f);
                    bool toePad = false;
                    foreach (var toe in toes) toePad |= Ellipse(p, toe + new Vector2(0f, 0.5f), 2.6f, 3.2f);
                    if (!palmPad && !toePad) continue;
                    bool sheen = Ellipse(p, new Vector2(18f, 19.3f), 1.8f, 1f);
                    foreach (var toe in toes) sheen |= Ellipse(p, toe + new Vector2(-0.8f, -1f), 0.9f, 0.9f);
                    shaded[x, y] = sheen ? 'p' : 'P';
                }
            // A few smouldering embers in the fur.
            foreach (var ember in new[] { new Vector2Int(10, 8), new Vector2Int(29, 11), new Vector2Int(33, 20), new Vector2Int(6, 19) })
                if (owner[ember.x, ember.y] >= 0 && shaded[ember.x, ember.y] == 'R') shaded[ember.x, ember.y] = 'e';
            // Outline everything.
            var outlined = (char[,])shaded.Clone();
            for (int y = 0; y < PawHeight; y++)
                for (int x = 0; x < PawWidth; x++)
                {
                    if (shaded[x, y] != '\0') continue;
                    for (int dy = -1; dy <= 1 && outlined[x, y] == '\0'; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if ((dx == 0) == (dy == 0) || nx < 0 || ny < 0 || nx >= PawWidth || ny >= PawHeight || shaded[nx, ny] == '\0') continue;
                            // The top row stays open where the forearm joins on.
                            if (y == 0) continue;
                            outlined[x, y] = 'N';
                            break;
                        }
                }
            return outlined;
        }

        private static Sprite FromGrid(string name, char[,] grid, Vector2 pivot)
        {
            int width = grid.GetLength(0), height = grid.GetLength(1);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[(height - y - 1) * width + x] = PixelColor(grid[x, y]);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
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
            // Full-rect meshes so the forearm can tile.
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static Color PixelColor(char c)
        {
            switch (c)
            {
                case 'N': return new Color(0.08f, 0.01f, 0.02f);
                case 'R': return new Color(0.62f, 0.04f, 0.06f);
                case 'r': return new Color(0.34f, 0.02f, 0.04f);
                case 'L': return new Color(0.86f, 0.16f, 0.1f);
                case 'e': return new Color(1f, 0.35f, 0.08f);
                case 'E': return new Color(1f, 0.72f, 0.2f);
                case 'C': return new Color(0.2f, 0.05f, 0.07f);
                case 'G': return Hellfire;
                case 'P': return new Color(0.22f, 0.03f, 0.07f);
                case 'p': return new Color(0.45f, 0.1f, 0.16f);
                default: return Color.clear;
            }
        }

        private static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

        private void OnDestroy() { mesh?.Release(); }
    }
}

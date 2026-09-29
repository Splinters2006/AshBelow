using UnityEngine;

namespace Slopgame
{
    public static class DungeonVisuals
    {
        private static Sprite square;
        private static Sprite emberBolt;
        private static Sprite thrownBlade;

        private static readonly Sprite[] enemySprites = new Sprite[3];
        private static Sprite flameSprite, shadowHero, duelistSprite, archdemonSprite, archdemonDetails, wardenSprite, wardenDetails;
        public static Sprite FlameSprite => flameSprite != null ? flameSprite : flameSprite = PixelSprite("Burn flame", new[]
        {
            "....W...", "...WW...", "...WW.W.", "..WWWWW.", ".WWWWWW.", ".WWWWWW.", "..WWWW..", "...WW..."
        });

        public static Sprite EnemySprite(bool ranged, bool tank)
        {
            int index = tank ? 2 : ranged ? 1 : 0;
            if (enemySprites[index] == null)
                enemySprites[index] = PixelSprite(tank ? "Iron brute" : ranged ? "Ember caster" : "Ashling", tank ? new[]
                {
                    "...DDDDDDDD.....", "..DWWWWWWWWD....", ".DWWDDDDDDWWD...", ".DWDWDDWDWWWD...",
                    "DDWWDDDDDDWWDD..", "DWWWWWWWWWWWWD..", "DWWDDWWWWDDWWD..", "DDDDWWWWWWDDDD..",
                    ".DWWWWDDWWWWD...", ".DWWWWDDWWWWD...", "..DDDDDDDDDD....", "..DWWWDDWWWD....",
                    "..DWWWDDWWWD....", "..DDDD..DDDD....", "..DDDD..DDDD....", "................"
                } : ranged ? new[]
                {
                    ".......WW.......", "......WWWW......", ".....WWWWWW.....", "....WDDDDDDW....",
                    "....WDDWDDWW....", "....WDDDDDDW....", "...WWWWWWWWW..W.", "...WWDDDDWWW.WWW",
                    "..WWWWDDWWWW..W.", "..WWWWDDWWWWW.D.", ".WWWWWDDWWWWW.D.", ".WWWWWWWWWWWW.D.",
                    "WWWWWWWWWWWWWWD.", "WWWDDWWWWDDWWWD.", "WWD..WWWW..DWWD.", "................"
                } : new[]
                {
                    "..WW......WW....", "..WWW....WWW....", "...WWWWWWWW.....", "..WWWWWWWWWW....",
                    "..WDDWDDWDDW....", "..WDDDDDDDDW....", "...WWDDDDWW.....", "....WWWWWW......",
                    "..WWWWWWWWWW....", ".WW.WWWWWW.WW...", ".WW.WWDDWW.WW...", "....WWWWWW......",
                    "....WW..WW......", "...WWW..WWW.....", "...DDD..DDD.....", "................"
                });
            return enemySprites[index];
        }

        private static Sprite PixelSprite(string name, string[] rows)
        {
            int width = rows[0].Length, height = rows.Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[(height - y - 1) * width + x] = rows[y][x] == 'W' ? Color.white
                        : rows[y][x] == 'D' ? new Color(0.2f, 0.22f, 0.28f) : Color.clear;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, width);
        }

        public static SpriteRenderer CreateEmberBolt(Transform parent, Vector2 position)
        {
            if (emberBolt == null)
            {
                const int width = 32, height = 16;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Ember bolt sprite",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        // The hot head sits at the collision position; the tail trails behind it.
                        float dx = x + 0.5f - 23f;
                        float dy = Mathf.Abs(y + 0.5f - 8f);
                        float head = Mathf.Sqrt(dx * dx + dy * dy);
                        float tail = Mathf.Clamp01((x + 0.5f - 2f) / 21f);
                        Color color = Color.clear;
                        if (head < 7f)
                            color = new Color(1f, 0.28f, 0.04f, 0.28f * (1f - head / 7f));
                        if (dx < 0f && dy < tail * 4f)
                            color = new Color(1f, 0.32f, 0.04f, tail * 0.75f);
                        if (head < 4.5f)
                            color = new Color(1f, 0.42f, 0.06f);
                        if (dx < 0f && dy < tail * 1.8f)
                            color = new Color(1f, 0.66f, 0.12f, tail);
                        if (head < 3f)
                            color = new Color(1f, 0.8f, 0.3f);
                        if (head < 1.8f)
                            color = new Color(1f, 0.97f, 0.78f);
                        pixels[y * width + x] = color;
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                emberBolt = Sprite.Create(texture, new Rect(0, 0, width, height),
                    new Vector2(23f / width, 0.5f), 40f);
            }
            var renderer = Create("Ember bolt", parent, position, Vector2.one, Color.white, 6);
            renderer.sprite = emberBolt;
            return renderer;
        }

        /// <summary>A thrown steel blade: the point sits at the collision position, the hilt trails behind.</summary>
        public static SpriteRenderer CreateThrownBlade(Transform parent, Vector2 position)
        {
            if (thrownBlade == null)
            {
                const int width = 32, height = 10;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Thrown blade sprite",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color[width * height];
                var steel = new Color(0.8f, 0.86f, 0.94f);
                var edge = new Color(0.45f, 0.95f, 1f);
                var grip = new Color(0.32f, 0.2f, 0.16f);
                var guard = new Color(0.55f, 0.6f, 0.7f);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        float dy = Mathf.Abs(y + 0.5f - 5f);
                        Color color = Color.clear;
                        if (x >= 3 && x <= 9 && dy < 1f) color = grip;
                        else if (x == 2 && dy < 1.5f) color = guard;
                        else if (x >= 10 && x <= 11 && dy < 4f) color = guard;
                        else if (x >= 12 && x <= 30)
                        {
                            // Tapers from a broad base to the point.
                            float half = Mathf.Lerp(2.6f, 0.4f, (x - 12f) / 18f);
                            if (dy < half) color = dy < 0.6f ? Color.white : dy > half - 1f ? edge : steel;
                        }
                        pixels[y * width + x] = color;
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                thrownBlade = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(26f / width, 0.5f), 40f);
            }
            var renderer = Create("Thrown blade", parent, position, Vector2.one, Color.white, 6);
            renderer.sprite = thrownBlade;
            return renderer;
        }

        public static SpriteRenderer Create(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order)
        {
            if (square == null)
            {
                var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                square = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            }
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = size;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void Detail(Transform parent, string name, Vector2 offset, Vector2 size, Color color, int order = 6)
        {
            var part = Create(name, parent, parent.position, size, color, order);
            part.transform.localPosition = offset;
        }

        /// <summary>The Admin's shadow-sovereign sprite (tinted by the class colour).</summary>
        public static Sprite ShadowHeroSprite => shadowHero != null ? shadowHero : shadowHero = PixelSprite("Shadow sovereign", new[]
        {
            "...W....W....W..", "...WW..WWW..WW..", "....WWWWWWWWW...", "...WWWWWWWWWWW..",
            "...WDDDDDDDDDW..", "...WDWWDDWWDDW..", "...WDDDDDDDDDW..", "....WDDDDDDDW...",
            "...WWWWWWWWWWW..", "..WWWDDWWDDWWW..", ".WWWWDDWWDDWWWW.", "WWWWWDDWWDDWWWWW",
            ".WWWWWDDDDWWWWW.", "..WWWWDDDDWWWW..", "..WWW..WW..WWW..", "...W........W..."
        });

        /// <summary>
        /// Dresses the hero. Returns the fixed-colour detail layer for regular heroes (so it can be flipped
        /// with the body), or null for the Admin.
        /// </summary>
        public static SpriteRenderer DecorateHero(Transform hero, WeaponType weapon, Color tint)
        {
            var body = hero.GetComponent<SpriteRenderer>();
            if (weapon == WeaponType.Shadow)
            {
                body.sprite = ShadowHeroSprite;
                Detail(hero, "Void mantle", new Vector2(0, -0.15f), new Vector2(1.15f, 0.72f), new Color(0.035f, 0.012f, 0.075f), 3);
                Detail(hero, "Left soul eye", new Vector2(-0.16f, 0.12f), new Vector2(0.15f, 0.06f), AbilityCatalog.Ice, 7);
                Detail(hero, "Right soul eye", new Vector2(0.16f, 0.12f), new Vector2(0.15f, 0.06f), AbilityCatalog.Ice, 7);
                ShadowVfx.Aura(hero);
                return null;
            }
            // A soft oval ground shadow under the feet (two overlapping bars read as an ellipse).
            Detail(hero, "Shadow", new Vector2(0f, -0.66f), new Vector2(0.95f, 0.14f), new Color(0.01f, 0.02f, 0.04f, 0.35f), 2);
            Detail(hero, "Shadow core", new Vector2(0f, -0.66f), new Vector2(0.65f, 0.22f), new Color(0.01f, 0.02f, 0.04f, 0.3f), 2);
            HeroVfx.Aura(hero, tint);
            if (!HeroSprites.Has(weapon)) return null;
            body.sprite = HeroSprites.Body(weapon);
            var details = new GameObject("Hero details").AddComponent<SpriteRenderer>();
            details.transform.SetParent(hero, false);
            details.sprite = HeroSprites.Accent(weapon);
            details.color = Color.white;
            details.sortingOrder = body.sortingOrder + 1;
            return details;
        }

        /// <summary>Body sprites for the guardians (tinted by the boss's body colour).</summary>
        public static Sprite BossSprite(BossKind kind)
        {
            if (kind == BossKind.AshWarden)
                return wardenSprite != null ? wardenSprite : wardenSprite = MirroredSprite("Ash warden", WardenGrid, false, WardenDetailColor);
            if (kind == BossKind.Duelist)
                return duelistSprite != null ? duelistSprite : duelistSprite = PixelSprite("Ashen duelist", new[]
                {
                    "......WWWW......", ".....WWWWWW.....", "....WWDDDDWW....", "....WDDDDDDW....",
                    "....WDDDDDDW....", ".....WWWWWW.....", "W...WWWWWWWW...W", ".W.WWWDWWDWWW.W.",
                    "..WWWWDWWDWWWW..", "...WWWDWWDWWW...", "....WWWWWWWW....", "....WWWDDWWW....",
                    "....WWW..WWW....", "...WWW....WWW...", "...WW......WW...", "..DDD......DDD.."
                });
            if (kind == BossKind.Archdemon)
                return archdemonSprite != null ? archdemonSprite : archdemonSprite = MirroredSprite("Hellfire archdemon", ArchdemonGrid, false, ArchdemonDetailColor);
            return null;
        }

        public static void DecorateDuelist(Transform boss)
        {
            var blade = new Color(0.45f, 0.95f, 1f);
            Detail(boss, "Left eye", new Vector2(-0.094f, 0.28f), new Vector2(0.09f, 0.05f), blade, 7);
            Detail(boss, "Right eye", new Vector2(0.094f, 0.28f), new Vector2(0.09f, 0.05f), blade, 7);
            for (int side = -1; side <= 1; side += 2)
            {
                var sword = Create("Blade", boss, boss.position, new Vector2(0.06f, 0.75f), blade, 6);
                sword.transform.localPosition = new Vector2(side * 0.58f, -0.12f);
                sword.transform.localRotation = Quaternion.Euler(0, 0, side * 32f);
            }
            Detail(boss, "Sash", new Vector2(0f, -0.2f), new Vector2(0.5f, 0.06f), new Color(0.85f, 0.2f, 0.25f));
        }

        // Left half of the 32x32 archdemon, mirrored for the right. Body layer (tinted): L highlight, W base, M shade,
        // D outline. Detail layer (fixed): H horn, h horn shade, E burning eyes, K maw, F fangs, O magma, Y molten core.
        private static readonly string[] ArchdemonGrid =
        {
            ".H..............", ".HH.............", "..hH............", "..hHH...........",
            "...hHH..........", "....hHHH........", ".....hhHHH......", ".......hhHHDDDDD",
            ".........hDLWWWW", "..........DMMWWW", "..........DWEMMW", "..........DWEEEM",
            "...H......DWWWWW", "..hH......DMKFKF", "..hHDDDDDDDDKOOO", ".DDLWWWWWWDDKFKF",
            "DLWWWWWWWWWDDMWW", "DWWWMWWWWWWWWWWW", "DWWMDMWWWWWWOWWW", "DWMD.DMWWWWWOOWW",
            "DWMD.DWWWWWWWOYY", "DWMD.DMWWWWWWOYY", "DMMD.DWMWWWWWOOW", "DWMD.DWWMWWWWWWW",
            "DWMD.DMWWWMMWWWW", "DMMD.DDMWWWWMDDD", "HhHh..DDDDDDDD..", "H.H...DMWWWMD...",
            "......DMWWMD....", ".......DWWMD....", "......DhHHhD....", ".....DhhhhhD....",
        };

        /// <summary>The archdemon's fixed-colour layer: horns, burning eyes, fanged maw and the magma in his chest.</summary>
        public static Sprite ArchdemonDetails => archdemonDetails != null ? archdemonDetails
            : archdemonDetails = MirroredSprite("Hellfire archdemon details", ArchdemonGrid, true, ArchdemonDetailColor);

        // Left half of the 32x32 Ash Warden, a hooded, crowned caster cradling an ember. Body layer (tinted): L highlight,
        // W base, M shade, D outline. Detail layer (fixed): G gold, g dark gold, E eyes, K hood void, O ember, Y ember core.
        private static readonly string[] WardenGrid =
        {
            "...............G", "..........G....G", "..........GG..GG", "..........GGGGGG",
            "..........gGgGgG", ".........DDDDDDD", "........DLWWWWWW", ".......DLWWWWWWW",
            ".......DWWDDDDDD", "......DLWDKKKKKK", "......DWWDKgGGGG", "......DWWDKGEEGG",
            "......DWMDKgGGGG", "......DWMDKKgGGg", ".....DLWMDKKKKKK", ".....DWWMMDDDDDD",
            "....DLWWWWMgGGGG", "...DLWWWWWWMDDDD", "..DLWWWWWWWWMWWW", ".DLWWMWWWWWWWMDD",
            "DLWWMDWWWWWWDLDO", "DWWMD.DWWWWDLDOY", "DWMD..DWWWWDLDOY", "DWMD..DWWWWWDDOO",
            "DMD...DWWMWWWWDD", ".D....DWWMWWWWWG", "......DWWMWWWWWG", ".....DLWWMWWWWWG",
            ".....DWWMWWWMWWG", "....DLWMWWWMWWWG", "....DWMWWWMWWWMG", "....DDDDDDDDDDDD",
        };

        /// <summary>The Ash Warden's fixed-colour layer: crown, gold mask, burning eyes and the ember in its hands.</summary>
        public static Sprite WardenDetails => wardenDetails != null ? wardenDetails
            : wardenDetails = MirroredSprite("Ash warden details", WardenGrid, true, WardenDetailColor);

        private static Color WardenDetailColor(char c)
        {
            switch (c)
            {
                case 'G': return new Color(0.98f, 0.78f, 0.32f);
                case 'g': return new Color(0.62f, 0.44f, 0.16f);
                case 'E': return new Color(1f, 0.97f, 0.75f);
                case 'K': return new Color(0.04f, 0.01f, 0.07f);
                case 'O': return new Color(1f, 0.45f, 0.1f);
                case 'Y': return new Color(1f, 0.9f, 0.5f);
                default: return Color.clear;
            }
        }

        /// <summary>Builds a symmetric sprite from its left half; one unit across, like the other boss bodies.</summary>
        private static Sprite MirroredSprite(string name, string[] halfRows, bool details, System.Func<char, Color> detailColor)
        {
            int half = halfRows[0].Length, width = half * 2, height = halfRows.Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    char c = halfRows[y][x < half ? x : width - 1 - x];
                    Color? body = c == 'L' ? new Color(1f, 1f, 1f) : c == 'W' ? new Color(0.82f, 0.82f, 0.84f)
                        : c == 'M' ? new Color(0.55f, 0.55f, 0.6f) : c == 'D' ? new Color(0.08f, 0.07f, 0.1f) : (Color?)null;
                    pixels[(height - y - 1) * width + x] = details ? (body.HasValue ? Color.clear : detailColor(c)) : body ?? Color.clear;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, width);
        }

        private static Color ArchdemonDetailColor(char c)
        {
            switch (c)
            {
                case 'H': return new Color(0.9f, 0.84f, 0.7f);
                case 'h': return new Color(0.5f, 0.4f, 0.32f);
                case 'E': return new Color(1f, 0.96f, 0.55f);
                case 'K': return new Color(0.05f, 0f, 0.02f);
                case 'F': return new Color(0.97f, 0.94f, 0.86f);
                case 'O': return new Color(1f, 0.45f, 0.08f);
                case 'Y': return new Color(1f, 0.86f, 0.35f);
                default: return Color.clear;
            }
        }

        public static void DecorateArchdemon(Transform boss)
        {
            var body = boss.GetComponent<SpriteRenderer>();
            var details = new GameObject("Archdemon details").AddComponent<SpriteRenderer>();
            details.transform.SetParent(boss, false);
            details.sprite = ArchdemonDetails;
            details.sortingOrder = body.sortingOrder + 1;
        }

        public static void DecorateWarden(Transform boss)
        {
            var body = boss.GetComponent<SpriteRenderer>();
            body.sprite = BossSprite(BossKind.AshWarden);
            var details = new GameObject("Warden details").AddComponent<SpriteRenderer>();
            details.transform.SetParent(boss, false);
            details.sprite = WardenDetails;
            details.sortingOrder = body.sortingOrder + 1;
        }

        public static void DecorateArena(Transform root)
        {
            for (int x = 15; x <= 39; x++)
                for (int y = 9; y <= 29; y++)
                    if (x == 15 || x == 39 || y == 9 || y == 29)
                        Create("Arena inlay", root, new Vector2(x, y), Vector2.one * 0.15f, new Color(0.56f, 0.38f, 0.22f), 1);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                var rune = Create("Arena rune", root, new Vector2(27, 19) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 7f,
                    new Vector2(0.12f, 0.45f), new Color(0.55f, 0.3f, 0.4f), 1);
                rune.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            }
        }

        public static void DrawMap(DungeonMap map, Transform root)
        {
            for (int x = 0; x < DungeonMap.Width; x++)
                for (int y = 0; y < DungeonMap.Height; y++)
                {
                    bool walkable = map.IsFloor(x, y);
                    if (!walkable && !map.IsFloor(x - 1, y) && !map.IsFloor(x + 1, y)
                        && !map.IsFloor(x, y - 1) && !map.IsFloor(x, y + 1)) continue;
                    Color color = walkable
                        ? ((x + y) % 2 == 0 ? new Color(0.12f, 0.17f, 0.21f) : new Color(0.14f, 0.19f, 0.23f))
                        : new Color(0.29f, 0.38f, 0.43f);
                    Create(walkable ? "Floor" : "Wall", root, new Vector2(x, y), Vector2.one * 0.97f, color, 0);
                }
        }
    }
}

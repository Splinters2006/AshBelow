using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The crystal shop visited before each boss: a warm, lantern-lit room with a hooded merchant behind a counter.
    /// Talking to him opens his wares, paid for in the crystals enemies drop: healing, boons for the guardian's arena
    /// and relics that last the rest of the descent. Each shop stocks a random few of them (the same on every machine
    /// in a co-op run), and each ware costs half as much again every time it is bought in the same shop.
    /// </summary>
    public sealed class CrystalShop : MonoBehaviour
    {
        public enum Ware
        {
            Draught, Elixir, HeartCrystal, Stoneskin, Whetstone, Quicksilver,
            EmberHone, WindrunnerBoots, QuickfingerGloves, HawkeyeLens, WardingSigil, VampireFang, PhoenixFeather
        }

        public enum Category { Healing, Arena, Relic }

        public sealed class Offer
        {
            public Ware Ware { get; }
            public Category Category { get; }
            public string Name { get; }
            public string Description { get; }
            public int BaseCost { get; }
            public Color Color { get; }
            /// <summary>The run boon a relic grants, counted in the hero's talents; null for other wares.</summary>
            public PowerupType? Powerup { get; }
            public Offer(Ware ware, Category category, string name, string description, int baseCost, Color color, PowerupType? powerup = null)
            { Ware = ware; Category = category; Name = name; Description = description; BaseCost = baseCost; Color = color; Powerup = powerup; }
        }

        public static readonly Color HealColor = new Color(1f, 0.42f, 0.5f);
        public static readonly Offer[] Offers =
        {
            new Offer(Ware.Draught, Category.Healing, "Healing Draught", "Restore 2 HP", 12, HealColor),
            new Offer(Ware.Elixir, Category.Healing, "Grand Elixir", "Restore all of your HP", 30, HealColor),
            new Offer(Ware.Stoneskin, Category.Arena, "Stoneskin Tonic", "+2 wards in the boss arena", 20, AbilityCatalog.Ice),
            new Offer(Ware.Whetstone, Category.Arena, "Whetstone", "+1 damage in the boss arena", 35, AbilityCatalog.Gold),
            new Offer(Ware.Quicksilver, Category.Arena, "Quicksilver", "+20% move speed in the boss arena", 25, DungeonUi.Teal),
            new Offer(Ware.HeartCrystal, Category.Relic, "Heart Crystal", "+1 max HP", 45, CrystalPouch.CrystalColor),
            new Offer(Ware.EmberHone, Category.Relic, "Ember Hone", "+1 base attack damage", 60, AbilityCatalog.Gold, PowerupType.Damage),
            new Offer(Ware.WindrunnerBoots, Category.Relic, "Windrunner Boots", "+0.7 move speed", 40, DungeonUi.Teal, PowerupType.Movement),
            new Offer(Ware.QuickfingerGloves, Category.Relic, "Quickfinger Gloves", "+20% attack and charge speed", 50, new Color(1f, 0.62f, 0.3f), PowerupType.AttackSpeed),
            new Offer(Ware.HawkeyeLens, Category.Relic, "Hawkeye Lens", "+10% crit and elemental effect chance", 50, new Color(0.55f, 0.8f, 1f), PowerupType.CriticalHits),
            new Offer(Ware.WardingSigil, Category.Relic, "Warding Sigil", "Block one extra hit on every floor", 55, AbilityCatalog.Ice, PowerupType.Armor),
            new Offer(Ware.VampireFang, Category.Relic, "Vampire Fang", "Heal 1 HP every few kills (Soul Harvest)", 50, HealColor, PowerupType.LifeSteal),
            new Offer(Ware.PhoenixFeather, Category.Relic, "Phoenix Feather", "10% shorter dodge cooldown", 35, new Color(1f, 0.45f, 0.25f), PowerupType.DodgeRecovery),
        };

        /// <summary>How many wares of each category a shop stocks, drawn at random from that category.</summary>
        public const int HealingStock = 1, ArenaStock = 2, RelicStock = 2;

        public const float TalkRange = 1.9f;
        public const int StoneskinWards = 2;
        private readonly Dictionary<Ware, int> bought = new Dictionary<Ware, int>();
        private readonly List<Offer> stock = new List<Offer>();
        private readonly List<SpriteRenderer> glows = new List<SpriteRenderer>();
        private readonly List<float> glowAlpha = new List<float>();
        private readonly List<Transform> floaters = new List<Transform>();
        private readonly List<Vector2> floaterRest = new List<Vector2>();
        private readonly List<Vector2> sparklePoints = new List<Vector2>();
        private DungeonRun run;
        private Transform merchant;
        private SpriteRenderer merchantHalo, merchantOutline;
        private float nextSparkle;
        public bool IsOpen { get; private set; }
        /// <summary>The wares on sale in this shop, in display order.</summary>
        public IReadOnlyList<Offer> Stock => stock;
        public string LastResult { get; private set; }
        /// <summary>Where the hero talks to the merchant: the front of his counter.</summary>
        public Vector2 Counter => new Vector2(DungeonMap.ShopCounter.center.x - 0.5f, DungeonMap.ShopCounter.yMin);

        public bool IsNear(DungeonPlayer player) => player != null && player.Health > 0
            && Vector2.Distance(player.transform.position, Counter) <= TalkRange;

        public int Cost(Offer offer) => offer.BaseCost + offer.BaseCost * Bought(offer.Ware) / 2;
        public int Bought(Ware ware) => bought.TryGetValue(ware, out int count) ? count : 0;

        public void Toggle()
        {
            IsOpen = !IsOpen;
            LastResult = null;
        }

        public void Close() => IsOpen = false;

        /// <summary>Draws this shop's stock: one category at a time, a random few wares each, from <paramref name="seed"/>.</summary>
        public void Restock(int seed)
        {
            var random = new System.Random(seed);
            stock.Clear();
            foreach (var (category, count) in new[] { (Category.Healing, HealingStock), (Category.Arena, ArenaStock), (Category.Relic, RelicStock) })
            {
                var choices = new List<Offer>(System.Array.FindAll(Offers, offer => offer.Category == category));
                for (int i = 0; i < count && choices.Count > 0; i++)
                {
                    int pick = random.Next(choices.Count);
                    stock.Add(choices[pick]);
                    choices.RemoveAt(pick);
                }
            }
        }

        /// <summary>Puts exactly <paramref name="offers"/> on sale (used by tests).</summary>
        public void SetStock(IEnumerable<Offer> offers)
        {
            stock.Clear();
            stock.AddRange(offers);
        }

        public bool CanBuy(Offer offer)
        {
            var player = run != null ? run.Player : null;
            if (player == null || !run.IsPlaying || player.Health <= 0 || !stock.Contains(offer) || player.Crystals.Crystals < Cost(offer)) return false;
            if (offer.Category == Category.Healing && player.Health >= player.MaxHealth) return false;
            // A relic whose boon is already at its highest rank has nothing left to give.
            if (offer.Powerup.HasValue && !player.Powerups.CanTake(offer.Powerup.Value)) return false;
            return true;
        }

        public bool Buy(Offer offer)
        {
            if (!CanBuy(offer)) return false;
            var player = run.Player;
            player.Crystals.Spend(Cost(offer));
            bought[offer.Ware] = Bought(offer.Ware) + 1;
            // The shop sits on the floor before the guardian's, so boons wait for the next floor.
            int arena = run.Floor + 1;
            var root = run.ProjectileRoot;
            Vector2 at = player.transform.position;
            switch (offer.Ware)
            {
                case Ware.Draught:
                    player.Heal(2);
                    LastResult = "The draught warms you. +2 HP.";
                    break;
                case Ware.Elixir:
                    player.Heal(player.MaxHealth);
                    LastResult = "Every wound closes. Full HP.";
                    break;
                case Ware.HeartCrystal:
                    player.RaiseMaxHealth(1);
                    LastResult = "The crystal beats with yours. +1 max HP.";
                    break;
                case Ware.Stoneskin:
                    player.Crystals.AddBoon(arena, 0, StoneskinWards, 0);
                    LastResult = $"Your skin hardens. {player.Crystals.PendingWards} wards await the guardian.";
                    break;
                case Ware.Whetstone:
                    player.Crystals.AddBoon(arena, 1, 0, 0);
                    LastResult = $"A keen edge. +{player.Crystals.PendingDamage} damage against the guardian.";
                    break;
                case Ware.Quicksilver:
                    player.Crystals.AddBoon(arena, 0, 0, 1);
                    LastResult = $"Your feet feel light. +{player.Crystals.PendingSwiftness * CrystalPouch.SwiftnessPerBoon:P0} speed in the arena.";
                    break;
                default:
                    var powerup = PowerupCatalog.Get(offer.Powerup.Value);
                    player.GrantPowerup(powerup.Type);
                    LastResult = $"{offer.Name} is yours. {powerup.Name} rank {player.Powerups.Count(powerup.Type)} for the descent.";
                    break;
            }
            HeroVfx.Motes(root, at, 0.7f, offer.Color, 12, 0.9f);
            HeroVfx.Pulse(root, at, 0.8f, new Color(offer.Color.r, offer.Color.g, offer.Color.b, 0.6f), 0.3f);
            HeroVfx.Sparks(root, Counter + Vector2.up * 0.6f, CrystalPouch.CrystalColor, 6, 2.5f, 0.3f, Vector2.up, 100f, 0.7f);
            return true;
        }

        private void Update()
        {
            if (run == null) return;
            var player = run.Player;
            if (IsOpen && (!run.IsPlaying || !IsNear(player))) IsOpen = false;
            float time = Time.time;
            for (int i = 0; i < glows.Count; i++)
            {
                // Lantern and crystal light breathes and flickers a little, each at its own pace.
                float flicker = 0.85f + 0.1f * Mathf.Sin(time * (2.3f + i * 0.37f) + i) + 0.05f * Mathf.Sin(time * 11f + i * 2.1f);
                var color = glows[i].color;
                glows[i].color = new Color(color.r, color.g, color.b, glowAlpha[i] * flicker);
            }
            for (int i = 0; i < floaters.Count; i++)
                floaters[i].position = floaterRest[i] + Vector2.up * (0.08f * Mathf.Sin(time * 2f + i * 1.3f));
            if (merchant != null) merchant.localScale = new Vector3(1.1f, 1.1f * (1f + 0.025f * Mathf.Sin(time * 2.2f)), 1f);
            if (merchantHalo != null)
            {
                // The merchant's rim light pulses so he reads as someone to talk to, and flares when he is in reach.
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f);
                bool beckoning = IsNear(player) && !IsOpen;
                float strength = beckoning ? 0.75f + 0.25f * pulse : 0.35f + 0.25f * pulse;
                merchantHalo.color = new Color(MerchantGlow.r, MerchantGlow.g, MerchantGlow.b, 0.7f * strength);
                merchantOutline.color = new Color(MerchantGlow.r, MerchantGlow.g, MerchantGlow.b, strength);
                merchantHalo.transform.localScale = Vector3.one * (beckoning ? 3.1f : 2.7f) * (1f + 0.06f * pulse);
            }
            if (time >= nextSparkle && sparklePoints.Count > 0 && run.ProjectileRoot != null)
            {
                nextSparkle = time + 0.35f;
                var point = sparklePoints[Random.Range(0, sparklePoints.Count)];
                HeroVfx.Motes(run.ProjectileRoot, point, 0.4f, new Color(0.85f, 0.65f, 1f, 0.8f), 3, 1.2f);
            }
        }

        // ---------------------------------------------------------------- the room

        private static Sprite glowSprite, merchantSprite, merchantOutlineSprite, bottleSprite;
        private static readonly Color MerchantGlow = new Color(0.85f, 0.7f, 1f);

        /// <summary>Builds the shop's furnishings inside <paramref name="level"/>; the map and stairs are drawn by the run.</summary>
        public static CrystalShop Create(DungeonRun run, Transform level)
        {
            var root = new GameObject("Crystal shop");
            root.transform.SetParent(level, false);
            var shop = root.AddComponent<CrystalShop>();
            shop.run = run;
            shop.Restock(run.Seed + run.Floor * 104729);
            shop.Furnish();
            return shop;
        }

        private SpriteRenderer Part(string name, Vector2 position, Vector2 size, Color color, int order = 1)
            => DungeonVisuals.Create(name, transform, position, size, color, order);

        private SpriteRenderer Glow(Vector2 position, float size, Color color, float alpha)
        {
            var glow = Part("Glow", position, Vector2.one * size, new Color(color.r, color.g, color.b, alpha), 2);
            glow.sprite = GlowSprite;
            glows.Add(glow);
            glowAlpha.Add(alpha);
            return glow;
        }

        private void Furnish()
        {
            var room = DungeonMap.ShopRoom;
            float left = room.xMin - 0.5f, right = room.xMax - 0.5f, bottom = room.yMin - 0.5f, top = room.yMax - 0.5f;
            Vector2 middle = new Vector2((left + right) / 2f, 17f);

            // A deep red rug with a gold border and a crystal diamond at its heart.
            Part("Rug border", middle, new Vector2(9.4f, 5.4f), new Color(0.62f, 0.44f, 0.16f));
            Part("Rug", middle, new Vector2(9f, 5f), new Color(0.38f, 0.08f, 0.13f));
            Part("Rug field", middle, new Vector2(8f, 4f), new Color(0.47f, 0.12f, 0.17f));
            for (int i = 0; i < 8; i++)
            {
                float x = middle.x - 3.5f + i;
                Part("Rug tassel", new Vector2(x, middle.y + 2.8f), new Vector2(0.08f, 0.25f), new Color(0.75f, 0.55f, 0.2f));
                Part("Rug tassel", new Vector2(x, middle.y - 2.8f), new Vector2(0.08f, 0.25f), new Color(0.75f, 0.55f, 0.2f));
            }
            var diamond = Part("Rug diamond", middle, Vector2.one * 1.6f, new Color(0.62f, 0.44f, 0.16f));
            diamond.transform.rotation = Quaternion.Euler(0, 0, 45f);
            var inner = Part("Rug diamond inset", middle, Vector2.one * 1.2f, new Color(0.3f, 0.12f, 0.42f));
            inner.transform.rotation = Quaternion.Euler(0, 0, 45f);
            foreach (var corner in new[] { new Vector2(-3f, 1.5f), new Vector2(3f, 1.5f), new Vector2(-3f, -1.5f), new Vector2(3f, -1.5f) })
            {
                var stud = Part("Rug stud", middle + corner, Vector2.one * 0.35f, new Color(0.62f, 0.44f, 0.16f));
                stud.transform.rotation = Quaternion.Euler(0, 0, 45f);
            }

            // The merchant's counter along the back wall, with him behind it under a banner.
            var counter = DungeonMap.ShopCounter;
            float counterX = counter.center.x - 0.5f;
            Part("Alcove", new Vector2(counterX, counter.yMin + 1f), new Vector2(counter.width, 1f), new Color(0.09f, 0.06f, 0.07f));
            Glow(new Vector2(counterX, counter.yMin + 1.2f), 3.4f, CrystalPouch.CrystalColor, 0.18f);
            merchantHalo = Part("Merchant halo", new Vector2(counterX, counter.yMin + 1.25f), Vector2.one * 2.7f, MerchantGlow, 2);
            merchantHalo.sprite = GlowSprite;
            merchant = Part("Merchant", new Vector2(counterX, counter.yMin + 1.2f), Vector2.one * 1.1f, Color.white, 4).transform;
            merchant.GetComponent<SpriteRenderer>().sprite = MerchantSprite;
            // A slightly larger silhouette just behind him forms a glowing outline that breathes with him.
            merchantOutline = DungeonVisuals.Create("Merchant outline", merchant, merchant.position, Vector2.one, MerchantGlow, 3);
            merchantOutline.transform.localScale = Vector3.one * 1.18f;
            merchantOutline.sprite = MerchantOutlineSprite;
            Part("Counter front", new Vector2(counterX, counter.yMin), new Vector2(counter.width, 1f), new Color(0.36f, 0.2f, 0.1f), 5);
            for (int i = 0; i < counter.width; i++)
                Part("Counter panel", new Vector2(counter.xMin + i, counter.yMin - 0.05f), new Vector2(0.7f, 0.6f), new Color(0.29f, 0.16f, 0.08f), 5);
            Part("Counter top", new Vector2(counterX, counter.yMin + 0.45f), new Vector2(counter.width + 0.2f, 0.22f), new Color(0.6f, 0.38f, 0.18f), 6);
            Part("Counter trim", new Vector2(counterX, counter.yMin - 0.47f), new Vector2(counter.width + 0.1f, 0.08f), new Color(0.62f, 0.44f, 0.16f), 6);
            Bottle(new Vector2(counter.xMin + 0.1f, counter.yMin + 0.75f), HealColor, 7);
            Bottle(new Vector2(counter.xMin + 0.5f, counter.yMin + 0.72f), AbilityCatalog.Ice, 7);
            Floater(new Vector2(counter.xMax - 1.3f, counter.yMin + 0.85f), 0.45f, 7);
            Glow(new Vector2(counter.xMax - 1.3f, counter.yMin + 0.85f), 1.3f, CrystalPouch.CrystalColor, 0.35f);
            Part("Banner", new Vector2(counterX, top + 0.72f), new Vector2(2.4f, 0.5f), new Color(0.3f, 0.12f, 0.42f), 2);
            Part("Banner trim", new Vector2(counterX, top + 0.45f), new Vector2(2.4f, 0.06f), new Color(0.75f, 0.55f, 0.2f), 3);
            var emblem = Part("Banner crystal", new Vector2(counterX, top + 0.72f), new Vector2(0.3f, 0.36f), Color.white, 3);
            emblem.sprite = DungeonVisuals.CrystalSprite;

            // Shelves of wares on the back wall either side of the counter.
            Shelf(new Vector2(left + 3f, top + 0.62f));
            Shelf(new Vector2(right - 3f, top + 0.62f));

            // Pedestals showing off a potion and a heart crystal in front of the counter.
            Pedestal(new Vector2(counter.xMin - 1.6f, counter.yMin - 1.2f), false);
            Pedestal(new Vector2(counter.xMax + 0.6f, counter.yMin - 1.2f), true);

            // Hanging lanterns along the walls cast warm pools of light.
            foreach (var spot in new[] { new Vector2(left + 0.5f, top + 0.55f), new Vector2(right - 0.5f, top + 0.55f),
                new Vector2(left + 5.5f, bottom - 0.4f), new Vector2(right - 5.5f, bottom - 0.4f) })
                Lantern(spot);

            // Crystal clusters glitter in the corners.
            Cluster(new Vector2(left + 0.6f, bottom + 0.55f), 1f);
            Cluster(new Vector2(right - 0.6f, bottom + 0.55f), -1f);
            Cluster(new Vector2(left + 0.7f, top - 1.2f), 1f);
            Cluster(new Vector2(right - 0.7f, top - 1.2f), -1f);
        }

        private void Bottle(Vector2 position, Color color, int order)
        {
            var bottle = Part("Bottle", position, new Vector2(0.28f, 0.42f), color, order);
            bottle.sprite = BottleSprite;
        }

        private void Floater(Vector2 position, float size, int order)
        {
            var gem = Part("Floating crystal", position, new Vector2(size * 0.85f, size), Color.white, order);
            gem.sprite = DungeonVisuals.CrystalSprite;
            floaters.Add(gem.transform);
            floaterRest.Add(position);
        }

        private void Shelf(Vector2 center)
        {
            Color wood = new Color(0.4f, 0.24f, 0.12f), bracket = new Color(0.25f, 0.15f, 0.08f);
            Color[] colors = { HealColor, AbilityCatalog.Ice, new Color(0.5f, 0.9f, 0.45f), AbilityCatalog.Gold, CrystalPouch.CrystalColor };
            for (int row = 0; row < 2; row++)
            {
                float y = center.y - 0.3f + row * 0.42f;
                Part("Shelf", new Vector2(center.x, y - 0.2f), new Vector2(3.2f, 0.09f), wood, 2);
                Part("Shelf bracket", new Vector2(center.x - 1.4f, y - 0.27f), new Vector2(0.08f, 0.16f), bracket, 2);
                Part("Shelf bracket", new Vector2(center.x + 1.4f, y - 0.27f), new Vector2(0.08f, 0.16f), bracket, 2);
                for (int i = 0; i < 5; i++)
                {
                    var bottle = Part("Shelf bottle", new Vector2(center.x - 1.2f + i * 0.6f + (row == 1 ? 0.25f : 0f), y), new Vector2(0.2f, 0.3f),
                        colors[(i + row * 2) % colors.Length], 3);
                    bottle.sprite = BottleSprite;
                }
            }
        }

        private void Pedestal(Vector2 position, bool heart)
        {
            Part("Pedestal base", position + Vector2.down * 0.3f, new Vector2(0.7f, 0.18f), new Color(0.45f, 0.42f, 0.48f));
            Part("Pedestal column", position, new Vector2(0.44f, 0.5f), new Color(0.56f, 0.53f, 0.6f));
            Part("Pedestal top", position + Vector2.up * 0.28f, new Vector2(0.64f, 0.14f), new Color(0.68f, 0.64f, 0.72f));
            Vector2 display = position + Vector2.up * 0.75f;
            Glow(display, 1.4f, heart ? CrystalPouch.CrystalColor : HealColor, 0.28f);
            if (heart) Floater(display, 0.42f, 3);
            else
            {
                var bottle = Part("Displayed potion", display, new Vector2(0.34f, 0.5f), HealColor, 3);
                bottle.sprite = BottleSprite;
                floaters.Add(bottle.transform);
                floaterRest.Add(display);
            }
            sparklePoints.Add(display);
        }

        private void Lantern(Vector2 position)
        {
            Color warm = new Color(1f, 0.72f, 0.35f);
            Glow(position, 4.2f, warm, 0.16f);
            Glow(position, 1.4f, warm, 0.4f);
            Part("Lantern chain", position + Vector2.up * 0.35f, new Vector2(0.05f, 0.3f), new Color(0.22f, 0.2f, 0.2f), 3);
            Part("Lantern cap", position + Vector2.up * 0.2f, new Vector2(0.34f, 0.09f), new Color(0.25f, 0.2f, 0.16f), 3);
            Part("Lantern", position, new Vector2(0.26f, 0.32f), new Color(1f, 0.82f, 0.45f), 3);
            Part("Lantern base", position + Vector2.down * 0.19f, new Vector2(0.3f, 0.07f), new Color(0.25f, 0.2f, 0.16f), 3);
        }

        private void Cluster(Vector2 position, float lean)
        {
            Glow(position, 2.2f, CrystalPouch.CrystalColor, 0.3f);
            (Vector2 offset, float size, float angle)[] shards =
            {
                (new Vector2(0f, 0.1f), 0.7f, 8f * lean), (new Vector2(-0.28f, -0.05f), 0.45f, 28f * lean), (new Vector2(0.27f, -0.08f), 0.4f, -22f * lean)
            };
            foreach (var (offset, size, angle) in shards)
            {
                var shard = Part("Crystal cluster", position + offset, new Vector2(size * 0.8f, size), Color.white, 3);
                shard.sprite = DungeonVisuals.CrystalSprite;
                shard.transform.rotation = Quaternion.Euler(0, 0, angle);
            }
            sparklePoints.Add(position);
        }

        /// <summary>A soft round falloff for light pools, drawn tinted and translucent.</summary>
        private static Sprite GlowSprite
        {
            get
            {
                if (glowSprite != null) return glowSprite;
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Shop glow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size / 2f) / (size / 2f);
                        float falloff = Mathf.Clamp01(1f - distance);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, falloff * falloff);
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return glowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            }
        }

        /// <summary>A round-bellied bottle with a cork; tinted by its contents.</summary>
        private static Sprite BottleSprite => bottleSprite != null ? bottleSprite : bottleSprite = DungeonVisuals.PaletteSprite("Shop bottle", new[]
        {
            "...CC...", "...CC...", "...GG...", "..GWWG..", ".GWWWWG.", "GWLWWWWG", "GWLWWWWG", "GWWWWWWG", ".GWWWWG.", "..GGGG.."
        }, key => key switch
        {
            'C' => new Color(0.55f, 0.38f, 0.22f),
            'G' => new Color(0.85f, 0.9f, 0.95f),
            'W' => new Color(0.8f, 0.8f, 0.8f),
            'L' => Color.white,
            _ => Color.clear
        });

        /// <summary>The merchant: a hooded figure in violet robes with glowing eyes, cradling a crystal.</summary>
        private static readonly string[] MerchantRows =
        {
            ".....HHHHHH.....", "....HHPPPPHH....", "...HHPPPPPPHH...", "...HPDDDDDDPH...", "..HHPDEDDEDPHH..",
            "..HPPDDDDDDPPH..", "..HPPPDDDDPPPH..", ".HPPPGPPPPGPPPH.", ".HPPGGPPPPPGGPH.", ".HPPPSKKKKSPPPH.",
            "HPPPPSKCCKSPPPPH", "HPPPPPKCCKPPPPPH", "HPPPPPPKKPPPPPPH", "HPPPPPPPPPPPPPPH", ".HHHHHHHHHHHHHH."
        };

        /// <summary>The merchant's silhouette in plain white, tinted to draw his glowing outline.</summary>
        private static Sprite MerchantOutlineSprite => merchantOutlineSprite != null ? merchantOutlineSprite
            : merchantOutlineSprite = DungeonVisuals.PaletteSprite("Crystal merchant outline", MerchantRows, key => key == '.' ? Color.clear : Color.white);

        private static Sprite MerchantSprite => merchantSprite != null ? merchantSprite : merchantSprite = DungeonVisuals.PaletteSprite("Crystal merchant", MerchantRows, key => key switch
        {
            'H' => new Color(0.16f, 0.07f, 0.24f),
            'P' => new Color(0.38f, 0.18f, 0.55f),
            'D' => new Color(0.05f, 0.03f, 0.07f),
            'E' => new Color(0.75f, 1f, 0.95f),
            'G' => new Color(0.82f, 0.62f, 0.25f),
            'S' => new Color(0.86f, 0.7f, 0.58f),
            'K' => new Color(0.55f, 0.3f, 0.85f),
            'C' => new Color(0.95f, 0.85f, 1f),
            _ => Color.clear
        });
    }
}

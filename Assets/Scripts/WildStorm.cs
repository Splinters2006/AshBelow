using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Wizard's class mechanic: every elemental effect he sets off (burn, freeze or shock) charges it. After 8
    /// a wild storm gathers over him for a while and keeps striking the nearest enemy, taking turns hurling a
    /// fireball, a lightning bolt and an ice bolt. Every storm strike is guaranteed to set off its element. With its R
    /// upgrade (Cataclysm, from the Ash shop) the storm grows wilder: a far bigger thunderhead that also rains meteors
    /// and lightning strikes from the sky onto enemies around him, on top of its usual volleys.
    /// </summary>
    public sealed class WildStorm : ChargedMechanic
    {
        public const int EffectsNeeded = 8;
        public const float Duration = 10f, Interval = 0.4f, Range = 8f, Height = 1.3f;
        public static readonly Color CloudColor = new Color(0.32f, 0.34f, 0.46f);
        /// <summary>Cataclysm's strikes from the sky: a meteor and a lightning strike take turns.</summary>
        public const float SkyInterval = 0.6f, SkyWarning = 0.45f, MeteorRadius = 1.5f, SkyBoltRadius = 1.1f, BigCloudHeight = 1.9f;
        private static readonly Color MeteorColor = new Color(1f, 0.5f, 0.2f);
        private static Sprite cloudSprite, bigCloudSprite, meteorSprite;
        private float stormUntil;
        private int turn, skyTurn;
        public bool IsRaging => Time.time < stormUntil;
        public override string Name => "Wild Storm";
        public override Color Color => AbilityCatalog.Ice;
        public override int Required => EffectsNeeded;
        public override string Status => IsRaging ? "STORM  " + Seconds(stormUntil - Time.time) : base.Status;

        public override void OnElementalEffect() { if (!IsRaging) AddCharge(1); }

        protected override bool Activate(Vector2 aim)
        {
            if (IsRaging) return false;
            stormUntil = Time.time + Duration;
            StartCoroutine(Storm());
            return true;
        }

        private Vector2 CloudPosition => (Vector2)transform.position + Vector2.up * (IsUpgraded ? BigCloudHeight : Height);

        private IEnumerator Storm()
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            var cloud = DungeonVisuals.Create("Wild storm", root, CloudPosition, Vector2.one, Color.white, 8);
            bool wild = IsUpgraded;
            cloud.sprite = wild ? BigCloudSprite : CloudSprite;
            float burst = wild ? 3f : 1.6f;
            HeroVfx.Pulse(root, CloudPosition, burst, CloudColor, 0.4f);
            CoopFx.Pulse(run, CloudPosition, burst, CloudColor, 0.4f);
            float nextStrike = Time.time + 0.3f, nextSky = Time.time + 0.6f;
            while (Time.time < stormUntil)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot || Player.Health <= 0) break;
                Vector2 origin = CloudPosition;
                cloud.transform.position = origin + new Vector2(Mathf.Sin(Time.time * 2f) * 0.08f, Mathf.Sin(Time.time * 3.1f) * 0.05f);
                // A flicker of inner lightning keeps the cloud alive between strikes.
                cloud.color = Color.Lerp(Color.white, new Color(0.8f, 0.9f, 1.4f), Random.value < 0.06f ? 1f : 0f);
                if (Time.time >= nextStrike)
                {
                    nextStrike = Time.time + Interval;
                    var target = FindTarget(origin);
                    if (target != null) Strike(origin, target);
                }
                if (wild && Time.time >= nextSky)
                {
                    nextSky = Time.time + SkyInterval;
                    StartCoroutine(SkyStrike(run, root, SkyTarget(run), skyTurn++ % 2 == 0));
                }
                yield return null;
            }
            stormUntil = Mathf.Min(stormUntil, Time.time);
            if (cloud != null)
            {
                if (root != null) HeroVfx.Motes(root, cloud.transform.position, 0.8f, CloudColor, 12, 0.6f);
                Destroy(cloud.gameObject);
            }
        }

        private DungeonEnemy FindTarget(Vector2 origin)
        {
            DungeonEnemy best = null;
            float nearest = Range;
            foreach (var enemy in Player.Run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.IsInvulnerable) continue;
                float distance = Vector2.Distance(origin, enemy.transform.position);
                if (distance > nearest || !Player.Run.HasLineOfSight(transform.position, enemy.transform.position)) continue;
                nearest = distance;
                best = enemy;
            }
            return best;
        }

        /// <summary>Takes turns: a fireball, a lightning bolt, then an ice bolt.</summary>
        private void Strike(Vector2 origin, DungeonEnemy target)
        {
            var run = Player.Run;
            Vector2 at = target.transform.position;
            Vector2 aim = (at - origin).normalized;
            int damage = Player.Damage + 2;
            switch (turn++ % 3)
            {
                case 0:
                    SpellProjectile.Spawn(Player, aim, damage, DamageElement.Fire, Color.white, Range + 1f, 0f, 0, origin, true);
                    break;
                case 1:
                    CombatVfx.GlowBolt(run.ProjectileRoot, origin, at, AbilityCatalog.Ice);
                    CoopFx.Bolt(run, origin, at, AbilityCatalog.Ice, true);
                    HeroVfx.Sparks(run.ProjectileRoot, at, Color.Lerp(AbilityCatalog.Ice, Color.white, 0.4f), 7, 4f, 0.25f);
                    CombatDamage.Apply(Player, target, damage, DamageElement.Lightning, origin, guaranteedEffect: true);
                    break;
                default:
                    SpellProjectile.Spawn(Player, aim, damage, DamageElement.Ice, AbilityCatalog.Ice, Range + 1f, 0f, 0, origin, true);
                    break;
            }
        }

        /// <summary>A random enemy within range of the Wizard, walls or not (it falls from above); else a random spot nearby.</summary>
        private Vector2 SkyTarget(DungeonRun run)
        {
            Vector2 hero = transform.position;
            DungeonEnemy target = null;
            int seen = 0;
            foreach (var enemy in run.Enemies)
                if (enemy != null && enemy.Health > 0 && !enemy.IsInvulnerable && Vector2.Distance(hero, enemy.transform.position) <= Range && Random.Range(0, ++seen) == 0) target = enemy;
            return target != null ? (Vector2)target.transform.position
                : PlayerAbilities.FindGroundLanding(run.Map, hero, Random.insideUnitCircle.normalized, Random.Range(1f, Range * 0.6f));
        }

        /// <summary>Cataclysm: after a short warning ring, a meteor or a lightning strike comes down on the spot and hits everything in it.</summary>
        private IEnumerator SkyStrike(DungeonRun run, Transform root, Vector2 spot, bool meteor)
        {
            float radius = meteor ? MeteorRadius : SkyBoltRadius;
            Color color = meteor ? MeteorColor : CombatDamage.ShockColor;
            CombatVfx.Ring(root, spot, radius, FlameMesh.Alpha(color, 0.6f), SkyWarning);
            CoopFx.Ring(run, spot, radius, FlameMesh.Alpha(color, 0.6f), SkyWarning);
            Vector2 sky = spot + (meteor ? new Vector2(-3f, 7f) : Vector2.up * 7f);
            SpriteRenderer rock = null;
            if (meteor)
            {
                rock = DungeonVisuals.Create("Storm meteor", root, sky, Vector2.one, Color.white, 9);
                rock.sprite = MeteorSprite;
            }
            for (float t = 0f; t < SkyWarning; t += Time.deltaTime)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot) break;
                if (rock != null)
                {
                    // It falls faster as it comes, tumbling and shedding embers.
                    float fall = t / SkyWarning;
                    rock.transform.position = Vector2.Lerp(sky, spot, fall * fall);
                    rock.transform.Rotate(0f, 0f, 540f * Time.deltaTime);
                    if (Random.value < 0.5f) HeroVfx.Sparks(root, rock.transform.position, MeteorColor, 2, 1.5f, 0.3f, sky - spot, 40f);
                }
                yield return null;
            }
            if (rock != null) Destroy(rock.gameObject);
            if (!run.IsPlaying || root != run.ProjectileRoot || Player == null) yield break;
            if (meteor)
            {
                HeroVfx.Pulse(root, spot, radius * 1.2f, MeteorColor, 0.4f);
                CoopFx.Pulse(run, spot, radius * 1.2f, MeteorColor, 0.4f);
                HeroVfx.Sparks(root, spot, FlameMesh.Yellow, 18, 5.5f, 0.45f, null, 360f, 1.3f);
                ScreenFx.Shake(0.18f, 0.18f);
            }
            else
            {
                CombatVfx.GlowBolt(root, sky, spot, CombatDamage.ShockColor);
                CoopFx.Bolt(run, sky, spot, CombatDamage.ShockColor, true);
                HeroVfx.Sparks(root, spot, CombatDamage.ShockColor, 12, 4.5f, 0.3f, null, 360f, 1.2f);
                ScreenFx.Shake(0.08f, 0.1f);
            }
            int damage = Player.Damage + 2;
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(spot, enemy.transform.position) <= radius + enemy.HitRadius)
                    CombatDamage.Apply(Player, enemy, damage, meteor ? DamageElement.Fire : DamageElement.Lightning, spot, meteor ? 0.8f : 0.3f, guaranteedEffect: true);
        }

        /// <summary>A small pixel-art thundercloud.</summary>
        private static Sprite CloudSprite => cloudSprite != null ? cloudSprite : cloudSprite = BuildSprite(1.8f, CloudPixel,
            "....DDDD..DD....", "..DDLLLLDDLLD...", ".DLLLWWLLLWWLD..", "DLWWWWWWWWWWWLD.",
            "DWWWMMWWWWMMWWD.", ".DMMMMMMMMMMMD..", "..DD.YD.DY.DD...", "......Y...Y.....");

        /// <summary>Cataclysm's thunderhead: twice as wide, towering, with lightning and ember-glow dripping from its belly.</summary>
        private static Sprite BigCloudSprite => bigCloudSprite != null ? bigCloudSprite : bigCloudSprite = BuildSprite(3.6f, CloudPixel,
            ".......DDDDD.....DDDD.....", ".....DDLLLLLDD.DDLLLLDD...",
            "...DDLLLWWWLLLDLLLWWLLLD..", "..DLLLWWWWWWWLLLWWWWWWLLD.",
            ".DLLWWWWWWWWWWWWWWWWWWWWLD", "DLWWWWWWWWWWWWWWWWWWWWWWWD",
            "DWWWMMMWWWWWMMMMWWWWMMMWWD", "DWMMMMMMMWMMMMMMMMWMMMMMWD",
            ".DMMMMMMMMMMMMMMMMMMMMMMD.", "..DDMMMDDDMMMMMDDDMMMDDD..",
            "....DYD.RD.DYD..DR.DYD....", ".....Y..R...YY...R..Y.....",
            "....Y........Y......Y.....");

        /// <summary>A tumbling lump of burning rock.</summary>
        private static Sprite MeteorSprite => meteorSprite != null ? meteorSprite : meteorSprite = BuildSprite(0.8f, MeteorPixel,
            "..DDDD..", ".DOOOOD.", "DOYYYOOD", "DOYWYOOD", "DOYYYOOD", "DOOOOOOD", ".DOOOOD.", "..DDDD..");

        private static Color CloudPixel(char c) => c == 'D' ? new Color(0.14f, 0.15f, 0.22f) : c == 'M' ? new Color(0.28f, 0.3f, 0.4f)
            : c == 'W' ? CloudColor : c == 'L' ? new Color(0.5f, 0.53f, 0.66f) : c == 'Y' ? new Color(0.85f, 0.95f, 1f)
            : c == 'R' ? new Color(1f, 0.55f, 0.25f) : Color.clear;

        private static Color MeteorPixel(char c) => c == 'D' ? new Color(0.45f, 0.12f, 0.08f) : c == 'O' ? MeteorColor
            : c == 'Y' ? new Color(1f, 0.85f, 0.35f) : c == 'W' ? Color.white : Color.clear;

        /// <summary>Pixel art from rows of palette keys (top row first), <paramref name="unitsWide"/> world units across.</summary>
        private static Sprite BuildSprite(float unitsWide, System.Func<char, Color> palette, params string[] rows)
        {
            int width = rows[0].Length, height = rows.Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, height - y - 1, palette(rows[y][x]));
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, width / unitsWide);
        }
    }
}

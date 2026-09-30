using UnityEngine;

namespace Slopgame
{
    public enum HazardShape : byte
    {
        /// <summary>The whole boss arena burns except a safe circle.</summary>
        Inferno,
        /// <summary>A meteor strike that leaves a burning crater.</summary>
        Pool,
        /// <summary>A straight pillar of fire.</summary>
        Beam,
        /// <summary>A wall of fire expanding outward; roll through it.</summary>
        Ring
    }

    /// <summary>What a hazard is made of. Only the look changes: every style strikes and burns the same way.</summary>
    public enum HazardStyle : byte
    {
        /// <summary>The Archdemon's fire.</summary>
        Hellfire,
        /// <summary>The Rime Warden's ice: hail craters and frost walls.</summary>
        Frost,
        /// <summary>The Steel Duelist's blade light.</summary>
        Steel,
        /// <summary>The Neon Arcology's crackling plasma.</summary>
        Plasma,
        Circuit,
        Artillery,
        Void,
        /// <summary>The Savage Wilds' toxic spores: green, flame-like fumes.</summary>
        Venom,
        /// <summary>The Ash Below's spike traps: holes rattle in a steel plate, then spikes stab up.</summary>
        Spikes
    }

    /// <summary>Everything needed to rebuild a hazard on another machine.</summary>
    public struct HazardSpec
    {
        public HazardShape Shape;
        public HazardStyle Style;
        public Vector2 Center, Direction;
        /// <summary>Safe/crater radius, beam length, or a ring's final radius.</summary>
        public float Radius;
        /// <summary>Beam or ring thickness.</summary>
        public float Width;
        public float Telegraph, Duration;
    }

    /// <summary>
    /// A telegraphed hazard area: hellfire, frost or blade light depending on its <see cref="HazardStyle"/>. After its
    /// warning it hurts the local hero while they stand inside; every co-op machine runs its own copy (announced by
    /// the host) and judges only its own hero.
    /// </summary>
    public sealed class HellfireZone : MonoBehaviour
    {
        /// <summary>A style's colours from brightest to darkest, standing in for the flame palette.</summary>
        private readonly struct Palette
        {
            public readonly Color Core, Bright, Main, Deep, Dark;
            public Palette(Color core, Color bright, Color main, Color deep, Color dark) { Core = core; Bright = bright; Main = main; Deep = deep; Dark = dark; }
        }

        private static readonly Palette HellfirePalette = new Palette(FlameMesh.Core, FlameMesh.Yellow, FlameMesh.Orange, FlameMesh.Crimson, FlameMesh.Ember);
        private static readonly Palette FrostPalette = new Palette(new Color(0.94f, 0.99f, 1f), new Color(0.7f, 0.92f, 1f),
            new Color(0.38f, 0.72f, 1f), new Color(0.14f, 0.32f, 0.78f), new Color(0.05f, 0.1f, 0.3f));
        private static readonly Palette SteelPalette = new Palette(Color.white, new Color(0.78f, 0.97f, 1f),
            new Color(0.45f, 0.95f, 1f), new Color(0.2f, 0.45f, 0.62f), new Color(0.08f, 0.15f, 0.22f));
        private static readonly Palette PlasmaPalette = new Palette(new Color(1f, 0.95f, 1f), new Color(1f, 0.6f, 0.95f),
            new Color(1f, 0.25f, 0.8f), new Color(0.5f, 0.1f, 0.6f), new Color(0.15f, 0.03f, 0.22f));
        private static readonly Palette SpikePalette = new Palette(Color.white, new Color(0.85f, 0.88f, 0.92f),
            new Color(0.6f, 0.63f, 0.68f), new Color(0.3f, 0.32f, 0.36f), new Color(0.1f, 0.1f, 0.12f));
        private static readonly Palette VenomPalette = new Palette(new Color(0.95f, 1f, 0.85f), new Color(0.75f, 1f, 0.35f),
            new Color(0.4f, 0.85f, 0.2f), new Color(0.18f, 0.45f, 0.1f), new Color(0.06f, 0.15f, 0.04f));

        private DungeonRun run;
        private HazardSpec spec;
        private Palette colors;
        private FlameMesh flames;
        private float age;
        private bool erupted;
        private Vector2 meteorFrom;

        /// <summary>
        /// Damage the zone deals to enemies caught in it; 0 (every boss hazard) leaves them alone. Environmental traps set
        /// it so heroes can lure enemies into them. Strikes hit each enemy once, burning ground once a second.
        /// </summary>
        public int EnemyDamage { get; set; }
        private readonly System.Collections.Generic.Dictionary<DungeonEnemy, float> enemyHitAt = new System.Collections.Generic.Dictionary<DungeonEnemy, float>();

        public bool IsBurning => age >= spec.Telegraph && age < spec.Telegraph + spec.Duration;
        /// <summary>The Cataclysm sea of fire and meteor craters are burning ground; beams and rings are strikes.</summary>
        private bool IsGround => spec.Shape == HazardShape.Inferno || spec.Shape == HazardShape.Pool;
        private float ActiveTime => age - spec.Telegraph;
        private float RingRadius => Mathf.Lerp(0.6f, spec.Radius, Mathf.Clamp01(ActiveTime / Mathf.Max(0.01f, spec.Duration)));

        public static HellfireZone Spawn(DungeonRun run, HazardSpec spec, bool announce = true)
        {
            if (run == null || run.ProjectileRoot == null) return null;
            var zone = new GameObject("Hellfire " + spec.Shape).AddComponent<HellfireZone>();
            zone.transform.SetParent(run.ProjectileRoot, false);
            zone.run = run;
            spec.Direction = spec.Direction.sqrMagnitude > 0.0001f ? spec.Direction.normalized : Vector2.right;
            zone.spec = spec;
            zone.colors = spec.Style == HazardStyle.Frost ? FrostPalette : spec.Style == HazardStyle.Steel ? SteelPalette
                : spec.Style == HazardStyle.Plasma ? PlasmaPalette : spec.Style == HazardStyle.Venom ? VenomPalette : spec.Style == HazardStyle.Spikes ? SpikePalette : HellfirePalette;
            if (spec.Style == HazardStyle.Circuit || spec.Style == HazardStyle.Artillery || spec.Style == HazardStyle.Void)
            {
                Color tint = spec.Style == HazardStyle.Circuit ? WorldCatalog.Neon
                    : spec.Style == HazardStyle.Artillery ? new Color(1f, 0.65f, 0.15f) : new Color(0.75f, 0.35f, 1f);
                zone.colors = new Palette(Color.white, Color.Lerp(tint, Color.white, 0.4f), tint, tint * 0.4f, tint * 0.15f);
            }
            zone.gameObject.name = spec.Style + " " + spec.Shape;
            zone.flames = new FlameMesh(zone.gameObject, spec.Shape == HazardShape.Inferno ? 2 : 3);
            zone.meteorFrom = spec.Center + new Vector2(Random.Range(-4f, 4f), 11f);
            if (announce && run.IsNetworked && run.Coop.IsHost) run.Coop.AnnounceHazard(spec);
            return zone;
        }

        public bool Contains(Vector2 point)
        {
            switch (spec.Shape)
            {
                case HazardShape.Inferno:
                    var arena = DungeonMap.Arena;
                    return point.x > arena.xMin - 0.5f && point.x < arena.xMax - 0.5f && point.y > arena.yMin - 0.5f && point.y < arena.yMax - 0.5f
                        && Vector2.Distance(point, spec.Center) > spec.Radius;
                case HazardShape.Pool:
                    return Vector2.Distance(point, spec.Center) <= spec.Radius;
                case HazardShape.Beam:
                    Vector2 offset = point - spec.Center;
                    float along = Vector2.Dot(offset, spec.Direction);
                    return along >= -0.3f && along <= spec.Radius && Mathf.Abs(Vector2.Dot(offset, Vector2.Perpendicular(spec.Direction))) <= spec.Width * 0.5f;
                default:
                    return Mathf.Abs(Vector2.Distance(point, spec.Center) - RingRadius) <= spec.Width * 0.5f;
            }
        }

        private void Update()
        {
            if (!run.IsPlaying) { Draw(); return; }
            age += Time.deltaTime;
            if (!erupted && age >= spec.Telegraph) Erupt();
            if (age >= spec.Telegraph + spec.Duration + 0.35f) { Destroy(gameObject); return; }
            var hero = run.Player;
            if (IsBurning && hero != null && hero.Health > 0 && !hero.IsInvulnerable && Contains(hero.transform.position))
            {
                int before = hero.Health;
                // Burning ground ticks once a second; pillars and fire walls strike like any other blow.
                if (IsGround) hero.Burn();
                else hero.Hit();
                if (spec.Style == HazardStyle.Venom && hero.Health > 0 && hero.Health != before) hero.Poison();
                if (hero.Health != before || hero.IsInvulnerable)
                    HeroVfx.Sparks(run.ProjectileRoot, hero.transform.position, colors.Main, 12, 4f, 0.4f, Vector2.up, 120f);
            }
            // Enemies are the host's to damage; a guest's copy only judges its own hero.
            if (IsBurning && EnemyDamage > 0 && !run.IsGuest) HurtEnemies();
            Draw();
        }

        private void HurtEnemies()
        {
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || !Contains(enemy.transform.position)) continue;
                if (enemyHitAt.TryGetValue(enemy, out float last) && (!IsGround || Time.time < last + 1f)) continue;
                enemyHitAt[enemy] = Time.time;
                HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, colors.Main, 8, 3.5f, 0.35f, Vector2.up, 120f);
                enemy.Hit(EnemyDamage, spec.Center, IsGround ? 0f : 0.6f);
            }
        }

        /// <summary>A steel plate full of holes; they rattle through the warning, then a bed of spikes stabs up and sinks back.</summary>
        private void DrawSpikes(bool warning, float warn, float fade)
        {
            float r = spec.Radius;
            flames.Disc(spec.Center, r, FlameMesh.Alpha(colors.Deep, 0.35f * fade), FlameMesh.Alpha(colors.Dark, 0.25f * fade));
            flames.Ring(spec.Center, r, 0.06f, FlameMesh.Alpha(colors.Main, (warning ? 0.35f + 0.5f * warn : 0.8f) * fade));
            float rise = warning ? 0f : Mathf.Clamp01(ActiveTime / 0.07f);
            float rattle = warning && warn > 0.55f ? Mathf.Sin(Time.time * 60f) * 0.03f : 0f;
            const float Spacing = 0.36f;
            for (float x = -r; x <= r; x += Spacing)
                for (float y = -r; y <= r; y += Spacing)
                {
                    Vector2 spot = spec.Center + new Vector2(x + ((int)Mathf.Round(y / Spacing) % 2 == 0 ? 0f : Spacing * 0.5f), y);
                    if (Vector2.Distance(spot, spec.Center) > r - 0.12f) continue;
                    flames.Disc(spot, 0.07f, FlameMesh.Alpha(colors.Dark, 0.9f * fade), FlameMesh.Alpha(colors.Dark, 0.6f * fade), 8);
                    if (warning)
                    {
                        // Tips glint in the holes just before they strike.
                        if (warn > 0.55f) flames.Diamond(spot + new Vector2(rattle, 0f), 0.05f, FlameMesh.Alpha(colors.Bright, (warn - 0.55f) * 2f));
                        continue;
                    }
                    float height = 0.42f * rise;
                    flames.Triangle(spot + new Vector2(-0.08f, 0f), spot + new Vector2(0.08f, 0f), spot + Vector2.up * height,
                        FlameMesh.Alpha(colors.Deep, fade), FlameMesh.Alpha(colors.Main, fade), FlameMesh.Alpha(colors.Core, fade));
                }
        }

        private void Erupt()
        {
            erupted = true;
            var root = run.ProjectileRoot;
            switch (spec.Shape)
            {
                case HazardShape.Inferno:
                    ScreenFx.Shake(0.55f, 1.1f);
                    ScreenFx.Flash(new Color(1f, 0.25f, 0.05f, 0.55f), 0.6f);
                    HeroVfx.Pulse(root, spec.Center, spec.Radius + 0.4f, AbilityCatalog.Gold, 0.6f);
                    break;
                case HazardShape.Pool:
                    ScreenFx.Shake(0.3f, 0.35f);
                    HeroVfx.Pulse(root, spec.Center, spec.Radius * 1.6f, colors.Bright, 0.45f);
                    HeroVfx.Sparks(root, spec.Center, colors.Main, 22, 7f, 0.55f, null, 360f, 1.6f);
                    CombatVfx.Ring(root, spec.Center, spec.Radius * 1.2f, colors.Core, 0.3f);
                    break;
                case HazardShape.Beam:
                    ScreenFx.Shake(0.2f, 0.3f);
                    HeroVfx.Sparks(root, spec.Center + spec.Direction * spec.Radius * 0.5f, colors.Main, 16, 5f, 0.45f,
                        Vector2.Perpendicular(spec.Direction), 60f, 1.3f);
                    break;
                case HazardShape.Ring:
                    ScreenFx.Shake(0.22f, 0.4f);
                    HeroVfx.Pulse(root, spec.Center, 1.6f, colors.Bright, 0.3f);
                    break;
            }
        }

        // ---------------------------------------------------------------- drawing

        private void Draw()
        {
            flames.Begin();
            bool warning = age < spec.Telegraph;
            float warn = spec.Telegraph > 0f ? Mathf.Clamp01(age / spec.Telegraph) : 1f;
            float fadeOut = Mathf.Clamp01((spec.Telegraph + spec.Duration + 0.35f - age) / 0.35f);
            if (spec.Style == HazardStyle.Circuit || spec.Style == HazardStyle.Artillery || spec.Style == HazardStyle.Void)
            {
                DrawDigital(warning, warn, fadeOut);
                flames.Commit();
                return;
            }
            switch (spec.Shape)
            {
                case HazardShape.Inferno: DrawInferno(warning, warn, fadeOut); break;
                case HazardShape.Pool: DrawPool(warning, warn, fadeOut); break;
                case HazardShape.Beam: DrawBeam(warning, warn, fadeOut); break;
                case HazardShape.Ring: DrawRing(warning, warn, fadeOut); break;
            }
            flames.Commit();
        }

        // Crisp scan lanes, targeting reticles and orbital waves, without the ash guardians' flames or ice.
        private void DrawDigital(bool warning, float progress, float fade)
        {
            Color bright = FlameMesh.Alpha(colors.Main, fade * (warning ? 0.65f : 1f));
            Color fill = FlameMesh.Alpha(colors.Main, fade * (warning ? 0.12f : 0.35f));
            if (spec.Shape == HazardShape.Beam)
            {
                Vector2 side = Vector2.Perpendicular(spec.Direction) * spec.Width * 0.5f;
                flames.Bar(spec.Center, spec.Direction, spec.Radius, spec.Width, fill, fill);
                flames.Bar(spec.Center + side, spec.Direction, spec.Radius, 0.05f, bright, bright);
                flames.Bar(spec.Center - side, spec.Direction, spec.Radius, 0.05f, bright, bright);
                if (!warning) flames.Bar(spec.Center, spec.Direction, spec.Radius, spec.Width * 0.22f, colors.Core, bright);
                for (float d = 0f; d < spec.Radius; d += 1.5f)
                    flames.Diamond(spec.Center + spec.Direction * d, 0.1f, bright);
            }
            else if (spec.Shape == HazardShape.Pool)
            {
                flames.Disc(spec.Center, spec.Radius, fill, fill, 32);
                flames.Ring(spec.Center, spec.Radius, 0.09f, bright, 32);
                flames.Ring(spec.Center, spec.Radius * (warning ? 1f - progress : 0.7f), 0.06f, bright, 32);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 dir = FlameMesh.Polar(i * Mathf.PI / 2f, 1f);
                    flames.Bar(spec.Center + dir * spec.Radius * 0.6f, dir, spec.Radius * 0.4f, 0.1f, bright, bright);
                }
                if (!warning)
                    for (int i = 0; i < 8; i++)
                        flames.Diamond(spec.Center + FlameMesh.Polar(i * Mathf.PI / 4f + Time.time, spec.Radius * 0.65f), 0.15f, bright);
            }
            else
            {
                float radius = warning ? 0.6f + progress * 0.5f : RingRadius;
                flames.Ring(spec.Center, radius, warning ? 0.08f : spec.Width, fill, bright, 72);
                flames.Ring(spec.Center, radius, 0.08f, FlameMesh.Alpha(colors.Core, fade), 72);
                for (int i = 0; i < 12; i++)
                    flames.Diamond(spec.Center + FlameMesh.Polar(i * Mathf.PI / 6f + Time.time, radius), 0.12f, bright);
            }
        }

        private void DrawInferno(bool warning, float warn, float fade)
        {
            var arena = DungeonMap.Arena;
            float time = Time.time, pulse = 0.5f + 0.5f * Mathf.Sin(time * Mathf.Lerp(5f, 16f, warn));
            float safe = spec.Radius;
            for (int x = arena.xMin; x < arena.xMax; x++)
                for (int y = arena.yMin; y < arena.yMax; y++)
                {
                    var cell = new Vector2(x, y);
                    float distance = Vector2.Distance(cell, spec.Center);
                    // Cells touching the sanctuary are left out; a curved band of fire closes the gap instead.
                    if (distance < safe + 0.72f) continue;
                    float seed = FlameMesh.Hash(x, y);
                    if (warning)
                    {
                        // The floor turns red at once and cracks with pulsing magma, so the doomed area reads instantly.
                        float glow = (0.45f + 0.3f * warn * pulse) * (0.75f + 0.25f * seed);
                        flames.Rect(cell - Vector2.one * 0.5f, cell + Vector2.one * 0.5f, FlameMesh.Alpha(colors.Deep, glow));
                        // Diagonal hazard stripes crawl across the doomed floor.
                        if (Mathf.Repeat(x + y - time * 2f, 3f) < 1f)
                            flames.Bar(cell + new Vector2(-0.5f, -0.5f), new Vector2(1f, 1f).normalized, 1.41f, 0.22f,
                                FlameMesh.Alpha(colors.Main, 0.18f + 0.22f * warn), FlameMesh.Alpha(colors.Main, 0.18f + 0.22f * warn));
                        if (seed > 0.45f)
                            flames.Bar(cell + new Vector2(seed - 0.5f, -0.4f), FlameMesh.Polar(seed * 6.3f, 1f), 0.7f, 0.07f,
                                FlameMesh.Alpha(colors.Bright, 0.35f + warn * 0.65f), FlameMesh.Alpha(colors.Main, 0f));
                        // Small flames start licking up over the last stretch of the warning.
                        float kindle = Mathf.InverseLerp(0.55f, 1f, warn);
                        if (kindle > 0f && seed > 0.35f)
                            Tongue(cell + new Vector2(seed - 0.5f, -0.45f) * 0.8f, Vector2.up, 0.45f, (0.3f + 0.5f * seed) * kindle, seed, kindle * 0.8f);
                        continue;
                    }
                    float heat = 0.55f + 0.2f * Mathf.Sin(time * 7f + seed * 20f);
                    flames.Rect(cell - Vector2.one * 0.5f, cell + Vector2.one * 0.5f,
                        FlameMesh.Alpha(Color.Lerp(colors.Deep, colors.Main, seed * 0.5f), heat * fade));
                    Tongue(cell + new Vector2(seed - 0.5f, -0.45f) * 0.8f, Vector2.up, 0.8f, 1.1f + seed * 0.7f, seed, fade);
                }
            // Rising embers above the sea of fire.
            if (!warning)
                for (int i = 0; i < 140; i++)
                {
                    float sx = FlameMesh.Hash(i, 1.7f), sy = FlameMesh.Hash(i, 9.3f);
                    Vector2 p = new Vector2(arena.xMin - 0.5f + sx * arena.width, arena.yMin - 0.5f + Mathf.Repeat(sy * arena.height + Time.time * (1.5f + sx * 2f), arena.height));
                    if (Vector2.Distance(p, spec.Center) < safe) continue;
                    flames.Diamond(p + Vector2.right * Mathf.Sin(Time.time * 3f + i) * 0.2f, 0.08f + sy * 0.06f, FlameMesh.Alpha(colors.Bright, 0.9f * fade));
                }
            flames.Ring(spec.Center, safe + 0.4f, 0.8f, warning ? FlameMesh.Alpha(colors.Deep, 0.12f + 0.3f * warn * pulse)
                : FlameMesh.Alpha(colors.Main, 0.7f * fade), FlameMesh.Alpha(colors.Deep, (warning ? 0.2f + 0.3f * warn * pulse : 0.6f) * fade), 64);
            // The sanctuary: a gold ring with rotating runes, then a white-hot wall of flame around it.
            float spin = time * 1.6f;
            if (warning)
            {
                // A pillar of gold light marks the sanctuary from anywhere in the arena.
                flames.Bar(spec.Center, Vector2.up, 7f, safe * 0.9f, FlameMesh.Alpha(AbilityCatalog.Gold, 0.2f + 0.12f * pulse), FlameMesh.Alpha(AbilityCatalog.Gold, 0f));
                flames.Ring(spec.Center, safe + 0.25f + (1f - Mathf.Repeat(time * 1.5f, 1f)) * 2.5f, 0.1f,
                    FlameMesh.Alpha(AbilityCatalog.Gold, 0.6f * Mathf.Repeat(time * 1.5f, 1f)), 64);
            }
            if (warning) DrawSanctuaryGuide(safe, pulse);
            flames.Disc(spec.Center, safe, FlameMesh.Alpha(AbilityCatalog.Gold, 0.12f + 0.08f * pulse), FlameMesh.Alpha(AbilityCatalog.Gold, 0.02f));
            flames.Ring(spec.Center, safe, warning ? 0.12f + 0.1f * pulse : 0.14f, FlameMesh.Alpha(AbilityCatalog.Gold, warning ? 1f : 0.8f * fade));
            for (int i = 0; i < 12; i++)
            {
                float angle = spin + i * Mathf.PI * 2f / 12f;
                flames.Diamond(spec.Center + FlameMesh.Polar(angle, safe - 0.35f), 0.14f, FlameMesh.Alpha(AbilityCatalog.Gold, 0.9f * fade));
                if (warning)
                    // Chevrons pull the eye toward the safe circle.
                    flames.Bar(spec.Center + FlameMesh.Polar(angle, safe + 1.6f - warn * 0.8f), -FlameMesh.Polar(angle, 1f), 0.8f, 0.12f,
                        FlameMesh.Alpha(AbilityCatalog.Gold, 0f), FlameMesh.Alpha(AbilityCatalog.Gold, pulse));
            }
            if (!warning)
            {
                int tongues = Mathf.CeilToInt(safe * 14f);
                for (int i = 0; i < tongues; i++)
                {
                    float angle = i * Mathf.PI * 2f / tongues;
                    Vector2 outward = FlameMesh.Polar(angle, 1f);
                    Tongue(spec.Center + outward * (safe + 0.05f), outward, 0.5f, 1.3f, FlameMesh.Hash(i, 3.1f), fade);
                }
                flames.Ring(spec.Center, safe + 0.1f, 0.18f, FlameMesh.Alpha(colors.Core, fade), FlameMesh.Alpha(colors.Bright, 0.4f * fade));
            }
        }

        /// <summary>A gold arrow from the local hero toward the sanctuary while they are still outside it.</summary>
        private void DrawSanctuaryGuide(float safe, float pulse)
        {
            var hero = run.Player;
            if (hero == null || hero.Health <= 0) return;
            Vector2 from = hero.transform.position, to = spec.Center - from;
            float distance = to.magnitude;
            if (distance < safe * 0.8f) return;
            Vector2 dir = to / distance;
            float length = Mathf.Min(2.4f, distance - safe * 0.6f), angle = Mathf.Atan2(dir.y, dir.x);
            Color gold = FlameMesh.Alpha(AbilityCatalog.Gold, 0.55f + 0.4f * pulse);
            Vector2 tip = from + dir * (0.7f + length);
            flames.Bar(from + dir * 0.7f, dir, length, 0.14f, FlameMesh.Alpha(AbilityCatalog.Gold, 0.1f), gold);
            flames.Bar(tip, -FlameMesh.Polar(angle + 0.6f, 1f), 0.55f, 0.14f, gold, gold);
            flames.Bar(tip, -FlameMesh.Polar(angle - 0.6f, 1f), 0.55f, 0.14f, gold, gold);
        }

        /// <summary>
        /// One tongue of the hazard: a licking flame for hellfire, a jutting ice shard for frost, a thin flickering
        /// glint of blade light for steel, and a jagged arc for plasma.
        /// </summary>
        private void Tongue(Vector2 root, Vector2 direction, float width, float height, float seed, float fade)
        {
            switch (spec.Style)
            {
                case HazardStyle.Frost:
                    Vector2 shard = (direction + Vector2.right * (seed - 0.5f) * 0.6f).normalized;
                    flames.Bar(root, shard, height * 0.55f, width * 0.45f, FlameMesh.Alpha(colors.Main, 0.9f * fade), FlameMesh.Alpha(colors.Core, 0.2f * fade));
                    flames.Diamond(root + shard * height * 0.55f, width * 0.18f, FlameMesh.Alpha(colors.Core, fade));
                    break;
                case HazardStyle.Steel:
                    float flicker = 0.5f + 0.5f * Mathf.Sin(Time.time * 25f + seed * 30f);
                    flames.Bar(root, direction, height * 0.5f * (0.6f + 0.4f * flicker), width * 0.12f,
                        FlameMesh.Alpha(colors.Core, fade * flicker), FlameMesh.Alpha(colors.Main, 0f));
                    break;
                case HazardStyle.Plasma:
                    // Two crooked segments that re-kink every few frames, like an electric arc.
                    float jitter = Mathf.Sin(Mathf.Floor(Time.time * 18f) * 12.9898f + seed * 78.233f);
                    Vector2 kink = root + (direction + Vector2.Perpendicular(direction) * jitter * 0.5f).normalized * height * 0.3f;
                    flames.Bar(root, (kink - root).normalized, Vector2.Distance(root, kink), width * 0.16f,
                        FlameMesh.Alpha(colors.Core, fade), FlameMesh.Alpha(colors.Main, 0.8f * fade));
                    flames.Bar(kink, (direction - Vector2.Perpendicular(direction) * jitter * 0.4f).normalized, height * 0.25f, width * 0.12f,
                        FlameMesh.Alpha(colors.Main, 0.8f * fade), FlameMesh.Alpha(colors.Bright, 0f));
                    break;
                default:
                    flames.Flame(root, direction, width, height, seed, fade);
                    break;
            }
        }

        private void DrawPool(bool warning, float warn, float fade)
        {
            if (spec.Style == HazardStyle.Spikes) { DrawSpikes(warning, warn, fade); return; }
            float r = spec.Radius, time = Time.time;
            if (warning)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 18f);
                flames.Disc(spec.Center, r * warn, FlameMesh.Alpha(colors.Deep, 0.35f), FlameMesh.Alpha(colors.Main, 0.2f + 0.2f * pulse));
                flames.Ring(spec.Center, r, 0.08f, FlameMesh.Alpha(colors.Main, 0.6f + 0.4f * pulse));
                flames.Bar(spec.Center - Vector2.right * r * 0.6f, Vector2.right, r * 1.2f, 0.06f, FlameMesh.Alpha(colors.Bright, 0.7f), FlameMesh.Alpha(colors.Bright, 0.7f));
                flames.Bar(spec.Center - Vector2.up * r * 0.6f, Vector2.up, r * 1.2f, 0.06f, FlameMesh.Alpha(colors.Bright, 0.7f), FlameMesh.Alpha(colors.Bright, 0.7f));
                // The meteor (or hailstone) itself plunges in over the last moments of the warning; venom just wells up.
                float fall = Mathf.InverseLerp(Mathf.Max(0f, spec.Telegraph - 0.45f), spec.Telegraph, age);
                if (fall > 0f && spec.Style != HazardStyle.Venom)
                {
                    Vector2 head = Vector2.Lerp(meteorFrom, spec.Center, fall * fall);
                    Vector2 back = (meteorFrom - spec.Center).normalized;
                    flames.Bar(head, back, 3.2f, 0.9f, FlameMesh.Alpha(colors.Main, 0.9f), FlameMesh.Alpha(colors.Deep, 0f));
                    flames.Bar(head, back, 1.8f, 0.4f, FlameMesh.Alpha(colors.Core, 1f), FlameMesh.Alpha(colors.Bright, 0f));
                    flames.Disc(head, 0.55f, colors.Core, FlameMesh.Alpha(colors.Main, 0.6f), 20);
                }
                return;
            }
            flames.Disc(spec.Center, r, FlameMesh.Alpha(colors.Bright, 0.7f * fade), FlameMesh.Alpha(colors.Deep, 0.55f * fade));
            flames.Ring(spec.Center, r, 0.16f, FlameMesh.Alpha(colors.Dark, 0.9f * fade), FlameMesh.Alpha(colors.Main, 0.5f * fade));
            int count = Mathf.CeilToInt(r * 9f);
            for (int i = 0; i < count; i++)
            {
                float seed = FlameMesh.Hash(i, spec.Center.x + spec.Center.y);
                Vector2 spot = spec.Center + FlameMesh.Polar(seed * 40f, r * Mathf.Sqrt(FlameMesh.Hash(i, 5.5f)) * 0.9f);
                Tongue(spot, Vector2.up, 0.55f, 0.9f + seed * 0.7f, seed, fade);
            }
            flames.Ring(spec.Center, r * (0.4f + 0.1f * Mathf.Sin(time * 9f)), 0.12f, FlameMesh.Alpha(colors.Core, 0.5f * fade));
        }

        private void DrawBeam(bool warning, float warn, float fade)
        {
            Vector2 dir = spec.Direction, side = Vector2.Perpendicular(dir);
            float length = spec.Radius, width = spec.Width, time = Time.time;
            if (warning)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 22f);
                flames.Bar(spec.Center, dir, length, width * warn, FlameMesh.Alpha(colors.Deep, 0.4f), FlameMesh.Alpha(colors.Deep, 0.15f));
                flames.Bar(spec.Center + side * width * 0.5f, dir, length, 0.05f, FlameMesh.Alpha(colors.Main, pulse), FlameMesh.Alpha(colors.Main, 0.2f));
                flames.Bar(spec.Center - side * width * 0.5f, dir, length, 0.05f, FlameMesh.Alpha(colors.Main, pulse), FlameMesh.Alpha(colors.Main, 0.2f));
                return;
            }
            flames.Bar(spec.Center, dir, length, width * 1.25f, FlameMesh.Alpha(colors.Deep, 0.6f * fade), FlameMesh.Alpha(colors.Deep, 0.3f * fade));
            flames.Bar(spec.Center, dir, length, width * 0.8f, FlameMesh.Alpha(colors.Main, 0.85f * fade), FlameMesh.Alpha(colors.Main, 0.5f * fade));
            flames.Bar(spec.Center, dir, length, width * 0.3f * (0.8f + 0.2f * Mathf.Sin(time * 30f)), FlameMesh.Alpha(colors.Core, fade), FlameMesh.Alpha(colors.Bright, 0.6f * fade));
            int count = Mathf.CeilToInt(length * 2.2f);
            for (int i = 0; i < count; i++)
            {
                float seed = FlameMesh.Hash(i, length);
                Vector2 root = spec.Center + dir * (i + 0.5f) / 2.2f + side * (seed - 0.5f) * width * 0.7f;
                // Blade light glints along the cut; fire and ice rise off it.
                Tongue(root, spec.Style == HazardStyle.Steel ? side * (seed < 0.5f ? -1f : 1f) : Vector2.up, 0.6f, 1.2f + seed, seed, fade);
            }
        }

        private void DrawRing(bool warning, float warn, float fade)
        {
            float time = Time.time;
            if (warning)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 20f);
                flames.Disc(spec.Center, 0.6f + warn * 0.8f, FlameMesh.Alpha(colors.Bright, 0.5f * pulse), FlameMesh.Alpha(colors.Deep, 0f));
                flames.Ring(spec.Center, 1.2f + warn * 2f, 0.06f, FlameMesh.Alpha(colors.Main, 0.3f + 0.4f * pulse));
                return;
            }
            float r = RingRadius, width = spec.Width;
            flames.Ring(spec.Center, r, width * 1.4f, FlameMesh.Alpha(colors.Deep, 0.2f * fade), FlameMesh.Alpha(colors.Deep, 0.55f * fade), 72);
            flames.Ring(spec.Center, r, width * 0.7f, FlameMesh.Alpha(colors.Main, 0.9f * fade), 72);
            flames.Ring(spec.Center, r, width * 0.22f, FlameMesh.Alpha(colors.Core, fade), 72);
            int tongues = Mathf.Clamp(Mathf.CeilToInt(r * 7f), 8, 110);
            for (int i = 0; i < tongues; i++)
            {
                float angle = i * Mathf.PI * 2f / tongues;
                Vector2 outward = FlameMesh.Polar(angle, 1f);
                Tongue(spec.Center + outward * (r - width * 0.3f), (outward + Vector2.up).normalized, 0.55f, 1f, FlameMesh.Hash(i, 7.7f), fade);
            }
        }

        private void OnDestroy() { flames?.Release(); }
    }
}

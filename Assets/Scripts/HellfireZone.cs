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

    /// <summary>Everything needed to rebuild a hazard on another machine.</summary>
    public struct HazardSpec
    {
        public HazardShape Shape;
        public Vector2 Center, Direction;
        /// <summary>Safe/crater radius, beam length, or a ring's final radius.</summary>
        public float Radius;
        /// <summary>Beam or ring thickness.</summary>
        public float Width;
        public float Telegraph, Duration;
    }

    /// <summary>
    /// A telegraphed area of hellfire. After its warning it burns the local hero while they stand inside;
    /// every co-op machine runs its own copy (announced by the host) and judges only its own hero.
    /// </summary>
    public sealed class HellfireZone : MonoBehaviour
    {
        private DungeonRun run;
        private HazardSpec spec;
        private FlameMesh flames;
        private float age;
        private bool erupted;
        private Vector2 meteorFrom;

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
                if (hero.Health != before || hero.IsInvulnerable)
                    HeroVfx.Sparks(run.ProjectileRoot, hero.transform.position, FlameMesh.Orange, 12, 4f, 0.4f, Vector2.up, 120f);
            }
            Draw();
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
                    HeroVfx.Pulse(root, spec.Center, spec.Radius * 1.6f, FlameMesh.Yellow, 0.45f);
                    HeroVfx.Sparks(root, spec.Center, FlameMesh.Orange, 22, 7f, 0.55f, null, 360f, 1.6f);
                    CombatVfx.Ring(root, spec.Center, spec.Radius * 1.2f, FlameMesh.Core, 0.3f);
                    break;
                case HazardShape.Beam:
                    ScreenFx.Shake(0.2f, 0.3f);
                    HeroVfx.Sparks(root, spec.Center + spec.Direction * spec.Radius * 0.5f, FlameMesh.Orange, 16, 5f, 0.45f,
                        Vector2.Perpendicular(spec.Direction), 60f, 1.3f);
                    break;
                case HazardShape.Ring:
                    ScreenFx.Shake(0.22f, 0.4f);
                    HeroVfx.Pulse(root, spec.Center, 1.6f, FlameMesh.Yellow, 0.3f);
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
            switch (spec.Shape)
            {
                case HazardShape.Inferno: DrawInferno(warning, warn, fadeOut); break;
                case HazardShape.Pool: DrawPool(warning, warn, fadeOut); break;
                case HazardShape.Beam: DrawBeam(warning, warn, fadeOut); break;
                case HazardShape.Ring: DrawRing(warning, warn, fadeOut); break;
            }
            flames.Commit();
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
                        flames.Rect(cell - Vector2.one * 0.5f, cell + Vector2.one * 0.5f, FlameMesh.Alpha(FlameMesh.Crimson, glow));
                        // Diagonal hazard stripes crawl across the doomed floor.
                        if (Mathf.Repeat(x + y - time * 2f, 3f) < 1f)
                            flames.Bar(cell + new Vector2(-0.5f, -0.5f), new Vector2(1f, 1f).normalized, 1.41f, 0.22f,
                                FlameMesh.Alpha(FlameMesh.Orange, 0.18f + 0.22f * warn), FlameMesh.Alpha(FlameMesh.Orange, 0.18f + 0.22f * warn));
                        if (seed > 0.45f)
                            flames.Bar(cell + new Vector2(seed - 0.5f, -0.4f), FlameMesh.Polar(seed * 6.3f, 1f), 0.7f, 0.07f,
                                FlameMesh.Alpha(FlameMesh.Yellow, 0.35f + warn * 0.65f), FlameMesh.Alpha(FlameMesh.Orange, 0f));
                        // Small flames start licking up over the last stretch of the warning.
                        float kindle = Mathf.InverseLerp(0.55f, 1f, warn);
                        if (kindle > 0f && seed > 0.35f)
                            flames.Flame(cell + new Vector2(seed - 0.5f, -0.45f) * 0.8f, Vector2.up, 0.45f, (0.3f + 0.5f * seed) * kindle, seed, kindle * 0.8f);
                        continue;
                    }
                    float heat = 0.55f + 0.2f * Mathf.Sin(time * 7f + seed * 20f);
                    flames.Rect(cell - Vector2.one * 0.5f, cell + Vector2.one * 0.5f,
                        FlameMesh.Alpha(Color.Lerp(FlameMesh.Crimson, FlameMesh.Orange, seed * 0.5f), heat * fade));
                    flames.Flame(cell + new Vector2(seed - 0.5f, -0.45f) * 0.8f, Vector2.up, 0.8f, 1.1f + seed * 0.7f, seed, fade);
                }
            // Rising embers above the sea of fire.
            if (!warning)
                for (int i = 0; i < 140; i++)
                {
                    float sx = FlameMesh.Hash(i, 1.7f), sy = FlameMesh.Hash(i, 9.3f);
                    Vector2 p = new Vector2(arena.xMin - 0.5f + sx * arena.width, arena.yMin - 0.5f + Mathf.Repeat(sy * arena.height + Time.time * (1.5f + sx * 2f), arena.height));
                    if (Vector2.Distance(p, spec.Center) < safe) continue;
                    flames.Diamond(p + Vector2.right * Mathf.Sin(Time.time * 3f + i) * 0.2f, 0.08f + sy * 0.06f, FlameMesh.Alpha(FlameMesh.Yellow, 0.9f * fade));
                }
            flames.Ring(spec.Center, safe + 0.4f, 0.8f, warning ? FlameMesh.Alpha(FlameMesh.Crimson, 0.12f + 0.3f * warn * pulse)
                : FlameMesh.Alpha(FlameMesh.Orange, 0.7f * fade), FlameMesh.Alpha(FlameMesh.Crimson, (warning ? 0.2f + 0.3f * warn * pulse : 0.6f) * fade), 64);
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
                    flames.Flame(spec.Center + outward * (safe + 0.05f), outward, 0.5f, 1.3f, FlameMesh.Hash(i, 3.1f), fade);
                }
                flames.Ring(spec.Center, safe + 0.1f, 0.18f, FlameMesh.Alpha(FlameMesh.Core, fade), FlameMesh.Alpha(FlameMesh.Yellow, 0.4f * fade));
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

        private void DrawPool(bool warning, float warn, float fade)
        {
            float r = spec.Radius, time = Time.time;
            if (warning)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 18f);
                flames.Disc(spec.Center, r * warn, FlameMesh.Alpha(FlameMesh.Crimson, 0.35f), FlameMesh.Alpha(FlameMesh.Orange, 0.2f + 0.2f * pulse));
                flames.Ring(spec.Center, r, 0.08f, FlameMesh.Alpha(FlameMesh.Orange, 0.6f + 0.4f * pulse));
                flames.Bar(spec.Center - Vector2.right * r * 0.6f, Vector2.right, r * 1.2f, 0.06f, FlameMesh.Alpha(FlameMesh.Yellow, 0.7f), FlameMesh.Alpha(FlameMesh.Yellow, 0.7f));
                flames.Bar(spec.Center - Vector2.up * r * 0.6f, Vector2.up, r * 1.2f, 0.06f, FlameMesh.Alpha(FlameMesh.Yellow, 0.7f), FlameMesh.Alpha(FlameMesh.Yellow, 0.7f));
                // The meteor itself plunges in over the last moments of the warning.
                float fall = Mathf.InverseLerp(Mathf.Max(0f, spec.Telegraph - 0.45f), spec.Telegraph, age);
                if (fall > 0f)
                {
                    Vector2 head = Vector2.Lerp(meteorFrom, spec.Center, fall * fall);
                    Vector2 back = (meteorFrom - spec.Center).normalized;
                    flames.Bar(head, back, 3.2f, 0.9f, FlameMesh.Alpha(FlameMesh.Orange, 0.9f), FlameMesh.Alpha(FlameMesh.Crimson, 0f));
                    flames.Bar(head, back, 1.8f, 0.4f, FlameMesh.Alpha(FlameMesh.Core, 1f), FlameMesh.Alpha(FlameMesh.Yellow, 0f));
                    flames.Disc(head, 0.55f, FlameMesh.Core, FlameMesh.Alpha(FlameMesh.Orange, 0.6f), 20);
                }
                return;
            }
            flames.Disc(spec.Center, r, FlameMesh.Alpha(FlameMesh.Yellow, 0.7f * fade), FlameMesh.Alpha(FlameMesh.Crimson, 0.55f * fade));
            flames.Ring(spec.Center, r, 0.16f, FlameMesh.Alpha(FlameMesh.Ember, 0.9f * fade), FlameMesh.Alpha(FlameMesh.Orange, 0.5f * fade));
            int count = Mathf.CeilToInt(r * 9f);
            for (int i = 0; i < count; i++)
            {
                float seed = FlameMesh.Hash(i, spec.Center.x + spec.Center.y);
                Vector2 spot = spec.Center + FlameMesh.Polar(seed * 40f, r * Mathf.Sqrt(FlameMesh.Hash(i, 5.5f)) * 0.9f);
                flames.Flame(spot, Vector2.up, 0.55f, 0.9f + seed * 0.7f, seed, fade);
            }
            flames.Ring(spec.Center, r * (0.4f + 0.1f * Mathf.Sin(time * 9f)), 0.12f, FlameMesh.Alpha(FlameMesh.Core, 0.5f * fade));
        }

        private void DrawBeam(bool warning, float warn, float fade)
        {
            Vector2 dir = spec.Direction, side = Vector2.Perpendicular(dir);
            float length = spec.Radius, width = spec.Width, time = Time.time;
            if (warning)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 22f);
                flames.Bar(spec.Center, dir, length, width * warn, FlameMesh.Alpha(FlameMesh.Crimson, 0.4f), FlameMesh.Alpha(FlameMesh.Crimson, 0.15f));
                flames.Bar(spec.Center + side * width * 0.5f, dir, length, 0.05f, FlameMesh.Alpha(FlameMesh.Orange, pulse), FlameMesh.Alpha(FlameMesh.Orange, 0.2f));
                flames.Bar(spec.Center - side * width * 0.5f, dir, length, 0.05f, FlameMesh.Alpha(FlameMesh.Orange, pulse), FlameMesh.Alpha(FlameMesh.Orange, 0.2f));
                return;
            }
            flames.Bar(spec.Center, dir, length, width * 1.25f, FlameMesh.Alpha(FlameMesh.Crimson, 0.6f * fade), FlameMesh.Alpha(FlameMesh.Crimson, 0.3f * fade));
            flames.Bar(spec.Center, dir, length, width * 0.8f, FlameMesh.Alpha(FlameMesh.Orange, 0.85f * fade), FlameMesh.Alpha(FlameMesh.Orange, 0.5f * fade));
            flames.Bar(spec.Center, dir, length, width * 0.3f * (0.8f + 0.2f * Mathf.Sin(time * 30f)), FlameMesh.Alpha(FlameMesh.Core, fade), FlameMesh.Alpha(FlameMesh.Yellow, 0.6f * fade));
            int count = Mathf.CeilToInt(length * 2.2f);
            for (int i = 0; i < count; i++)
            {
                float seed = FlameMesh.Hash(i, length);
                Vector2 root = spec.Center + dir * (i + 0.5f) / 2.2f + side * (seed - 0.5f) * width * 0.7f;
                flames.Flame(root, Vector2.up, 0.6f, 1.2f + seed, seed, fade);
            }
        }

        private void DrawRing(bool warning, float warn, float fade)
        {
            float time = Time.time;
            if (warning)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 20f);
                flames.Disc(spec.Center, 0.6f + warn * 0.8f, FlameMesh.Alpha(FlameMesh.Yellow, 0.5f * pulse), FlameMesh.Alpha(FlameMesh.Crimson, 0f));
                flames.Ring(spec.Center, 1.2f + warn * 2f, 0.06f, FlameMesh.Alpha(FlameMesh.Orange, 0.3f + 0.4f * pulse));
                return;
            }
            float r = RingRadius, width = spec.Width;
            flames.Ring(spec.Center, r, width * 1.4f, FlameMesh.Alpha(FlameMesh.Crimson, 0.2f * fade), FlameMesh.Alpha(FlameMesh.Crimson, 0.55f * fade), 72);
            flames.Ring(spec.Center, r, width * 0.7f, FlameMesh.Alpha(FlameMesh.Orange, 0.9f * fade), 72);
            flames.Ring(spec.Center, r, width * 0.22f, FlameMesh.Alpha(FlameMesh.Core, fade), 72);
            int tongues = Mathf.Clamp(Mathf.CeilToInt(r * 7f), 8, 110);
            for (int i = 0; i < tongues; i++)
            {
                float angle = i * Mathf.PI * 2f / tongues;
                Vector2 outward = FlameMesh.Polar(angle, 1f);
                flames.Flame(spec.Center + outward * (r - width * 0.3f), (outward + Vector2.up).normalized, 0.55f, 1f, FlameMesh.Hash(i, 7.7f), fade);
            }
        }

        private void OnDestroy() { flames?.Release(); }
    }
}

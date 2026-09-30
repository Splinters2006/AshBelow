using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Orbital Laser: one unbroken beam of plasma from the sky that follows the Augment's cursor for a few seconds,
    /// burning what it touches. The beam is a straight column (soft outer glow, bright body, white-hot core) that
    /// shimmers as it runs, with a blazing, spinning burn spot where it meets the floor and a scorched trail behind it.
    /// Teammates see the same beam, steered by the caster's updates; only the caster's machine deals the damage.
    /// </summary>
    public sealed class OrbitalLaser : MonoBehaviour
    {
        public const float Duration = 3f, Radius = 0.9f, Speed = 7f, Tick = 0.2f;
        private const float SkyHeight = 12f, FadeIn = 0.15f, FadeOut = 0.25f, ScorchLife = 0.8f;
        /// <summary>Teammates' beams, by the caster they belong to.</summary>
        private static readonly Dictionary<ulong, OrbitalLaser> ghosts = new Dictionary<ulong, OrbitalLaser>();
        private DungeonRun run;
        private DungeonPlayer player;
        private float until, nextTick, age, endedAt = -1f;
        private int damage;
        private bool ghost;
        private ulong ghostOwner;
        private Vector2 ghostGoal;
        // FlameMesh draws in its owner's local space, so the owner stays at the origin and the beam's spot lives here.
        private Vector2 position;
        private FlameMesh beam, ground;
        private readonly List<Vector3> scorches = new List<Vector3>();

        public static void Call(DungeonPlayer player, Vector2 start, float duration, int damage)
        {
            var laser = Create(player.Run, start, duration);
            laser.player = player;
            laser.damage = damage;
            ScreenFx.Flash(FlameMesh.Alpha(CyborgAttack.Plasma, 0.15f), 0.2f);
            CoopFx.OrbitalLaser(player.Run, start, Tick * 3f);
        }

        /// <summary>A teammate's beam: started, or steered toward <paramref name="at"/> and kept alive a little longer.</summary>
        public static void Ghost(DungeonRun run, ulong owner, Vector2 at, float keepAlive)
        {
            if (ghosts.TryGetValue(owner, out var laser) && laser != null && laser.endedAt < 0f)
            {
                laser.ghostGoal = at;
                laser.until = Mathf.Max(laser.until, Time.time + keepAlive);
                return;
            }
            laser = Create(run, at, keepAlive);
            laser.ghost = true;
            laser.ghostOwner = owner;
            laser.ghostGoal = at;
            ghosts[owner] = laser;
        }

        private static OrbitalLaser Create(DungeonRun run, Vector2 start, float duration)
        {
            var laser = new GameObject("Orbital laser").AddComponent<OrbitalLaser>();
            laser.transform.SetParent(run.ProjectileRoot, false);
            laser.position = start;
            laser.run = run;
            laser.until = Time.time + duration;
            laser.ground = new FlameMesh(laser.gameObject, 3);
            // The column itself draws over everything on the floor.
            var column = new GameObject("Beam");
            column.transform.SetParent(laser.transform, false);
            laser.beam = new FlameMesh(column, 12);
            return laser;
        }

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null || (!ghost && player == null)) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            age += Time.deltaTime;
            if (endedAt < 0f && Time.time >= until) endedAt = Time.time;
            if (endedAt >= 0f && Time.time - endedAt >= Mathf.Max(FadeOut, ScorchLife)) { Destroy(gameObject); return; }
            if (endedAt < 0f)
            {
                Vector2 goal = ghost ? ghostGoal : player.CursorPoint;
                position = Vector2.MoveTowards(position, goal, Speed * Time.deltaTime);
                Burn();
            }
            Draw();
        }

        private void Burn()
        {
            Vector2 at = position;
            // A scorch mark every little way along its path.
            if (scorches.Count == 0 || Vector2.Distance(at, scorches[scorches.Count - 1]) > 0.25f) scorches.Add(new Vector3(at.x, at.y, Time.time));
            if (Time.frameCount % 2 == 0)
                HeroVfx.Sparks(run.ProjectileRoot, at, Color.Lerp(CyborgAttack.Plasma, Color.white, 0.4f), 2, 3.5f, 0.2f, null, 360f, 0.7f);
            if (ghost || Time.time < nextTick) return;
            nextTick = Time.time + Tick;
            CoopFx.OrbitalLaser(run, at, Tick * 3f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= Radius + enemy.HitRadius)
                {
                    CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, at + Vector2.up, 0f);
                    HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, CyborgAttack.Core, 4, 3f, 0.2f, Vector2.up, 120f, 0.8f);
                }
        }

        private void Draw()
        {
            Vector2 at = position;
            float strength = endedAt >= 0f ? Mathf.Clamp01(1f - (Time.time - endedAt) / FadeOut) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / FadeIn));
            float time = Time.time;
            float shimmer = 1f + 0.08f * Mathf.Sin(time * 47f) + 0.05f * Mathf.Sin(time * 83f);
            Color plasma = CyborgAttack.Plasma, core = CyborgAttack.Core;

            ground.Begin();
            // The scorched trail it leaves behind, cooling from glowing to dark.
            for (int i = scorches.Count - 1; i >= 0; i--)
            {
                float since = time - scorches[i].z;
                if (since > ScorchLife) { scorches.RemoveRange(0, i + 1); break; }
                float cool = since / ScorchLife;
                Vector2 spot = scorches[i];
                ground.Ellipse(spot, Radius * 0.55f, Radius * 0.35f, Color.Lerp(FlameMesh.Alpha(plasma, 0.35f), new Color(0.05f, 0.05f, 0.06f, 0.35f), cool) * new Color(1f, 1f, 1f, 1f - cool),
                    new Color(0f, 0f, 0f, 0f), 14);
            }
            if (strength > 0f)
            {
                // The burn spot: a blazing pool, a spinning ring of plasma and the hit area's rim.
                ground.Disc(at, Radius * 1.25f, FlameMesh.Alpha(plasma, 0.35f * strength), FlameMesh.Alpha(plasma, 0f), 32);
                ground.Disc(at, Radius * 0.5f * shimmer, FlameMesh.Alpha(Color.white, 0.9f * strength), FlameMesh.Alpha(core, 0.3f * strength), 24);
                ground.Ring(at, Radius, 0.05f, FlameMesh.Alpha(core, 0.8f * strength), 40);
                for (int i = 0; i < 3; i++)
                {
                    float a0 = time * 9f + i * Mathf.PI * 2f / 3f;
                    for (int s = 0; s < 6; s++)
                    {
                        float b0 = a0 + s * 0.12f, b1 = b0 + 0.12f;
                        float r = Radius * 0.75f;
                        ground.Quad(at + FlameMesh.Polar(b0, r - 0.05f), at + FlameMesh.Polar(b0, r + 0.05f), at + FlameMesh.Polar(b1, r + 0.05f), at + FlameMesh.Polar(b1, r - 0.05f),
                            FlameMesh.Alpha(core, strength * (1f - s / 6f)), FlameMesh.Alpha(core, strength * (1f - s / 6f)),
                            FlameMesh.Alpha(core, strength * (1f - (s + 1) / 6f)), FlameMesh.Alpha(core, strength * (1f - (s + 1) / 6f)));
                    }
                }
            }
            ground.Commit();

            beam.Begin();
            if (strength > 0f)
            {
                Vector2 sky = at + Vector2.up * SkyHeight;
                // Three layers of one straight column: a wide soft glow, the plasma body and a white-hot core.
                Column(at, sky, Radius * 1.1f * shimmer * strength, FlameMesh.Alpha(plasma, 0.22f * strength), FlameMesh.Alpha(plasma, 0f));
                Column(at, sky, Radius * 0.55f * shimmer * strength, FlameMesh.Alpha(plasma, 0.75f * strength), FlameMesh.Alpha(plasma, 0.1f * strength));
                Column(at, sky, Radius * 0.22f * shimmer * strength, FlameMesh.Alpha(Color.white, strength), FlameMesh.Alpha(core, 0.5f * strength));
                // Energy rippling down the beam.
                for (int i = 0; i < 4; i++)
                {
                    float y = Mathf.Repeat(-time * 14f + i * SkyHeight / 4f, SkyHeight);
                    Vector2 c = at + Vector2.up * y;
                    float w = Radius * 0.6f * strength;
                    beam.Quad(c + new Vector2(-w, 0f), c + new Vector2(0f, 0.09f), c + new Vector2(w, 0f), c + new Vector2(0f, -0.09f),
                        FlameMesh.Alpha(core, 0f), FlameMesh.Alpha(Color.white, 0.5f * strength), FlameMesh.Alpha(core, 0f), FlameMesh.Alpha(Color.white, 0.5f * strength));
                }
            }
            beam.Commit();
        }

        /// <summary>A straight vertical band from the floor up into the sky, bright at its centre line and soft at its edges.</summary>
        private void Column(Vector2 bottom, Vector2 top, float halfWidth, Color centre, Color edge)
        {
            Vector2 side = Vector2.right * halfWidth;
            beam.Quad(bottom - side, top - side, top, bottom, edge, edge, centre, centre);
            beam.Quad(bottom, top, top + side, bottom + side, centre, centre, edge, edge);
            // A rounded foot where it meets the floor.
            beam.Ellipse(bottom, halfWidth, halfWidth * 0.45f, centre, edge, 16);
        }

        private void OnDestroy()
        {
            if (ghost && ghosts.TryGetValue(ghostOwner, out var laser) && laser == this) ghosts.Remove(ghostOwner);
            beam?.Release();
            ground?.Release();
        }
    }
}

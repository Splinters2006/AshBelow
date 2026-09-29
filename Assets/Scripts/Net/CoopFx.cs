using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Mirrors a local hero's attack effects to teammates. Call sites keep drawing their own effect as before and
    /// add one of these calls; teammates replay a cosmetic copy that never deals damage.
    /// </summary>
    public static class CoopFx
    {
        private static void Send(DungeonRun run, FxKind kind, Vector2 a, Vector2 b = default, Color? color = null, float f1 = 0f, float f2 = 0f, int n = 0)
        {
            if (run == null || !run.IsNetworked) return;
            run.Coop.SendFx(new FxMessage { Kind = kind, A = a, B = b, Color = color ?? Color.white, F1 = f1, F2 = f2, N = n });
        }

        public static void Arrow(DungeonRun run, Vector2 position, Vector2 direction, float range) => Send(run, FxKind.Arrow, position, direction, null, range);
        public static void Spell(DungeonRun run, Vector2 position, Vector2 direction, Color color, float range, float radius, int pierces)
            => Send(run, FxKind.Spell, position, direction, color, range, radius, pierces);
        public static void Slash(DungeonRun run, Vector2 center, Vector2 aim, float reach, float cone, Color color)
            => Send(run, FxKind.Slash, center, aim, color, reach, cone);
        public static void Bolt(DungeonRun run, Vector2 from, Vector2 to, Color color, bool glow = false)
            => Send(run, glow ? FxKind.GlowBolt : FxKind.Bolt, from, to, color);
        public static void Ring(DungeonRun run, Vector2 center, float radius, Color color, float duration = 0.4f)
            => Send(run, FxKind.Ring, center, default, color, radius, duration);
        public static void Pulse(DungeonRun run, Vector2 center, float radius, Color color, float duration = 0.35f)
            => Send(run, FxKind.Pulse, center, default, color, radius, duration);
        public static void Rift(DungeonRun run, Vector2 from, Vector2 to, float strength) => Send(run, FxKind.Rift, from, to, null, strength);
        public static void Execution(DungeonRun run, Vector2 center, float radius) => Send(run, FxKind.Execution, center, default, null, radius);
        public static void Singularity(DungeonRun run, Vector2 center, float radius, float duration = 1.1f)
            => Send(run, FxKind.Singularity, center, default, null, radius, duration);

        public static void Punch(DungeonRun run, Vector2 origin, Vector2 aim, float length, float halfWidth, Color color)
            => Send(run, FxKind.Punch, origin, aim, color, length, halfWidth);

        public static void Knife(DungeonRun run, Vector2 position, Vector2 direction, float range) => Send(run, FxKind.Knife, position, direction, null, range);

        public static void RearHit(DungeonRun run, Vector2 position, Vector2 facing, float hitRadius)
            => Send(run, FxKind.RearHit, position, facing, null, hitRadius);

        public static void Play(DungeonRun run, FxMessage fx)
        {
            var root = run.ProjectileRoot;
            Color color = fx.Color;
            switch (fx.Kind)
            {
                case FxKind.Arrow: PlayerProjectile.SpawnGhost(run, fx.A, fx.B, fx.F1); break;
                case FxKind.Spell: SpellProjectile.SpawnGhost(run, fx.A, fx.B, color, fx.F1, fx.F2, fx.N); break;
                case FxKind.Slash: HeroVfx.Slash(root, fx.A, fx.B, fx.F1, fx.F2, color); break;
                case FxKind.Bolt: CombatVfx.Bolt(root, fx.A, fx.B, color); break;
                case FxKind.GlowBolt: CombatVfx.GlowBolt(root, fx.A, fx.B, color); break;
                case FxKind.Ring: CombatVfx.Ring(root, fx.A, fx.F1, color, fx.F2); break;
                case FxKind.Pulse: HeroVfx.Pulse(root, fx.A, fx.F1, color, fx.F2); break;
                case FxKind.Rift: ShadowVfx.Rift(root, fx.A, fx.B, fx.F1); break;
                case FxKind.Execution: ShadowVfx.Execution(root, fx.A, fx.F1); break;
                case FxKind.Singularity: ShadowVfx.Singularity(root, fx.A, fx.F1, fx.F2); break;
                case FxKind.Punch: BrawlerVfx.Punch(root, fx.A, fx.B, fx.F1, fx.F2, color); break;
                case FxKind.RearHit: RearHitMarker.Draw(root, fx.A, fx.B, fx.F1); break;
                case FxKind.Knife:
                    // The ghost blade flies home to the teammate who threw it.
                    RemoteHero thrower = null;
                    foreach (var hero in run.Coop.RemoteHeroes) if (hero != null && hero.Id == fx.Origin) thrower = hero;
                    if (thrower != null) ReturningKnife.SpawnGhost(run, thrower.transform, fx.A, fx.B, fx.F1);
                    break;
            }
        }
    }
}

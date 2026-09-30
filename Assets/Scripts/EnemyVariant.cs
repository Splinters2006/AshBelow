using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A special kind of floor enemy layered on <see cref="DungeonEnemy"/>: its own look, stats set at spawn and
    /// optional death behaviour. Variants are chosen from the run seed, so every co-op machine spawns the same ones.
    /// </summary>
    [RequireComponent(typeof(DungeonEnemy))]
    public abstract class EnemyVariant : MonoBehaviour
    {
        public abstract string DisplayName { get; }
        public abstract Sprite Sprite { get; }
        public abstract Color Tint { get; }
        /// <summary>The world this enemy was spawned in, which decides how it looks.</summary>
        protected WorldDefinition World
        {
            get
            {
                if (owner == null) owner = GetComponent<DungeonEnemy>();
                return owner != null && owner.Run != null ? owner.Run.World : WorldCatalog.All[0];
            }
        }
        private DungeonEnemy owner;
        /// <summary>Crystals dropped on death.</summary>
        public virtual int CrystalValue => 1;

        /// <summary>Adjusts a freshly spawned ashling's stats into this variant.</summary>
        public abstract void Configure(DungeonEnemy enemy);

        /// <summary>Runs on every machine as the enemy dies.</summary>
        public virtual void OnDeath(DungeonEnemy enemy) { }

        /// <summary>Host-side movement override, evaluated only while alive, playing and not held.</summary>
        public virtual bool Move(DungeonEnemy enemy, Vector2 target, bool visible) => false;

        /// <summary>
        /// True while the enemy telegraphs a special attack: it flashes white like a charging caster. The host decides;
        /// guests follow the snapshot's charging flag.
        /// </summary>
        public bool IsWindingUp => Winding || netWindingUp;
        /// <summary>Host-side windup state of the variant's own attack.</summary>
        protected virtual bool Winding => false;
        private bool netWindingUp;
        public void SetNetWindup(bool winding) => netWindingUp = winding;

        // ---------------------------------------------------------------- helpers for world specialists

        /// <summary>Keeps between <paramref name="near"/> and <paramref name="far"/> of the target, facing it.</summary>
        protected static void Kite(DungeonEnemy enemy, Vector2 target, bool visible, float near, float far)
        {
            Vector2 position = enemy.transform.position;
            float distance = Vector2.Distance(position, target);
            enemy.Facing.TurnToward(target - position, Time.deltaTime * enemy.ActionSpeedMultiplier);
            Vector2 direction = !visible ? enemy.Run.DirectionToPlayer(position)
                : distance > far ? (target - position).normalized : distance < near ? (position - target).normalized : Vector2.zero;
            enemy.transform.position = enemy.Run.Map.Move(position, direction * enemy.Speed * enemy.MoveMultiplier * Time.deltaTime, enemy.MoveRadius);
        }

        /// <summary>How far a straight line runs from <paramref name="from"/> before it meets a wall.</summary>
        protected static float Reach(DungeonMap map, Vector2 from, Vector2 direction, float max)
        {
            float travel = 0f;
            while (travel + 0.2f <= max && map.CanStand(from + direction * (travel + 0.2f), 0.1f)) travel += 0.2f;
            return travel;
        }

        /// <summary>Vanishes and reappears on open ground <paramref name="radius"/> from <paramref name="around"/>, in sight of it.</summary>
        protected static bool Blink(DungeonEnemy enemy, Vector2 around, float radius, Color color)
        {
            var run = enemy.Run;
            float start = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 12; i++)
            {
                Vector2 spot = around + FlameMesh.Polar(start + i * Mathf.PI / 6f, radius);
                if (!run.Map.CanStand(spot, enemy.MoveRadius) || !run.HasLineOfSight(around, spot)) continue;
                Vector2 from = enemy.transform.position;
                HeroVfx.Pulse(run.ProjectileRoot, from, 0.7f, color, 0.3f);
                CoopFx.Pulse(run, from, 0.7f, color, 0.3f);
                HeroVfx.Sparks(run.ProjectileRoot, spot, color, 10, 3.5f, 0.3f);
                CoopFx.Pulse(run, spot, 0.9f, color, 0.3f);
                enemy.transform.position = spot;
                return true;
            }
            return false;
        }
    }
}

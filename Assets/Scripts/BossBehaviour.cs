using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// One arena guardian's fighting style. <see cref="HostTick"/> runs the AI on the host (or offline);
    /// <see cref="VisualTick"/> runs on every machine, and guests follow the host through <see cref="ApplyNetState"/>.
    /// </summary>
    public abstract class BossBehaviour : MonoBehaviour
    {
        public DungeonBoss Boss { get; private set; }
        protected DungeonEnemy Enemy => Boss.Enemy;
        protected DungeonRun Run => Boss.Enemy.Run;
        protected bool IsEnraged => Boss.IsEnraged;
        /// <summary>True while fighting in the Neon Arcology.</summary>
        protected bool HighTech => WorldCatalog.ForFloor(Run.Floor).HighTech;

        public abstract string Title { get; }
        public abstract string Tell { get; }
        public abstract int BaseHealth(int floor);
        /// <summary>0-15; mirrored to guests through the enemy snapshot.</summary>
        public abstract byte NetState { get; }
        public virtual bool IsCharging => false;
        public virtual bool IsInvulnerable => false;
        public virtual float HitRadius => 0.85f;
        public virtual bool DealsContactDamage => true;
        public virtual float ContactReach => 1.05f;
        /// <summary>Where the boss stands on the arena floor (differs from its transform while airborne).</summary>
        public virtual Vector2 GroundPosition => transform.position;

        public void Setup(DungeonBoss boss)
        {
            Boss = boss;
            OnSetup();
        }

        protected abstract void OnSetup();
        public abstract Color BodyColor();
        public abstract void HostTick(Vector2 toHero);
        public virtual void VisualTick() { }
        public abstract void ApplyNetState(bool charging, byte state);
        public virtual void OnDefeated() { }

        /// <summary>Spawns an aimed enemy bolt (announced to co-op guests by the bolt itself).</summary>
        protected void Fire(Vector2 from, Vector2 direction, float speed = EnemyProjectile.DefaultSpeed, BoltKind? kind = null)
            => EnemyProjectile.Spawn(Run, Run.ProjectileRoot, from + direction.normalized * 0.5f, direction, true, speed, kind ?? Bolts);

        /// <summary>The guardian's theme: what its bolts and hazards look like (the Archdemon's hellfire by default).</summary>
        protected virtual BoltKind Bolts => BoltKind.Ember;
        protected virtual HazardStyle Hazards => HazardStyle.Hellfire;

        /// <summary>Where every living hero stands: the local one plus any co-op teammates.</summary>
        protected IEnumerable<Vector2> LivingHeroPositions()
        {
            if (Run.Player.Health > 0) yield return Run.Player.transform.position;
            if (!Run.IsNetworked) yield break;
            foreach (var hero in Run.Coop.RemoteHeroes)
                if (hero != null && hero.IsAlive) yield return hero.transform.position;
        }

        protected void Hazard(HazardShape shape, Vector2 center, Vector2 direction, float radius, float width, float telegraph, float duration)
            => HellfireZone.Spawn(Run, new HazardSpec
            {
                Shape = shape, Style = Hazards, Center = center, Direction = direction, Radius = radius, Width = width, Telegraph = telegraph, Duration = duration
            });

        protected Color Flashing(Color normal, Color flash, bool active)
            => Enemy.IsFlashing || active ? Color.Lerp(flash, Color.white, 0.5f + Mathf.Sin(Time.time * 18f) * 0.5f)
                : Enemy.IsParalyzed ? DemonessAttack.ParalyzedTint(Time.time)
                : Enemy.IsFrozen ? DungeonEnemy.FrozenTint
                : Enemy.IsChilled ? AbilityCatalog.Ice : normal;
    }
}

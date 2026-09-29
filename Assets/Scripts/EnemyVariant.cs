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
        /// <summary>Crystals dropped on death.</summary>
        public virtual int CrystalValue => 1;

        /// <summary>Adjusts a freshly spawned ashling's stats into this variant.</summary>
        public abstract void Configure(DungeonEnemy enemy);

        /// <summary>Runs on every machine as the enemy dies.</summary>
        public virtual void OnDeath(DungeonEnemy enemy) { }
    }
}

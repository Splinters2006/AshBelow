using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Base for a lasting status look drawn round an enemy (ice, scorchblood...). Its <see cref="FlameMesh"/> layers live
    /// under the floor's effect root in world space, so the enemy's own scale and sorting group never bend them: a layer
    /// under the enemy's sorting order draws behind it, one over draws in front. Layers are rebuilt every frame and come
    /// back by themselves if the floor that held them is torn down.
    /// </summary>
    [RequireComponent(typeof(DungeonEnemy))]
    public abstract class EnemyStatusVisual : MonoBehaviour
    {
        protected DungeonEnemy Enemy { get; private set; }
        protected SpriteRenderer Body { get; private set; }

        private sealed class Layer
        {
            public GameObject Owner;
            public FlameMesh Mesh;
            public bool Drawn;
        }

        private Layer[] layers;

        /// <summary>The sorting order of each layer, from back to front.</summary>
        protected abstract int[] LayerOrders { get; }
        /// <summary>True to draw every layer as pixel art (see <see cref="FlameMesh.Pixelated"/>).</summary>
        protected virtual bool Pixelated => false;

        protected virtual void Awake()
        {
            Enemy = GetComponent<DungeonEnemy>();
            Body = GetComponent<SpriteRenderer>();
            layers = new Layer[LayerOrders.Length];
            for (int i = 0; i < layers.Length; i++) layers[i] = new Layer();
        }

        /// <summary>World size of the enemy's body sprite, never smaller than a basic enemy.</summary>
        protected Vector2 BodySize
        {
            get
            {
                Vector2 size = Body != null && Body.sprite != null ? (Vector2)Body.bounds.size : Vector2.one * Enemy.HitRadius * 2f;
                return Vector2.Max(size, Vector2.one * 0.45f);
            }
        }

        /// <summary>Starts drawing the given layer this frame (call <see cref="Commit"/> after); null when there is no effect root.</summary>
        protected FlameMesh Begin(int index)
        {
            var layer = layers[index];
            if (layer.Owner == null)
            {
                layer.Mesh?.Release();
                layer.Mesh = null;
                var root = Enemy.Run != null ? Enemy.Run.ProjectileRoot : null;
                if (root == null) return null;
                layer.Owner = new GameObject($"{GetType().Name} ({gameObject.name})");
                layer.Owner.transform.SetParent(root, false);
                layer.Mesh = new FlameMesh(layer.Owner, LayerOrders[index]) { Pixelated = Pixelated };
            }
            layer.Mesh.Begin();
            layer.Drawn = true;
            return layer.Mesh;
        }

        protected void Commit(int index) => layers[index].Mesh?.Commit();

        /// <summary>Empties every layer, once, when the status is gone.</summary>
        protected void Clear()
        {
            foreach (var layer in layers)
            {
                if (!layer.Drawn || layer.Mesh == null || layer.Owner == null) continue;
                layer.Mesh.Begin();
                layer.Mesh.Commit();
                layer.Drawn = false;
            }
        }

        protected virtual void OnDestroy()
        {
            if (layers == null) return;
            foreach (var layer in layers)
            {
                layer.Mesh?.Release();
                if (layer.Owner != null) Destroy(layer.Owner);
            }
        }
    }
}

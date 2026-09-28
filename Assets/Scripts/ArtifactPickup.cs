using UnityEngine;

namespace Slopgame
{
    public sealed class ArtifactPickup : MonoBehaviour
    {
        private Vector2 origin;
        private Transform gem;
        public static ArtifactPickup Spawn(Transform parent, Vector2 position)
        {
            var root = new GameObject("Boss artifact");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var pickup = root.AddComponent<ArtifactPickup>();
            pickup.origin = position;
            pickup.gem = DungeonVisuals.Create("Relic crystal", root.transform, position, Vector2.one * 0.45f, AbilityCatalog.Gold, 7).transform;
            DungeonVisuals.Create("Relic core", pickup.gem, position, Vector2.one * 0.5f, Color.white, 8);
            CombatVfx.Ring(parent, position, 1f, AbilityCatalog.Gold, 1.2f);
            return pickup;
        }

        private void Update()
        {
            gem.position = origin + Vector2.up * (0.15f + Mathf.Sin(Time.time * 2.4f) * 0.12f);
            gem.rotation = Quaternion.Euler(0, 0, 45f + Mathf.Sin(Time.time) * 12f);
        }
    }
}

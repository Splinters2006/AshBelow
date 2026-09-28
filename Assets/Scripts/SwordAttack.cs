using UnityEngine;

namespace Slopgame
{
    public sealed class SwordAttack : MonoBehaviour
    {
        public const float Reach = 1.65f;
        public const float ConeAngle = 100f;
        public DungeonPlayer Player { get; set; }
        private float readyAt, visibleUntil;
        private MeshRenderer arc;
        private Mesh mesh;
        private Material material;

        private void Awake()
        {
            var visual = new GameObject("Sword cone", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(transform, false);
            // Player's sprite is scaled; keep the cone's world radius equal to its hit range.
            visual.transform.localScale = new Vector3(1 / transform.lossyScale.x, 1 / transform.lossyScale.y, 1);
            const int segments = 24;
            var vertices = new Vector3[segments + 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 3];
            for (int i = 0; i <= segments; i++)
            {
                float angle = (-ConeAngle / 2 + ConeAngle * i / segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * Reach;
                if (i == segments) continue;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0.4f, 1f, 0.85f, 0.35f);
            mesh = new Mesh { name = "Sword cone", vertices = vertices, triangles = triangles, colors = colors };
            mesh.RecalculateBounds();
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Shader.Find("Sprites/Default"));
            arc = visual.GetComponent<MeshRenderer>();
            arc.sharedMaterial = material;
            arc.sortingOrder = 5;
            arc.enabled = false;
        }

        public static bool ContainsTarget(Vector2 offset, Vector2 aim)
        {
            return offset.sqrMagnitude <= Reach * Reach &&
                (offset.sqrMagnitude < 0.0001f || Vector2.Dot(offset.normalized, aim.normalized) >= Mathf.Cos(ConeAngle * 0.5f * Mathf.Deg2Rad));
        }

        public bool TryAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || Time.time < readyAt || aim.sqrMagnitude < 0.001f) return false;
            readyAt = Time.time + 0.42f;
            visibleUntil = Time.time + 0.12f;
            arc.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            arc.enabled = true;
            for (int i = Player.Run.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = Player.Run.Enemies[i];
                if (ContainsTarget(enemy.transform.position - transform.position, aim)
                    && Player.Run.HasLineOfSight(transform.position, enemy.transform.position)) enemy.Hit(Player.Damage);
            }
            return true;
        }

        public void Hide() { visibleUntil = 0; if (arc != null) arc.enabled = false; }
        private void LateUpdate() { arc.enabled = Player.Run.IsPlaying && !Player.IsRolling && Time.time < visibleUntil; }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
    }
}

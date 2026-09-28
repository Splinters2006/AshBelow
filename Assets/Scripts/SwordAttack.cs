using UnityEngine;
using System.Collections.Generic;

namespace Slopgame
{
    public sealed class SwordAttack : MonoBehaviour
    {
        public const float Reach = 2.3f;
        public const float ConeAngle = 60f;
        public const float HeavyReach = 3.6f;
        public const float HeavyConeAngle = 100f;
        public const float HeavyCooldown = 5f;
        public const float HeavyWindup = 0.45f;
        public const float HeavySweepDuration = 0.35f;
        public bool IsHeavyAttacking { get; private set; }
        public float HeavyCooldownRemaining => Mathf.Max(0, heavyReadyAt - Time.time);
        public DungeonPlayer Player { get; set; }
        private float readyAt, visibleUntil;
        private float heavyReadyAt, heavyStartedAt;
        private Vector2 heavyAim;
        private readonly HashSet<DungeonEnemy> heavyHits = new HashSet<DungeonEnemy>();
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

        public static bool ContainsTarget(Vector2 offset, Vector2 aim, float reach = Reach, float coneAngle = ConeAngle)
        {
            return offset.sqrMagnitude <= reach * reach &&
                (offset.sqrMagnitude < 0.0001f || Vector2.Dot(offset.normalized, aim.normalized) >= Mathf.Cos(coneAngle * 0.5f * Mathf.Deg2Rad));
        }

        public bool TryAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || IsHeavyAttacking || Time.time < readyAt || aim.sqrMagnitude < 0.001f) return false;
            SetArc(Reach, 1f, false);
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

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || IsHeavyAttacking || Time.time < readyAt
                || HeavyCooldownRemaining > 0 || aim.sqrMagnitude < 0.001f) return false;
            heavyAim = aim.normalized;
            heavyStartedAt = Time.time;
            heavyReadyAt = Time.time + HeavyCooldown;
            readyAt = Time.time + HeavyWindup + HeavySweepDuration + 0.2f;
            IsHeavyAttacking = true;
            heavyHits.Clear();
            arc.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            SetArc(HeavyReach, 1f, true, 0.12f);
            arc.enabled = true;
            return true;
        }

        private void SetArc(float reach, float sweep, bool heavy, float alpha = 0.35f)
        {
            var vertices = mesh.vertices;
            var colors = mesh.colors;
            int segments = vertices.Length - 2;
            float coneAngle = heavy ? HeavyConeAngle : ConeAngle;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (-coneAngle / 2 + coneAngle * sweep * i / segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
            }
            Color color = heavy ? new Color(1f, 0.65f, 0.2f, alpha) : new Color(0.4f, 1f, 0.85f, alpha);
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        public void Hide()
        {
            visibleUntil = 0;
            IsHeavyAttacking = false;
            heavyHits.Clear();
            if (arc != null) arc.enabled = false;
        }

        private void LateUpdate()
        {
            if (!Player.Run.IsPlaying || Player.IsRolling) { Hide(); return; }
            if (IsHeavyAttacking)
            {
                float elapsed = Time.time - heavyStartedAt;
                if (elapsed < HeavyWindup) return;
                float sweep = Mathf.Clamp01((elapsed - HeavyWindup) / HeavySweepDuration);
                SetArc(HeavyReach, sweep, true, 0.5f);
                for (int i = Player.Run.Enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = Player.Run.Enemies[i];
                    Vector2 offset = enemy.transform.position - transform.position;
                    if (!heavyHits.Contains(enemy) && ContainsTarget(offset, heavyAim, HeavyReach, HeavyConeAngle)
                        && Vector2.SignedAngle(heavyAim, offset) <= -HeavyConeAngle / 2 + HeavyConeAngle * sweep
                        && Player.Run.HasLineOfSight(transform.position, enemy.transform.position))
                    {
                        heavyHits.Add(enemy);
                        enemy.Hit(Player.Damage * 3);
                    }
                }
                if (sweep >= 1f) { IsHeavyAttacking = false; visibleUntil = Time.time + 0.1f; }
            }
            arc.enabled = IsHeavyAttacking || Time.time < visibleUntil;
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
    }
}

using UnityEngine;

namespace Slopgame
{
    public sealed class SwordAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float Reach = 2.3f;
        public const float ConeAngle = 60f;
        /// <summary>The Assassin stabs rather than sweeps, so her hit area is a narrow wedge.</summary>
        public const float StabAngle = 40f;
        public bool ShowChargePreview { get; set; } = true;
        public bool IsHeavyAttacking => Player.Shield != null && Player.Shield.IsBlocking;
        public float HeavyCooldownRemaining => Player.Shield != null ? Player.Shield.CooldownRemaining : DebugMode.Cooldown(Mathf.Max(0f, shadowReadyAt - Time.time));
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && !IsHeavyAttacking && Time.time >= readyAt;
        public DungeonPlayer Player { get; set; }
        private float readyAt, visibleUntil;
        private float shadowReadyAt;
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

        public float ChargedCone(float charge) => Mathf.Lerp(Player.ClassWeapon == WeaponType.Daggers ? StabAngle : ConeAngle,
            Player.ClassWeapon == WeaponType.Daggers ? 22f
                : 120f + Player.Powerups.Count(PowerupType.SweepingEdge) * 15f, Mathf.Clamp01(charge));

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            return TrySwipe(aim, Player.Charge.Damage(charge), Reach, ChargedCone(charge));
        }

        public bool TrySwipe(Vector2 aim, int damage, float reach, float cone)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            SetArc(reach, cone, Player.ClassWeapon == WeaponType.Daggers
                ? new Color(0.75f, 0.45f, 1f, 0.55f) : new Color(0.4f, 1f, 0.85f, 0.45f));
            readyAt = Time.time + 0.42f * Player.Powerups.AttackIntervalMultiplier;
            FaceArc(aim);
            if (Player.ClassWeapon == WeaponType.Daggers)
            {
                // A straight thrust: the dagger itself shows the reach, so the sweep cone stays hidden.
                visibleUntil = 0f;
                arc.enabled = false;
                StabVfx.Play(Player.Run.ProjectileRoot, transform.position, aim, reach, SlashColor);
                CoopFx.Stab(Player.Run, transform.position, aim, reach, SlashColor);
            }
            else
            {
                visibleUntil = Time.time + 0.15f;
                arc.enabled = true;
                HeroVfx.Slash(Player.Run.ProjectileRoot, transform.position, aim, reach, cone, SlashColor);
                CoopFx.Slash(Player.Run, transform.position, aim, reach, cone, SlashColor);
            }
            for (int i = Player.Run.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = Player.Run.Enemies[i];
                if (ContainsTarget(enemy.transform.position - transform.position, aim, reach, cone)
                    && Player.Run.HasLineOfSight(transform.position, enemy.transform.position))
                    CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, transform.position);
            }
            return true;
        }

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (Player.Shield != null) return Player.Shield.Raise(aim);
            if (!CanAttack || HeavyCooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            if (Player.ClassWeapon == WeaponType.Daggers)
            {
                if (!Player.Abilities.Shadowstep(aim, Mathf.Min(PlayerAbilities.ShadowstepDistance, aim.magnitude))) return false;
            }
            else Player.Abilities.Dash(aim, Mathf.Min(3f, aim.magnitude));
            Player.Charge.Cancel();
            shadowReadyAt = Time.time + 4f;
            return true;
        }

        private Color SlashColor => Player.ClassWeapon == WeaponType.Daggers ? new Color(0.8f, 0.5f, 1f)
            : Player.ClassWeapon == WeaponType.Hammer ? AbilityCatalog.Gold : new Color(0.5f, 1f, 0.9f);

        private void FaceArc(Vector2 aim)
        {
            arc.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }

        private void SetArc(float reach, float coneAngle, Color color)
        {
            var vertices = mesh.vertices;
            var colors = mesh.colors;
            int segments = vertices.Length - 2;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (-coneAngle / 2 + coneAngle * i / segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
            }
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        public void Hide()
        {
            visibleUntil = 0;
            Player.Charge.Cancel();
            Player.Shield?.Cancel();
            if (arc != null) arc.enabled = false;
        }

        private void LateUpdate()
        {
            if (!Player.Run.IsPlaying || Player.IsRolling) { Hide(); return; }
            if (IsHeavyAttacking)
            {
                FaceArc(Player.Shield.Direction);
                SetArc(0.9f, 120f, new Color(0.4f, 0.75f, 1f, 0.65f));
            }
            else if (ShowChargePreview && Player.Charge.IsCharging)
            {
                FaceArc(Player.AimDirection);
                SetArc(Reach, ChargedCone(Player.Charge.Amount),
                    Player.ClassWeapon == WeaponType.Daggers
                        ? Color.Lerp(new Color(0.6f, 0.3f, 1f, 0.12f), new Color(0.9f, 0.65f, 1f, 0.5f), Player.Charge.Amount)
                        : Color.Lerp(new Color(0.4f, 1f, 0.85f, 0.12f), new Color(1f, 0.8f, 0.25f, 0.3f), Player.Charge.Amount));
            }
            arc.enabled = IsHeavyAttacking || (ShowChargePreview && Player.Charge.IsCharging) || Time.time < visibleUntil;
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
    }
}

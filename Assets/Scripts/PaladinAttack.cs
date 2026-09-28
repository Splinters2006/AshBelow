using UnityEngine;

namespace Slopgame
{
    public sealed class PaladinAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float ChargeDuration = 3f;
        public const float BlessingRadius = 4f;
        public const float BlessingDuration = 8f;
        public const int BlessingDamage = 2;
        public DungeonPlayer Player { get; set; }
        public bool IsHeavyAttacking => swipe.IsHeavyAttacking;
        public float HeavyCooldownRemaining => swipe.HeavyCooldownRemaining;
        public bool CanAttack => swipe.CanAttack && Time.time >= readyAt;
        private SwordAttack swipe;
        private float readyAt;
        private LineRenderer circle;
        private Material material;

        public void Initialize(DungeonPlayer player, SwordAttack sword)
        {
            Player = player;
            swipe = sword;
            swipe.ShowChargePreview = false;
            var visual = new GameObject("Blessing charge radius");
            visual.transform.SetParent(transform, false);
            circle = visual.AddComponent<LineRenderer>();
            material = new Material(Shader.Find("Sprites/Default"));
            circle.sharedMaterial = material;
            circle.useWorldSpace = true;
            circle.loop = true;
            circle.positionCount = 64;
            circle.widthMultiplier = 0.06f;
            circle.sortingOrder = 5;
            circle.enabled = false;
        }

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            if (charge < 1f)
                return swipe.TrySwipe(aim, Mathf.Max(1, Player.BaseDamage / 2) + Player.Blessing.BonusDamage, 1.6f, SwordAttack.ConeAngle);
            foreach (var ally in FindObjectsByType<DungeonPlayer>())
            {
                if (ally.Run != Player.Run || ally.Health <= 0
                    || Vector2.Distance(transform.position, ally.transform.position) > BlessingRadius) continue;
                ally.Blessing.Apply(BlessingDamage, BlessingDuration + Player.Permanent.BlessingDuration);
                CombatVfx.Ring(Player.Run.ProjectileRoot, ally.transform.position, 0.6f, AbilityCatalog.Gold);
                HeroVfx.Motes(Player.Run.ProjectileRoot, ally.transform.position, 0.6f, AbilityCatalog.Gold, 14, 1f);
            }
            CombatVfx.Ring(Player.Run.ProjectileRoot, transform.position, BlessingRadius, AbilityCatalog.Gold, 0.6f);
            HeroVfx.Pulse(Player.Run.ProjectileRoot, transform.position, BlessingRadius, AbilityCatalog.Gold, 0.55f);
            readyAt = Time.time + 0.6f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        public bool TryHeavyAttack(Vector2 aim) => swipe.TryHeavyAttack(aim);
        public void Hide() { swipe.Hide(); if (circle != null) circle.enabled = false; }

        private void LateUpdate()
        {
            circle.enabled = Player.Run.IsPlaying && Player.Charge.IsCharging && !Player.IsRolling && !IsHeavyAttacking;
            if (!circle.enabled) return;
            Color color = AbilityCatalog.Gold;
            color.a = Mathf.Lerp(0.2f, 1f, Player.Charge.Amount);
            circle.startColor = circle.endColor = color;
            for (int i = 0; i < circle.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / circle.positionCount;
                circle.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * BlessingRadius);
            }
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}

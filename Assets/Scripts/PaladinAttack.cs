using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class PaladinAttack : MonoBehaviour, IPlayerWeapon
    {
        // Two thirds of the original three seconds.
        public const float ChargeDuration = 2f;
        public const float BlessingRadius = 4f;
        public const float BlessingDuration = 8f;
        public const int BlessingDamage = 2;
        /// <summary>Right click: holy swords fall from the sky onto every enemy within this radius.</summary>
        public const float HolySwordRadius = 3f, HolySwordCooldown = 12f;
        public const int HolySwordTargets = 8;
        private const float HolySwordStagger = 0.07f, HolySwordReach = 1.2f;
        public DungeonPlayer Player { get; set; }
        public bool IsHeavyAttacking => false;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, swordReadyAt - Time.time));
        public bool CanAttack => swipe.CanAttack && Time.time >= readyAt && !InSanctuary;
        private bool InSanctuary => relics != null && relics.IsSanctuaryActive;
        private SwordAttack swipe;
        private PaladinRelics relics;
        private float readyAt, swordReadyAt;
        private LineRenderer circle;
        private Material material;

        public void Initialize(DungeonPlayer player, SwordAttack sword)
        {
            Player = player;
            swipe = sword;
            swipe.ShowChargePreview = false;
            relics = GetComponent<PaladinRelics>();
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
            Player.Run.Coop?.SupportAllies(transform.position, BlessingRadius, SupportKind.Bless, BlessingDamage,
                BlessingDuration + Player.Permanent.BlessingDuration);
            CombatVfx.Ring(Player.Run.ProjectileRoot, transform.position, BlessingRadius, AbilityCatalog.Gold, 0.6f);
            HeroVfx.Pulse(Player.Run.ProjectileRoot, transform.position, BlessingRadius, AbilityCatalog.Gold, 0.55f);
            CoopFx.Ring(Player.Run, transform.position, BlessingRadius, AbilityCatalog.Gold, 0.6f);
            CoopFx.Pulse(Player.Run, transform.position, BlessingRadius, AbilityCatalog.Gold, 0.55f);
            readyAt = Time.time + 0.6f * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        /// <summary>Calls down a holy sword on each enemy nearby, one after another. Nothing happens without a target.</summary>
        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || InSanctuary || HeavyCooldownRemaining > 0f) return false;
            var targets = new List<DungeonEnemy>();
            Vector2 origin = transform.position;
            foreach (var enemy in Player.Run.Enemies)
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(origin, enemy.transform.position) <= HolySwordRadius + enemy.HitRadius
                    && Player.Run.HasLineOfSight(origin, enemy.transform.position)) targets.Add(enemy);
            if (targets.Count == 0) return false;
            targets.Sort((a, b) => Vector2.SqrMagnitude((Vector2)a.transform.position - origin)
                .CompareTo(Vector2.SqrMagnitude((Vector2)b.transform.position - origin)));
            if (targets.Count > HolySwordTargets) targets.RemoveRange(HolySwordTargets, targets.Count - HolySwordTargets);
            swordReadyAt = Time.time + HolySwordCooldown;
            Player.Charge.Cancel();
            int damage = Player.Damage * 3;
            for (int i = 0; i < targets.Count; i++) StartCoroutine(HolySword(targets[i], i * HolySwordStagger, damage));
            var root = Player.Run.ProjectileRoot;
            HeroVfx.Pulse(root, origin, 1.2f, AbilityCatalog.Gold, 0.35f);
            HeroVfx.Motes(root, origin, 0.7f, AbilityCatalog.Gold, 12, 0.8f);
            CombatVfx.Ring(root, origin, HolySwordRadius, AbilityCatalog.Gold, 0.45f);
            CoopFx.Ring(Player.Run, origin, HolySwordRadius, AbilityCatalog.Gold, 0.45f);
            return true;
        }

        private IEnumerator HolySword(DungeonEnemy target, float delay, int damage)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (!run.IsPlaying || root != run.ProjectileRoot || target == null || target.Health <= 0) yield break;
            // The sword tracks its target while the sigil forms, then commits to that spot.
            var sword = HolySwordVfx.Play(root, target.transform.position, target.transform);
            CoopFx.HolySword(run, target.transform.position);
            yield return new WaitForSeconds(HolySwordVfx.ImpactDelay);
            if (!run.IsPlaying || root != run.ProjectileRoot || sword == null) yield break;
            ScreenFx.Shake(0.08f, 0.12f);
            if (target == null || target.Health <= 0
                || Vector2.Distance(sword.Target, target.transform.position) > HolySwordReach + target.HitRadius) yield break;
            CombatDamage.Apply(Player, target, damage, DamageElement.Physical, sword.Target + Vector2.up, 0.2f);
            if (target.Health > 0) target.Chill(0.8f);
        }
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

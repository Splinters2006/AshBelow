using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Reaper's scythe. Left click is a slow, wide sweep; a fully charged sweep harvests a soul from every enemy it
    /// cuts. Right click spends souls on skulls that hunt their prey down: a tap throws one, half a charge throws one
    /// that strikes fear into its victim, and a full charge looses three. Souls also pay for his artifacts and for the
    /// skeletons of his <see cref="ArmyOfTheDead"/>.
    /// </summary>
    public sealed class ReaperAttack : MonoBehaviour, IPlayerWeapon
    {
        public const float Reach = 2.9f, TapCone = 110f, ChargedCone = 150f, ChargeDuration = 1.1f, SwingInterval = 0.75f;
        public const float SkullChargeTime = 0.9f, SkullCooldown = 1.5f, SkullRange = 13f, SkullFear = 1f, SkullSpread = 18f;
        public const int MaxSouls = 99;
        public static readonly Color Soul = new Color(0.55f, 1f, 0.8f), Bone = new Color(0.93f, 0.92f, 0.84f), Shade = new Color(0.2f, 0.45f, 0.4f);
        private DungeonPlayer player;
        /// <summary>Setting the hero hands him the souls his Grave Goods buried with him.</summary>
        public DungeonPlayer Player
        {
            get => player;
            set { player = value; Souls = Mathf.Min(MaxSouls, value != null && value.Permanent != null ? value.Permanent.StartingSouls : 0); }
        }
        public int Souls { get; private set; }
        private float readyAt, skullReadyAt, skullStartedAt, nextMote;
        private bool skullCharging;
        private int skullPinged;
        private GameObject previewObject;
        private FlameMesh preview;

        public bool IsSkullCharging => skullCharging;
        /// <summary>How far the held skull has charged, 0-1.</summary>
        public float SkullCharge => skullCharging ? Mathf.Clamp01((Time.time - skullStartedAt) / SkullChargeTime) : 0f;
        public bool IsHeavyAttacking => skullCharging;
        public float HeavyCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, skullReadyAt - Time.time));
        public void ReduceHeavyCooldown(float seconds) => skullReadyAt = Cooldowns.Shorten(skullReadyAt, seconds);
        public bool CanAttack => Player.Run.IsPlaying && !Player.IsRolling && !Player.IsBusy && !skullCharging && Time.time >= readyAt;

        public void AddSouls(int amount) { if (amount > 0) Souls = Mathf.Min(MaxSouls, Souls + amount); }

        /// <summary>Spends souls if he holds enough (debug mode pays for everything).</summary>
        public bool Spend(int amount)
        {
            if (DebugMode.Enabled) return true;
            if (amount <= 0 || Souls < amount) return false;
            Souls -= amount;
            LastSpent = Time.time < LastSpentAt + SpentNoticeTime ? LastSpent + amount : amount;
            LastSpentAt = Time.time;
            return true;
        }

        /// <summary>How long the HUD shows what was just spent beside the soul count.</summary>
        public const float SpentNoticeTime = 1.6f;
        /// <summary>Souls spent in the last moment (spends close together add up), and when; the HUD flashes them.</summary>
        public int LastSpent { get; private set; }
        public float LastSpentAt { get; private set; } = float.NegativeInfinity;

        private void Start()
        {
            previewObject = new GameObject("Scythe arc");
            preview = new FlameMesh(previewObject, 5);
        }

        private void OnDestroy()
        {
            preview?.Release();
            if (previewObject != null) Destroy(previewObject);
        }

        // ---------------------------------------------------------------- the scythe

        public bool TryAttack(Vector2 aim, float charge = 0f)
        {
            if (!CanAttack || aim.sqrMagnitude < 0.001f) return false;
            aim.Normalize();
            bool harvest = charge >= 1f;
            if (!harvest && IsTechniqueActive) return TechniqueStrike(aim, charge);
            float cone = Mathf.Lerp(TapCone, ChargedCone, Mathf.Clamp01(charge));
            Sweep(aim, Player.Charge.Damage(charge), Reach, cone, harvest, harvest ? 1.6f : 1f);
            var run = Player.Run;
            Color color = harvest ? Soul : Bone;
            ScytheSwingVfx.Play(run.ProjectileRoot, transform, aim, cone, Reach, harvest ? 0.34f : 0.28f, color);
            HeroVfx.Slash(run.ProjectileRoot, transform.position, aim, Reach, cone, FlameMesh.Alpha(color, 0.55f), 0.18f);
            CoopFx.Slash(run, transform.position, aim, Reach, cone, color);
            if (harvest) ScreenFx.Shake(0.12f, 0.15f);
            readyAt = Time.time + SwingInterval * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        /// <summary>Cuts every enemy whose body reaches into the cone; a harvest pulls a soul out of each one. Returns how many were cut.</summary>
        private int Sweep(Vector2 aim, int damage, float reach, float cone, bool harvest, float knockback)
        {
            var run = Player.Run;
            Vector2 origin = transform.position;
            int cut = 0;
            for (int i = run.Enemies.Count - 1; i >= 0; i--)
            {
                if (i >= run.Enemies.Count) continue;
                var enemy = run.Enemies[i];
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 at = enemy.transform.position;
                if (!SwordAttack.OverlapsCone(at - origin, aim, reach, cone, enemy.HitRadius) || !run.HasLineOfSight(origin, at)) continue;
                bool soul = harvest && !enemy.IsInvulnerable;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, origin, knockback);
                if (soul) SoulWisp.Drop(run, at);
                cut++;
            }
            return cut;
        }

        // ---------------------------------------------------------------- skulls (right click)

        /// <summary>Souls a skull throw spends, and so which throw it is: 1 a skull, 2 a fearsome skull, 3 three of them. Never more than he holds.</summary>
        public static int SkullTier(float charge, int souls) => Mathf.Min(charge >= 1f ? 3 : charge >= 0.5f ? 2 : 1, Mathf.Max(0, souls));

        public bool TryHeavyAttack(Vector2 aim)
        {
            if (!Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy || skullCharging || HeavyCooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            if (Souls < 1 && !DebugMode.Enabled)
            {
                // Nothing to throw: a puff of grey dust.
                HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, new Color(0.5f, 0.5f, 0.5f), 5, 1.8f, 0.25f, aim, 60f, 0.7f);
                return false;
            }
            Player.Charge.Cancel();
            skullCharging = true;
            skullStartedAt = Time.time;
            skullPinged = 0;
            return true;
        }

        private void Update()
        {
            if (Player == null) return;
            var run = Player.Run;
            if (IsTechniqueActive && run.IsPlaying && Time.time >= nextMote)
            {
                nextMote = Time.time + 0.12f;
                HeroVfx.Motes(run.ProjectileRoot, transform.position, 0.5f, Soul, 2, 0.5f);
            }
            if (!skullCharging) return;
            if (!run.IsPlaying || Player.Health <= 0 || Player.IsRolling) { skullCharging = false; return; }
            float charge = SkullCharge;
            // A ping as the skull reaches each stronger throw he can afford.
            int reached = SkullTier(charge, DebugMode.Enabled ? 3 : Souls);
            if (reached > skullPinged)
            {
                if (skullPinged > 0) HeroVfx.Pulse(run.ProjectileRoot, Hand(Player.AimDirection), 0.35f + 0.15f * reached, Soul, 0.25f);
                skullPinged = reached;
            }
            if (!KeyBindings.IsHeld(GameAction.Special)) ThrowSkulls(Player.AimDirection, charge);
        }

        private Vector2 Hand(Vector2 aim) => (Vector2)transform.position + aim * 0.45f;

        private void ThrowSkulls(Vector2 aim, float charge)
        {
            skullCharging = false;
            int tier = SkullTier(charge, DebugMode.Enabled ? 3 : Souls);
            if (tier <= 0 || !Spend(tier)) return;
            var run = Player.Run;
            Vector2 origin = Hand(aim);
            int damage = Player.Damage * 2;
            if (tier >= 3)
                for (int i = -1; i <= 1; i++)
                    ReaperSkull.Launch(Player, origin, Quaternion.Euler(0, 0, i * SkullSpread) * aim, damage, SkullRange, SkullFear, true);
            else ReaperSkull.Launch(Player, origin, aim, damage, SkullRange, tier >= 2 ? SkullFear : 0f, true);
            HeroVfx.Sparks(run.ProjectileRoot, origin, Soul, 6 + tier * 3, 3.5f, 0.3f, aim, 70f);
            CoopFx.Pulse(run, origin, 0.4f + 0.2f * tier, Soul, 0.25f);
            skullReadyAt = Time.time + SkullCooldown * Player.Powerups.SkillCooldownMultiplier;
            readyAt = Mathf.Max(readyAt, Time.time + 0.25f);
        }

        // ---------------------------------------------------------------- boss artifacts

        public const float ShadeWalkTime = 3f, FearIncarnateTime = 1.5f, SowTime = 2f, SowSpreadRadius = 3f, SowReach = 3f, TechniqueTime = 10f;
        /// <summary>Feast's three sittings: souls eaten and health restored.</summary>
        public static readonly int[] FeastSouls = { 1, 3, 5 }, FeastHealing = { 1, 2, 3 };
        private float techniqueUntil, lastStrikeAt;
        private int comboStep;
        public bool IsTechniqueActive => Time.time < techniqueUntil;

        public bool CastArtifact(AbilityType type, Vector2 aim, int rank, float cursorDistance)
        {
            if (Player == null || !Player.Run.IsPlaying || Player.IsRolling || Player.IsBusy) return false;
            rank = Mathf.Clamp(rank, 1, PlayerAbilities.MaxRank);
            switch (type)
            {
                case AbilityType.ShadeWalk: ShadeWalk(rank); return true;
                case AbilityType.FearIncarnate: return FearIncarnate(rank);
                case AbilityType.Feast: return Feast(rank);
                case AbilityType.Sow: return Sow((Vector2)transform.position + aim * cursorDistance, rank);
                case AbilityType.Reap: return Reap(rank);
                case AbilityType.ReapersTechnique: ReapersTechnique(rank); return true;
                default: return false;
            }
        }

        /// <summary>His body turns to shade: enemies' touch passes straight through him.</summary>
        public void ShadeWalk(int rank)
        {
            Player.ShadeWalk(ShadeWalkTime + (rank - 1));
            HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.8f, Shade, 16, 0.9f);
        }

        /// <summary>Every enemy is struck with fear, and gives up a soul when it dies.</summary>
        public bool FearIncarnate(int rank)
        {
            var run = Player.Run;
            Vector2 center = transform.position;
            float duration = FearIncarnateTime + 0.25f * (rank - 1);
            int struck = 0;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || enemy.IsInvulnerable) continue;
                enemy.Fear(center, duration);
                enemy.SoulBound = true;
                HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, Soul, 8, 3f, 0.35f, (Vector2)enemy.transform.position - center, 90f);
                struck++;
            }
            if (struck == 0) return false;
            HeroVfx.Pulse(run.ProjectileRoot, center, 9f, Shade, 0.6f);
            CoopFx.Pulse(run, center, 9f, Shade, 0.6f);
            ScreenFx.Flash(FlameMesh.Alpha(Shade, 0.35f), 0.4f);
            ScreenFx.Shake(0.25f, 0.4f);
            return true;
        }

        /// <summary>
        /// Which Feast sitting to eat, 0-2, or -1 for none: the biggest he can pay for, but no bigger than the wound
        /// needs (2 missing HP never costs more than 3 souls).
        /// </summary>
        public static int FeastTier(int souls, int missingHealth)
        {
            if (missingHealth <= 0) return -1;
            int tier = -1;
            for (int i = 0; i < FeastSouls.Length; i++)
            {
                if (souls < FeastSouls[i]) break;
                tier = i;
                if (FeastHealing[i] >= missingHealth) break;
            }
            return tier;
        }

        /// <summary>Eats 1, 3 or 5 souls to heal 1, 2 or 3 HP. Ranks above the first leave him untouchable for a second each.</summary>
        public bool Feast(int rank)
        {
            int tier = FeastTier(DebugMode.Enabled ? MaxSouls : Souls, Player.MaxHealth - Player.Health);
            if (tier < 0 || !Spend(FeastSouls[tier])) return false;
            Player.Heal(FeastHealing[tier]);
            if (rank > 1) Player.Protect(rank - 1);
            FeastVfx.Play(Player.Run.ProjectileRoot, transform, FeastSouls[tier], FeastHealing[tier]);
            CoopFx.Heal(Player.Run, transform.position, 0.6f);
            ScreenFx.Flash(FlameMesh.Alpha(HealVfx.Mint, 0.16f), 0.3f);
            return true;
        }

        /// <summary>Sows fear in the enemy nearest <paramref name="at"/>; if it dies afraid, the fear spreads to everything near it.</summary>
        public bool Sow(Vector2 at, int rank)
        {
            var target = Player.Abilities.NearestEnemy(at, SowReach);
            if (target == null || target.IsInvulnerable) return false;
            float duration = SowTime + 0.5f * (rank - 1);
            target.Fear(transform.position, duration);
            target.SownFear = duration;
            var run = Player.Run;
            SowMarkVfx.Attach(run.ProjectileRoot, target);
            CombatVfx.Ring(run.ProjectileRoot, target.transform.position, 0.8f, Soul, 0.5f);
            CoopFx.Ring(run, target.transform.position, 0.8f, Soul, 0.5f);
            CombatVfx.Bolt(run.ProjectileRoot, transform.position, target.transform.position, Shade);
            return true;
        }

        private void SpreadSow(DungeonEnemy fallen, float duration)
        {
            var run = Player.Run;
            Vector2 at = fallen.transform.position;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy == fallen || enemy.Health <= 0 || enemy.IsInvulnerable
                    || Vector2.Distance(at, enemy.transform.position) > SowSpreadRadius + enemy.HitRadius) continue;
                enemy.Fear(at, duration);
                // The seed travels with the fear, so it can spread again.
                enemy.SownFear = duration;
                SowMarkVfx.Attach(run.ProjectileRoot, enemy);
                CombatVfx.Bolt(run.ProjectileRoot, at, enemy.transform.position, Soul);
            }
            HeroVfx.Pulse(run.ProjectileRoot, at, SowSpreadRadius, Shade, 0.4f);
            CoopFx.Pulse(run, at, SowSpreadRadius, Shade, 0.4f);
        }

        /// <summary>
        /// Reaps the fear out of every frightened enemy: each full second it still had to run is a soul for the Reaper
        /// and a blow for the enemy.
        /// </summary>
        public bool Reap(int rank)
        {
            var run = Player.Run;
            var afraid = new List<DungeonEnemy>();
            foreach (var enemy in run.Enemies) if (enemy != null && enemy.Health > 0 && enemy.IsFeared) afraid.Add(enemy);
            if (afraid.Count == 0) return false;
            var vfx = ReapVfx.Begin(run.ProjectileRoot, transform);
            foreach (var enemy in afraid)
            {
                Vector2 at = enemy.transform.position;
                int seconds = Mathf.FloorToInt(enemy.ConsumeFear() + 0.0001f);
                if (vfx != null) vfx.Add(at, enemy.HitRadius, seconds);
                CoopFx.Slash(run, at + Vector2.left * 0.5f, Vector2.right, 1f, 140f, Soul);
                if (seconds <= 0) continue;
                AddSouls(seconds);
                CombatDamage.Apply(Player, enemy, (Player.Damage * 2 + rank - 1) * seconds, DamageElement.Demonic, transform.position, 0.3f);
            }
            ScreenFx.Flash(FlameMesh.Alpha(Soul, 0.18f), 0.25f);
            ScreenFx.Shake(0.12f + 0.03f * Mathf.Min(afraid.Count, 6), 0.2f);
            return true;
        }

        /// <summary>For a while his quick cuts become a string of three heavier scythe arts; charging is unchanged.</summary>
        public void ReapersTechnique(int rank)
        {
            techniqueUntil = Time.time + TechniqueTime + 2f * (rank - 1);
            comboStep = 0;
            lastStrikeAt = 0f;
            HeroVfx.Pulse(Player.Run.ProjectileRoot, transform.position, 1.4f, Soul, 0.45f);
        }

        /// <summary>The Technique's string: a wide cut, a doubled back-cut, then a full spin.</summary>
        private bool TechniqueStrike(Vector2 aim, float charge)
        {
            comboStep = Time.time - lastStrikeAt > 1.4f ? 0 : (comboStep + 1) % 3;
            lastStrikeAt = Time.time;
            var run = Player.Run;
            var root = run.ProjectileRoot;
            Vector2 origin = transform.position;
            int damage = Player.Charge.Damage(charge);
            float interval = 0.45f;
            if (comboStep == 0)
            {
                Sweep(aim, Mathf.RoundToInt(damage * 1.5f), Reach, 170f, false, 1f);
                ScytheSwingVfx.Play(root, transform, aim, 170f, Reach, 0.22f, Soul);
                HeroVfx.Slash(root, origin, aim, Reach, 170f, FlameMesh.Alpha(Soul, 0.55f), 0.18f);
                CoopFx.Slash(run, origin, aim, Reach, 170f, Soul);
            }
            else if (comboStep == 1)
            {
                Sweep(aim, Mathf.RoundToInt(damage * 1.5f), Reach + 0.3f, 170f, false, 1.2f);
                ScytheSwingVfx.Play(root, transform, aim, 170f, Reach + 0.3f, 0.22f, Bone, true);
                HeroVfx.Slash(root, origin, aim, Reach + 0.3f, 170f, FlameMesh.Alpha(Bone, 0.55f), 0.18f);
                CoopFx.Slash(run, origin, aim, Reach + 0.3f, 170f, Bone);
            }
            else
            {
                Sweep(aim, Mathf.RoundToInt(damage * 2.5f), Reach + 0.4f, 360f, false, 2f);
                ScytheSwingVfx.Play(root, transform, aim, 360f, Reach + 0.4f, 0.36f, Soul);
                HeroVfx.Slash(root, origin, aim, Reach + 0.4f, 360f, FlameMesh.Alpha(Soul, 0.55f), 0.22f);
                CoopFx.Slash(run, origin, aim, Reach + 0.4f, 360f, Soul);
                ScreenFx.Shake(0.15f, 0.2f);
                interval = 0.7f;
            }
            readyAt = Time.time + interval * Player.Powerups.AttackIntervalMultiplier;
            return true;
        }

        /// <summary>
        /// An enemy died on this machine: a soul-bound one leaves its soul, one the Reaper killed while it could not
        /// move pays the Grave Tithe, and sown fear spreads from one that died afraid.
        /// </summary>
        /// <param name="localKill">True when this machine's hero dealt the killing blow.</param>
        public void OnEnemyDied(DungeonEnemy enemy, bool localKill)
        {
            if (enemy.SoulBound) SoulWisp.Drop(Player.Run, enemy.transform.position);
            if (localKill && enemy.IsImmobilized && Player.Health > 0 && Player.Powerups.Count(PowerupType.GraveTithe) > 0)
                SoulWisp.Drop(Player.Run, enemy.transform.position);
            if (enemy.SownFear > 0f && enemy.IsFeared) SpreadSow(enemy, enemy.SownFear);
        }

        // ---------------------------------------------------------------- previews

        public void Hide()
        {
            Player.Charge.Cancel();
            skullCharging = false;
        }

        private void LateUpdate()
        {
            if (preview == null) return;
            preview.Begin();
            if (Player != null && Player.Run.IsPlaying && Player.Health > 0)
            {
                Vector2 origin = transform.position, aim = Player.AimDirection;
                if (Player.Charge.IsCharging)
                {
                    // The sweep's cone, turning from bone to soul-green as the harvest comes ready.
                    float charge = Player.Charge.Amount, cone = Mathf.Lerp(TapCone, ChargedCone, charge) * Mathf.Deg2Rad;
                    Color fill = FlameMesh.Alpha(Color.Lerp(Bone, Soul, charge), 0.1f + 0.2f * charge);
                    float start = Mathf.Atan2(aim.y, aim.x) - cone * 0.5f;
                    const int Segments = 24;
                    for (int i = 0; i < Segments; i++)
                        preview.Triangle(origin, origin + FlameMesh.Polar(start + cone * i / Segments, Reach),
                            origin + FlameMesh.Polar(start + cone * (i + 1) / Segments, Reach), fill, fill, fill);
                }
                else if (skullCharging)
                {
                    // One aim line per skull the throw would loose, and a skull-light gathering in his hand.
                    float charge = SkullCharge;
                    int tier = SkullTier(charge, DebugMode.Enabled ? 3 : Souls);
                    Vector2 hand = Hand(aim);
                    for (int i = tier >= 3 ? -1 : 0; i <= (tier >= 3 ? 1 : 0); i++)
                        preview.Bar(hand, Quaternion.Euler(0, 0, i * SkullSpread) * aim, 1.2f + 1.2f * charge, 0.05f, FlameMesh.Alpha(Soul, 0.6f), FlameMesh.Alpha(Soul, 0f));
                    preview.Disc(hand, 0.12f + 0.14f * charge, FlameMesh.Alpha(Bone, 0.9f), FlameMesh.Alpha(Soul, 0.2f), 16);
                    if (tier >= 2) preview.Ring(hand, 0.34f + 0.05f * Mathf.Sin(Time.time * 14f), 0.04f, FlameMesh.Alpha(Soul, 0.8f), 20);
                }
            }
            preview.Commit();
        }
    }
}

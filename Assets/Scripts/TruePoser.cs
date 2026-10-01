using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Samurai's class mechanic, a toggle. Pressed once, she strikes a pose: everything she does deals no damage,
    /// and each enemy is instead owed what it would have taken. Pressed again, she sheathes her katana and every cut
    /// lands at once, multiplied by <see cref="ReleaseMultiplier"/>. With its R upgrade (Open Veins, from the Ash shop)
    /// the sheathe also opens a bleed on each enemy, based on all the damage it dealt them. A sheathe starts a cooldown
    /// that only holds back the next sheathe: she can strike the pose again at once, but has to hold it until it runs out.
    /// </summary>
    public sealed class TruePoser : ClassMechanic
    {
        public const float ReleaseMultiplier = 1.25f, Cooldown = 10f, SheatheTime = 0.5f, ImpactTime = 0.16f;
        private readonly Dictionary<DungeonEnemy, int> owed = new Dictionary<DungeonEnemy, int>();
        private float readyAt, nextMote;
        private Coroutine sheathing;
        public override string Name => "True Poser";
        public override Color Color => SamuraiAttack.Blood;
        /// <summary>True while she holds the pose: her damage is stored instead of dealt.</summary>
        public bool IsPosing { get; private set; }
        private float CooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, readyAt - Time.time));
        /// <summary>All the damage waiting on the sheathe, before its multiplier.</summary>
        public int Owed
        {
            get
            {
                int total = 0;
                foreach (var entry in owed) if (entry.Key != null && entry.Key.Health > 0) total += entry.Value;
                return total;
            }
        }
        /// <summary>True while she is posing and the last sheathe's cooldown still keeps the katana out.</summary>
        public bool IsSheatheLocked => IsPosing && CooldownRemaining > 0f;
        public override float Readiness => IsPosing ? 1f - Mathf.Clamp01(CooldownRemaining / Cooldown) : 1f;
        public override string Status => IsSheatheLocked ? $"{Seconds(CooldownRemaining)}  /  {Owed}" : IsPosing ? $"SHEATHE  /  {Owed}" : "READY";
        public override void ReduceCooldown(float seconds) => readyAt = Cooldowns.Shorten(readyAt, seconds);

        public override bool TryActivate(Vector2 aim)
        {
            if (!CanAct || sheathing != null) return false;
            if (IsPosing)
            {
                if (CooldownRemaining > 0f) return false;
                sheathing = StartCoroutine(Sheathe());
                return true;
            }
            IsPosing = true;
            owed.Clear();
            var run = Player.Run;
            HeroVfx.Pulse(run.ProjectileRoot, transform.position, 1.2f, SamuraiAttack.Steel, 0.35f);
            CombatVfx.Ring(run.ProjectileRoot, transform.position, 0.9f, SamuraiAttack.Blood, 0.4f);
            CoopFx.Ring(run, transform.position, 0.9f, SamuraiAttack.Blood, 0.4f);
            return true;
        }

        /// <summary>A blow that landed during the pose: it deals nothing now and is owed to <paramref name="enemy"/> at the sheathe.</summary>
        public void Store(DungeonEnemy enemy, int damage)
        {
            if (enemy == null || damage <= 0) return;
            owed[enemy] = (owed.TryGetValue(enemy, out int stored) ? stored : 0) + damage;
            if (Player.Run.ProjectileRoot != null)
                HeroVfx.Sparks(Player.Run.ProjectileRoot, enemy.transform.position, SamuraiAttack.Steel, 3, 1.5f, 0.2f);
        }

        /// <summary>
        /// She slides the katana home, unhurried; as it clicks shut the screen cuts to impact frames, and when they let go
        /// every cut she made while posing opens at once.
        /// </summary>
        private IEnumerator Sheathe()
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            Player.Weapon?.Hide();
            Player.Charge.Cancel();
            Player.Occupy(SheatheTime + ImpactTime);
            // Nothing can touch her while the katana slides home.
            Player.Protect(SheatheTime + ImpactTime);
            KatanaVfx.Sheathe(root, transform.position, Player.AimDirection, SamuraiAttack.Blood, SheatheTime / 0.72f);
            // The click comes 72% of the way through the effect, exactly when the cuts land.
            for (float t = 0f; t < SheatheTime; t += Time.deltaTime)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot || Player.Health <= 0) { Drop(); yield break; }
                yield return null;
            }
            Vector2 hero = transform.position;
            // Each living enemy's cut is ruled out now, so the impact frames and the wounds they leave line up.
            var marked = new List<DungeonEnemy>();
            var lines = new List<Vector2>();
            foreach (var entry in owed)
            {
                var enemy = entry.Key;
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 at = enemy.transform.position, across = Random.insideUnitCircle.normalized * (enemy.HitRadius + 0.5f);
                marked.Add(enemy);
                lines.Add(at - across);
                lines.Add(at + across);
            }
            if (marked.Count > 0)
            {
                KatanaVfx.Impact(root, hero, lines.ToArray(), SamuraiAttack.Blood, ImpactTime);
                for (float t = 0f; t < ImpactTime; t += Time.deltaTime)
                {
                    if (!run.IsPlaying || root != run.ProjectileRoot || Player.Health <= 0) { Drop(); yield break; }
                    yield return null;
                }
            }
            IsPosing = false;
            int cuts = 0;
            for (int i = 0; i < marked.Count; i++)
            {
                var enemy = marked[i];
                if (enemy == null || enemy.Health <= 0 || !owed.TryGetValue(enemy, out int stored)) continue;
                int damage = Mathf.Max(1, Mathf.RoundToInt(stored * ReleaseMultiplier));
                Vector2 from = lines[i * 2], to = lines[i * 2 + 1], at = enemy.transform.position;
                KatanaVfx.Slice(root, from, to, SamuraiAttack.Blood, 0.5f);
                HeroVfx.Sparks(root, at, SamuraiAttack.Blood, 16, 6f, 0.45f);
                HeroVfx.Sparks(root, at, SamuraiAttack.Steel, 6, 3f, 0.25f);
                CombatVfx.Ring(root, at, enemy.HitRadius + 0.6f, SamuraiAttack.Blood, 0.3f);
                CoopFx.Bolt(run, from, to, SamuraiAttack.Steel, true);
                enemy.Hit(damage, hero, 0.6f);
                // Open Veins: the sheathe leaves them bleeding for what it just dealt.
                if (IsUpgraded) CombatDamage.InflictBleed(Player, enemy, damage);
                cuts++;
            }
            owed.Clear();
            if (cuts > 0)
            {
                ScreenFx.Flash(FlameMesh.Alpha(SamuraiAttack.Blood, 0.3f), 0.25f);
                ScreenFx.Shake(0.22f + 0.04f * Mathf.Min(cuts, 8), 0.3f);
            }
            readyAt = Time.time + Cooldown;
            sheathing = null;
        }

        /// <summary>The pose was broken off (the floor changed or she fell): what was owed is lost.</summary>
        private void Drop()
        {
            IsPosing = false;
            owed.Clear();
            sheathing = null;
        }

        private void Update()
        {
            if (!IsPosing || Player == null || !Player.Run.IsPlaying || Time.time < nextMote) return;
            nextMote = Time.time + 0.15f;
            HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.55f, SamuraiAttack.Steel, 2, 0.5f);
        }
    }
}

using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Knight's class mechanic: for 2.25 seconds he turns red with rage and nothing can hurt him: bolts are stopped
    /// from every side and blows, slams and guardian attacks glance off him (each grants a ward). He can walk but do
    /// nothing else. It does not reflect. In co-op, enemies target him first for a few seconds. With its R upgrade (Retribution, from the Ash shop), the rage bursts out when the taunt
    /// ends: every nearby enemy takes his damage once for each hit he stopped or took while it lasted.
    /// </summary>
    public sealed class ShieldTaunt : ClassMechanic
    {
        public const float Duration = 2.25f, Cooldown = 12f, Reach = 2.025f, Cone = 360f, AggroDuration = 6f;
        public static readonly Color ShieldColor = new Color(0.4f, 0.75f, 1f);
        // Half as wide again as the taunt's own ring.
        public const float RetributionRadius = Reach * 1.6f * 1.5f;
        private float until, readyAt, aggroUntil;
        private ShieldTauntVfx vfx;
        private bool wasTaunting, listening;
        /// <summary>Bolts stopped and blows taken during this taunt; Retribution's multiplier.</summary>
        public int Hits { get; private set; }
        public override string Name => "Shield Taunt";
        public override Color Color => ShieldColor;
        public float CooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, readyAt - Time.time));
        public override void ReduceCooldown(float seconds) => readyAt = Cooldowns.Shorten(readyAt, seconds);
        public override float Readiness => IsTaunting ? 0f : 1f - CooldownRemaining / Cooldown;
        public override string Status => IsTaunting ? "HOLDING" : CooldownRemaining > 0f ? Seconds(CooldownRemaining) : "READY";
        public bool IsTaunting => Player.Run.IsPlaying && Player.Health > 0 && Time.time < until;
        /// <summary>While true, enemies choose this Knight over closer heroes.</summary>
        public bool DrawsAggro => Player.Health > 0 && Time.time < aggroUntil;
        public Vector2 Direction { get; private set; } = Vector2.right;

        public override bool TryActivate(Vector2 aim)
        {
            if (!CanAct || CooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            Direction = aim.normalized;
            until = Time.time + Duration;
            Hits = 0;
            if (!listening) { listening = true; Player.Struck += OnStruck; }
            wasTaunting = true;
            aggroUntil = Time.time + AggroDuration;
            readyAt = Time.time + Cooldown;
            Player.Weapon?.Hide();
            Player.Charge.Cancel();
            // He can walk with the shield up, but no attacks, abilities or dodges (see DungeonPlayer.IsHoldingShield).
            // A furious bellow instead of a shield: a ward of rage around him and a shake.
            var root = Player.Run.ProjectileRoot;
            if (vfx != null) vfx.End();
            vfx = ShieldTauntVfx.Play(root, transform, Duration, Reach);
            CoopFx.ShieldTaunt(Player.Run, Duration, Reach);
            HeroVfx.Sparks(root, transform.position, HeroBuffs.TauntColor, 18, 5f, 0.4f);
            ScreenFx.Shake(0.2f, 0.18f);
            return true;
        }

        private void Update()
        {
            if (Player == null || Player.Run == null || Player.Run.ProjectileRoot == null) return;
            if (IsTaunting || !wasTaunting) return;
            wasTaunting = false;
            // Also drops the ward if the taunt was cut short (he died, or the run stopped).
            if (vfx != null) vfx.End();
            Retribution();
        }

        // Every blow that lands on him counts, whether it cost HP or only a ward.
        private void OnStruck(bool warded) { if (IsTaunting) Soak(); }

        private void Soak()
        {
            Hits++;
            // Retribution's stored hits circle him as sparks.
            if (IsUpgraded && vfx != null) vfx.Charge();
        }

        private void OnDestroy() { if (listening && Player != null) Player.Struck -= OnStruck; }

        /// <summary>Retribution: as the taunt ends, every enemy nearby takes his damage times the hits it soaked up.</summary>
        private void Retribution()
        {
            var run = Player.Run;
            if (!IsUpgraded || Hits <= 0 || !run.IsPlaying || Player.Health <= 0) return;
            Vector2 at = transform.position;
            int damage = Player.Damage * Hits;
            var root = run.ProjectileRoot;
            ShieldTauntVfx.Retribution(root, at, RetributionRadius, Hits);
            CoopFx.Retribution(run, at, RetributionRadius, Hits);
            HeroVfx.Sparks(root, at, HeroBuffs.TauntColor, 24, 6f, 0.45f);
            ScreenFx.Shake(0.25f, 0.25f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || Vector2.Distance(at, enemy.transform.position) > RetributionRadius + enemy.HitRadius) continue;
                CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, at, 1.2f);
            }
        }

        /// <summary>
        /// Stops a bolt at <paramref name="position"/> heading in from any side within reach; each block adds a ward. A
        /// bolt close enough to strike him is stopped whatever its heading (one grazing past or spawned on top of him),
        /// so nothing slips through the taunt.
        /// </summary>
        public bool TryBlock(Vector2 position, Vector2 incoming)
        {
            Vector2 offset = position - (Vector2)transform.position;
            if (!IsTaunting || offset.sqrMagnitude > Reach * Reach) return false;
            float strike = Player.HitRadius + StrikeMargin;
            if (Vector2.Dot(incoming, offset) >= 0f && offset.sqrMagnitude > strike * strike) return false;
            Player.Powerups.AddWard();
            Soak();
            HeroVfx.Sparks(Player.Run.ProjectileRoot, position, ShieldColor, 8, 3.5f, 0.25f, -incoming, 100f);
            if (vfx != null) vfx.Block(offset);
            return true;
        }

        /// <summary>A bolt this much beyond his body still counts as striking him (it is stopped whichever way it flies).</summary>
        private const float StrikeMargin = 0.1f;

        /// <summary>
        /// A blow that would have landed on him while he rages (a touch, a slam, a guardian's attack) glances off instead:
        /// it costs nothing, adds a ward like a stopped bolt and counts toward Retribution. Burning ground only glances
        /// off (<paramref name="ward"/> false), so standing in fire can't farm wards.
        /// </summary>
        public bool TryShrugOff(bool ward = true)
        {
            if (!IsTaunting) return false;
            if (ward) Player.Powerups.AddWard();
            Soak();
            if (Player.Run.ProjectileRoot != null)
                HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, HeroBuffs.TauntColor, 10, 4f, 0.3f);
            if (vfx != null) vfx.Block(Random.insideUnitCircle);
            return true;
        }
    }
}

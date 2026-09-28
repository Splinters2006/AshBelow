using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class PlayerAbilities : MonoBehaviour
    {
        public DungeonPlayer Player { get; set; }
        public const int SlotCount = 2;
        public const int MaxRank = 3;
        private readonly AbilityType[] equipped = new AbilityType[SlotCount];
        private readonly float[] readyAt = new float[SlotCount];
        private readonly Dictionary<AbilityType, int> ranks = new Dictionary<AbilityType, int>();
        private float castReadyAt;
        public AbilityType Equipped(int slot) => slot >= 0 && slot < SlotCount ? equipped[slot] : AbilityType.None;
        public int Rank(AbilityType type) => ranks.TryGetValue(type, out int value) ? value : 0;
        public bool IsEquipped(AbilityType type) => type != AbilityType.None && (equipped[0] == type || equipped[1] == type);
        public float CooldownRemaining(int slot) => DebugMode.Cooldown(Mathf.Max(0f, readyAt[slot] - Time.time));
        public int EmptySlot => equipped[0] == AbilityType.None ? 0 : equipped[1] == AbilityType.None ? 1 : -1;

        public bool Claim(AbilityType type, int slot)
        {
            var definition = AbilityCatalog.Get(type);
            if (definition == null || definition.ClassWeapon != Player.ClassWeapon || slot < 0 || slot >= SlotCount) return false;
            if (IsEquipped(type))
            {
                if (Rank(type) >= MaxRank) return false;
                ranks[type] = Rank(type) + 1;
                return true;
            }
            equipped[slot] = type;
            if (Rank(type) == 0) ranks[type] = 1;
            return true;
        }

        public bool TryUse(int slot, Vector2 aim)
        {
            if (slot < 0 || slot >= SlotCount || !Player.Run.IsPlaying || Player.IsRolling
                || Time.time < castReadyAt || CooldownRemaining(slot) > 0f || aim.sqrMagnitude < 0.001f) return false;
            var definition = AbilityCatalog.Get(equipped[slot]);
            if (definition == null) return false;
            Player.Weapon?.Hide();
            aim.Normalize();
            int rank = Rank(definition.Type);
            int damage = Player.Damage * 3 + rank - 1;
            var powers = Player.Powerups;
            switch (definition.Type)
            {
                case AbilityType.ShieldRush:
                    Dash(aim, 3.5f, damage + powers.Count(PowerupType.RushPower) * 2); break;
                case AbilityType.Earthshatter:
                    AreaAttack(transform.position, 2.6f + powers.Count(PowerupType.ShatterRadius) * 0.4f,
                        damage, DamageElement.Physical, definition.Color, 2f); break;
                case AbilityType.Aegis:
                    Player.Protect(2f + (rank - 1) * 0.3f + powers.Count(PowerupType.AegisDuration) * 0.4f); break;
                case AbilityType.Volley:
                    Fan(aim, 7 + powers.Count(PowerupType.VolleyCount) * 2, 10f, Player.Damage + rank - 1); break;
                case AbilityType.PiercingShot:
                    SpellProjectile.Spawn(Player, aim, damage + powers.Count(PowerupType.PiercingPower) * 2,
                        DamageElement.Physical, definition.Color, 5f, 0f, 3 + rank); break;
                case AbilityType.Windstep:
                    Dash(aim, 3.5f + powers.Count(PowerupType.WindstepDistance) * 0.5f);
                    Fan(aim, 3, 15f, Player.Damage + rank - 1); break;
                case AbilityType.Fireball:
                    SpellProjectile.Spawn(Player, aim, damage, DamageElement.Fire, definition.Color, 7f,
                        1.7f + powers.Count(PowerupType.FireballRadius) * 0.4f); break;
                case AbilityType.FrostNova:
                    AreaAttack(transform.position, 3.1f, damage, DamageElement.Ice, definition.Color,
                        3f + powers.Count(PowerupType.FrostDuration) + (rank - 1) * 0.5f); break;
                case AbilityType.Blink:
                    Dash(aim, 4f + powers.Count(PowerupType.BlinkDistance) * 0.5f);
                    AreaAttack(transform.position, 1.7f, Player.Damage + rank, DamageElement.Ice, definition.Color, 2f); break;
                case AbilityType.FanOfKnives:
                    Fan(aim, 12 + powers.Count(PowerupType.KnifeCount) * 2, 360f / (12 + powers.Count(PowerupType.KnifeCount) * 2), Player.Damage + rank); break;
                case AbilityType.VenomStrike:
                    foreach (var enemy in Player.Run.Enemies.ToArray())
                        if (InArea(enemy, transform.position, 2.5f)) enemy.Burn(4 + powers.Count(PowerupType.VenomDuration), rank, AbilityCatalog.Green);
                    AreaAttack(transform.position, 2.5f, damage, DamageElement.Physical, definition.Color); break;
                case AbilityType.ShadowVeil:
                    Player.Protect(1.5f + (rank - 1) * 0.3f + powers.Count(PowerupType.VeilDuration) * 0.4f); break;
                case AbilityType.HealingLight:
                    ForAllies(ally => ally.Heal(2 + rank - 1 + powers.Count(PowerupType.HealingPower))); break;
                case AbilityType.Judgment:
                    AreaAttack(transform.position, 3f, damage + powers.Count(PowerupType.JudgmentPower) * 2,
                        DamageElement.Physical, definition.Color, 2f); break;
                case AbilityType.Sanctuary:
                    ForAllies(ally => ally.Protect(2f + (rank - 1) * 0.3f + powers.Count(PowerupType.SanctuaryDuration) * 0.4f)); break;
                case AbilityType.Eclipse:
                case AbilityType.SoulRend:
                case AbilityType.ShadowReign:
                    var admin = Player.GetComponent<AdminAttack>();
                    if (admin == null || !admin.CastRelic(definition.Type, aim, rank)) return false;
                    break;
            }
            CombatVfx.Ring(Player.Run.ProjectileRoot, transform.position, 0.65f, definition.Color);
            if (Player.ClassWeapon != WeaponType.Shadow)
            {
                HeroVfx.Pulse(Player.Run.ProjectileRoot, transform.position, 1.1f, definition.Color, 0.35f);
                HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, definition.Color, 10, 3.5f, 0.35f);
            }
            readyAt[slot] = Time.time + definition.Cooldown;
            castReadyAt = Time.time + 0.2f;
            return true;
        }

        private void ForAllies(System.Action<DungeonPlayer> action)
        {
            foreach (var ally in FindObjectsByType<DungeonPlayer>())
                if (ally.Run == Player.Run && Vector2.Distance(transform.position, ally.transform.position) <= 4f)
                {
                    action(ally);
                    HeroVfx.Motes(Player.Run.ProjectileRoot, ally.transform.position, 0.7f, AbilityCatalog.Gold, 14, 1f);
                }
            CombatVfx.Ring(Player.Run.ProjectileRoot, transform.position, 4f, AbilityCatalog.Gold);
        }

        private void Fan(Vector2 aim, int count, float spacing, int damage)
        {
            for (int i = 0; i < count; i++)
                PlayerProjectile.Spawn(Player.Run, transform.position,
                    Quaternion.Euler(0, 0, (i - (count - 1) * 0.5f) * spacing) * aim, damage);
        }

        public static bool FindShadowstepLanding(DungeonMap map, Vector2 from, Vector2 aim, float distance, out Vector2 landing)
        {
            landing = from;
            if (map == null || aim.sqrMagnitude < 0.001f || distance < 0.15f) return false;
            aim.Normalize();
            int steps = Mathf.CeilToInt(distance / 0.1f);
            // Only the destination must be clear: intervening walls are intentionally ignored.
            for (int i = steps; i > 0; i--)
            {
                float travel = distance * i / steps;
                if (travel < 0.15f) break;
                Vector2 candidate = from + aim * travel;
                if (!map.CanStand(candidate)) continue;
                landing = candidate;
                return true;
            }
            return false;
        }

        public bool Shadowstep(Vector2 aim, float distance = 3f)
        {
            if (Player.ClassWeapon != WeaponType.Daggers || !Player.Run.IsPlaying) return false;
            Vector2 from = transform.position;
            if (!FindShadowstepLanding(Player.Run.Map, from, aim, distance, out Vector2 landing)) return false;
            transform.position = landing;
            Player.Protect(0.35f);
            Vector2 travel = landing - from;
            var color = new Color(0.7f, 0.35f, 1f);
            CombatVfx.Bolt(Player.Run.ProjectileRoot, from, landing, color);
            CombatVfx.Ring(Player.Run.ProjectileRoot, from, 0.45f, color, 0.2f);
            CombatVfx.Ring(Player.Run.ProjectileRoot, landing, 0.6f, color, 0.3f);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, landing, color, 10, 3.2f, 0.3f);
            foreach (var enemy in Player.Run.Enemies.ToArray())
            {
                Vector2 position = enemy.transform.position;
                float along = Mathf.Clamp01(Vector2.Dot(position - from, travel) / travel.sqrMagnitude);
                Vector2 closest = from + travel * along;
                float radius = enemy.HitRadius + 0.14f;
                if (enemy.Health <= 0 || (position - closest).sqrMagnitude > radius * radius) continue;
                CombatVfx.Bolt(Player.Run.ProjectileRoot, position + new Vector2(-0.4f, -0.5f),
                    position + new Vector2(0.4f, 0.5f), color);
                CombatDamage.ApplyShadowstep(Player, enemy);
            }
            return true;
        }

        public void Dash(Vector2 aim, float distance, int damage = 0)
        {
            Vector2 from = transform.position;
            var hit = new HashSet<DungeonEnemy>();
            int steps = Mathf.CeilToInt(distance / 0.15f);
            for (int i = 0; i < steps; i++)
            {
                transform.position = Player.Run.Map.Move(transform.position, aim.normalized * (distance / steps));
                if (damage <= 0) continue;
                foreach (var enemy in Player.Run.Enemies.ToArray())
                    if (!hit.Contains(enemy) && InArea(enemy, transform.position, 0.9f))
                    {
                        hit.Add(enemy);
                        CombatDamage.Apply(Player, enemy, damage, DamageElement.Physical, from);
                    }
            }
            Player.Protect(0.35f);
            CombatVfx.GlowBolt(Player.Run.ProjectileRoot, from, transform.position, AbilityCatalog.Ice);
            HeroVfx.Sparks(Player.Run.ProjectileRoot, transform.position, AbilityCatalog.Ice, 8, 3f, 0.3f, (Vector2)transform.position - from, 120f);
        }

        private bool InArea(DungeonEnemy enemy, Vector2 center, float radius) => enemy.Health > 0
            && Vector2.Distance(center, enemy.transform.position) <= radius + enemy.HitRadius
            && Player.Run.HasLineOfSight(center, enemy.transform.position);

        public void AreaAttack(Vector2 center, float radius, int damage, DamageElement element, Color color, float slow = 0f)
        {
            CombatVfx.Ring(Player.Run.ProjectileRoot, center, radius, color);
            HeroVfx.Pulse(Player.Run.ProjectileRoot, center, radius, color, 0.4f);
            foreach (var enemy in Player.Run.Enemies.ToArray())
                if (InArea(enemy, center, radius))
                {
                    CombatDamage.Apply(Player, enemy, damage, element, center);
                    if (enemy.Health > 0 && slow > 0f) enemy.Chill(slow);
                }
        }
    }
}

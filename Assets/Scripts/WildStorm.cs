using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Wizard's class mechanic: every elemental effect he sets off (burn, freeze or shock) charges it. After 8
    /// a wild storm gathers over him for a while and keeps striking the nearest enemy, taking turns hurling a
    /// fireball, a lightning bolt and an ice bolt. Every storm strike is guaranteed to set off its element.
    /// </summary>
    public sealed class WildStorm : ChargedMechanic
    {
        public const int EffectsNeeded = 8;
        public const float Duration = 10f, Interval = 0.4f, Range = 8f, Height = 1.3f;
        public static readonly Color CloudColor = new Color(0.32f, 0.34f, 0.46f);
        private static Sprite cloudSprite;
        private float stormUntil;
        private int turn;
        public bool IsRaging => Time.time < stormUntil;
        public override string Name => "Wild Storm";
        public override Color Color => AbilityCatalog.Ice;
        public override int Required => EffectsNeeded;
        public override string Status => IsRaging ? "STORM  " + Seconds(stormUntil - Time.time) : base.Status;

        public override void OnElementalEffect() { if (!IsRaging) AddCharge(1); }

        protected override bool Activate(Vector2 aim)
        {
            if (IsRaging) return false;
            stormUntil = Time.time + Duration;
            StartCoroutine(Storm());
            return true;
        }

        private Vector2 CloudPosition => (Vector2)transform.position + Vector2.up * Height;

        private IEnumerator Storm()
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            var cloud = DungeonVisuals.Create("Wild storm", root, CloudPosition, Vector2.one, Color.white, 8);
            cloud.sprite = CloudSprite;
            HeroVfx.Pulse(root, CloudPosition, 1.6f, CloudColor, 0.4f);
            CoopFx.Pulse(run, CloudPosition, 1.6f, CloudColor, 0.4f);
            float nextStrike = Time.time + 0.3f;
            while (Time.time < stormUntil)
            {
                if (!run.IsPlaying || root != run.ProjectileRoot || Player.Health <= 0) break;
                Vector2 origin = CloudPosition;
                cloud.transform.position = origin + new Vector2(Mathf.Sin(Time.time * 2f) * 0.08f, Mathf.Sin(Time.time * 3.1f) * 0.05f);
                // A flicker of inner lightning keeps the cloud alive between strikes.
                cloud.color = Color.Lerp(Color.white, new Color(0.8f, 0.9f, 1.4f), Random.value < 0.06f ? 1f : 0f);
                if (Time.time >= nextStrike)
                {
                    nextStrike = Time.time + Interval;
                    var target = FindTarget(origin);
                    if (target != null) Strike(origin, target);
                }
                yield return null;
            }
            stormUntil = Mathf.Min(stormUntil, Time.time);
            if (cloud != null)
            {
                if (root != null) HeroVfx.Motes(root, cloud.transform.position, 0.8f, CloudColor, 12, 0.6f);
                Destroy(cloud.gameObject);
            }
        }

        private DungeonEnemy FindTarget(Vector2 origin)
        {
            DungeonEnemy best = null;
            float nearest = Range;
            foreach (var enemy in Player.Run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.IsInvulnerable) continue;
                float distance = Vector2.Distance(origin, enemy.transform.position);
                if (distance > nearest || !Player.Run.HasLineOfSight(transform.position, enemy.transform.position)) continue;
                nearest = distance;
                best = enemy;
            }
            return best;
        }

        /// <summary>Takes turns: a fireball, a lightning bolt, then an ice bolt.</summary>
        private void Strike(Vector2 origin, DungeonEnemy target)
        {
            var run = Player.Run;
            Vector2 at = target.transform.position;
            Vector2 aim = (at - origin).normalized;
            int damage = Player.Damage + 2;
            switch (turn++ % 3)
            {
                case 0:
                    SpellProjectile.Spawn(Player, aim, damage, DamageElement.Fire, Color.white, Range + 1f, 0f, 0, origin, true);
                    break;
                case 1:
                    CombatVfx.GlowBolt(run.ProjectileRoot, origin, at, AbilityCatalog.Ice);
                    CoopFx.Bolt(run, origin, at, AbilityCatalog.Ice, true);
                    HeroVfx.Sparks(run.ProjectileRoot, at, Color.Lerp(AbilityCatalog.Ice, Color.white, 0.4f), 7, 4f, 0.25f);
                    CombatDamage.Apply(Player, target, damage, DamageElement.Lightning, origin, guaranteedEffect: true);
                    break;
                default:
                    SpellProjectile.Spawn(Player, aim, damage, DamageElement.Ice, AbilityCatalog.Ice, Range + 1f, 0f, 0, origin, true);
                    break;
            }
        }

        /// <summary>A small pixel-art thundercloud.</summary>
        private static Sprite CloudSprite
        {
            get
            {
                if (cloudSprite != null) return cloudSprite;
                string[] rows =
                {
                    "....DDDD..DD....", "..DDLLLLDDLLD...", ".DLLLWWLLLWWLD..", "DLWWWWWWWWWWWLD.",
                    "DWWWMMWWWWMMWWD.", ".DMMMMMMMMMMMD..", "..DD.YD.DY.DD...", "......Y...Y.....",
                };
                int width = rows[0].Length, height = rows.Length;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        char c = rows[y][x];
                        Color color = c == 'D' ? new Color(0.14f, 0.15f, 0.22f) : c == 'M' ? new Color(0.28f, 0.3f, 0.4f)
                            : c == 'W' ? CloudColor : c == 'L' ? new Color(0.5f, 0.53f, 0.66f) : c == 'Y' ? new Color(0.85f, 0.95f, 1f) : Color.clear;
                        texture.SetPixel(x, height - y - 1, color);
                    }
                texture.Apply(false, true);
                return cloudSprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, width / 1.8f);
            }
        }
    }
}

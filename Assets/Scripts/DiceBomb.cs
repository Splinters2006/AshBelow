using UnityEngine;

namespace Slopgame
{
    /// <summary>Dice Bomb: a pair of dice tossed to the cursor. Each rolls as it lands and explodes for its face times the damage.</summary>
    public sealed class DiceBomb : MonoBehaviour
    {
        public const float Range = 6f, Fuse = 0.9f, BlastRadius = 1.4f;
        private static readonly Sprite[] faces = new Sprite[6];
        private DungeonPlayer player;
        private SpriteRenderer body;
        private int damage, face;
        private float explodeAt;

        /// <summary>A white die showing 1 to 6 pips.</summary>
        private static Sprite Face(int value)
        {
            if (faces[value - 1] != null) return faces[value - 1];
            string[] pips =
            {
                ".......\n.......\n.......\n...o...\n.......\n.......\n.......",
                ".......\n.o.....\n.......\n.......\n.......\n.....o.\n.......",
                ".......\n.o.....\n.......\n...o...\n.......\n.....o.\n.......",
                ".......\n.o...o.\n.......\n.......\n.......\n.o...o.\n.......",
                ".......\n.o...o.\n.......\n...o...\n.......\n.o...o.\n.......",
                ".......\n.o...o.\n.......\n.o...o.\n.......\n.o...o.\n.......",
            };
            var rows = pips[value - 1].Split('\n');
            for (int i = 0; i < rows.Length; i++) rows[i] = rows[i].Replace('.', 'W');
            return faces[value - 1] = DungeonVisuals.PaletteSprite("Die " + value, rows, key => key == 'W' ? new Color(0.97f, 0.95f, 0.9f) : key == 'o' ? new Color(0.7f, 0.1f, 0.12f) : Color.clear);
        }

        public static void Toss(DungeonPlayer player, Vector2 target, int damage)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 spot = target + new Vector2(i == 0 ? -0.35f : 0.35f, Random.Range(-0.2f, 0.2f));
                if (!player.Run.Map.CanStand(spot, 0.1f)) spot = target;
                var sprite = DungeonVisuals.Create("Die", player.Run.ProjectileRoot, spot, Vector2.one * 0.34f, Color.white, 6);
                var die = sprite.gameObject.AddComponent<DiceBomb>();
                die.player = player;
                die.body = sprite;
                die.damage = damage;
                die.face = Random.Range(1, 7);
                die.explodeAt = Time.time + Fuse + i * 0.12f;
                CombatVfx.Ring(player.Run.ProjectileRoot, spot, BlastRadius, FlameMesh.Alpha(GamblerAttack.Gold, 0.5f), Fuse);
                CoopFx.Ring(player.Run, spot, BlastRadius, FlameMesh.Alpha(GamblerAttack.Gold, 0.5f), Fuse);
            }
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null) { Destroy(gameObject); return; }
            // It tumbles through random faces, then settles on its roll just before it goes off.
            bool settled = Time.time >= explodeAt - 0.3f;
            body.sprite = Face(settled ? face : 1 + (int)(Time.time * 20f) % 6);
            transform.rotation = Quaternion.Euler(0, 0, settled ? 0f : Time.time * 600f);
            if (!run.IsPlaying || Time.time < explodeAt) return;
            Vector2 at = transform.position;
            HeroVfx.Pulse(run.ProjectileRoot, at, BlastRadius, GamblerAttack.Gold, 0.35f);
            HeroVfx.Sparks(run.ProjectileRoot, at, GamblerAttack.Gold, 8 + face * 3, 4f + face * 0.5f, 0.4f);
            CoopFx.Pulse(run, at, BlastRadius, GamblerAttack.Gold, 0.35f);
            ScreenFx.Shake(0.05f * face, 0.15f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= BlastRadius + enemy.HitRadius)
                    CombatDamage.Apply(player, enemy, damage * face, DamageElement.Physical, at, 1f);
            Destroy(gameObject);
        }
    }
}

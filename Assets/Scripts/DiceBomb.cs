using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Dice Bomb: the Gambler throws a pair of dice at the cursor. Each one sails up out of her hand in an arc,
    /// tumbling through its faces over its shadow, hits the floor, bounces twice and rolls to a stop showing its roll.
    /// A short fuse later it explodes for its face times the damage. Teammates see the same dice thrown; only the
    /// thrower's machine deals the damage.
    /// </summary>
    public sealed class DiceBomb : MonoBehaviour
    {
        public const float Range = 6f, Fuse = 0.45f, BlastRadius = 1.4f, Stagger = 0.12f, DieSize = 0.36f;
        private const float BounceTime = 0.28f, RollDistance = 0.45f;
        private static readonly Sprite[] faces = new Sprite[6];
        private DungeonRun run;
        private DungeonPlayer player;
        private SpriteRenderer body, shadow;
        private Vector2 from, landing, rest;
        private int damage, face;
        private float age, delay, flight, height, landedAt = -1f;
        private bool ghost;

        /// <summary>A white die showing 1 to 6 pips, with a dark outline and its lower and right edges shaded.</summary>
        internal static Sprite Face(int value)
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
            var inner = pips[value - 1].Split('\n');
            var rows = new string[9];
            rows[0] = rows[8] = ".OOOOOOO.";
            for (int y = 0; y < 7; y++)
            {
                var row = inner[y].ToCharArray();
                for (int x = 0; x < 7; x++)
                    if (row[x] == '.') row[x] = y == 6 || x == 6 ? 'D' : y == 0 || x == 0 ? 'L' : 'W';
                rows[y + 1] = "O" + new string(row) + "O";
            }
            return faces[value - 1] = DungeonVisuals.PaletteSprite("Die " + value, rows, key => key switch
            {
                'O' => new Color(0.3f, 0.26f, 0.24f),
                'W' => new Color(0.97f, 0.95f, 0.9f),
                'L' => Color.white,
                'D' => new Color(0.78f, 0.74f, 0.7f),
                'o' => new Color(0.7f, 0.1f, 0.12f),
                _ => Color.clear
            });
        }

        public static void Toss(DungeonPlayer player, Vector2 target, int damage)
        {
            var run = player.Run;
            Vector2 origin = player.transform.position;
            for (int i = 0; i < 2; i++)
            {
                Vector2 spot = target + new Vector2(i == 0 ? -0.35f : 0.35f, Random.Range(-0.2f, 0.2f));
                if (!run.Map.CanStand(spot, 0.1f)) spot = target;
                int face = Random.Range(1, 7);
                float delay = i * Stagger;
                var die = Create(run, origin, spot, face, delay);
                die.player = player;
                die.damage = damage;
                CoopFx.Dice(run, origin, spot, face, delay);
            }
        }

        public static void SpawnGhost(DungeonRun run, Vector2 origin, Vector2 landing, int face, float delay)
            => Create(run, origin, landing, Mathf.Clamp(face, 1, 6), delay).ghost = true;

        private static DiceBomb Create(DungeonRun run, Vector2 origin, Vector2 landing, int face, float delay)
        {
            var body = DungeonVisuals.Create("Die", run.ProjectileRoot, origin, Vector2.one * DieSize, Color.white, 7);
            body.sprite = Face(1 + Random.Range(0, 6));
            var die = body.gameObject.AddComponent<DiceBomb>();
            die.run = run;
            die.body = body;
            die.from = origin;
            die.landing = landing;
            die.face = face;
            die.delay = delay;
            float distance = Vector2.Distance(origin, landing);
            die.flight = 0.28f + distance * 0.05f;
            die.height = 0.9f + distance * 0.15f;
            // After landing it bounces on a little way along its throw, if the floor allows.
            Vector2 throwDir = distance > 0.01f ? (landing - origin) / distance : Vector2.right;
            Vector2 rolled = landing + throwDir * RollDistance;
            die.rest = run.Map.CanStand(rolled, 0.1f) ? rolled : landing;
            die.shadow = DungeonVisuals.Create("Die shadow", run.ProjectileRoot, origin, new Vector2(DieSize * 1.2f, DieSize * 0.5f), Color.white, 2);
            die.shadow.sprite = DungeonVisuals.CoinShadow;
            body.enabled = die.shadow.enabled = delay <= 0f;
            return die;
        }

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null || (!ghost && player == null)) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            age += Time.deltaTime;
            float t = age - delay;
            if (t < 0f) return;
            body.enabled = shadow.enabled = true;
            Vector2 ground;
            float lift;
            if (t < flight)
            {
                // In the air: an arc over its shadow, tumbling fast through its faces.
                float u = t / flight;
                ground = Vector2.Lerp(from, landing, u);
                lift = Mathf.Sin(u * Mathf.PI) * height;
                body.sprite = Face(1 + (int)(age * 22f + face) % 6);
                transform.rotation = Quaternion.Euler(0, 0, age * 900f);
            }
            else if (t < flight + BounceTime)
            {
                // Two bounces, each lower, rolling on toward where it will rest; a new face shows on each hop.
                float u = (t - flight) / BounceTime;
                if (landedAt < 0f) Land();
                ground = Vector2.Lerp(landing, rest, 1f - (1f - u) * (1f - u));
                bool first = u < 0.6f;
                float hop = first ? u / 0.6f : (u - 0.6f) / 0.4f;
                lift = Mathf.Sin(hop * Mathf.PI) * (first ? 0.28f : 0.1f);
                body.sprite = Face(u < 0.8f ? 1 + (face + (first ? 2 : 4)) % 6 : face);
                transform.rotation = Quaternion.Euler(0, 0, (1f - u) * 200f * (first ? 1f : 0.5f));
            }
            else
            {
                // At rest, showing its roll; it shivers faster as the fuse burns down.
                ground = rest;
                float fuse = (t - flight - BounceTime) / Fuse;
                lift = 0f;
                body.sprite = Face(face);
                transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(age * Mathf.Lerp(20f, 60f, fuse)) * 8f * fuse);
                body.color = Color.Lerp(Color.white, new Color(1f, 0.75f, 0.5f), Mathf.Abs(Mathf.Sin(age * Mathf.Lerp(8f, 30f, fuse))) * fuse);
                if (fuse >= 1f) { Explode(rest); return; }
            }
            transform.position = ground + Vector2.up * lift;
            shadow.transform.position = ground + Vector2.down * DieSize * 0.45f;
            float shrink = 1f - Mathf.Clamp01(lift / (height + 0.01f)) * 0.5f;
            shadow.transform.localScale = new Vector3(DieSize * 1.2f * shrink, DieSize * 0.5f * shrink, 1f);
        }

        /// <summary>First touchdown: a clack of dust and the blast ring marked out for the fuse.</summary>
        private void Land()
        {
            landedAt = Time.time;
            HeroVfx.Sparks(run.ProjectileRoot, landing, new Color(0.8f, 0.75f, 0.65f, 0.7f), 5, 2f, 0.2f, Vector2.up, 160f, 0.6f);
            CombatVfx.Ring(run.ProjectileRoot, rest, BlastRadius, FlameMesh.Alpha(GamblerAttack.Gold, 0.5f), BounceTime + Fuse);
        }

        private void Explode(Vector2 at)
        {
            HeroVfx.Pulse(run.ProjectileRoot, at, BlastRadius, GamblerAttack.Gold, 0.35f);
            HeroVfx.Sparks(run.ProjectileRoot, at, GamblerAttack.Gold, 8 + face * 3, 4f + face * 0.5f, 0.4f);
            HeroVfx.Sparks(run.ProjectileRoot, at, Color.white, 4 + face, 3f, 0.25f, null, 360f, 0.7f);
            if (!ghost) ScreenFx.Shake(0.05f * face, 0.15f);
            if (!ghost)
                foreach (var enemy in run.Enemies.ToArray())
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= BlastRadius + enemy.HitRadius)
                        CombatDamage.Apply(player, enemy, damage * face, DamageElement.Physical, at, 1f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (shadow != null) Destroy(shadow.gameObject);
        }
    }
}

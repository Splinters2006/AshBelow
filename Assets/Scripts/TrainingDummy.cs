using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A straw training dummy in the crystal shop, there to try a build on. In co-op each machine has its own, which the
    /// party never syncs: every hero only sees the hits they land themselves. It is an enemy every attack can reach, but it
    /// never moves, fights back or dies, keeps the stairs open, and shows each hit it takes as a number. Left alone for a
    /// few seconds, it patches itself back to full health.
    /// </summary>
    /// <remarks>Added before its <see cref="DungeonEnemy"/>, so the enemy knows from its first frame that it is a dummy.</remarks>
    public sealed class TrainingDummy : MonoBehaviour
    {
        public const int Count = 3, DummyHealth = 99999;
        /// <summary>How wide the dummy stands; its sprite is a little taller than it is wide.</summary>
        public const float Size = 1.15f, Radius = 0.55f;
        public const float RestoreAfter = 3f;
        public static readonly Color Straw = new Color(0.86f, 0.72f, 0.42f);
        private static Sprite sprite;

        private DungeonEnemy Enemy => enemy != null ? enemy : enemy = GetComponent<DungeonEnemy>();
        private DungeonEnemy enemy;
        private Vector2 home;
        private float lastHitAt = float.NegativeInfinity;

        /// <summary>Stands the shop's dummies in a row along its front wall.</summary>
        public static void PlaceIn(DungeonRun run, Transform level)
        {
            if (run == null) return;
            var room = DungeonMap.ShopRoom;
            float middle = (room.xMin + room.xMax - 1) / 2f, y = room.yMin + 0.9f;
            for (int i = 0; i < Count; i++)
                Create(run, level, new Vector2(middle + (i - (Count - 1) / 2f) * 3f, y));
        }

        /// <summary>
        /// Stands <paramref name="count"/> dummies evenly around <paramref name="center"/>, the first straight above it
        /// (five make a pentagon: the testing grounds).
        /// </summary>
        public static void PlaceRing(DungeonRun run, Transform level, Vector2 center, int count, float radius)
        {
            if (run == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                float angle = (90f + 360f * i / count) * Mathf.Deg2Rad;
                Create(run, level, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        /// <summary>Stands one dummy at <paramref name="position"/> (the co-op party hall's; each machine has its own).</summary>
        public static void Place(DungeonRun run, Transform level, Vector2 position)
        {
            if (run != null) Create(run, level, position);
        }

        private static void Create(DungeonRun run, Transform level, Vector2 position)
        {
            var body = DungeonVisuals.Create("Training dummy", level, position, Vector2.one * Size, Color.white, 3);
            body.sprite = Sprite;
            body.gameObject.AddComponent<TrainingDummy>();
            var enemy = body.gameObject.AddComponent<DungeonEnemy>();
            enemy.Run = run;
            enemy.Health = DummyHealth;
            enemy.Speed = 0f;
            run.Enemies.Add(enemy);
            var shadow = DungeonVisuals.Create("Dummy shadow", body.transform, position + Vector2.down * 0.78f, new Vector2(0.6f, 0.12f), new Color(0f, 0f, 0f, 0.35f), 2);
            shadow.transform.localScale = new Vector3(0.75f, 0.12f, 1f);
        }

        private void Awake() => home = transform.position;

        /// <summary>The dummy took <paramref name="damage"/> (a damage-over-time tick if <paramref name="tick"/>).</summary>
        public void Struck(int damage, bool tick)
        {
            lastHitAt = Time.time;
            DamageNumbers.Show((Vector2)transform.position + Vector2.up * 0.35f, damage, tick ? DamageNumbers.TickColor : DamageNumbers.HitColor);
            // It can never fall: a blow that would empty it just patches it up again.
            if (Enemy.Health - damage <= 0) Enemy.Health = DummyHealth + damage;
        }

        private void LateUpdate()
        {
            // Pulls, shoves and knockback never move it for long: it stays planted where the shop stood it.
            transform.position = home;
            if (Enemy != null && Enemy.Health < DummyHealth && Time.time - lastHitAt >= RestoreAfter) Enemy.Health = DummyHealth;
        }

        /// <summary>
        /// A burlap dummy on a post: a stitched head with cross eyes under a straw tuft, a rope collar, a crossbar for arms
        /// with straw spilling from the ends, a painted bullseye on its chest and a straw skirt, all on a plank stand.
        /// </summary>
        private static Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite("Training dummy", new[]
        {
            "........H.Hs.H..........", ".......sHsHsHs..........", "......OOOOOOOOOO........", ".....OBBBBBBBBbbO.......",
            "....OBBBBBBBBBBbbO......", "....OBKBKBBBKBKbbO......", "....OBBKBBBBBKBbbO......", "....OBKBKBBBKBKbbO......",
            "....OBBBBBBBBBBbbO......", "....OBBBKKKKKBBbbO......", ".....OBBBBBBBBbbO.......", "......OOYYYYYYOO........",
            "..H...OYyYyYyYyO....H...", ".sHOOOOOOOOOOOOOOOOOOsH.", "HsSPPPOBBBBBBBBbbOPPPSsH", ".HspppOBBRRRRRRbbOpppsH.",
            "..s..OBBRRWWWWRRbbO..s..", ".....OBRRWWRRWWRRbO.....", ".....OBRWWRRRRWWRbO.....", ".....OBRWRRRRRRWRbO.....",
            ".....OBRWWRRRRWWRbO.....", ".....OBRRWWRRWWRRbO.....", ".....OBBRRWWWWRRbbO.....", ".....OBBBRRRRRRbbbO.....",
            ".....OsSsSHsSsSHsSO.....", "......OsHsSOOsSHsO......", ".......OOO.PP.OOO.......", "...........PP...........",
            "..........OPpO..........", "..........OPpO..........", ".......OOOPPPpOOO.......", "......OGGGGGGGGGGO......",
            ".....OGgGgGgGgGgGgO.....", "......OOOOOOOOOOOO......"
        }, key => key switch
        {
            'O' => new Color(0.3f, 0.2f, 0.1f),
            'S' => Straw,
            's' => new Color(0.69f, 0.54f, 0.27f),
            'H' => new Color(0.96f, 0.87f, 0.59f),
            'B' => new Color(0.76f, 0.63f, 0.44f),
            'b' => new Color(0.59f, 0.46f, 0.31f),
            'K' => new Color(0.24f, 0.14f, 0.09f),
            'R' => new Color(0.78f, 0.18f, 0.16f),
            'W' => new Color(0.95f, 0.92f, 0.85f),
            'P' => new Color(0.48f, 0.3f, 0.15f),
            'p' => new Color(0.33f, 0.2f, 0.1f),
            'Y' => new Color(0.84f, 0.75f, 0.47f),
            'y' => new Color(0.63f, 0.53f, 0.31f),
            'G' => new Color(0.43f, 0.31f, 0.2f),
            'g' => new Color(0.31f, 0.23f, 0.14f),
            _ => Color.clear
        });
    }
}

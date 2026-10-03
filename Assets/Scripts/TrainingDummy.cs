using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A straw training dummy in the crystal shop, there to try a build on. It is an enemy every attack can reach, but it
    /// never moves, fights back or dies, keeps the stairs open, and shows each hit it takes as a number. Left alone for a
    /// few seconds, it patches itself back to full health.
    /// </summary>
    /// <remarks>Added before its <see cref="DungeonEnemy"/>, so the enemy knows from its first frame that it is a dummy.</remarks>
    public sealed class TrainingDummy : MonoBehaviour
    {
        public const int Count = 3, DummyHealth = 99999;
        public const float RestoreAfter = 3f;
        public static readonly Color Straw = new Color(0.86f, 0.72f, 0.42f);
        private static Sprite sprite;

        private DungeonEnemy Enemy => enemy != null ? enemy : enemy = GetComponent<DungeonEnemy>();
        private DungeonEnemy enemy;
        private Vector2 home;
        private float lastHitAt = float.NegativeInfinity;

        /// <summary>Stands the shop's dummies in a row along its front wall. Solo descents only: co-op does not sync them.</summary>
        public static void PlaceIn(DungeonRun run, Transform level)
        {
            if (run == null || run.IsNetworked) return;
            var room = DungeonMap.ShopRoom;
            float middle = (room.xMin + room.xMax - 1) / 2f, y = room.yMin + 0.6f;
            for (int i = 0; i < Count; i++)
                Create(run, level, new Vector2(middle + (i - (Count - 1) / 2f) * 3f, y));
        }

        private static void Create(DungeonRun run, Transform level, Vector2 position)
        {
            var body = DungeonVisuals.Create("Training dummy", level, position, Vector2.one * 0.75f, Color.white, 3);
            body.sprite = Sprite;
            body.gameObject.AddComponent<TrainingDummy>();
            var enemy = body.gameObject.AddComponent<DungeonEnemy>();
            enemy.Run = run;
            enemy.Health = DummyHealth;
            enemy.Speed = 0f;
            run.Enemies.Add(enemy);
            var shadow = DungeonVisuals.Create("Dummy shadow", body.transform, position + Vector2.down * 0.42f, new Vector2(0.6f, 0.12f), new Color(0f, 0f, 0f, 0.35f), 2);
            shadow.transform.localScale = new Vector3(0.8f, 0.16f, 1f);
        }

        private void Awake() => home = transform.position;

        /// <summary>The dummy took <paramref name="damage"/> (a damage-over-time tick if <paramref name="tick"/>).</summary>
        public void Struck(int damage, bool tick)
        {
            lastHitAt = Time.time;
            DamageNumbers.Show(transform.position, damage, tick ? DamageNumbers.TickColor : DamageNumbers.HitColor);
            // It can never fall: a blow that would empty it just patches it up again.
            if (Enemy.Health - damage <= 0) Enemy.Health = DummyHealth + damage;
        }

        private void LateUpdate()
        {
            // Pulls, shoves and knockback never move it for long: it stays planted where the shop stood it.
            transform.position = home;
            if (Enemy != null && Enemy.Health < DummyHealth && Time.time - lastHitAt >= RestoreAfter) Enemy.Health = DummyHealth;
        }

        /// <summary>A straw sack on a post, with a painted target on its chest.</summary>
        private static Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite("Training dummy", new[]
        {
            "....OOOO....", "...OSSSSO...", "...OSKSKO...", "...OSSSSO...", "....OOOO....", "..OOORROOO..",
            ".OSSRWWRSSO.", ".OSSRWRRSSO.", "..OSRRRRSO..", "..OSSSSSSO..", "...OOPPOO...", ".....PP.....",
            ".....PP.....", "....OPPO....", "...OOOOOO..."
        }, key => key switch
        {
            'O' => new Color(0.3f, 0.2f, 0.1f),
            'S' => Straw,
            'K' => new Color(0.2f, 0.12f, 0.08f),
            'R' => new Color(0.78f, 0.18f, 0.16f),
            'W' => new Color(0.95f, 0.92f, 0.85f),
            'P' => new Color(0.48f, 0.3f, 0.15f),
            _ => Color.clear
        });
    }
}

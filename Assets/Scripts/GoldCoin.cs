using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A gold coin dropped by a slain enemy. Only a Gambler sees them (each machine drops coins for its own hero);
    /// walking close draws the coin in and adds it to his purse.
    /// </summary>
    public sealed class GoldCoin : MonoBehaviour
    {
        public const float MagnetRadius = 1.8f, PickupRadius = 0.45f;
        private DungeonRun run;
        private Vector2 rest;
        private float phase;

        public static GoldCoin Drop(DungeonRun run, Vector2 position)
        {
            if (run == null || run.ProjectileRoot == null) return null;
            var sprite = DungeonVisuals.Create("Gold coin", run.ProjectileRoot, position, new Vector2(0.24f, 0.24f), GamblerAttack.Gold, 5);
            var coin = sprite.gameObject.AddComponent<GoldCoin>();
            coin.run = run;
            coin.rest = position;
            coin.phase = Random.value * Mathf.PI * 2f;
            return coin;
        }

        private void Update()
        {
            if (run == null || !run.IsPlaying || run.Player == null) return;
            var player = run.Player;
            var purse = player.Weapon as GamblerAttack;
            Vector2 hero = player.transform.position;
            float distance = Vector2.Distance(rest, hero);
            if (purse != null && player.Health > 0 && distance <= MagnetRadius)
                rest = Vector2.MoveTowards(rest, hero, (7f - distance * 2f) * Time.deltaTime);
            transform.position = rest + Vector2.up * (0.06f * Mathf.Sin(Time.time * 4f + phase));
            // A coin spins by squashing its width.
            transform.localScale = new Vector3(0.24f * Mathf.Abs(Mathf.Cos(Time.time * 3f + phase)) + 0.04f, 0.24f, 1f);
            if (purse == null || player.Health <= 0 || Vector2.Distance(rest, hero) > PickupRadius) return;
            purse.AddCoins(1);
            HeroVfx.Sparks(run.ProjectileRoot, rest, GamblerAttack.Gold, 6, 2.5f, 0.25f);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}

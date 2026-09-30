using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Insurance: while the policy holds, a gold seal is stamped around the Gambler and four coins circle her inside a
    /// soft green-gold ward, blinking in its last moments before they pop. When a hit is paid for, the ward flashes and
    /// the premium visibly bursts out of her as coins; when she can't pay, the ward cracks red and the claim is denied
    /// (the policy itself holds, for when she can pay again).
    /// </summary>
    public sealed class InsuranceVfx : MonoBehaviour
    {
        public static readonly Color Policy = new Color(0.35f, 0.9f, 0.5f);
        private const int OrbitCoins = 4;
        private const float Radius = 0.62f, StampTime = 0.25f, WarnTime = 1.5f, ClaimFlash = 0.3f;
        private static readonly Dictionary<Transform, InsuranceVfx> active = new Dictionary<Transform, InsuranceVfx>();
        private Transform root, hero;
        private float age, until, claimAt = -10f;
        private bool claimPaid;
        private FlameMesh mesh;
        private readonly SpriteRenderer[] coins = new SpriteRenderer[OrbitCoins];

        /// <summary>Starts the policy's aura on <paramref name="hero"/>, or extends the one already there.</summary>
        public static void Play(Transform root, Transform hero, float duration)
        {
            if (root == null || hero == null) return;
            if (active.TryGetValue(hero, out var existing) && existing != null)
            {
                existing.until = Mathf.Max(existing.until, existing.age + duration);
                existing.age = Mathf.Min(existing.age, StampTime);
                return;
            }
            var aura = new GameObject("Insurance").AddComponent<InsuranceVfx>();
            aura.transform.SetParent(root, false);
            aura.root = root;
            aura.hero = hero;
            aura.until = duration;
            aura.mesh = new FlameMesh(aura.gameObject, 8);
            for (int i = 0; i < OrbitCoins; i++)
            {
                aura.coins[i] = DungeonVisuals.CreateCoin("Policy coin", aura.transform, hero.position, 0.22f, 9);
                aura.coins[i].enabled = false;
            }
            active[hero] = aura;
            HeroVfx.Motes(root, hero.position, 0.7f, GamblerAttack.Gold, 12, 0.8f);
        }

        /// <summary>
        /// A hit landed while insured. Paid: a gold flash and the premium thrown out as coins. Unpaid: the ward flashes
        /// red and cracks.
        /// </summary>
        public static void Claim(Transform root, Transform hero, bool paid)
        {
            if (root == null || hero == null) return;
            Vector2 at = hero.position;
            if (active.TryGetValue(hero, out var aura) && aura != null)
            {
                aura.claimAt = aura.age;
                aura.claimPaid = paid;
            }
            if (paid)
            {
                HeroVfx.Pulse(root, at, 1.1f, FlameMesh.Alpha(GamblerAttack.Gold, 0.8f), 0.25f);
                for (int i = 0; i < DungeonPlayer.InsurancePremium; i++)
                {
                    float angle = (60f + 60f * i / Mathf.Max(1, DungeonPlayer.InsurancePremium - 1) + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                    PaidCoin.Throw(root, at + Vector2.up * 0.1f, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(2.6f, 3.6f));
                }
                HeroVfx.Sparks(root, at, Color.white, 6, 3f, 0.2f, Vector2.up, 160f, 0.6f);
            }
            else
            {
                HeroVfx.Pulse(root, at, 0.9f, new Color(1f, 0.25f, 0.2f, 0.7f), 0.25f);
                HeroVfx.Sparks(root, at, new Color(1f, 0.3f, 0.25f), 10, 3.5f, 0.3f);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (hero == null || age >= until + 0.2f) { Pop(); Destroy(gameObject); return; }
            Vector2 center = (Vector2)hero.position + Vector2.up * 0.05f;
            float time = Time.time;
            // Fades in, and blinks as the policy runs out.
            float alpha = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((until + 0.2f - age) / 0.2f);
            float left = until - age;
            if (left < WarnTime) alpha *= 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(time * Mathf.Lerp(18f, 6f, left / WarnTime)));
            float claim = Mathf.Clamp01((age - claimAt) / ClaimFlash);
            float flash = claim < 1f ? 1f - claim : 0f;
            Color ward = claim < 1f && !claimPaid ? Color.Lerp(Policy, new Color(1f, 0.25f, 0.2f), flash) : Color.Lerp(Policy, GamblerAttack.Gold, flash);

            mesh.Begin();
            float radius = Radius * (1f + 0.12f * flash);
            // The ward: a soft dome with a bright rim.
            mesh.Disc(center, radius, FlameMesh.Alpha(ward, (0.05f + 0.25f * flash) * alpha), FlameMesh.Alpha(ward, (0.18f + 0.4f * flash) * alpha), 36);
            mesh.Ring(center, radius, 0.035f + 0.05f * flash, FlameMesh.Alpha(Color.Lerp(ward, Color.white, 0.3f), 0.7f * alpha), 40);
            // The seal: a gold ring stamped down from wide to snug, with tick marks like a coin's milled edge.
            float stamp = Mathf.Clamp01(age / StampTime);
            float sealRadius = Mathf.Lerp(radius * 2.2f, radius * 0.82f, 1f - (1f - stamp) * (1f - stamp));
            Color seal = FlameMesh.Alpha(GamblerAttack.Gold, (stamp < 1f ? 0.9f : 0.35f) * alpha);
            mesh.Ring(center, sealRadius, stamp < 1f ? 0.07f : 0.025f, seal, 40);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f + time * 0.8f;
                Vector2 dir = FlameMesh.Polar(a, 1f);
                mesh.Bar(center + dir * (sealRadius - 0.04f), dir, 0.08f, 0.02f, seal, seal);
            }
            // A denied claim cracks the ward.
            if (flash > 0f && !claimPaid)
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f + FlameMesh.Hash(i, 2.2f);
                    mesh.Bar(center, FlameMesh.Polar(a, 1f), radius * (0.6f + 0.4f * FlameMesh.Hash(i, 8.1f)), 0.04f,
                        FlameMesh.Alpha(Color.white, flash), FlameMesh.Alpha(new Color(1f, 0.25f, 0.2f), 0f));
                }
            mesh.Commit();

            // Four coins orbit on a tilted ring, flipping as they go and passing behind her on the far side.
            for (int i = 0; i < OrbitCoins; i++)
            {
                float a = time * 2.4f + i * Mathf.PI * 2f / OrbitCoins;
                var coin = coins[i];
                coin.enabled = alpha > 0.02f;
                coin.transform.position = center + new Vector2(Mathf.Cos(a) * radius * 1.05f, Mathf.Sin(a) * radius * 0.45f + 0.1f);
                float flip = Mathf.Abs(Mathf.Cos(time * 7f + i));
                coin.transform.localScale = new Vector3(0.22f * (0.2f + 0.8f * flip), 0.22f, 1f);
                bool behind = Mathf.Sin(a) > 0f;
                coin.sortingOrder = behind ? 3 : 9;
                coin.color = new Color(1f, 1f, 1f, alpha * (behind ? 0.7f : 1f));
            }
        }

        private void Pop()
        {
            if (root == null) return;
            foreach (var coin in coins)
                if (coin != null && coin.enabled) HeroVfx.Sparks(root, coin.transform.position, GamblerAttack.Gold, 4, 2f, 0.25f, null, 360f, 0.6f);
        }

        private void OnDestroy()
        {
            if (hero != null && active.TryGetValue(hero, out var aura) && aura == this) active.Remove(hero);
            mesh?.Release();
        }

        /// <summary>One coin of a paid premium: tossed up and out, it spins, falls back, bounces once and fades.</summary>
        private sealed class PaidCoin : MonoBehaviour
        {
            private const float Life = 0.8f, Gravity = 11f;
            private Vector2 velocity, ground;
            private float age, height, rise;
            private SpriteRenderer sprite;

            public static void Throw(Transform root, Vector2 from, Vector2 velocity)
            {
                var sprite = DungeonVisuals.CreateCoin("Premium coin", root, from, 0.2f, 9);
                var coin = sprite.gameObject.AddComponent<PaidCoin>();
                coin.sprite = sprite;
                coin.ground = from;
                // Split the toss into drift along the floor and a hop up off it.
                coin.velocity = new Vector2(velocity.x, velocity.y * 0.35f);
                coin.rise = Mathf.Max(2.5f, velocity.y * 1.1f);
            }

            private void Update()
            {
                float dt = Time.deltaTime;
                age += dt;
                ground += velocity * dt;
                velocity *= 1f - 2f * dt;
                rise -= Gravity * dt;
                height += rise * dt;
                if (height < 0f) { height = 0f; rise = -rise * 0.35f; }
                transform.position = ground + Vector2.up * height;
                float flip = Mathf.Abs(Mathf.Cos(age * 22f));
                transform.localScale = new Vector3(0.2f, 0.2f * (0.15f + 0.85f * flip), 1f);
                sprite.color = new Color(1f, 1f, 1f, Mathf.Clamp01((Life - age) / 0.25f));
                if (age >= Life) Destroy(gameObject);
            }
        }
    }
}

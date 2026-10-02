using System.Collections;
using UnityEngine;

namespace Slopgame
{
    /// <summary>The Gambler's demos: his coins, the Purse and his six artifacts.</summary>
    public sealed partial class AbilityDemo
    {
        private static Color CoinGold => GamblerAttack.Gold;

        private IEnumerator Gambler() => kind switch
        {
            DemoKind.Charged => GamblerCharged(),
            DemoKind.Heavy => CoinVolley(),
            DemoKind.Mechanic => PurseDemo(),
            _ => ability.Type switch
            {
                AbilityType.Windfall => Windfall(),
                AbilityType.AllIn => AllIn(),
                AbilityType.Jackpot => Jackpot(),
                AbilityType.CardToss => CardToss(),
                AbilityType.DiceBomb => DiceBombDemo(),
                AbilityType.Insurance => Insurance(),
                _ => null
            }
        };

        /// <summary>A flipped coin, then one he weighs in his hand first: it lands far harder.</summary>
        private IEnumerator GamblerCharged()
        {
            Set(5f, new Vector2(-3.6f, 0f), new Vector2(1.6f, 0f));
            yield return Wait(0.5f);
            Face(Vector2.right);
            Coin(HeroAt, Vector2.right, GamblerAttack.CoinRange);
            yield return Wait(1f);
            yield return Charge(1.2f, (mesh, charge) => Lane(mesh, HeroAt, Vector2.right, GamblerAttack.CoinRange, 0.07f, ChargeFill(charge, CoinGold)));
            var coin = Coin(HeroAt, Vector2.right, GamblerAttack.CoinRange);
            coin.transform.localScale *= 1.5f;
            coin.Aim();
            coin.OnHit = (dummy, at) =>
            {
                dummy.Hit(at + Vector2.left, 1f);
                HeroVfx.Pulse(fx, at, 0.45f, new Color(1f, 0.9f, 0.5f, 0.8f), 0.18f);
                HeroVfx.Sparks(fx, at, CoinGold, 14, 4.5f, 0.3f, Vector2.right, 110f, 0.9f);
            };
            yield return Wait(1.6f);
        }

        /// <summary>Right click: one coin for every coin in his purse, thrown across a quarter circle.</summary>
        private IEnumerator CoinVolley()
        {
            Set(5.5f, new Vector2(-3.4f, 0f), new Vector2(0.4f, 0f), new Vector2(0f, 1.9f), new Vector2(0f, -1.9f), new Vector2(1.2f, 1f), new Vector2(1.2f, -1f));
            yield return Wait(0.7f);
            Face(Vector2.right);
            const int Coins = 11;
            for (int i = 0; i < Coins; i++)
                Coin(HeroAt, Quaternion.Euler(0, 0, -GamblerAttack.VolleyCone * 0.5f + GamblerAttack.VolleyCone * i / (Coins - 1)) * Vector2.right, GamblerAttack.VolleyRange);
            HeroVfx.Sparks(fx, HeroAt, CoinGold, 10, 3.5f, 0.3f, Vector2.right, GamblerAttack.VolleyCone);
            yield return Wait(2f);
        }

        /// <summary>R: he opens his purse mid-fight and buys with his coins: a draught that heals, then a charm that wards.</summary>
        private IEnumerator PurseDemo()
        {
            Set(4.5f, new Vector2(0f, 0f));
            TintHero(new Color(0.8f, 0.3f, 0.3f), 1.4f);
            yield return Wait(0.8f);
            HeroVfx.Sparks(fx, HeroAt, CoinGold, 10, 3f, 0.3f, Vector2.up, 120f);
            HeroVfx.Motes(fx, HeroAt, 0.7f, CoinGold, 14, 0.9f);
            yield return Wait(0.7f);
            // The healing draught.
            HeroVfx.Motes(fx, HeroAt, 0.7f, new Color(1f, 0.45f, 0.5f), 12, 0.9f);
            HealVfx.Play(fx, hero.transform);
            yield return Wait(1.1f);
            // The warding charm.
            HeroVfx.Sparks(fx, HeroAt, CoinGold, 6, 2.5f, 0.3f, Vector2.up, 120f);
            HeroVfx.Pulse(fx, HeroAt, 0.95f, AbilityCatalog.Ice, 0.3f);
            CombatVfx.Ring(fx, HeroAt, 0.8f, AbilityCatalog.Ice, 0.5f);
            yield return Wait(1.4f);
        }

        /// <summary>His purse coughs up five coins at once.</summary>
        private IEnumerator Windfall()
        {
            Set(4.5f, new Vector2(0f, -0.4f));
            yield return Wait(0.6f);
            Cast();
            HeroVfx.Sparks(fx, HeroAt, CoinGold, 16, 4f, 0.4f, Vector2.up, 120f);
            HeroVfx.Motes(fx, HeroAt, 0.6f, CoinGold, 14, 0.8f);
            CoinRainVfx.Play(fx, HeroAt);
            yield return Wait(2.4f);
        }

        /// <summary>Double or nothing on every coin he carries: the flip lands his way one time, and against him the next.</summary>
        private IEnumerator AllIn()
        {
            Set(4.5f, new Vector2(0f, -0.4f));
            yield return Wait(0.6f);
            Cast();
            bool won = loops % 2 == 0;
            CoinFlipVfx.Play(fx, hero.transform, won);
            if (won) HeroVfx.Sparks(fx, HeroAt, CoinGold, 18, 4.5f, 0.4f);
            else HeroVfx.Sparks(fx, HeroAt, new Color(0.5f, 0.45f, 0.4f), 10, 2.5f, 0.35f);
            yield return Wait(2.6f);
        }

        /// <summary>Every coin goes into the machine and it always pays: speed, damage or a heal, a different one each spin.</summary>
        private IEnumerator Jackpot()
        {
            Set(4.5f, new Vector2(0f, -0.4f));
            yield return Wait(0.6f);
            Cast();
            var prize = (GamblerAttack.JackpotPrize)(loops % 3);
            Color color = JackpotVfx.PrizeColor(prize);
            HeroVfx.Pulse(fx, HeroAt, 1.6f, CoinGold, 0.45f);
            HeroVfx.Sparks(fx, HeroAt, CoinGold, 16, 5f, 0.45f);
            HeroVfx.Sparks(fx, HeroAt, color, 12, 4f, 0.5f);
            JackpotVfx.Play(fx, hero.transform, prize);
            TintHero(color, 2.6f);
            yield return Wait(3f);
        }

        /// <summary>One spinning playing card; its suit decides what it does to whoever it cuts.</summary>
        private void Card(Vector2 direction, ThrownCard.Suit suit)
        {
            Color color = ThrownCard.SuitColor(suit);
            var holder = Sprite("Playing card", DungeonVisuals.GlowSprite, HeroAt, Vector2.one * ThrownCard.CardSize * 2.4f, FlameMesh.Alpha(color, 0.45f), 5);
            var face = DungeonVisuals.Create("Card face", holder.transform, HeroAt, Vector2.one, Color.white, 6);
            face.sprite = ThrownCard.CardSprite(suit);
            // The holder is the glow, 2.4 cards across; the face rides on it at card size.
            face.transform.localPosition = Vector2.zero;
            face.transform.localScale = Vector3.one / 2.4f;
            CombatVfx.Trail(holder.gameObject, FlameMesh.Alpha(color, 0.7f), 0.14f, 0.18f);
            var shot = Shoot(holder, direction, ThrownCard.Speed, ThrownCard.Range, 0.1f, false, (dummy, at) =>
            {
                dummy.Hit(at - direction, 0.5f);
                HeroVfx.Sparks(fx, at, color, 9, 3.5f, 0.3f, direction, 120f, 0.9f);
                HeroVfx.Pulse(fx, at, 0.45f, FlameMesh.Alpha(color, 0.7f), 0.15f);
                // The suit's pip pops up out of the hit.
                var pip = Sprite("Suit pop", ThrownCard.PipSprite(suit), at + Vector2.up * 0.2f, Vector2.one * 0.35f, Color.Lerp(color, Color.white, 0.35f), 9);
                pip.gameObject.AddComponent<ThrownCard.SuitPop>();
                if (suit == ThrownCard.Suit.Hearts) dummy.Burn(3f);
                else if (suit == ThrownCard.Suit.Diamonds) dummy.Freeze(2f);
                else if (suit == ThrownCard.Suit.Clubs) dummy.Stun(1.2f);
                else dummy.Paralyze(2f);
            });
            shot.Turn = false;
            shot.Spin = 1000f;
        }

        /// <summary>Three cards in a spread: hearts burn, diamonds freeze, clubs shock and spades paralyse.</summary>
        private IEnumerator CardToss()
        {
            Set(5.5f, new Vector2(-3.8f, 0f), new Vector2(1.4f, 1.1f), new Vector2(1.6f, 0f), new Vector2(1.4f, -1.1f));
            for (int toss = 0; toss < 2; toss++)
            {
                yield return Wait(0.7f);
                Cast();
                Face(Vector2.right);
                for (int i = 0; i < 3; i++)
                    Card(Quaternion.Euler(0, 0, (i - 1) * ThrownCard.Spread) * Vector2.right, (ThrownCard.Suit)((i + toss * 3 + loops) % 4));
                // A flick of the wrist: the fan of cards snaps out in a spray of glitter.
                HeroVfx.Sparks(fx, HeroAt + Vector2.right * 0.3f, Color.white, 6, 3f, 0.18f, Vector2.right, 60f, 0.6f);
                yield return Wait(1.6f);
            }
            yield return Wait(0.8f);
        }

        /// <summary>A pair of dice arcs into the pack; each blows up for his damage times the face it rolled.</summary>
        private IEnumerator DiceBombDemo()
        {
            Vector2 target = At(1.6f, 0f);
            Set(5.5f, new Vector2(-3.8f, 0f), new Vector2(1f, 0.6f), new Vector2(2.2f, -0.2f), new Vector2(1.4f, -1.1f));
            yield return Wait(0.6f);
            Cast();
            Face(Vector2.right);
            for (int i = 0; i < 2; i++)
                StartCoroutine(Die(HeroAt, target + new Vector2(i == 0 ? -0.35f : 0.35f, i == 0 ? 0.15f : -0.15f), 1 + (loops * 2 + i * 3 + 2) % 6, i * DiceBomb.Stagger));
            yield return Wait(3f);
        }

        private IEnumerator Die(Vector2 from, Vector2 landing, int face, float delay)
        {
            Transform stage = fx;
            if (delay > 0f) yield return Wait(delay);
            if (stage == null) yield break;
            var body = DungeonVisuals.Create("Die", stage, from, Vector2.one * DiceBomb.DieSize, Color.white, 7);
            float distance = Vector2.Distance(from, landing), flight = 0.28f + distance * 0.05f, height = 0.9f + distance * 0.15f, age = 0f;
            // In the air: an arc, tumbling fast through its faces.
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                if (body == null) yield break;
                age += Time.deltaTime;
                float u = t / flight;
                body.transform.position = Vector2.Lerp(from, landing, u) + Vector2.up * Mathf.Sin(u * Mathf.PI) * height;
                body.sprite = DiceBomb.Face(1 + (int)(age * 22f + face) % 6);
                body.transform.rotation = Quaternion.Euler(0, 0, age * 900f);
                yield return null;
            }
            if (body == null || stage == null) yield break;
            // It clacks down showing its roll, and shivers faster as the fuse burns down.
            body.transform.position = landing;
            body.sprite = DiceBomb.Face(face);
            HeroVfx.Sparks(stage, landing, new Color(0.8f, 0.75f, 0.65f, 0.7f), 5, 2f, 0.2f, Vector2.up, 160f, 0.6f);
            CombatVfx.Ring(stage, landing, DiceBomb.BlastRadius, FlameMesh.Alpha(CoinGold, 0.5f), DiceBomb.Fuse + 0.2f);
            for (float t = 0f; t < DiceBomb.Fuse + 0.2f; t += Time.deltaTime)
            {
                if (body == null) yield break;
                float fuse = t / (DiceBomb.Fuse + 0.2f);
                body.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.Lerp(20f, 60f, fuse)) * 8f * fuse);
                body.color = Color.Lerp(Color.white, new Color(1f, 0.75f, 0.5f), Mathf.Abs(Mathf.Sin(t * Mathf.Lerp(8f, 30f, fuse))) * fuse);
                yield return null;
            }
            if (body == null || stage == null) yield break;
            HeroVfx.Pulse(stage, landing, DiceBomb.BlastRadius, CoinGold, 0.35f);
            HeroVfx.Sparks(stage, landing, CoinGold, 8 + face * 3, 4f + face * 0.5f, 0.4f);
            HeroVfx.Sparks(stage, landing, Color.white, 4 + face, 3f, 0.25f, null, 360f, 0.7f);
            foreach (var dummy in Within(landing, DiceBomb.BlastRadius)) dummy.Hit(landing, 0.2f * face);
            Destroy(body.gameObject);
        }

        /// <summary>While the policy lasts, a blow costs him five coins instead of a heart.</summary>
        private IEnumerator Insurance()
        {
            Set(5f, new Vector2(0f, 0f), new Vector2(3.6f, 0.4f), new Vector2(-3.6f, -0.3f));
            yield return Wait(0.5f);
            Cast();
            InsuranceVfx.Play(fx, hero.transform, 3.4f);
            for (int i = 0; i < 3; i++)
            {
                yield return Wait(0.5f);
                var shooter = dummies[i % dummies.Count];
                shooter.Flash();
                var bolt = DungeonVisuals.CreateEmberBolt(fx, shooter.Position);
                yield return Bolt(bolt, shooter.Position, 0.25f, 8f);
                Destroy(bolt.gameObject);
                // The claim is paid in coins; he is not hurt.
                InsuranceVfx.Claim(fx, hero.transform, true);
            }
            yield return Wait(1.6f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Deals a boss's attacks in a freshly shuffled order each cycle, so every attack still comes up once per cycle
    /// but the order can't be memorised. The last attack of one cycle never opens the next.
    /// </summary>
    public sealed class AttackDeck<T>
    {
        private readonly T[] cards;
        private readonly List<T> pile = new List<T>();
        private T last;
        private bool dealt;

        public AttackDeck(params T[] cards) => this.cards = cards;

        /// <summary>A fixed opening attack, played outside the deck; the first shuffle won't lead with it again.</summary>
        public T Open(T card)
        {
            last = card;
            dealt = true;
            return card;
        }

        public T Draw()
        {
            if (pile.Count == 0) Shuffle();
            T card = pile[pile.Count - 1];
            pile.RemoveAt(pile.Count - 1);
            last = card;
            dealt = true;
            return card;
        }

        private void Shuffle()
        {
            pile.AddRange(cards);
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
            // Drawn from the end: keep the previous cycle's closer from repeating straight away.
            if (dealt && pile.Count > 1 && EqualityComparer<T>.Default.Equals(pile[pile.Count - 1], last))
                (pile[0], pile[pile.Count - 1]) = (pile[pile.Count - 1], pile[0]);
        }
    }
}

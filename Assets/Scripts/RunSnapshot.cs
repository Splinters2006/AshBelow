using System;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A solo descent saved at the start of a floor, so a signed-in player can leave for the main menu and carry on later,
    /// on this PC or another (see <see cref="CloudRunSave"/>). Floors are built from the seed, so resuming rebuilds the
    /// floor and it starts over. Enum values are kept by name, so reordering a catalog cannot scramble a save.
    /// </summary>
    [Serializable]
    public sealed class RunSnapshot
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        /// <summary>UTC ticks when it was saved: the newest copy wins between this PC and the cloud.</summary>
        public long savedAt;
        /// <summary>
        /// The descent is over. Kept rather than deleted, so an older copy on another PC cannot bring an ended run back.
        /// </summary>
        public bool ended;
        public string weapon;
        public int seed, floor, kills, runAsh, guardians;
        /// <summary>True when the floor is the crystal shop (which keeps the number of the floor before it).</summary>
        public bool inShop;
        public bool rerolledThisWorld;
        /// <summary>Ash this floor's kills have already paid, so fighting the floor again after a resume does not pay it twice.</summary>
        public int floorAshPaid;
        public HeroSnapshot hero = new HeroSnapshot();

        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>The save in <paramref name="json"/>, or null when it is damaged or from a newer version of the game.</summary>
        public static RunSnapshot FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var run = JsonUtility.FromJson<RunSnapshot>(json);
                if (run == null || run.version != CurrentVersion) return null;
                if (run.ended) return run;
                return run.hero != null && run.floor > 0 && !string.IsNullOrEmpty(run.weapon) ? run : null;
            }
            catch (ArgumentException) { return null; }
        }

        public WeaponType? Weapon => TryParse(weapon, out WeaponType value) ? value : (WeaponType?)null;

        /// <summary>Parses an enum value saved by name; false for names this version does not know.</summary>
        public static bool TryParse<T>(string name, out T value) where T : struct, Enum
            => Enum.TryParse(name, out value) && Enum.IsDefined(typeof(T), value);
    }

    /// <summary>The hero's side of a <see cref="RunSnapshot"/>: stats, talents, abilities, crystals and class resources.</summary>
    [Serializable]
    public sealed class HeroSnapshot
    {
        public int health, maxHealth, damage;
        public float speed;
        public List<SavedCount> talents = new List<SavedCount>();
        public List<SavedCount> abilities = new List<SavedCount>();
        public string abilityQ, abilityE, mutationPath;
        public bool cheatDeathSpent;
        public int crystals, boonFloor = -1, boonDamage, boonWards, boonSwiftness;
        public List<SavedCount> purchases = new List<SavedCount>();
        /// <summary>Class resources that last the descent (souls, coins, the safe, ...), by name.</summary>
        public List<SavedCount> extras = new List<SavedCount>();

        public int Extra(string key, int fallback)
        {
            foreach (var entry in extras) if (entry.id == key) return entry.value;
            return fallback;
        }

        public void SetExtra(string key, int value)
        {
            extras.RemoveAll(entry => entry.id == key);
            extras.Add(new SavedCount(key, value));
        }
    }

    [Serializable]
    public struct SavedCount
    {
        public string id;
        public int value;
        public SavedCount(string id, int value) { this.id = id; this.value = value; }
    }

    /// <summary>A part of the hero with state that lasts the whole descent, saved with the run.</summary>
    public interface IRunPersistent
    {
        void SaveRun(HeroSnapshot hero);
        /// <summary>Called on a fresh hero, after the hero's own stats are back and before the first floor is built.</summary>
        void LoadRun(HeroSnapshot hero);
    }
}

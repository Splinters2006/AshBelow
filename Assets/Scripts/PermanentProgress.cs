using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Slopgame
{
    // The schema uses stable string IDs so catalog ordering and added upgrades cannot shift purchases.
    public sealed class PermanentProgress
    {
        [Serializable] public sealed class RankEntry { public string id; public int rank; }
        [Serializable] public sealed class SaveData
        {
            public int version;
            public int ash;
            /// <summary>The most guardians beaten in a single descent (the third falls on floor 15).</summary>
            public int guardians;
            /// <summary>The same record per hero class, keyed by weapon; class mechanics unlock from these.</summary>
            public List<RankEntry> classGuardians;
            public List<RankEntry> upgrades;
            /// <summary>Worlds cleared at least once, by index; each unlocks two Ash shop items.</summary>
            public List<int> clearedWorlds;
            /// <summary>Crystals Smuggler's Stash put aside, back when it carried them over to the next descent.</summary>
            public int stashedCrystals;
            /// <summary>Switched-off toggles, such as the Infernal Pact.</summary>
            public List<string> flags;
            /// <summary>Encyclopedia entries met in any descent: heroes, talents, abilities, guardians and worlds.</summary>
            public List<string> discovered;
        }
        private SaveData data = Fresh();
        private bool dirty, recoveredBackup;
        // Ranks bought for free in debug mode: they last for the session, count only while the mode is on, and are never saved.
        private readonly Dictionary<string, int> debugRanks = new Dictionary<string, int>();
        public string SavePath { get; }
        public int Ash => data.ash;
        public int GuardiansDefeated => data.guardians;
        public bool IsReadOnly { get; private set; }
        public string LastError { get; private set; }
        public bool HasUnsavedChanges => dirty;
        /// <summary>Counts every write to disk, so cloud sync can tell when there is something new to upload.</summary>
        public int Revision { get; private set; }
        /// <summary>True while nothing has been earned yet, so another save can replace this one without asking.</summary>
        public bool IsEmpty => data.ash == 0 && data.guardians == 0 && data.upgrades.Count == 0 && data.clearedWorlds.Count == 0 && data.discovered.Count == 0;
        public event Action Changed;

        public PermanentProgress(string directory)
        {
            SavePath = Path.Combine(directory, "progress.json");
            Load();
        }

        private static SaveData Fresh() => new SaveData { version = 1, ash = 0, classGuardians = new List<RankEntry>(), upgrades = new List<RankEntry>(),
            clearedWorlds = new List<int>(), flags = new List<string>(), discovered = new List<string>() };
        private static string ClassKey(WeaponType weapon) => weapon.ToString().ToLowerInvariant();

        /// <summary>The most guardians beaten in a single descent as this class.</summary>
        public int GuardiansDefeatedAs(WeaponType weapon)
        {
            string key = ClassKey(weapon);
            var entry = data.classGuardians.Find(value => value.id == key);
            return entry == null ? 0 : entry.rank;
        }
        public int Rank(string id)
        {
            var entry = data.upgrades.Find(value => value.id == id);
            int rank = entry == null ? 0 : entry.rank;
            if (DebugMode.Enabled && debugRanks.TryGetValue(id, out int debugRank)) rank = Math.Max(rank, debugRank);
            return Math.Min(rank, PermanentUpgradeCatalog.Get(id)?.MaxRank ?? int.MaxValue);
        }

        private static SaveData Read(string path) => Parse(File.ReadAllText(path));

        private static SaveData Parse(string json)
        {
            var loaded = JsonUtility.FromJson<SaveData>(json);
            if (loaded == null || loaded.version != 1 || loaded.ash < 0 || loaded.guardians < 0 || loaded.upgrades == null)
                throw new InvalidDataException("Unsupported or damaged progress file.");
            var ids = new HashSet<string>();
            foreach (var entry in loaded.upgrades)
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.rank < 0 || !ids.Add(entry.id))
                    throw new InvalidDataException("Invalid upgrade data.");
            // Saves from before per-class records, world clears and toggles simply start them empty.
            if (loaded.classGuardians == null) loaded.classGuardians = new List<RankEntry>();
            if (loaded.clearedWorlds == null) loaded.clearedWorlds = new List<int>();
            if (loaded.flags == null) loaded.flags = new List<string>();
            if (loaded.discovered == null) loaded.discovered = new List<string>();
            if (loaded.stashedCrystals < 0) loaded.stashedCrystals = 0;
            ids.Clear();
            foreach (var entry in loaded.classGuardians)
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.rank < 0 || !ids.Add(entry.id))
                    throw new InvalidDataException("Invalid guardian data.");
            return loaded;
        }

        private void Load()
        {
            if (!File.Exists(SavePath) && !File.Exists(SavePath + ".bak")) return;
            try
            {
                // Never overwrite progress from a newer game with an older save schema.
                if (File.Exists(SavePath))
                {
                    var header = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                    if (header != null && header.version > 1)
                    { IsReadOnly = true; LastError = "This save requires a newer game version."; return; }
                }
                data = Read(SavePath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                try
                {
                    data = Read(SavePath + ".bak");
                    recoveredBackup = true;
                    dirty = true;
                    LastError = "Recovered progress from the backup save.";
                }
                catch (Exception backupError) when (backupError is IOException || backupError is UnauthorizedAccessException || backupError is ArgumentException)
                {
                    IsReadOnly = true;
                    LastError = "Could not read progress. Existing saves are preserved; restore a backup before buying upgrades.";
                }
            }
        }

        public void AwardAsh(int amount)
        {
            if (amount <= 0 || IsReadOnly) return;
            data.ash = (int)Math.Min(int.MaxValue, (long)data.ash + amount);
            dirty = true;
            Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Records that the <paramref name="ordinal"/>-th guardian of a descent fell (1 = floor 5), and, when
        /// <paramref name="weapon"/> is given, that this class felled it.
        /// </summary>
        public void RecordGuardian(int ordinal, WeaponType? weapon = null)
        {
            if (IsReadOnly) return;
            bool changed = false;
            if (ordinal > data.guardians) { data.guardians = ordinal; changed = true; }
            if (weapon.HasValue && ordinal > GuardiansDefeatedAs(weapon.Value))
            {
                string key = ClassKey(weapon.Value);
                var entry = data.classGuardians.Find(value => value.id == key);
                if (entry == null) data.classGuardians.Add(new RankEntry { id = key, rank = ordinal });
                else entry.rank = ordinal;
                changed = true;
            }
            if (!changed) return;
            dirty = true;
            Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Class upgrades gated behind guardians (the class mechanics) need that class to have beaten them; world rewards need
        /// their world cleared once; a mechanic's R upgrade also needs the mechanic itself. Debug mode unlocks everything.
        /// </summary>
        public bool IsAvailable(PermanentUpgradeDefinition upgrade) => upgrade != null && (DebugMode.Enabled
            || (upgrade.ClassWeapon.HasValue ? GuardiansDefeatedAs(upgrade.ClassWeapon.Value) : data.guardians) >= upgrade.RequiredGuardians
            && (upgrade.RequiredWorld < 0 || HasClearedWorld(upgrade.RequiredWorld))
            && (upgrade.RequiredGuardian == null || HasSlain(upgrade.RequiredGuardian))
            && (upgrade.RequiredUpgrade == null || Rank(upgrade.RequiredUpgrade) > 0));

        /// <summary>
        /// True once the guardian with this title has been slain in any descent (by any hero). Guardians enter the
        /// encyclopedia as they fall, so kills from before this was asked still count.
        /// </summary>
        public bool HasSlain(string guardianTitle) => !string.IsNullOrEmpty(guardianTitle) && IsDiscovered(Encyclopedia.GuardianId(guardianTitle));

        /// <summary>
        /// True once this world or any later one has been cleared: getting past a world counts, so worlds skipped on
        /// the travel map (or cleared before clears were saved) don't keep their rewards locked.
        /// </summary>
        public bool HasClearedWorld(int index) => data.clearedWorlds.Exists(world => world >= index);

        /// <summary>A world was cleared (its third guardian beaten): its Ash shop rewards, and every earlier world's, unlock.</summary>
        public void RecordWorldCleared(int index)
        {
            if (IsReadOnly || data.clearedWorlds.Contains(index)) return;
            data.clearedWorlds.Add(index);
            dirty = true;
            Save();
            Changed?.Invoke();
        }

        public int StashedCrystals => data.stashedCrystals;

        /// <summary>Takes out any crystals an older save still has put aside (Smuggler's Stash sells them for Ash now).</summary>
        public int TakeStash()
        {
            int crystals = data.stashedCrystals;
            if (crystals <= 0 || IsReadOnly) return 0;
            data.stashedCrystals = 0;
            dirty = true;
            Save();
            return crystals;
        }

        /// <summary>True when a toggle has been switched off in the Ash shop.</summary>
        public bool IsSwitchedOff(string id) => data.flags.Contains(id);

        public void Switch(string id, bool on)
        {
            if (IsReadOnly || IsSwitchedOff(id) == !on) return;
            if (on) data.flags.Remove(id);
            else data.flags.Add(id);
            dirty = true;
            Save();
            Changed?.Invoke();
        }

        /// <summary>True once an encyclopedia entry has turned up in any descent.</summary>
        public IReadOnlyList<string> Discovered => data.discovered;
        public bool IsDiscovered(string id) => data.discovered.Contains(id);

        /// <summary>Records an encyclopedia entry (see <see cref="Encyclopedia"/> for the id scheme); saves only when it is new.</summary>
        public void Discover(string id)
        {
            if (IsReadOnly || string.IsNullOrEmpty(id) || data.discovered.Contains(id)) return;
            data.discovered.Add(id);
            dirty = true;
            Save();
            Changed?.Invoke();
        }

        public bool TryPurchase(string id)
        {
            var upgrade = PermanentUpgradeCatalog.Get(id);
            // Debug mode sells every upgrade for free, whatever its requirements, without spending Ash or writing the save.
            if (DebugMode.Enabled)
            {
                if (upgrade == null || Rank(id) >= upgrade.MaxRank) return false;
                debugRanks[id] = Rank(id) + 1;
                Changed?.Invoke();
                return true;
            }
            if (IsReadOnly || upgrade == null || !IsAvailable(upgrade) || Rank(id) >= upgrade.MaxRank || Ash < upgrade.Cost(Rank(id))) return false;
            var next = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            next.ash -= upgrade.Cost(Rank(id));
            var entry = next.upgrades.Find(value => value.id == id);
            if (entry == null) next.upgrades.Add(new RankEntry { id = id, rank = 1 });
            else entry.rank++;
            // A purchase only takes effect after both its cost and rank are written together.
            if (!Persist(next)) return false;
            data = next;
            dirty = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>The saved progress as JSON, for the cloud copy. Debug-mode ranks are never part of it.</summary>
        public string ExportJson() => JsonUtility.ToJson(data);

        /// <summary>A short line about a save, for choosing between this PC's progress and the cloud's.</summary>
        public static string Describe(string json)
        {
            try { return Describe(Parse(json)); }
            catch (Exception error) when (error is InvalidDataException || error is ArgumentException) { return "Unreadable save"; }
        }
        public string Describe() => Describe(data);
        private static string Describe(SaveData save)
        {
            int ranks = 0;
            foreach (var entry in save.upgrades) ranks += entry.rank;
            return $"{save.ash} ash  /  {ranks} upgrade ranks  /  {save.clearedWorlds.Count} worlds cleared  /  {save.discovered.Count} discoveries";
        }

        /// <summary>Replaces this progress with a save from elsewhere (the cloud), writing it to disk first.</summary>
        public bool Adopt(string json)
        {
            if (IsReadOnly) return false;
            SaveData next;
            try { next = Parse(json); }
            catch (Exception error) when (error is InvalidDataException || error is ArgumentException)
            {
                LastError = "The cloud save is damaged or needs a newer game version.";
                return false;
            }
            if (!Persist(next)) return false;
            data = next;
            dirty = false;
            Changed?.Invoke();
            return true;
        }

        public bool Save()
        {
            if (IsReadOnly) return false;
            if (!dirty) return true;
            if (!Persist(data)) return false;
            dirty = false;
            return true;
        }

        private bool Persist(SaveData next)
        {
            string temp = SavePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(next, true));
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, recoveredBackup ? null : SavePath + ".bak");
                else File.Move(temp, SavePath);
                recoveredBackup = false;
                LastError = null;
                Revision++;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is NotSupportedException)
            {
                LastError = "Progress could not be saved. Check disk space and save-folder permissions.";
                return false;
            }
        }
    }
}

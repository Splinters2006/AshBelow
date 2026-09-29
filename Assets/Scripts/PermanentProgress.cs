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
            public List<RankEntry> upgrades;
        }
        private SaveData data = Fresh();
        private bool dirty, recoveredBackup;
        public string SavePath { get; }
        public int Ash => data.ash;
        public int GuardiansDefeated => data.guardians;
        public bool IsReadOnly { get; private set; }
        public string LastError { get; private set; }
        public bool HasUnsavedChanges => dirty;
        public event Action Changed;

        public PermanentProgress(string directory)
        {
            SavePath = Path.Combine(directory, "progress.json");
            Load();
        }

        private static SaveData Fresh() => new SaveData { version = 1, ash = 0, upgrades = new List<RankEntry>() };
        public int Rank(string id)
        {
            var entry = data.upgrades.Find(value => value.id == id);
            return entry == null ? 0 : Math.Min(entry.rank, PermanentUpgradeCatalog.Get(id)?.MaxRank ?? int.MaxValue);
        }

        private static SaveData Read(string path)
        {
            var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (loaded == null || loaded.version != 1 || loaded.ash < 0 || loaded.guardians < 0 || loaded.upgrades == null)
                throw new InvalidDataException("Unsupported or damaged progress file.");
            var ids = new HashSet<string>();
            foreach (var entry in loaded.upgrades)
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.rank < 0 || !ids.Add(entry.id))
                    throw new InvalidDataException("Invalid upgrade data.");
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

        /// <summary>Records that the <paramref name="ordinal"/>-th guardian of a descent fell (1 = floor 5).</summary>
        public void RecordGuardian(int ordinal)
        {
            if (IsReadOnly || ordinal <= data.guardians) return;
            data.guardians = ordinal;
            dirty = true;
            Save();
            Changed?.Invoke();
        }

        public bool IsAvailable(PermanentUpgradeDefinition upgrade) => upgrade != null && data.guardians >= upgrade.RequiredGuardians;

        public bool TryPurchase(string id)
        {
            var upgrade = PermanentUpgradeCatalog.Get(id);
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

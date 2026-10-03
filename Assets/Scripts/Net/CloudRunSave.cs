using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The signed-in account's saved solo descent (see <see cref="RunSnapshot"/>), kept in a file on this PC and in Cloud
    /// Save, so a descent left for the main menu can be continued later, here or on another PC. The newest copy wins;
    /// an ended descent is saved as ended rather than deleted, so an older copy elsewhere cannot bring it back.
    /// </summary>
    public sealed class CloudRunSave : MonoBehaviour
    {
        private const string Key = "run";
        private const float RetryDelay = 30f, RefreshInterval = 60f;

        public DungeonRun Run { get; set; }
        public PlayerAccount Account { get; set; }
        /// <summary>Where the per-account save files go (beside the progress file).</summary>
        public string Directory { get; set; }
        /// <summary>The descent waiting to be continued, or null.</summary>
        public RunSnapshot Saved => latest != null && !latest.ended ? latest : null;
        /// <summary>True while the cloud copy is being fetched.</summary>
        public bool IsChecking { get; private set; }

        private RunSnapshot latest;
        // The account the save belongs to; requests that finish after a switch of account are ignored.
        private string owner;
        private bool uploadPending, uploading;
        private float retryAt, refreshAt;

        private void Start()
        {
            Account.SignedIn += OnSignedIn;
            Account.SignedOut += OnSignedOut;
            if (Account.IsSignedIn) OnSignedIn();
        }

        private void OnDestroy()
        {
            if (Account == null) return;
            Account.SignedIn -= OnSignedIn;
            Account.SignedOut -= OnSignedOut;
        }

        private void OnSignedIn()
        {
            owner = Account.Username;
            latest = ReadLocal();
            uploadPending = false;
            _ = Pull();
        }

        private void OnSignedOut()
        {
            owner = null;
            latest = null;
            uploadPending = false;
        }

        private void Update()
        {
            if (owner == null || uploading) return;
            if (uploadPending) { if (Time.unscaledTime >= retryAt) _ = Upload(); }
            // A descent saved on another PC shows up while this one waits on the menu.
            else if (Run.IsInMainMenu && !IsChecking && Time.unscaledTime >= refreshAt) _ = Pull();
        }

        /// <summary>Saves <paramref name="run"/> as the account's descent: on this PC at once, then to the cloud.</summary>
        public void Store(RunSnapshot run)
        {
            if (owner == null || run == null) return;
            run.savedAt = DateTime.UtcNow.Ticks;
            Keep(run.ToJson());
        }

        /// <summary>The descent is over: nothing is left to continue, here or on another PC.</summary>
        public void End()
        {
            if (owner == null || latest == null || latest.ended) return;
            Keep(new RunSnapshot { ended = true, savedAt = DateTime.UtcNow.Ticks }.ToJson());
        }

        private void Keep(string json)
        {
            latest = RunSnapshot.FromJson(json);
            WriteLocal(json);
            uploadPending = true;
            retryAt = 0f;
            // Straight away rather than on the next frame, so a save made as the game closes still has a chance to go up.
            if (!uploading) _ = Upload();
        }

        /// <summary>Fetches the cloud copy and keeps whichever copy is newer.</summary>
        private async Task Pull()
        {
            if (owner == null || IsChecking) return;
            string user = owner;
            IsChecking = true;
            refreshAt = Time.unscaledTime + RefreshInterval;
            try
            {
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
                if (owner != user) return;
                var cloud = items.TryGetValue(Key, out var item) ? RunSnapshot.FromJson(item.Value.GetAs<string>()) : null;
                if (cloud != null && (latest == null || cloud.savedAt > latest.savedAt))
                {
                    latest = cloud;
                    WriteLocal(cloud.ToJson());
                }
                // Saved here while the cloud could not be reached: it goes up now.
                else if (latest != null && (cloud == null || latest.savedAt > cloud.savedAt)) uploadPending = true;
            }
            catch (Exception error) { Debug.LogWarning("Could not check the saved descent: " + error); }
            finally { IsChecking = false; }
        }

        private async Task Upload()
        {
            if (owner == null || latest == null) return;
            string user = owner, json = latest.ToJson();
            uploading = true;
            uploadPending = false;
            try { await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { Key, json } }); }
            catch (Exception error)
            {
                Debug.LogWarning("Could not upload the saved descent: " + error);
                if (owner == user) { uploadPending = true; retryAt = Time.unscaledTime + RetryDelay; }
            }
            finally { uploading = false; }
        }

        private string LocalPath => Path.Combine(Directory, "run-" + owner + ".json");

        private RunSnapshot ReadLocal()
        {
            try { return File.Exists(LocalPath) ? RunSnapshot.FromJson(File.ReadAllText(LocalPath)) : null; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning("Could not read the saved descent: " + error.Message);
                return null;
            }
        }

        private void WriteLocal(string json)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(LocalPath, json);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning("Could not save the descent on this PC: " + error.Message);
            }
        }
    }
}

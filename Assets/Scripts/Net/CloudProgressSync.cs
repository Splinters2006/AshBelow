using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Keeps the permanent progress in Cloud Save while an account is signed in, so it follows the player to other PCs.
    /// The save on disk stays the real one: the cloud copy is uploaded a few seconds after each write. When the account's
    /// cloud save and this PC's progress disagree at sign-in, the player picks which to keep before anything is uploaded.
    /// </summary>
    public sealed class CloudProgressSync : MonoBehaviour
    {
        private const string Key = "progress";
        // Writes come in bursts (ash, then discoveries, then a purchase), so they are gathered into one upload.
        private const float UploadDelay = 4f, RetryDelay = 30f;

        public DungeonRun Run { get; set; }
        public PlayerAccount Account { get; set; }
        public string Status { get; private set; }
        public bool IsBusy { get; private set; }
        /// <summary>The account's cloud save while the player chooses between it and this PC's progress.</summary>
        public string CloudJson { get; private set; }
        public bool IsChoosing => CloudJson != null;
        /// <summary>True once this PC and the cloud agree on which progress is current; only then are uploads made.</summary>
        public bool IsLinked { get; private set; }

        private PermanentProgress Progress => Run.Progress;
        private string syncedJson;
        private int syncedRevision = -1;
        private float uploadAt = -1f;

        private void Start()
        {
            Account.SignedIn += OnSignedIn;
            Account.SignedOut += OnSignedOut;
        }

        private void OnDestroy()
        {
            if (Account == null) return;
            Account.SignedIn -= OnSignedIn;
            Account.SignedOut -= OnSignedOut;
        }

        private void OnSignedIn() => _ = Pull();

        private void OnSignedOut()
        {
            IsLinked = false;
            CloudJson = syncedJson = null;
            uploadAt = -1f;
            Status = null;
        }

        private void Update()
        {
            if (!IsLinked || IsBusy || !Account.IsSignedIn || Progress.IsReadOnly) return;
            if (Progress.Revision == syncedRevision) { uploadAt = -1f; return; }
            if (uploadAt < 0f) uploadAt = Time.unscaledTime + UploadDelay;
            else if (Time.unscaledTime >= uploadAt) _ = Upload();
        }

        private void OnApplicationQuit()
        {
            // Best effort: the request may not finish before the process ends, and the next sign-in compares saves anyway.
            if (IsLinked && !IsBusy && Account.IsSignedIn && Progress.Revision != syncedRevision) _ = Upload();
        }

        /// <summary>Fetches the account's cloud save and decides, or asks, which progress wins.</summary>
        public async Task Pull()
        {
            if (IsBusy || !Account.IsSignedIn) return;
            IsLinked = false;
            CloudJson = null;
            if (Progress.IsReadOnly) { Status = "Cloud sync is paused until this PC's save can be read."; return; }
            IsBusy = true;
            Status = "Checking cloud save…";
            try
            {
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
                if (!Account.IsSignedIn) return;
                string cloud = items.TryGetValue(Key, out var item) ? item.Value.GetAs<string>() : null;
                string local = Progress.ExportJson();
                if (cloud == local) Link(cloud, "Progress is synced.");
                else if (cloud == null) await Push(local, "Progress uploaded to your account.");
                // A fresh PC simply takes the account's progress.
                else if (Progress.IsEmpty) Adopt(cloud);
                else { CloudJson = cloud; Status = "This account already has progress. Choose which to keep."; }
            }
            catch (Exception error) { Fail("Could not reach the cloud save", error); }
            finally { IsBusy = false; }
        }

        /// <summary>Uploads pending progress when already synced; otherwise checks the cloud save again.</summary>
        public void SyncNow()
        {
            if (IsBusy || !Account.IsSignedIn) return;
            Progress.Save();
            if (IsLinked) _ = Upload();
            else _ = Pull();
        }

        /// <summary>Uploads anything not yet in the cloud, then signs out, so the latest progress is not left behind.</summary>
        public async void SignOutAfterUpload()
        {
            if (IsBusy || !Account.IsSignedIn) return;
            Progress.Save();
            if (IsLinked && Progress.Revision != syncedRevision) await Upload();
            Account.SignOut();
        }

        /// <summary>Replaces this PC's progress with the cloud save the player was shown.</summary>
        public void KeepCloud()
        {
            if (!IsChoosing || IsBusy) return;
            string cloud = CloudJson;
            CloudJson = null;
            Adopt(cloud);
        }

        /// <summary>Overwrites the cloud save with this PC's progress.</summary>
        public async void KeepLocal()
        {
            if (!IsChoosing || IsBusy) return;
            CloudJson = null;
            IsBusy = true;
            try { await Push(Progress.ExportJson(), "This PC's progress is now in your account."); }
            catch (Exception error) { Fail("Could not upload progress", error); }
            finally { IsBusy = false; }
        }

        /// <summary>Removes the cloud copy, for when the account itself is being deleted.</summary>
        public async Task DeleteCloudCopy()
        {
            IsLinked = false;
            await CloudSaveService.Instance.Data.Player.DeleteAllAsync();
        }

        private void Adopt(string cloud)
        {
            if (Progress.Adopt(cloud)) Link(cloud, "Progress loaded from your account.");
            else Status = "Could not load the cloud save. " + Progress.LastError;
        }

        private async Task Upload()
        {
            IsBusy = true;
            try { await Push(Progress.ExportJson(), null); }
            catch (Exception error) { Fail("Could not upload progress", error); }
            finally { IsBusy = false; }
        }

        private async Task Push(string json, string done)
        {
            int revision = Progress.Revision;
            if (json != syncedJson)
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { Key, json } });
            if (!Account.IsSignedIn) return;
            Link(json, done ?? "Progress is synced.");
            syncedRevision = revision;
        }

        private void Link(string json, string message)
        {
            IsLinked = true;
            syncedJson = json;
            syncedRevision = Progress.Revision;
            uploadAt = -1f;
            Status = message;
        }

        private void Fail(string what, Exception error)
        {
            Debug.LogWarning(what + ": " + error);
            // A failed upload stays linked and tries again shortly; a failed check needs the player to press Sync now.
            Status = what + ". " + (IsLinked ? "Trying again shortly." : "Use Sync now to try again.");
            uploadAt = IsLinked ? Time.unscaledTime + RetryDelay : -1f;
        }
    }
}

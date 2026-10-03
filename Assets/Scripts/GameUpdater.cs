using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Slopgame
{
    public enum UpdateState { Idle, Checking, UpToDate, Available, Failed, Installing }

    /// <summary>
    /// Checks GitHub for a newer release of the game (on launch, and from the main menu's button) and, once the player
    /// agrees, hands over to the updater shipped next to the game (Update.cmd / Update.sh): the game closes, the updater
    /// backs up the save, installs the new build and opens the game again.
    /// </summary>
    public sealed class GameUpdater : MonoBehaviour
    {
        public const string Repository = "Splinters2006/AshBelow";
        /// <summary>Written next to the game by the build (and by the updater): which release ZIP this install is.</summary>
        public const string MarkerFile = ".ashbelow-release.json";
        /// <summary>Left by an update this game started; read (and removed) on the next launch.</summary>
        public const string ResultFile = ".ashbelow-update-result.json";

        [Serializable]
        public sealed class ReleaseMarker { public string tag; public long asset_id; public string asset_name; }
        [Serializable]
        private sealed class Release { public string tag_name; public Asset[] assets; }
        [Serializable]
        private sealed class Asset { public long id; public string name; }
        [Serializable]
        private sealed class Result { public bool ok; public string message; }

        public UpdateState State { get; private set; }
        /// <summary>The newest release's tag, once a check has found it.</summary>
        public string LatestTag { get; private set; }
        /// <summary>What the last check or update came to, for the menu to show (null when there is nothing to say).</summary>
        public string Message { get; private set; }
        /// <summary>True while the menu should ask whether to install the update just found.</summary>
        public bool PromptOpen { get; private set; }
        /// <summary>True for an installed release with its updater beside it; the editor and bare builds can only check.</summary>
        public bool CanInstall => !Application.isEditor && File.Exists(UpdaterScript) && File.Exists(Path.Combine(InstallDirectory, "update-game.py"));

        private static bool IsLinux => Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor;
        /// <summary>The game folder: the one holding AshBelow_Data (the project folder in the editor).</summary>
        private static string InstallDirectory => Path.GetDirectoryName(Application.dataPath);
        private static string UpdaterScript => Path.Combine(InstallDirectory, IsLinux ? "Update.sh" : "Update.cmd");

        private void Start()
        {
            // Releases look for updates by themselves (not straight after one, which shows how it went); scripted test runs never do.
            bool scripted = Application.isBatchMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-coopSmoke") >= 0;
            if (!ReadLastResult() && CanInstall && !scripted) Check();
        }

        /// <summary>Asks GitHub for the latest release. Finding a newer one opens the prompt to install it.</summary>
        public void Check()
        {
            if (State == UpdateState.Checking || State == UpdateState.Installing) return;
            StartCoroutine(CheckLatest());
        }

        public void Decline() => PromptOpen = false;

        /// <summary>Opens the prompt again for an update found earlier and put off.</summary>
        public void Ask() { if (State == UpdateState.Available && CanInstall) PromptOpen = true; }

        private IEnumerator CheckLatest()
        {
            State = UpdateState.Checking;
            Message = null;
            using var request = UnityWebRequest.Get($"https://api.github.com/repos/{Repository}/releases/latest");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.timeout = 20;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail(request.responseCode == 404 ? "No release has been published yet." : "Could not reach GitHub. Check your internet connection.");
                yield break;
            }
            Release release;
            try { release = JsonUtility.FromJson<Release>(request.downloadHandler.text); }
            catch (ArgumentException) { release = null; }
            var asset = release != null ? PickAsset(release.assets) : null;
            if (asset == null) { Fail($"The latest release has no {(IsLinux ? "Linux" : "Windows")} build."); yield break; }
            LatestTag = release.tag_name;
            if (IsInstalled(asset))
            {
                State = UpdateState.UpToDate;
                Message = $"You have the latest version ({LatestTag}).";
                yield break;
            }
            State = UpdateState.Available;
            if (CanInstall) PromptOpen = true;
            else Message = $"{LatestTag} is out. This copy cannot update itself: download it from the GitHub releases page.";
        }

        /// <summary>
        /// This platform's build ZIP, chosen the way the updater does: the Linux prefix also matches the Windows one, so
        /// a name belongs to the longest prefix it starts with.
        /// </summary>
        private static Asset PickAsset(Asset[] assets)
        {
            Asset found = null;
            if (assets == null) return null;
            foreach (var asset in assets)
            {
                if (asset?.name == null || !asset.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                bool linux = asset.name.StartsWith("AshBelow-Linux-");
                bool windows = !linux && asset.name.StartsWith("AshBelow-");
                if (IsLinux ? !linux : !windows) continue;
                // More than one candidate is ambiguous, and the updater would refuse it too.
                if (found != null) return null;
                found = asset;
            }
            return found;
        }

        private static bool IsInstalled(Asset asset)
        {
            try
            {
                string path = Path.Combine(InstallDirectory, MarkerFile);
                if (!File.Exists(path)) return false;
                var marker = JsonUtility.FromJson<ReleaseMarker>(File.ReadAllText(path));
                return marker != null && (marker.asset_id == asset.id || marker.asset_name == asset.name);
            }
            catch (Exception) { return false; }
        }

        /// <summary>The player agreed: start the updater (it waits for this game to close) and quit.</summary>
        public void Install()
        {
            PromptOpen = false;
            if (State != UpdateState.Available || !CanInstall) return;
            if (IsLinux && !HasPython())
            {
                Fail("Updating needs Python 3.10 or newer. Install python3 with your package manager, then try again.");
                return;
            }
            try
            {
                int pid = Process.GetCurrentProcess().Id;
                ProcessStartInfo start;
                if (IsLinux)
                    // No console to show it in, so the updater writes its output beside the game.
                    start = new ProcessStartInfo("/bin/sh", $"{Quote(UpdaterScript)} --wait-pid {pid} --relaunch --log {Quote(Path.Combine(InstallDirectory, "update.log"))}");
                else
                {
                    // Its console window shows the progress; it closes by itself unless something goes wrong.
                    start = new ProcessStartInfo("cmd.exe", $"/c \"{Quote(UpdaterScript)} --wait-pid {pid} --relaunch\"") { CreateNoWindow = false };
                    start.EnvironmentVariables["ASHBELOW_NO_PAUSE"] = "1";
                }
                start.UseShellExecute = false;
                start.WorkingDirectory = InstallDirectory;
                Process.Start(start);
            }
            catch (Exception error)
            {
                Fail("Could not start the updater: " + error.Message);
                return;
            }
            State = UpdateState.Installing;
            Message = "Closing to update. The game opens again when it is done.";
            Application.Quit();
        }

        private static bool HasPython()
        {
            foreach (string python in new[] { "python3", "python" })
            {
                try
                {
                    using var check = Process.Start(new ProcessStartInfo(python, "-c \"import sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)\"")
                        { UseShellExecute = false, CreateNoWindow = true });
                    if (check != null && check.WaitForExit(5000) && check.ExitCode == 0) return true;
                }
                catch (Exception) { /* Not installed under this name. */ }
            }
            return false;
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

        private void Fail(string message)
        {
            State = UpdateState.Failed;
            Message = message;
        }

        /// <summary>Shows how an update this game started went, once, when the game opens again.</summary>
        private bool ReadLastResult()
        {
            string path = Path.Combine(InstallDirectory, ResultFile);
            try
            {
                if (!File.Exists(path)) return false;
                var result = JsonUtility.FromJson<Result>(File.ReadAllText(path));
                File.Delete(path);
                if (result == null || string.IsNullOrEmpty(result.message)) return false;
                Message = result.message;
                State = result.ok ? UpdateState.UpToDate : UpdateState.Failed;
                return true;
            }
            catch (Exception) { return false; /* A missing or broken note is not worth bothering the player with. */ }
        }
    }
}

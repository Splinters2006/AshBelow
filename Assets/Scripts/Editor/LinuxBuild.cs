using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Slopgame.Editor
{
    public static class LinuxBuild
    {
        private const string Scene = "Assets/Scenes/Dungeon.unity";

        [MenuItem("Slopgame/Linux/Build x86_64 and ZIP")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
                throw new BuildFailedException("Linux Build Support is missing for this editor. Install Linux Build Support (Mono) for Unity 6000.6.3f1.");
            if (!File.Exists(Scene)) throw new BuildFailedException("Open Slopgame > Create playable dungeon scene before building.");
            if (!Application.isBatchMode && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Shares the company/product names (and so the save folder) and window defaults with the Windows build.
            WindowsBuild.Configure();
            // The "AshBelow-Linux-" prefix is how the updater tells this ZIP apart from the Windows one in a release.
            string releases = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Linux"));
            string release = "AshBelow-Linux-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string folder = Path.Combine(releases, release);
            Directory.CreateDirectory(folder);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(folder, "AshBelow.x86_64"),
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Linux build failed: " + report.summary.result);
            File.Copy("scripts/update-game.py", Path.Combine(folder, "update-game.py"));
            WindowsBuild.WriteReleaseMarker(folder, release);
            File.Copy("scripts/Update.sh", Path.Combine(folder, "Update.sh"));
            File.WriteAllText(Path.Combine(folder, "START-HERE.txt"),
                "Extract the entire ZIP, then run ./AshBelow.x86_64 (first run: chmod +x AshBelow.x86_64). Keep all accompanying files and folders together.\n\n"
                + "WASD / arrows: move | Mouse: aim | Hold/release left: charged attack | Right: class skill | Q/E: artifact abilities\n"
                + "Space: dodge | F: interact / descend after clearing the floor\n\n"
                + "Ash and shop upgrades are saved under ~/.config/unity3d/DefaultCompany/Ash Below.\n"
                + "To update: close the game, run 'sh Update.sh' (needs Python 3.10+), then run ./AshBelow.x86_64 again.\n"
                + "The updater backs up saves and replaces this installation with the new version. Do not delete your save folder.\n");
            string archive = Path.Combine(releases, release + ".zip");
            ZipFile.CreateFromDirectory(folder, archive, System.IO.Compression.CompressionLevel.Optimal, true);
            Debug.Log("LINUX_BUILD_OK: " + archive);
        }
    }
}

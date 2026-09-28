using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Slopgame.Editor
{
    public static class WindowsBuild
    {
        private const string Scene = "Assets/Scenes/Dungeon.unity";

        [MenuItem("Slopgame/Windows/Configure Windows defaults")]
        public static void Configure()
        {
            PlayerSettings.productName = "Ash Below";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
            Debug.Log("WINDOWS_CONFIGURATION_OK: x64 build command, Mono, Direct3D 11, resizable 1280x720 window. Windows module installed: "
                + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64));
        }

        [MenuItem("Slopgame/Windows/Build EXE and ZIP")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Windows Build Support is missing for this editor. Install Windows Build Support (Mono) for Unity 6000.6.3f1, or build with the matching Windows editor.");
            if (!File.Exists(Scene)) throw new BuildFailedException("Open Slopgame > Create playable dungeon scene before building.");
            if (!Application.isBatchMode && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Configure();
            // Separate each release so old DLLs/data cannot leak into a new bundle.
            string releases = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows"));
            string release = "AshBelow-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string folder = Path.Combine(releases, release);
            Directory.CreateDirectory(folder);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(folder, "AshBelow.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Windows build failed: " + report.summary.result);
            File.WriteAllText(Path.Combine(folder, "START-HERE.txt"),
                "Extract the entire ZIP, then open AshBelow.exe. Keep all accompanying files and folders together.\r\n\r\n"
                + "WASD / arrows: move | Mouse: aim | Left click: slash | Right click: heavy swipe\r\n"
                + "Space: dodge | E: descend after clearing the floor | Alt+F4: quit\r\n");
            string archive = Path.Combine(releases, release + ".zip");
            ZipFile.CreateFromDirectory(folder, archive, System.IO.Compression.CompressionLevel.Optimal, true);
            Debug.Log("WINDOWS_BUILD_OK: " + archive);
        }
    }
}

using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// Detects import of the Game State Machine sample and updates SamplePathInfo.cs with the versioned sample path.
    /// </summary>
    /// <remarks>
    /// Runs from Unity asset postprocessing.
    /// When an imported asset matches the Game State Machine sample location, the tracker reads the package version from <c>package.json</c>
    /// and writes the correct path of the sample in <see cref="SamplePathInfo.SamplePath"/>.
    /// This keeps sample graph and asset references aligned with the user-imported sample version.
    /// </remarks>
    public class GameStateMachinePathTracker : AssetPostprocessor
    {
        private const string PackageJsonPath = "Packages/com.unity.graphtoolkit-samples/package.json";
        private const string AssetsSamplesRoot = "Assets/Samples/";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var str in importedAssets)
            {
                if (!str.StartsWith(AssetsSamplesRoot) || !str.Contains("GameStateMachine"))
                    continue;
                if (str.EndsWith("SamplePathInfo.cs"))
                    continue;

                WriteSamplePathInfo();
                break;
            }
        }

        private static void WriteSamplePathInfo()
        {
            if (!File.Exists(PackageJsonPath))
            {
                Debug.LogError("Could not find package.json at: " + PackageJsonPath);
                return;
            }

            try
            {
                var packageData = JsonUtility.FromJson<PackageData>(File.ReadAllText(PackageJsonPath));
                if (string.IsNullOrEmpty(packageData.version) || string.IsNullOrEmpty(packageData.displayName))
                {
                    Debug.LogError("Could not read version or displayName from package.json");
                    return;
                }

                var sampleRootPath = $"{AssetsSamplesRoot}{packageData.displayName}/{packageData.version}/GameStateMachine Sample/";
                var samplePathInfoPath = $"{sampleRootPath}Editor/SamplePathInfo.cs";

                var samplePathInfoContent =
                    $@"// AUTO-GENERATED: Do not modify manually.
namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{{
    public static class SamplePathInfo
    {{
        public const string SamplePath = ""{sampleRootPath}"";
    }}
}}";
                var existing = File.Exists(samplePathInfoPath) ? NormalizeLineEndings(File.ReadAllText(samplePathInfoPath)) : null;
                if (existing == NormalizeLineEndings(samplePathInfoContent))
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(samplePathInfoPath) ?? string.Empty);
                File.WriteAllText(samplePathInfoPath, samplePathInfoContent);
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"GameStateMachinePathTracker: failed to write SamplePathInfo: {e.Message}");
            }
        }

        private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");

        [Serializable]
        private class PackageData
        {
            public string version;
            public string displayName;
        }
    }
}

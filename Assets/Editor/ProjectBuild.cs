using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WindEnergy.Editor
{
    public static class ProjectBuild
    {
        [Serializable]
        private sealed class ExportEvidence
        {
            public string result;
            public string outputPath;
            public string unityVersion;
            public string finishedUtc;
            public int errors;
            public int warnings;
            public ulong bytes;
            public double seconds;
        }

        public static void ExportIos()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                throw new BuildFailedException("Select iOS before exporting.");
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length != 1 || scenes[0] != "Assets/Scenes/WindEnergyAR.unity")
                throw new BuildFailedException("Exactly the approved AR scene must be enabled.");
            var workspace = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var output = Path.Combine(workspace, "builds", "ios", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            if (Directory.Exists(output)) throw new IOException("Refusing to overwrite an existing export.");
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.Development
            });
            var summary = report.summary;
            File.WriteAllText(Path.Combine(output, "unity-export-report.json"), JsonUtility.ToJson(new ExportEvidence
            {
                result = summary.result.ToString(), outputPath = output,
                unityVersion = Application.unityVersion, finishedUtc = DateTime.UtcNow.ToString("O"),
                errors = summary.totalErrors, warnings = summary.totalWarnings,
                bytes = summary.totalSize, seconds = summary.totalTime.TotalSeconds
            }, true) + "\n");
            if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0 || !Directory.Exists(Path.Combine(output, "Unity-iPhone.xcodeproj")))
                throw new BuildFailedException("iOS export failed; inspect BuildReport and editor log.");
            Debug.Log("P2_IOS_EXPORT_SUCCEEDED: " + output);
        }
    }
}

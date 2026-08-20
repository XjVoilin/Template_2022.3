using System;
using System.IO;
using UnityEngine;

namespace Game.Editor.Build
{
    [Serializable]
    public sealed class TemplateBuildSettings
    {
        public const string RelativePath = "ProjectSettings/JulyBuildSettings.json";

        public string platform = "auto";
        public string environment = "dev";
        public string coreVersion = "0.1.0";
        public string contentVersion = "0.1.0";
        public string packageName = "DefaultPackage";
        public string outputRoot = "BuildArtifacts";
        public bool generateLuban = true;
        public bool generateHybridClr = true;
        public bool buildPlayer = true;
        public bool developmentBuild = true;
        public string coscliPath = "Tools/coscli/coscli.exe";
        public string cosBucket = string.Empty;
        public string cosPrefix = "releases";

        public static TemplateBuildSettings Load()
        {
            var fullPath = Path.GetFullPath(RelativePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Build settings not found: {RelativePath}", fullPath);

            var settings = JsonUtility.FromJson<TemplateBuildSettings>(File.ReadAllText(fullPath));
            return settings ?? throw new InvalidDataException($"Invalid build settings: {RelativePath}");
        }

        public void Save()
        {
            var fullPath = Path.GetFullPath(RelativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, JsonUtility.ToJson(this, true));
        }
    }

    public sealed class TemplateBuildRequest
    {
        public string Platform;
        public string Environment;
        public string CoreVersion;
        public string ContentVersion;
        public bool ContentOnly;
        public bool Upload;
        public bool Interactive;
        public bool AllowOverwrite;
    }

    internal static class TemplateBuildKeys
    {
        public const string Settings = "template.settings";
        public const string Request = "template.request";
        public const string ProjectRoot = "template.projectRoot";
        public const string ArtifactRoot = "template.artifactRoot";
        public const string YooAssetOutput = "template.yooAssetOutput";
        public const string PlayerOutput = "template.playerOutput";
        public const string ReleaseManifest = "template.releaseManifest";
    }
}

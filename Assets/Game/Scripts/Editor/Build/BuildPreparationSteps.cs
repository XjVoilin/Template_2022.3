using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CozyYard.Editor;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using July.Build;
using UnityEditor;
using UnityEngine;

namespace GameTemplate.Editor.Build
{
    internal sealed class BuildPreflightStep : IBuildStep
    {
        private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+$");
        private static readonly Regex SegmentPattern = new(@"^[A-Za-z0-9._-]+$");

        public string Name => "Preflight";

        public string Validate(BuildContext context)
        {
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            if (EditorUserBuildSettings.activeBuildTarget != context.Target)
                return $"Active build target must be {context.Target}, current target is " +
                       $"{EditorUserBuildSettings.activeBuildTarget}.";
            if (!VersionPattern.IsMatch(request.CoreVersion ?? string.Empty))
                return $"Invalid coreVersion '{request.CoreVersion}'. Expected x.y.z.";
            if (!VersionPattern.IsMatch(request.ContentVersion ?? string.Empty))
                return $"Invalid contentVersion '{request.ContentVersion}'. Expected x.y.z.";
            if (!IsSafeSegment(context.Environment) || !IsSafeSegment(context.Platform))
                return "Environment and platform may contain only letters, digits, dot, underscore, and hyphen.";
            if (string.IsNullOrWhiteSpace(settings.packageName))
                return "packageName is required in ProjectSettings/JulyBuildSettings.json.";

            var existingManifest = Path.Combine(BuildArtifactPaths.GetYooPackageDirectory(context),
                "release-manifest.json");
            if (File.Exists(existingManifest) && !request.AllowOverwrite)
                return $"Release {request.ContentVersion} already exists: {existingManifest}. " +
                       "Bump contentVersion or pass -allowOverwrite true.";

            var sdkError = IntegrationProfileMenu.ValidateBuildProfile(context.Platform);
            if (!string.IsNullOrEmpty(sdkError)) return sdkError;

            if (settings.generateHybridClr)
            {
                if (!new InstallerController().HasInstalledHybridCLR())
                    return "HybridCLR is not initialized. Run HybridCLR/Installer before building.";
                if (!SettingsUtil.Enable)
                    return "HybridCLR is disabled in ProjectSettings/HybridCLRSettings.asset.";
                if (SettingsUtil.HotUpdateAssemblyNamesExcludePreserved.Count == 0)
                    return "HybridCLR has no hot-update assemblies configured.";
                if (request.ContentOnly && !Directory.Exists(HybridClrArtifactUtility.GetBaselineDirectory(context)))
                    return $"AOT baseline not found for coreVersion {request.CoreVersion}. Run a full build first.";
            }

            return null;
        }

        public BuildStepResult Execute(BuildContext context)
        {
            Directory.CreateDirectory(context.GetRequired<string>(TemplateBuildKeys.ArtifactRoot));
            return BuildStepResult.Success();
        }

        private static bool IsSafeSegment(string value) =>
            !string.IsNullOrWhiteSpace(value) && SegmentPattern.IsMatch(value);
    }

    internal sealed class GenerateLubanStep : IBuildStep
    {
        public string Name => "Generate Luban Tables";
        public string Validate(BuildContext context) =>
            LubanGeneratorWindow.ValidatePrerequisites() ? null : "Luban prerequisites are not available.";
        public BuildStepResult Execute(BuildContext context) => LubanGeneratorWindow.GenerateAll()
            ? BuildStepResult.Success()
            : BuildStepResult.Failure("Luban generation failed.");
    }

    internal sealed class GenerateHybridClrStep : IBuildStep
    {
        public string Name => "Generate HybridCLR";
        public string Validate(BuildContext context) => null;

        public BuildStepResult Execute(BuildContext context)
        {
            PrebuildCommand.GenerateAll();
            AssetDatabase.Refresh();
            return BuildStepResult.Success();
        }
    }

    internal sealed class CompileHotUpdateStep : IBuildStep
    {
        public string Name => "Compile Hot-update Assemblies";
        public string Validate(BuildContext context) => null;

        public BuildStepResult Execute(BuildContext context)
        {
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            CompileDllCommand.CompileDll(context.Target, settings.developmentBuild);
            return BuildStepResult.Success();
        }
    }

    internal sealed class SyncHybridClrArtifactsStep : IBuildStep
    {
        public string Name => "Sync HybridCLR Artifacts";
        public string Validate(BuildContext context) => null;
        public BuildStepResult Execute(BuildContext context) => HybridClrArtifactUtility.Sync(context);
    }

    internal sealed class ArchiveAotBaselineStep : IBuildStep
    {
        public string Name => "Archive AOT Baseline";

        public string Validate(BuildContext context)
        {
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var baseline = HybridClrArtifactUtility.GetBaselineDirectory(context);
            if (Directory.Exists(baseline) && !request.AllowOverwrite)
                return $"AOT baseline already exists: {baseline}. Bump coreVersion or pass -allowOverwrite true.";
            return null;
        }

        public BuildStepResult Execute(BuildContext context) => HybridClrArtifactUtility.ArchiveBaseline(context);
    }

    [Serializable]
    internal sealed class HybridClrReleaseManifest
    {
        public string[] hotUpdateAssemblies;
        public string[] aotMetadataAssemblies;
    }

    internal static class HybridClrArtifactUtility
    {
        private const string HotUpdateDirectory = "Assets/Game/Res/HotUpdateDlls";
        private const string AotMetadataDirectory = "Assets/Game/Res/AOTMetaDlls";
        private const string ManifestPath = HotUpdateDirectory + "/hybridclr-manifest.json";
        private static readonly Regex AotSection = new(
            @"//\s*\{\{\s*AOT assemblies(.+?)//\s*\}\}", RegexOptions.Singleline);
        private static readonly Regex DllName = new("\"([^\"]+\\.dll)\"");

        public static string GetBaselineDirectory(BuildContext context)
        {
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var projectRoot = context.GetRequired<string>(TemplateBuildKeys.ProjectRoot);
            return Path.GetFullPath(Path.Combine(projectRoot, settings.outputRoot, "AOTBaselines",
                context.Target.ToString(), context.Platform, request.CoreVersion));
        }

        public static BuildStepResult Sync(BuildContext context)
        {
            Directory.CreateDirectory(HotUpdateDirectory);
            Directory.CreateDirectory(AotMetadataDirectory);

            var hotNames = SettingsUtil.HotUpdateAssemblyNamesExcludePreserved
                .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var hotSource = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(context.Target);
            foreach (var name in hotNames)
            {
                var error = CopyDll(hotSource, HotUpdateDirectory, name);
                if (error != null) return BuildStepResult.Failure(error);
            }

            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var aotSource = request.ContentOnly
                ? GetBaselineDirectory(context)
                : SettingsUtil.GetAssembliesPostIl2CppStripDir(context.Target);
            var referenceFile = request.ContentOnly
                ? Path.Combine(aotSource, "AOTGenericReferences.cs")
                : Path.Combine(Application.dataPath,
                    SettingsUtil.HybridCLRSettings.outputAOTGenericReferenceFile);
            var aotNames = ReadAotAssemblyNames(referenceFile);
            aotNames.Add("Aot.Runtime");

            var copiedAotNames = new List<string>();
            foreach (var name in aotNames.OrderBy(name => name, StringComparer.Ordinal))
            {
                var source = Path.Combine(aotSource, name + ".dll");
                if (!File.Exists(source))
                {
                    Debug.LogWarning($"[HybridCLR] AOT metadata assembly not found, skipped: {source}");
                    continue;
                }
                File.Copy(source, Path.Combine(AotMetadataDirectory, name + ".dll.bytes"), true);
                copiedAotNames.Add(name);
            }

            var manifest = new HybridClrReleaseManifest
            {
                hotUpdateAssemblies = hotNames,
                aotMetadataAssemblies = copiedAotNames.ToArray(),
            };
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.Refresh();
            return BuildStepResult.Success();
        }

        public static BuildStepResult ArchiveBaseline(BuildContext context)
        {
            var source = SettingsUtil.GetAssembliesPostIl2CppStripDir(context.Target);
            if (!Directory.Exists(source))
                return BuildStepResult.Failure($"Stripped AOT directory not found: {source}");

            var destination = GetBaselineDirectory(context);
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source, "*.dll", SearchOption.TopDirectoryOnly))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);

            var referenceFile = Path.Combine(Application.dataPath,
                SettingsUtil.HybridCLRSettings.outputAOTGenericReferenceFile);
            if (File.Exists(referenceFile))
                File.Copy(referenceFile, Path.Combine(destination, "AOTGenericReferences.cs"), true);
            return BuildStepResult.Success();
        }

        private static string CopyDll(string sourceDirectory, string destinationDirectory, string name)
        {
            var source = Path.Combine(sourceDirectory, name + ".dll");
            if (!File.Exists(source)) return $"Hot-update assembly not found: {source}";
            if (new FileInfo(source).Length == 0) return $"Hot-update assembly is empty: {source}";
            File.Copy(source, Path.Combine(destinationDirectory, name + ".dll.bytes"), true);
            return null;
        }

        private static HashSet<string> ReadAotAssemblyNames(string path)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return names;
            var section = AotSection.Match(File.ReadAllText(path));
            if (!section.Success) return names;
            foreach (Match match in DllName.Matches(section.Groups[1].Value))
                names.Add(Path.GetFileNameWithoutExtension(match.Groups[1].Value));
            return names;
        }
    }
}

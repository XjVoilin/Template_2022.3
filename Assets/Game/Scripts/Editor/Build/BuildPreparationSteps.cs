using System;
using System.IO;
using System.Text.RegularExpressions;
using CozyYard.Editor;
using HybridCLR.Editor;
using HybridCLR.Editor.Installer;
using July.Build;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Build
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

            var sdkError = PlatformProfileMenu.ValidateBuildProfile(context.Platform);
            if (!string.IsNullOrEmpty(sdkError)) return sdkError;

            if (settings.generateHybridClr)
            {
                if (!new InstallerController().HasInstalledHybridCLR())
                    return "HybridCLR is not initialized. Run HybridCLR/Installer before building.";
                if (!SettingsUtil.Enable)
                    return "HybridCLR is disabled in ProjectSettings/HybridCLRSettings.asset.";
                if (!HybridCLRBuildService.ValidateSettings(false))
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
            var profile = HybridClrArtifactUtility.CreateProfile(context);
            return HybridCLRBuildService.GenerateAllAndCopyDlls(profile, context.Target)
                ? BuildStepResult.Success()
                : BuildStepResult.Failure("HybridCLR Generate All or DLL copy failed.");
        }
    }

    internal sealed class CompileHotUpdateStep : IBuildStep
    {
        public string Name => "Compile Hot-update Assemblies";
        public string Validate(BuildContext context) => null;

        public BuildStepResult Execute(BuildContext context)
        {
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var profile = HybridClrArtifactUtility.CreateProfile(context);
            return HybridCLRBuildService.CompileHotUpdateOnly(profile, context.Target,
                context.Platform, request.CoreVersion, settings.developmentBuild)
                ? BuildStepResult.Success()
                : BuildStepResult.Failure("HybridCLR hot-update compilation failed.");
        }
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

        public BuildStepResult Execute(BuildContext context)
        {
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var profile = HybridClrArtifactUtility.CreateProfile(context);
            return HybridCLRBuildService.BackupAotDlls(profile, context.Target,
                context.Platform, request.CoreVersion)
                ? BuildStepResult.Success()
                : BuildStepResult.Failure("HybridCLR AOT baseline backup failed.");
        }
    }

    internal static class HybridClrArtifactUtility
    {
        private const string HotUpdateDirectory = "Assets/Game/Res/HotUpdateDlls";
        private const string AotMetadataDirectory = "Assets/Game/Res/AOTMetaDlls";

        public static HybridCLRBuildProfile CreateProfile(BuildContext context)
        {
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            var projectRoot = context.GetRequired<string>(TemplateBuildKeys.ProjectRoot);
            var backupRoot = Path.GetFullPath(Path.Combine(projectRoot, settings.outputRoot,
                "AOTBaselines"));
            var referencesPath = Path.Combine(Application.dataPath,
                SettingsUtil.HybridCLRSettings.outputAOTGenericReferenceFile);
            return new HybridCLRBuildProfile(HotUpdateDirectory, AotMetadataDirectory,
                backupRoot, referencesPath, new[] { "Aot.Runtime" });
        }

        public static string GetBaselineDirectory(BuildContext context)
        {
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            return HybridCLRBuildService.GetAotBackupDirectory(CreateProfile(context),
                context.Target, context.Platform, request.CoreVersion);
        }
    }
}

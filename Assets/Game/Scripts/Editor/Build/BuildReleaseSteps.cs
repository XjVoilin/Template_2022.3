using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using July.Build;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using Debug = UnityEngine.Debug;
using BuildContext = July.Build.BuildContext;

namespace Game.Editor.Build
{
    internal static class BuildArtifactPaths
    {
        public static string GetYooBuildRoot(BuildContext context)
        {
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var projectRoot = context.GetRequired<string>(TemplateBuildKeys.ProjectRoot);
            return Path.GetFullPath(Path.Combine(projectRoot, settings.outputRoot, "YooAsset",
                context.Environment, context.Platform, request.CoreVersion));
        }

        public static string GetYooPackageDirectory(BuildContext context)
        {
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            return Path.Combine(GetYooBuildRoot(context), context.Target.ToString(),
                settings.packageName, context.Version);
        }
    }

    internal sealed class BuildYooAssetStep : IBuildStep
    {
        public string Name => "Build YooAsset Content";
        public string Validate(BuildContext context) => null;

        public BuildStepResult Execute(BuildContext context)
        {
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var uniqueBundleName = AssetBundleCollectorSettingData.Setting.UniqueBundleName;
            var shaderBundleName = DefaultPackRule.CreateShadersPackRuleResult()
                .GetBundleName(settings.packageName, uniqueBundleName);

            var parameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = BuildArtifactPaths.GetYooBuildRoot(context),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = EBuildPipeline.ScriptableBuildPipeline.ToString(),
                BuildBundleType = (int)EBuildBundleType.AssetBundle,
                BuildTarget = context.Target,
                PackageName = settings.packageName,
                PackageVersion = context.Version,
                PackageNote = $"{context.Environment}/{context.Platform}",
                EnableSharePackRule = true,
                VerifyBuildingResult = true,
                FileNameStyle = EFileNameStyle.HashName,
                BuildinFileCopyOption = request.ContentOnly
                    ? EBuildinFileCopyOption.None
                    : EBuildinFileCopyOption.ClearAndCopyAll,
                BuildinFileCopyParams = string.Empty,
                CompressOption = ECompressOption.LZ4,
                ClearBuildCacheFiles = false,
                UseAssetDependencyDB = true,
                BuiltinShadersBundleName = shaderBundleName,
            };

            var result = new ScriptableBuildPipeline().Run(parameters, true);
            if (!result.Success)
                return BuildStepResult.Failure(
                    $"YooAsset failed at {result.FailedTask}: {result.ErrorInfo}");

            context.Set(TemplateBuildKeys.YooAssetOutput, result.OutputPackageDirectory);
            return BuildStepResult.Success();
        }
    }

    internal sealed class BuildWebGlPlayerStep : IBuildStep
    {
        public string Name => "Build WebGL Player";

        public string Validate(BuildContext context)
        {
            return EditorBuildSettings.scenes.Any(scene => scene.enabled && File.Exists(scene.path))
                ? null
                : "No enabled scenes exist in EditorBuildSettings.";
        }

        public BuildStepResult Execute(BuildContext context)
        {
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            var output = Path.Combine(context.GetRequired<string>(TemplateBuildKeys.ArtifactRoot), "Player");
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path).ToArray();
            var options = settings.developmentBuild ? BuildOptions.Development : BuildOptions.None;
            var report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = context.Target,
                locationPathName = output,
                options = options,
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                return BuildStepResult.Failure(
                    $"WebGL player build ended with {report.summary.result} and " +
                    $"{report.summary.totalErrors} errors.");

            context.Set(TemplateBuildKeys.PlayerOutput, output);
            return BuildStepResult.Success();
        }
    }

    [Serializable]
    internal sealed class ReleaseManifestData
    {
        public int schemaVersion = 1;
        public string project;
        public string platform;
        public string environment;
        public string coreVersion;
        public string contentVersion;
        public string packageName;
        public string generatedUtc;
        public ReleaseFileData[] files;
    }

    [Serializable]
    internal sealed class ReleaseFileData
    {
        public string path;
        public long size;
        public string sha256;
    }

    internal sealed class WriteReleaseManifestStep : IBuildStep
    {
        public string Name => "Write Release Manifest";
        public string Validate(BuildContext context) => null;

        public BuildStepResult Execute(BuildContext context)
        {
            if (!context.TryGet<string>(TemplateBuildKeys.YooAssetOutput, out var output) ||
                !Directory.Exists(output))
                return BuildStepResult.Failure("YooAsset output directory is unavailable.");

            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var manifestPath = Path.Combine(output, "release-manifest.json");
            var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(path, manifestPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new ReleaseFileData
                {
                    path = Path.GetRelativePath(output, path).Replace('\\', '/'),
                    size = new FileInfo(path).Length,
                    sha256 = ComputeSha256(path),
                }).ToArray();

            var manifest = new ReleaseManifestData
            {
                project = Application.productName,
                platform = context.Platform,
                environment = context.Environment,
                coreVersion = request.CoreVersion,
                contentVersion = context.Version,
                packageName = settings.packageName,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                files = files,
            };
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            context.Set(TemplateBuildKeys.ReleaseManifest, manifestPath);
            Debug.Log($"[TemplateBuild] Release manifest contains {files.Length} files: {manifestPath}");
            return BuildStepResult.Success();
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
    }

    internal sealed class UploadToCosStep : IBuildStep
    {
        public string Name => "Upload Release to COS";

        public string Validate(BuildContext context)
        {
            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            if (string.IsNullOrWhiteSpace(settings.cosBucket))
                return "cosBucket is required when -upload true is used.";
            var executable = ResolveCosCli(context, settings);
            return File.Exists(executable) ? null : $"coscli not found: {executable}";
        }

        public BuildStepResult Execute(BuildContext context)
        {
            if (!context.TryGet<string>(TemplateBuildKeys.YooAssetOutput, out var source))
                return BuildStepResult.Failure("YooAsset output directory is unavailable.");

            var settings = context.GetRequired<TemplateBuildSettings>(TemplateBuildKeys.Settings);
            var request = context.GetRequired<TemplateBuildRequest>(TemplateBuildKeys.Request);
            var prefix = (settings.cosPrefix ?? string.Empty).Trim('/');
            var remote = $"cos://{settings.cosBucket}/" +
                         $"{prefix}/{context.Environment}/{context.Platform}/" +
                         $"{request.CoreVersion}/{context.Version}/";
            var result = RunProcess(ResolveCosCli(context, settings),
                $"sync {Quote(source)} {Quote(remote)} --recursive");
            return result.exitCode == 0
                ? BuildStepResult.Success()
                : BuildStepResult.Failure($"coscli exited with {result.exitCode}: {result.stderr}");
        }

        private static string ResolveCosCli(BuildContext context, TemplateBuildSettings settings)
        {
            var projectRoot = context.GetRequired<string>(TemplateBuildKeys.ProjectRoot);
            var configured = Path.GetFullPath(Path.Combine(projectRoot, settings.coscliPath));
            if (File.Exists(configured)) return configured;
            if (configured.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return configured.Substring(0, configured.Length - 4);
            return configured;
        }

        private static (int exitCode, string stderr) RunProcess(string executable, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo);
            if (process == null) return (-1, "Failed to start coscli.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (!string.IsNullOrWhiteSpace(stdout.Result)) Debug.Log(stdout.Result.TrimEnd());
            return (process.ExitCode, stderr.Result.Trim());
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}

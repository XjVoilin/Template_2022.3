using System;
using System.Collections.Generic;
using System.Linq;
using July.Build;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Editor.Build
{
    public static class TemplateBuildPipeline
    {
        public static void BuildFromCommandLine()
        {
            var request = CreateRequest(
                contentOnly: GetBoolArgument("-contentOnly", false),
                interactive: false);
            request.Platform = GetArgument("-platform", request.Platform);
            request.Environment = GetArgument("-environment", request.Environment);
            request.CoreVersion = GetArgument("-coreVersion", request.CoreVersion);
            request.ContentVersion = GetArgument("-contentVersion",
                GetArgument("-version", request.ContentVersion));
            request.Upload = GetBoolArgument("-upload", false);
            request.AllowOverwrite = GetBoolArgument("-allowOverwrite", false);

            var result = Run(request);
            if (!result.Succeeded)
                throw new BuildFailedException(
                    $"Template build failed at '{result.FailedStep}': {result.Error}");
        }

        public static BuildResult Run(TemplateBuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var settings = TemplateBuildSettings.Load();
            var platform = ResolvePlatform(request.Platform);
            var projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            var artifactRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot,
                settings.outputRoot, request.Environment, platform, request.CoreVersion,
                request.ContentVersion));
            var context = new BuildContext(BuildTarget.WebGL, platform,
                    request.Environment, request.ContentVersion, request.Interactive)
                .Set(TemplateBuildKeys.Settings, settings)
                .Set(TemplateBuildKeys.Request, request)
                .Set(TemplateBuildKeys.ProjectRoot, projectRoot)
                .Set(TemplateBuildKeys.ArtifactRoot, artifactRoot);

            var steps = CreateSteps(settings, request);
            var result = new BuildRunner(new UnityBuildHost()).Run(context, steps);
            if (result.Succeeded)
            {
                context.TryGet<string>(TemplateBuildKeys.ReleaseManifest, out var manifest);
                Debug.Log($"[TemplateBuild] Complete in {result.Elapsed}. Manifest: {manifest}");
            }
            else if (result.Outcome != BuildOutcome.Cancelled)
            {
                Debug.LogError($"[TemplateBuild] Failed at {result.FailedStep}: {result.Error}");
            }
            return result;
        }

        private static IReadOnlyList<IBuildStep> CreateSteps(TemplateBuildSettings settings,
            TemplateBuildRequest request)
        {
            var steps = new List<IBuildStep> { new BuildPreflightStep() };
            if (settings.generateLuban) steps.Add(new GenerateLubanStep());

            if (settings.generateHybridClr)
            {
                steps.Add(request.ContentOnly
                    ? (IBuildStep)new CompileHotUpdateStep()
                    : new GenerateHybridClrStep());
                if (!request.ContentOnly) steps.Add(new ArchiveAotBaselineStep());
            }

            steps.Add(new BuildYooAssetStep());
            if (!request.ContentOnly && settings.buildPlayer) steps.Add(new BuildWebGlPlayerStep());
            steps.Add(new WriteReleaseManifestStep());
            if (request.Upload) steps.Add(new UploadToCosStep());
            return steps;
        }

        private static TemplateBuildRequest CreateRequest(bool contentOnly, bool interactive)
        {
            var settings = TemplateBuildSettings.Load();
            return new TemplateBuildRequest
            {
                Platform = settings.platform,
                Environment = settings.environment,
                CoreVersion = settings.coreVersion,
                ContentVersion = settings.contentVersion,
                ContentOnly = contentOnly,
                Interactive = interactive,
            };
        }

        private static string ResolvePlatform(string configured)
        {
            return string.Equals(configured, "auto", StringComparison.OrdinalIgnoreCase)
                ? PlatformProfileMenu.ResolveActivePlatform()
                : (configured ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string GetArgument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return fallback;
        }

        private static bool GetBoolArgument(string name, bool fallback)
        {
            var value = GetArgument(name, null);
            if (value == null)
                return Environment.GetCommandLineArgs()
                    .Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) || fallback;
            return bool.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }
}

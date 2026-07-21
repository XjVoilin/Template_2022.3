using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Resource.YooAsset;
using YooAsset;

namespace GameTemplate.Aot
{
    internal static class YooAssetBootstrap
    {
        public static async UniTask<ResourcePackage> InitializeAsync(YooAssetOptions options,
            CancellationToken ct)
        {
            YooAssets.Initialize();
            var package = YooAssets.TryGetPackage(options.PackageName) ??
                          YooAssets.CreatePackage(options.PackageName);
            if (options.SetAsDefaultPackage) YooAssets.SetDefaultPackage(package);

            if (package.InitializeStatus == EOperationStatus.None)
            {
                var parameters = options.CreateInitializeParameters?.Invoke(package) ??
                                 CreateParameters(options);
                var operation = package.InitializeAsync(parameters);
                await UniTask.WaitUntil(() => operation.IsDone, cancellationToken: ct);
                EnsureSucceeded(operation.Status, operation.Error, "Initialize YooAsset package");
            }
            else if (package.InitializeStatus != EOperationStatus.Succeed)
            {
                throw new InvalidOperationException(
                    $"YooAsset package '{options.PackageName}' is {package.InitializeStatus}.");
            }

            if (!package.PackageValid)
            {
                var version = package.RequestPackageVersionAsync(true);
                await UniTask.WaitUntil(() => version.IsDone, cancellationToken: ct);
                EnsureSucceeded(version.Status, version.Error, "Request YooAsset package version");

                var manifest = package.UpdatePackageManifestAsync(version.PackageVersion);
                await UniTask.WaitUntil(() => manifest.IsDone, cancellationToken: ct);
                EnsureSucceeded(manifest.Status, manifest.Error, "Update YooAsset package manifest");
            }
            return package;
        }

        private static InitializeParameters CreateParameters(YooAssetOptions options)
        {
            switch (options.PlayMode)
            {
                case EPlayMode.EditorSimulateMode:
#if UNITY_EDITOR
                    var simulate = EditorSimulateModeHelper.SimulateBuild(options.PackageName);
                    return new EditorSimulateModeParameters
                    {
                        EditorFileSystemParameters =
                            FileSystemParameters.CreateDefaultEditorFileSystemParameters(
                                simulate.PackageRootDirectory)
                    };
#else
                    throw new InvalidOperationException("EditorSimulateMode is editor-only.");
#endif
                case EPlayMode.OfflinePlayMode:
                    return new OfflinePlayModeParameters
                    {
                        BuildinFileSystemParameters =
                            FileSystemParameters.CreateDefaultBuildinFileSystemParameters()
                    };
                case EPlayMode.HostPlayMode:
                    var remote = new RemoteServices(options.DefaultHostServer,
                        options.FallbackHostServer);
                    return new HostPlayModeParameters
                    {
                        BuildinFileSystemParameters =
                            FileSystemParameters.CreateDefaultBuildinFileSystemParameters(),
                        CacheFileSystemParameters =
                            FileSystemParameters.CreateDefaultCacheFileSystemParameters(remote)
                    };
                case EPlayMode.WebPlayMode:
                    return new WebPlayModeParameters
                    {
                        WebServerFileSystemParameters =
                            FileSystemParameters.CreateDefaultWebServerFileSystemParameters()
                    };
                default:
                    throw new InvalidOperationException(
                        $"Play mode {options.PlayMode} requires CreateInitializeParameters.");
            }
        }

        private static void EnsureSucceeded(EOperationStatus status, string error, string operation)
        {
            if (status != EOperationStatus.Succeed)
                throw new InvalidOperationException($"{operation} failed: {error}");
        }

        private sealed class RemoteServices : IRemoteServices
        {
            private readonly string _main;
            private readonly string _fallback;

            public RemoteServices(string main, string fallback)
            {
                _main = (main ?? string.Empty).TrimEnd('/');
                _fallback = string.IsNullOrWhiteSpace(fallback)
                    ? _main
                    : fallback.TrimEnd('/');
            }

            public string GetRemoteMainURL(string fileName) => $"{_main}/{fileName}";
            public string GetRemoteFallbackURL(string fileName) => $"{_fallback}/{fileName}";
        }
    }
}

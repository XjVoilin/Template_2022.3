using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Launch;
using July.Logging;
using UnityEngine;
using YooAsset;
#if !UNITY_EDITOR
using HybridCLR;
#endif

namespace GameTemplate.Aot
{
    [Serializable]
    internal sealed class HybridClrReleaseManifest
    {
        public string[] hotUpdateAssemblies;
        public string[] aotMetadataAssemblies;
    }

    public sealed class LoadHotUpdateAssembliesStep : ILaunchStep
    {
        private const string PackageName = "DefaultPackage";
        private const string ManifestAddress = "hybridclr-manifest";

        public string Name => "Load Hot-update Assemblies";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            await UniTask.CompletedTask;
            return true;
#else
            var package = YooAssets.TryGetPackage(PackageName);
            if (package == null || package.InitializeStatus != EOperationStatus.Succeed)
                throw new InvalidOperationException("YooAsset package is not initialized.");

            var manifest = await LoadManifestAsync(package, ct);
            foreach (var assemblyName in manifest.aotMetadataAssemblies ?? Array.Empty<string>())
            {
                var bytes = await LoadBytesAsync(package, assemblyName + ".dll", ct);
                var result = RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
                JLogger.Log($"[HybridCLR] Metadata {assemblyName}: {result}");
            }

            foreach (var assemblyName in manifest.hotUpdateAssemblies ?? Array.Empty<string>())
            {
                var bytes = await LoadBytesAsync(package, assemblyName + ".dll", ct);
                var assembly = Assembly.Load(bytes);
                JLogger.Log($"[HybridCLR] Loaded {assembly.GetName().Name} ({bytes.Length} bytes)");
            }
            return true;
#endif
        }

#if !UNITY_EDITOR
        private static async UniTask<HybridClrReleaseManifest> LoadManifestAsync(
            ResourcePackage package, CancellationToken ct)
        {
            var bytes = await LoadBytesAsync(package, ManifestAddress, ct);
            var manifest = JsonUtility.FromJson<HybridClrReleaseManifest>(
                System.Text.Encoding.UTF8.GetString(bytes));
            return manifest ?? throw new InvalidOperationException("Invalid HybridCLR release manifest.");
        }

        private static async UniTask<byte[]> LoadBytesAsync(ResourcePackage package,
            string address, CancellationToken ct)
        {
            var handle = package.LoadAssetAsync<TextAsset>(address);
            try
            {
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: ct);
                if (handle.Status != EOperationStatus.Succeed || handle.AssetObject is not TextAsset asset)
                    throw new InvalidOperationException(
                        $"Failed to load '{address}': {handle.LastError}");
                return asset.bytes;
            }
            finally
            {
                handle.Release();
            }
        }
#endif
    }
}

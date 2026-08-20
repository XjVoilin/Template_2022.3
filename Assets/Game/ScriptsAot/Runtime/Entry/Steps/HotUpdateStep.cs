using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Launch;
using July.Logging;
using July.Resource;
using July.Resource.YooAsset;
using UnityEngine;
using YooAsset;
#if !UNITY_EDITOR
using HybridCLR;
#endif

namespace Game.Aot
{
    /// <summary>
    /// 按 YooAsset 标签下载并加载 HybridCLR 的 AOT 元数据与热更程序集。
    /// 构建实际收集到的 DLL 就是运行时加载列表，无需额外维护程序集清单。
    /// </summary>
    public sealed class HotUpdateStep : ILaunchStep
    {
        private const string HotUpdateTag = "HotUpdate";
        private const string AotMetadataTag = "AOTMetadata";

        public string Name => "Hot Update";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            await UniTask.CompletedTask;
#else
            var resource = ArchContext.Current.GetSystem<IResourceSystem>() ??
                           throw new InvalidOperationException("资源系统尚未注册。");
            var package = GetPackage(resource);
            var hotUpdateAddresses = GetAddresses(package, HotUpdateTag);
            var aotMetadataAddresses = GetAddresses(package, AotMetadataTag);

            await DownloadAsync(resource, ct);
            await LoadAotMetadataAsync(resource, aotMetadataAddresses, ct);
            await LoadHotUpdateAssembliesAsync(resource, hotUpdateAddresses, ct);
#endif
            return true;
        }

#if !UNITY_EDITOR
        private static async UniTask DownloadAsync(
            IResourceSystem resource, CancellationToken ct)
        {
            await UniTask.WhenAll(
                DownloadByTagAsync(resource, HotUpdateTag, ct),
                DownloadByTagAsync(resource, AotMetadataTag, ct));
        }

        private static async UniTask DownloadByTagAsync(
            IResourceSystem resource, string tag, CancellationToken ct)
        {
            if (!await resource.DownloadByTagWithRetryAsync(tag, ct: ct))
                throw new InvalidOperationException($"资源下载失败，标签: {tag}");
        }

        private static async UniTask LoadAotMetadataAsync(
            IResourceSystem resource, string[] addresses, CancellationToken ct)
        {
            foreach (var address in addresses)
            {
                ct.ThrowIfCancellationRequested();
                var bytes = await LoadBytesAsync(resource, address, ct);
                var result = RuntimeApi.LoadMetadataForAOTAssembly(
                    bytes, HomologousImageMode.SuperSet);
                JLogger.Log($"[HybridCLR] AOT 元数据 {address}: {result}");
            }
        }

        private static async UniTask LoadHotUpdateAssembliesAsync(
            IResourceSystem resource, string[] addresses, CancellationToken ct)
        {
            foreach (var address in addresses)
            {
                ct.ThrowIfCancellationRequested();
                var bytes = await LoadBytesAsync(resource, address, ct);
                var assembly = Assembly.Load(bytes);
                JLogger.Log(
                    $"[HybridCLR] 热更程序集加载成功: {assembly.GetName().Name} ({bytes.Length} bytes)");
            }
        }

        private static ResourcePackage GetPackage(IResourceSystem resource)
        {
            if (resource is not YooAssetResourceSystem yooAsset || yooAsset.Package == null)
                throw new InvalidOperationException("YooAsset 资源包尚未初始化。");
            return yooAsset.Package;
        }

        private static string[] GetAddresses(ResourcePackage package, string tag)
        {
            var assets = package.GetAssetInfos(tag);
            if (assets.Length == 0)
                throw new InvalidOperationException($"未找到标签为 {tag} 的资源。");

            var addresses = new string[assets.Length];
            for (var index = 0; index < assets.Length; index++)
            {
                var address = assets[index].Address;
                if (string.IsNullOrWhiteSpace(address))
                    throw new InvalidOperationException($"标签 {tag} 中存在无地址资源。");
                addresses[index] = address;
            }

            Array.Sort(addresses, StringComparer.Ordinal);
            return addresses;
        }

        private static async UniTask<byte[]> LoadBytesAsync(
            IResourceSystem resource, string address, CancellationToken ct)
        {
            using var handle = await resource.LoadAssetAsync<TextAsset>(address, ct);
            if (handle?.Asset == null)
                throw new InvalidOperationException($"资源加载失败: {address}");
            return handle.Asset.bytes;
        }
#endif
    }
}

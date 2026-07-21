using System.Threading;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Launch;
using July.Resource.YooAsset;
using YooAsset;

namespace GameTemplate.Aot
{
    public sealed class InitResourceStep : ILaunchStep
    {
        public string Name => "Init Resource";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var bootConfig = SeedServices.Resolve<BootConfig>();
            var endpoints = SeedServices.Resolve<CDNEndpoints>();
            var playMode = bootConfig.PlayMode;
#if !UNITY_EDITOR
            if (playMode == JPlayMode.EditorSimulateMode)
                playMode = JPlayMode.OfflinePlayMode;
#endif
            var options = new YooAssetOptions
            {
                PackageName = "DefaultPackage",
                PlayMode = playMode switch
                {
                    JPlayMode.EditorSimulateMode => EPlayMode.EditorSimulateMode,
                    JPlayMode.OfflinePlayMode => EPlayMode.OfflinePlayMode,
                    JPlayMode.HostPlayMode => EPlayMode.HostPlayMode,
                    JPlayMode.WebPlayMode => EPlayMode.WebPlayMode,
                    JPlayMode.CustomPlayMode => EPlayMode.CustomPlayMode,
                    _ => EPlayMode.OfflinePlayMode
                },
                DefaultHostServer = endpoints.MainURL,
                FallbackHostServer = endpoints.MainURL,
                UpdateManifestAfterInitialization = false,
            };

            await YooAssetBootstrap.InitializeAsync(options, ct);
            ArchContext.Current.RegisterSystem(new YooAssetResourceSystem(options));
            return true;
        }
    }
}

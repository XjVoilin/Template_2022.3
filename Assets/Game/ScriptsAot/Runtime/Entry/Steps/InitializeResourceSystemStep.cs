using System.Threading;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Launch;
using July.Resource.YooAsset;
using YooAsset;

namespace Game.Aot
{
    public sealed class InitializeResourceSystemStep : ILaunchStep
    {
        public string Name => "Initialize Resource System";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            var config = SeedServices.Resolve<GameConfig>();
            var endpoints = SeedServices.Resolve<CDNEndpoints>();
            var options = CreateOptions(config, endpoints);
            var resourceSystem = new YooAssetResourceSystem(options);

            // 热更程序集会在 Arch 生命周期初始化新增系统之前加载，因此这里需要提前初始化资源系统。
            await resourceSystem.InitializeAsync(ct);
            ArchContext.Current.RegisterSystem(resourceSystem);
            return true;
        }

        private static YooAssetOptions CreateOptions(GameConfig config, CDNEndpoints endpoints)
        {
            var playMode = ResolvePlayMode(config.PlayMode);
            var options = new YooAssetOptions
            {
                PackageName = "DefaultPackage",
                PlayMode = playMode,
                DefaultHostServer = endpoints.MainURL,
                FallbackHostServer = endpoints.MainURL,
                UpdateManifestAfterInitialization = playMode != EPlayMode.CustomPlayMode,
            };

#if UNITY_WEBGL && JULYGF_WX_MINIGAME
            options.CreateInitializeParameters = _ =>
                WeChatYooAssetFileSystem.CreateInitializeParameters(endpoints.MainURL);
#elif UNITY_WEBGL && JULYGF_DY_MINIGAME
            options.CreateInitializeParameters = _ =>
                TikTokYooAssetFileSystem.CreateInitializeParameters(endpoints.MainURL);
#endif
            return options;
        }

        private static EPlayMode ResolvePlayMode(ResourcePlayMode playMode)
        {
#if UNITY_EDITOR
            return playMode switch
            {
                ResourcePlayMode.EditorSimulateMode => EPlayMode.EditorSimulateMode,
                ResourcePlayMode.OfflinePlayMode => EPlayMode.OfflinePlayMode,
                ResourcePlayMode.HostPlayMode => EPlayMode.HostPlayMode,
                ResourcePlayMode.WebPlayMode => EPlayMode.WebPlayMode,
                ResourcePlayMode.CustomPlayMode => EPlayMode.CustomPlayMode,
                _ => EPlayMode.EditorSimulateMode,
            };
#elif JULYGF_WX_MINIGAME || JULYGF_DY_MINIGAME
            return EPlayMode.WebPlayMode;
#else
            return playMode switch
            {
                ResourcePlayMode.HostPlayMode => EPlayMode.HostPlayMode,
                ResourcePlayMode.WebPlayMode => EPlayMode.WebPlayMode,
                ResourcePlayMode.CustomPlayMode => EPlayMode.CustomPlayMode,
                _ => EPlayMode.OfflinePlayMode,
            };
#endif
        }
    }
}

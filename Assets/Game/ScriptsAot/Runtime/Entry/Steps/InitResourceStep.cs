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
            };

#if UNITY_WEBGL && JULYGF_WX_MINIGAME
            options.PlayMode = EPlayMode.WebPlayMode;
            options.CreateInitializeParameters = _ =>
                WeChatYooAssetFileSystem.CreateInitializeParameters(endpoints.MainURL);
#elif UNITY_WEBGL && JULYGF_DY_MINIGAME
            options.PlayMode = EPlayMode.WebPlayMode;
            options.CreateInitializeParameters = _ =>
                TikTokYooAssetFileSystem.CreateInitializeParameters(endpoints.MainURL);
#endif

            var resourceSystem = new YooAssetResourceSystem(options);
            await resourceSystem.InitializeAsync(ct);
            ArchContext.Current.RegisterSystem(resourceSystem);
            return true;
        }
    }
}

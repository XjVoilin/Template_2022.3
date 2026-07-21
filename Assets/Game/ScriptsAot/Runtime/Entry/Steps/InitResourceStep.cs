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

        public UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var bootConfig = SeedServices.Resolve<BootConfig>();
            var endpoints = SeedServices.Resolve<CDNEndpoints>();
            var options = new YooAssetOptions
            {
                PackageName = "DefaultPackage",
                PlayMode = bootConfig.PlayMode switch
                {
                    JPlayMode.EditorSimulateMode => EPlayMode.EditorSimulateMode,
                    JPlayMode.OfflinePlayMode => EPlayMode.OfflinePlayMode,
                    JPlayMode.HostPlayMode => EPlayMode.HostPlayMode,
                    JPlayMode.WebPlayMode => EPlayMode.WebPlayMode,
                    JPlayMode.CustomPlayMode => EPlayMode.CustomPlayMode,
                    _ => EPlayMode.OfflinePlayMode
                },
                DefaultHostServer = endpoints.MainURL,
                FallbackHostServer = endpoints.MainURL
            };

            ArchContext.Current.RegisterSystem(new YooAssetResourceSystem(options));
            return UniTask.FromResult(true);
        }
    }
}

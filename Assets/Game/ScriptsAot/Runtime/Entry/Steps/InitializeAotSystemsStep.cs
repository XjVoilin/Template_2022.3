using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Analytics;
using July.Arch;
using July.Launch;
using July.Logging;
using July.Platform;

namespace GameTemplate.Aot
{
    public sealed class InitializeAotSystemsStep : ILaunchStep
    {
        private const int WeChatPlatformType = 3;
        private const int DouyinPlatformType = 4;
        private readonly BootConfig _bootConfig;

        public InitializeAotSystemsStep(BootConfig bootConfig) => _bootConfig = bootConfig;

        public string Name => "Initialize AOT Systems";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            SeedServices.Register(_bootConfig);
            SeedServices.Register(string.IsNullOrWhiteSpace(_bootConfig.CdnUrl)
                ? CDNEndpoints.Empty
                : new CDNEndpoints(_bootConfig.CdnUrl.TrimEnd('/')));

            var context = new ArchContext();
            context.RegisterSystem(new PlatformSystem(CreatePlatformAdapter()));
            context.RegisterSystem(new AnalyticsSystem(CreateAnalyticsChannels()));
            await context.InitializeAsync(ct);
            return true;
        }

        private static IPlatformAdapter CreatePlatformAdapter()
        {
#if JULYGF_WX_MINIGAME
            return new WeChatPlatformAdapter(WeChatPlatformType);
#elif JULYGF_DY_MINIGAME
            return new TikTokPlatformAdapter(DouyinPlatformType);
#else
            return new DefaultPlatformAdapter();
#endif
        }

        private IAnalyticsChannel[] CreateAnalyticsChannels()
        {
            var settings = _bootConfig.Analytics;
            if (settings == null || !settings.Enabled)
                return Array.Empty<IAnalyticsChannel>();

            if (string.IsNullOrWhiteSpace(settings.AppId) ||
                string.IsNullOrWhiteSpace(settings.ServerUrl))
            {
                JLogger.LogWarning(
                    "[Analytics] ThinkingData is enabled but AppId or ServerUrl is empty");
                return Array.Empty<IAnalyticsChannel>();
            }

            var options = new ThinkingDataOptions(settings.AppId.Trim(), settings.ServerUrl.Trim())
            {
                IsProduction = !settings.DebugMode,
                ForwardUnityErrors = settings.UploadUnityErrors,
            };
            return new IAnalyticsChannel[] { new ThinkingDataChannel(options) };
        }
    }
}

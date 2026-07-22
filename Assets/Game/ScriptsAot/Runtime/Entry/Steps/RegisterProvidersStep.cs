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
    /// <summary>Project composition root for selecting framework-provided adapters.</summary>
    public sealed class RegisterProvidersStep : ILaunchStep
    {
        private const int WeChatPlatformType = 3;
        private const int DouyinPlatformType = 4;
        private readonly BootConfig _bootConfig;

        public RegisterProvidersStep(BootConfig bootConfig)
        {
            _bootConfig = bootConfig ?? new BootConfig();
        }

        public string Name => "Register Providers";

        public UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var context = ArchContext.Current;
            context.RegisterSystem(new PlatformSystem(CreatePlatformAdapter()));
            context.RegisterSystem(new AnalyticsSystem(CreateAnalyticsChannels(_bootConfig)));
            return UniTask.FromResult(true);
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

        private static IAnalyticsChannel[] CreateAnalyticsChannels(BootConfig bootConfig)
        {
            var settings = bootConfig?.Analytics;
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

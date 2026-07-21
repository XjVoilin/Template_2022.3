using System;
using July.Analytics;
using July.Logging;
using July.Platform;

namespace GameTemplate.Aot
{
    public static class IntegrationFactory
    {
        private const string WeChatAdapterType =
            "GameTemplate.Integrations.WeChat.WeChatPlatformAdapter, GameTemplate.Platform.WeChat";
        private const string DouyinAdapterType =
            "GameTemplate.Integrations.Douyin.DouyinPlatformAdapter, GameTemplate.Platform.Douyin";
        private const string ThinkingDataChannelType =
            "GameTemplate.Integrations.ThinkingData.ThinkingDataChannel, GameTemplate.Analytics.ThinkingData";

        public static IPlatformAdapter CreatePlatformAdapter()
        {
#if JULYGF_WX_MINIGAME
            return Create<IPlatformAdapter>(WeChatAdapterType) ?? new DefaultPlatformAdapter();
#elif JULYGF_DY_MINIGAME
            return Create<IPlatformAdapter>(DouyinAdapterType) ?? new DefaultPlatformAdapter();
#else
            return new DefaultPlatformAdapter();
#endif
        }

        public static IAnalyticsChannel[] CreateAnalyticsChannels(BootConfig bootConfig)
        {
            var settings = bootConfig?.Analytics;
            if (settings == null || !settings.Enabled) return Array.Empty<IAnalyticsChannel>();

#if JULYGF_THINKINGDATA
            if (string.IsNullOrWhiteSpace(settings.AppId) || string.IsNullOrWhiteSpace(settings.ServerUrl))
            {
                JLogger.LogWarning("[Integrations] ThinkingData is enabled but AppId or ServerUrl is empty");
                return Array.Empty<IAnalyticsChannel>();
            }

            var channel = Create<IAnalyticsChannel>(ThinkingDataChannelType,
                settings.AppId.Trim(), settings.ServerUrl.Trim(), settings.DebugMode,
                settings.UploadUnityErrors);
            return channel == null ? Array.Empty<IAnalyticsChannel>() : new[] { channel };
#else
            JLogger.LogWarning("[Integrations] Analytics is enabled in BootConfig but JULYGF_THINKINGDATA is not defined");
            return Array.Empty<IAnalyticsChannel>();
#endif
        }

        private static T Create<T>(string assemblyQualifiedTypeName, params object[] arguments)
            where T : class
        {
            try
            {
                var type = Type.GetType(assemblyQualifiedTypeName, false);
                if (type == null)
                {
                    JLogger.LogError($"[Integrations] Type not found: {assemblyQualifiedTypeName}");
                    return null;
                }

                if (!typeof(T).IsAssignableFrom(type))
                {
                    JLogger.LogError($"[Integrations] {type.FullName} does not implement {typeof(T).FullName}");
                    return null;
                }

                return Activator.CreateInstance(type, arguments) as T;
            }
            catch (Exception exception)
            {
                JLogger.LogError($"[Integrations] Failed to create {assemblyQualifiedTypeName}: {exception.Message}");
                return null;
            }
        }
    }
}

using System.Collections.Generic;
using July.Analytics;
using ThinkingData.Analytics;
using UnityEngine;
using UnityEngine.Scripting;

namespace GameTemplate.Integrations.ThinkingData
{
    [Preserve]
    public sealed class ThinkingDataChannel : IAnalyticsChannel
    {
        private const string UnityErrorEvent = "UnityLogInfo";

        private readonly string _appId;
        private readonly string _serverUrl;
        private readonly bool _debugMode;
        private readonly bool _uploadUnityErrors;
        private bool _initialized;
        private bool _forwardingUnityError;

        [Preserve]
        public ThinkingDataChannel(string appId, string serverUrl, bool debugMode,
            bool uploadUnityErrors)
        {
            _appId = appId;
            _serverUrl = serverUrl;
            _debugMode = debugMode;
            _uploadUnityErrors = uploadUnityErrors;
        }

        public void Initialize()
        {
            if (_initialized) return;
            var config = new TDConfig(_appId, _serverUrl)
            {
                mode = _debugMode ? TDMode.Debug : TDMode.Normal,
                timeZone = TDTimeZone.Asia_Shanghai
            };
            TDAnalytics.Init(config);
            TDAnalytics.EnableAutoTrack(
                TDAutoTrackEventType.AppInstall |
                TDAutoTrackEventType.AppStart |
                TDAutoTrackEventType.AppEnd);
            TDAnalytics.EnableLog(false);

#if !UNITY_EDITOR
            if (_uploadUnityErrors)
                Application.logMessageReceived += OnLogMessageReceived;
#endif
            _initialized = true;
        }

        public void Track(string eventName, Dictionary<string, object> parameters)
        {
            TDAnalytics.Track(eventName, parameters ?? new Dictionary<string, object>());
        }

        public void SetUserId(string userId)
        {
            TDAnalytics.Login(userId);
            TDAnalytics.Flush();
        }

        public void SetUserProperties(Dictionary<string, object> properties)
        {
            if (properties == null) return;
            TDAnalytics.UserSet(properties);
            TDAnalytics.Flush();
        }

        public void Flush()
        {
            TDAnalytics.Flush();
        }

        public void SetLogEnabled(bool enabled)
        {
            TDAnalytics.EnableLog(enabled);
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            Application.logMessageReceived -= OnLogMessageReceived;
            TDAnalytics.Flush();
            _initialized = false;
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (_forwardingUnityError || (type != LogType.Error && type != LogType.Exception)) return;
            try
            {
                _forwardingUnityError = true;
                TDAnalytics.Track(UnityErrorEvent, new Dictionary<string, object>
                {
                    { "content", $"{type}\n{condition}\n{stackTrace}" }
                });
            }
            finally
            {
                _forwardingUnityError = false;
            }
        }
    }
}

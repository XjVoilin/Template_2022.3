using System;
using UnityEngine;

namespace GameTemplate.Aot
{
    public enum JPlayMode
    {
        EditorSimulateMode,
        OfflinePlayMode,
        HostPlayMode,
        WebPlayMode,
        CustomPlayMode,
    }

    [Serializable]
    public sealed class AnalyticsBootConfig
    {
        [Tooltip("Enable analytics channels registered by the template")]
        public bool Enabled;

        [Tooltip("ThinkingData application id")]
        public string AppId = string.Empty;

        [Tooltip("ThinkingData receiver url")]
        public string ServerUrl = string.Empty;

        [Tooltip("Use ThinkingData debug mode instead of normal mode")]
        public bool DebugMode = true;

        [Tooltip("Forward Unity errors and exceptions to ThinkingData")]
        public bool UploadUnityErrors = true;
    }

    [Serializable]
    public sealed class BootConfig
    {
        [Tooltip("YooAsset runtime mode")]
        public JPlayMode PlayMode = JPlayMode.EditorSimulateMode;

        [Tooltip("Remote resource root used by Host/Web play modes")]
        public string CdnUrl = string.Empty;

        [Tooltip("Project-owned analytics configuration")]
        public AnalyticsBootConfig Analytics = new();
    }
}

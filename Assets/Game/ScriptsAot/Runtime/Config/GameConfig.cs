using System;
using UnityEngine;
using July.Audio;
using July.UI;

namespace Game.Aot
{
    public enum ResourcePlayMode
    {
        EditorSimulateMode,
        OfflinePlayMode,
        HostPlayMode,
        WebPlayMode,
        CustomPlayMode,
    }

    [Serializable]
    public sealed class AnalyticsConfig
    {
        [Tooltip("是否启用数据分析")]
        public bool Enabled;

        [Tooltip("ThinkingData 应用 ID")]
        public string AppId = string.Empty;

        [Tooltip("ThinkingData 接收地址")]
        public string ServerUrl = string.Empty;

        [Tooltip("是否使用 ThinkingData Debug 模式")]
        public bool DebugMode = true;

        [Tooltip("是否上报 Unity 错误和异常")]
        public bool UploadUnityErrors = true;
    }

    [Serializable]
    public sealed class GameConfig
    {
        [Header("资源")]
        [Tooltip("YooAsset 运行模式")]
        public ResourcePlayMode PlayMode = ResourcePlayMode.EditorSimulateMode;

        [Tooltip("Host/Web 模式使用的远程资源根地址")]
        public string CdnUrl = string.Empty;

        [Header("数据分析")]
        public AnalyticsConfig Analytics = new();

        [Header("UI")]
        public UIConfig UI = UIConfig.Default;

        [Header("音频")]
        public AudioConfig Audio = AudioConfig.Default;

        [Header("Tip")]
        public TipConfig Tip = TipConfig.Default;
    }
}

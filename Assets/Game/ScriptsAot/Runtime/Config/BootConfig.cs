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
    public sealed class BootConfig
    {
        [Tooltip("YooAsset runtime mode")]
        public JPlayMode PlayMode = JPlayMode.EditorSimulateMode;

        [Tooltip("Remote resource root used by Host/Web play modes")]
        public string CdnUrl = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using July.Audio;
using July.Bootstrap;
using July.Networking;
using July.Release;
using July.UI;

namespace Game.Aot
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "JulyGF/Game Config")]
    public class GameConfig : ScriptableObject, IReleaseBootConfig, IReleaseResourceConfig
    {
        [Header("启动")]
        public BootstrapConfig Bootstrap = new();

        [Header("UI")]
        public UIConfig UI = UIConfig.Default;

        [Header("音频")]
        public AudioConfig Audio = AudioConfig.Default;

        [Header("提示")]
        public TipConfig Tip = TipConfig.Default;

        [Header("网络")]
        public HttpConfig Http = HttpConfig.Default;

        UnityEngine.Object IReleaseBootConfig.Asset => this;
        ReleaseEnvironment IReleaseBootConfig.env
        {
            get => Bootstrap.Deployment.Environment;
            set
            {
                if (!Enum.IsDefined(typeof(ReleaseEnvironment), value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "未知发布环境。");
                Bootstrap.Deployment.Environment = value;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        string IReleaseBootConfig.cdnUrl => Bootstrap.Deployment.CdnBaseUrl;
        public string EnvName => Bootstrap.Deployment.Environment.ToString();
        public string GetConfigServerUrl() => GetConfigServerUrl(Bootstrap.Deployment.Environment);
        public string GetConfigServerUrl(ReleaseEnvironment environment) => Bootstrap.Deployment.ConfigServerUrls.Get(environment);
        ReleaseResourceSettings IReleaseResourceConfig.Resources => Bootstrap.Resource;
        IReadOnlyList<string> IReleaseResourceConfig.AdditionalAotMetadataAssemblies => Bootstrap.HotUpdate.AdditionalAotMetadataAssemblies;
    }
}

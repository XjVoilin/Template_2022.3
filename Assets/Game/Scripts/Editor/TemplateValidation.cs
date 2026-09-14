using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>只编译目标平台脚本，不切换工程平台、不生成主包或上传资源。</summary>
    public static class TemplateValidation
    {
        [MenuItem("JulyGF/验证/编译微信与抖音脚本")]
        public static void CompileMiniGameScripts()
        {
            // Unity 的平台模块引用取决于当前编辑器目标，不能只设置编译参数的 target。
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("双平台脚本验证需要 WebGL 目标；CI 请使用 -buildTarget WebGL。");
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL).Split(';');
            if (symbols.Contains("JULYGF_WX_MINIGAME") || symbols.Contains("JULYGF_DY_MINIGAME"))
                throw new InvalidOperationException("双平台验证使用临时宏；请先移除 WebGL 中持久保存的微信／抖音平台宏，避免两个分支同时启用。");
            foreach (var platform in new[] { "WeChat", "TikTok" })
            {
                var directory = Path.GetFullPath("Logs/TemplateValidation/" + platform);
                Directory.CreateDirectory(directory);
                var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
                {
                    target = BuildTarget.WebGL,
                    group = BuildTargetGroup.WebGL,
                    options = ScriptCompilationOptions.None,
                    extraScriptingDefines = new[]
                    {
                        platform == "WeChat" ? "JULYGF_WX_MINIGAME" : "JULYGF_DY_MINIGAME",
                        "JULYGF_YOOASSET_MINIGAME"
                    }
                }, directory);
                if (result.assemblies == null || result.assemblies.Count == 0)
                    throw new InvalidOperationException(platform + " Player 脚本编译失败。");
                foreach (var assembly in new[] { "Aot.Runtime", "Game.Runtime", "July.Bootstrap",
                             "July.Platform." + platform, "YooAsset.MiniGame" })
                    if (!result.assemblies.Any(p => Path.GetFileNameWithoutExtension(p) == assembly))
                        throw new InvalidOperationException(platform + " 编译缺少程序集：" + assembly);
                Debug.Log($"[TemplateValidation] {platform} Player 脚本编译通过，共 {result.assemblies.Count} 个程序集。");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameTemplate.Editor
{
    public static class IntegrationProfileMenu
    {
        public const string WeChatSymbol = "JULYGF_WX_MINIGAME";
        public const string DouyinSymbol = "JULYGF_DY_MINIGAME";
        public const string ThinkingDataSymbol = "JULYGF_THINKINGDATA";

        public const string WeChatVersion = "0.1.33";
        public const string DouyinVersion = "6.7.4";
        public const string ThinkingDataVersion = "3.4.2";

        private const string WeChatChangelog = "Assets/WX-WASM-SDK-V2/CHANGELOG.md";
        private const string DouyinPackage =
            "Assets/Plugins/ByteGame/com.bytedance.starksdk/package.json";
        private const string ThinkingDataSource =
            "Assets/Plugins/ThinkingAnalytics/TDAnalytics.cs";

        [MenuItem("JulyGF/SDK 集成/平台/使用编辑器配置", false, 100)]
        private static void UseDefaultPlatform()
        {
            UpdateSymbols(symbols =>
            {
                symbols.Remove(WeChatSymbol);
                symbols.Remove(DouyinSymbol);
            });
        }

        [MenuItem("JulyGF/SDK 集成/平台/使用微信小游戏 0.1.33", false, 101)]
        private static void UseWeChat()
        {
            if (!RequireVersion("微信小游戏", WeChatChangelog, WeChatVersion,
                    text => Regex.Match(text, @"(?m)^##.*?v([0-9]+\.[0-9]+\.[0-9]+)").Groups[1].Value))
                return;

            UpdateSymbols(symbols =>
            {
                symbols.Remove(DouyinSymbol);
                symbols.Add(WeChatSymbol);
            });
        }

        [MenuItem("JulyGF/SDK 集成/平台/使用抖音小游戏 6.7.4", false, 102)]
        private static void UseDouyin()
        {
            if (!RequireVersion("抖音小游戏", DouyinPackage, DouyinVersion,
                    text => Regex.Match(text, "\\\"version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"").Groups[1].Value))
                return;

            UpdateSymbols(symbols =>
            {
                symbols.Remove(WeChatSymbol);
                symbols.Add(DouyinSymbol);
            });
        }

        [MenuItem("JulyGF/SDK 集成/数数分析/启用 3.4.2", false, 120)]
        private static void EnableThinkingData()
        {
            if (!RequireVersion("数数分析", ThinkingDataSource, ThinkingDataVersion,
                    text => Regex.Match(text, @"SDK VERSION:([0-9]+\.[0-9]+\.[0-9]+)").Groups[1].Value))
                return;
            UpdateSymbols(symbols => symbols.Add(ThinkingDataSymbol));
        }

        [MenuItem("JulyGF/SDK 集成/数数分析/停用", false, 121)]
        private static void DisableThinkingData()
        {
            UpdateSymbols(symbols => symbols.Remove(ThinkingDataSymbol));
        }

        [MenuItem("JulyGF/SDK 集成/停用全部 SDK 集成", false, 140)]
        private static void DisableAll()
        {
            UpdateSymbols(symbols =>
            {
                symbols.Remove(WeChatSymbol);
                symbols.Remove(DouyinSymbol);
                symbols.Remove(ThinkingDataSymbol);
            });
        }

        private static bool RequireVersion(string displayName, string relativePath,
            string expectedVersion, Func<string, string> readVersion)
        {
            var fullPath = Path.GetFullPath(relativePath);
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("未安装 SDK",
                    $"请安装 {displayName} {expectedVersion}：\n{relativePath}", "确定");
                return false;
            }

            var actualVersion = readVersion(File.ReadAllText(fullPath));
            if (actualVersion == expectedVersion) return true;
            EditorUtility.DisplayDialog("SDK 版本不匹配",
                $"{displayName} 要求版本 {expectedVersion}，当前检测到 '{actualVersion}'。" +
                "\n本次没有修改集成宏。", "确定");
            return false;
        }

        public static string ValidateBuildProfile(string platform)
        {
            var symbols = GetSymbols();
            var hasWeChat = symbols.Contains(WeChatSymbol);
            var hasDouyin = symbols.Contains(DouyinSymbol);
            if (hasWeChat && hasDouyin)
                return $"{WeChatSymbol} and {DouyinSymbol} cannot be enabled together.";

            switch ((platform ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "editor":
                    if (hasWeChat || hasDouyin)
                        return "The editor profile requires both mini-game platform symbols to be disabled.";
                    break;
                case "wechat":
                    if (!hasWeChat || hasDouyin)
                        return $"The wechat profile requires {WeChatSymbol} only.";
                    var weChatError = ValidateVersion("WeChat", WeChatChangelog, WeChatVersion,
                        text => Regex.Match(text, @"(?m)^##.*?v([0-9]+\.[0-9]+\.[0-9]+)").Groups[1].Value);
                    if (weChatError != null) return weChatError;
                    break;
                case "douyin":
                    if (!hasDouyin || hasWeChat)
                        return $"The douyin profile requires {DouyinSymbol} only.";
                    var douyinError = ValidateVersion("Douyin", DouyinPackage, DouyinVersion,
                        text => Regex.Match(text, "\\\"version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"").Groups[1].Value);
                    if (douyinError != null) return douyinError;
                    break;
                default:
                    return $"Unsupported platform profile '{platform}'. Use editor, wechat, or douyin.";
            }

            if (symbols.Contains(ThinkingDataSymbol))
                return ValidateVersion("ThinkingData", ThinkingDataSource, ThinkingDataVersion,
                    text => Regex.Match(text, @"SDK VERSION:([0-9]+\.[0-9]+\.[0-9]+)").Groups[1].Value);
            return null;
        }

        public static string ResolveActivePlatform()
        {
            var symbols = GetSymbols();
            if (symbols.Contains(WeChatSymbol)) return "wechat";
            if (symbols.Contains(DouyinSymbol)) return "douyin";
            return "editor";
        }

        private static string ValidateVersion(string displayName, string relativePath,
            string expectedVersion, Func<string, string> readVersion)
        {
            var fullPath = Path.GetFullPath(relativePath);
            if (!File.Exists(fullPath))
                return $"Install {displayName} {expectedVersion} at {relativePath}.";

            var actualVersion = readVersion(File.ReadAllText(fullPath));
            return actualVersion == expectedVersion
                ? null
                : $"{displayName} requires {expectedVersion}, but installed files report '{actualVersion}'.";
        }

        private static HashSet<string> GetSymbols()
        {
            return new HashSet<string>(
                PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0),
                StringComparer.Ordinal);
        }

        private static void UpdateSymbols(Action<HashSet<string>> update)
        {
            const BuildTargetGroup target = BuildTargetGroup.WebGL;
            var symbols = new HashSet<string>(
                PlayerSettings.GetScriptingDefineSymbolsForGroup(target)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0),
                StringComparer.Ordinal);
            update(symbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(target,
                string.Join(";", symbols.OrderBy(value => value, StringComparer.Ordinal)));
            Debug.Log($"[SDK 集成] WebGL 宏：{string.Join(";", symbols)}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class PlatformProfileMenu
    {
        public const string WeChatSymbol = "JULYGF_WX_MINIGAME";
        public const string DouyinSymbol = "JULYGF_DY_MINIGAME";
        public const string YooAssetMiniGameSymbol = "JULYGF_YOOASSET_MINIGAME";

        public const string WeChatVersion = "0.1.33";
        public const string DouyinVersion = "6.7.4";

        private const string WeChatChangelog = "Assets/WX-WASM-SDK-V2/CHANGELOG.md";
        private const string DouyinPackage =
            "Assets/Plugins/ByteGame/com.bytedance.starksdk/package.json";

        [MenuItem("JulyGF/Platform/Use Editor", false, 100)]
        private static void UseEditor()
        {
            UpdateSymbols(symbols =>
            {
                symbols.Remove(WeChatSymbol);
                symbols.Remove(DouyinSymbol);
                symbols.Remove(YooAssetMiniGameSymbol);
            });
        }

        [MenuItem("JulyGF/Platform/Use WeChat Mini Game 0.1.33", false, 101)]
        private static void UseWeChat()
        {
            if (!RequireVersion("WeChat", WeChatChangelog, WeChatVersion,
                    text => Regex.Match(text, @"(?m)^##.*?v([0-9]+\.[0-9]+\.[0-9]+)")
                        .Groups[1].Value))
                return;

            UpdateSymbols(symbols =>
            {
                symbols.Remove(DouyinSymbol);
                symbols.Add(WeChatSymbol);
                symbols.Add(YooAssetMiniGameSymbol);
            });
        }

        [MenuItem("JulyGF/Platform/Use Douyin Mini Game 6.7.4", false, 102)]
        private static void UseDouyin()
        {
            if (!RequireVersion("Douyin", DouyinPackage, DouyinVersion,
                    text => Regex.Match(text, "\\\"version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"")
                        .Groups[1].Value))
                return;

            UpdateSymbols(symbols =>
            {
                symbols.Remove(WeChatSymbol);
                symbols.Add(DouyinSymbol);
                symbols.Add(YooAssetMiniGameSymbol);
            });
        }

        public static string ValidateBuildProfile(string platform)
        {
            var symbols = GetSymbols();
            var hasWeChat = symbols.Contains(WeChatSymbol);
            var hasDouyin = symbols.Contains(DouyinSymbol);
            var hasMiniGameFileSystem = symbols.Contains(YooAssetMiniGameSymbol);
            if (hasWeChat && hasDouyin)
                return $"{WeChatSymbol} and {DouyinSymbol} cannot be enabled together.";

            switch ((platform ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "editor":
                    if (hasWeChat || hasDouyin || hasMiniGameFileSystem)
                        return "The editor profile requires all mini-game symbols to be disabled.";
                    break;
                case "wechat":
                    if (!hasWeChat || hasDouyin || !hasMiniGameFileSystem)
                        return $"The wechat profile requires {WeChatSymbol} and " +
                               $"{YooAssetMiniGameSymbol}.";
                    return ValidateVersion("WeChat", WeChatChangelog, WeChatVersion,
                        text => Regex.Match(text, @"(?m)^##.*?v([0-9]+\.[0-9]+\.[0-9]+)")
                            .Groups[1].Value);
                case "douyin":
                    if (!hasDouyin || hasWeChat || !hasMiniGameFileSystem)
                        return $"The douyin profile requires {DouyinSymbol} and " +
                               $"{YooAssetMiniGameSymbol}.";
                    return ValidateVersion("Douyin", DouyinPackage, DouyinVersion,
                        text => Regex.Match(text,
                            "\\\"version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"")
                            .Groups[1].Value);
                default:
                    return $"Unsupported platform profile '{platform}'. " +
                           "Use editor, wechat, or douyin.";
            }

            return null;
        }

        public static string ResolveActivePlatform()
        {
            var symbols = GetSymbols();
            if (symbols.Contains(WeChatSymbol)) return "wechat";
            if (symbols.Contains(DouyinSymbol)) return "douyin";
            return "editor";
        }

        private static bool RequireVersion(string displayName, string relativePath,
            string expectedVersion, Func<string, string> readVersion)
        {
            var error = ValidateVersion(displayName, relativePath, expectedVersion, readVersion);
            if (error == null) return true;
            EditorUtility.DisplayDialog("SDK validation", error, "OK");
            return false;
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
                : $"{displayName} requires {expectedVersion}, " +
                  $"but installed files report '{actualVersion}'.";
        }

        private static HashSet<string> GetSymbols() => new(
            PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0),
            StringComparer.Ordinal);

        private static void UpdateSymbols(Action<HashSet<string>> update)
        {
            const BuildTargetGroup target = BuildTargetGroup.WebGL;
            var symbols = GetSymbols();
            update(symbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(target,
                string.Join(";", symbols.OrderBy(value => value, StringComparer.Ordinal)));
            Debug.Log($"[Platform] WebGL symbols: {string.Join(";", symbols)}");
        }
    }
}

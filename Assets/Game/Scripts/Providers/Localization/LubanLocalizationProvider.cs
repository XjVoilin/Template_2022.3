using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using JulyCore.Provider.Config;
using JulyCore.Provider.Localization;

namespace GameTemplate
{
    /// <summary>
    /// 基于 Luban Language 表的多语言提供者。
    /// Language 表结构：Key | CN | EN | ...（多语言列）
    /// 添加新语言：Excel 加列 -> ColumnMap 加一行
    /// </summary>
    public class LubanLocalizationProvider : ProviderBase, ILocalizationProvider
    {
        public override int Priority => Frameworkconst.PriorityLocalizationProvider;
        protected override LogChannel LogChannel => LogChannel.Localization;

        private readonly IConfigProvider _configProvider;
        private readonly Dictionary<string, Dictionary<string, string>> _packs = new();

        /// <summary>
        /// 语言列映射：语言代码 -> 从 Language 行提取对应列的值。
        /// 添加新语言只需在此加一行。
        /// </summary>
        private static readonly Dictionary<string, Func<Language, string>> ColumnMap = new()
        {
            ["CN"] = e => e.CN,
            // ["EN"] = e => e.EN,
        };

        public LubanLocalizationProvider(IConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        protected override UniTask OnInitAsync()
        {
            if (!_configProvider.TryGetTable<TbLanguage>(out var table))
            {
                LogWarning("Language 表未找到，本地化数据为空");
                return UniTask.CompletedTask;
            }

            foreach (var (lang, selector) in ColumnMap)
            {
                var dict = new Dictionary<string, string>(table.DataList.Count);
                foreach (var entry in table.DataList)
                    dict[entry.Key] = selector(entry).Replace("\\n", "\n");
                _packs[lang] = dict;
            }

            Log($"Luban 本地化数据加载完成，{ColumnMap.Count} 种语言，{table.DataList.Count} 条文本");
            return UniTask.CompletedTask;
        }

        #region ILocalizationProvider

        public UniTask<bool> LoadLanguageAsync(string languageCode, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(_packs.ContainsKey(languageCode));
        }

        public void UnloadLanguage(string languageCode)
        {
            _packs.Remove(languageCode);
        }

        public bool IsLanguageLoaded(string languageCode)
        {
            return _packs.ContainsKey(languageCode);
        }

        public string GetText(string languageCode, string key, string defaultValue = null)
        {
            if (_packs.TryGetValue(languageCode, out var dict) && dict.TryGetValue(key, out var text))
                return text;
            return defaultValue ?? key;
        }

        public bool HasKey(string languageCode, string key)
        {
            return _packs.TryGetValue(languageCode, out var dict) && dict.ContainsKey(key);
        }

        public IReadOnlyList<string> GetAllKeys(string languageCode)
        {
            if (_packs.TryGetValue(languageCode, out var dict))
                return dict.Keys.ToList();
            return Array.Empty<string>();
        }

        #endregion

        protected override void OnShutdown()
        {
            _packs.Clear();
        }
    }
}

using July.Config;
using July.Localization;
using System;
using System.Collections.Generic;
using cfg;

namespace Game
{
    /// <summary>
    /// 盒子的多语言数据源：从 Luban TbLanguage 展开。
    /// 多语言列映射在这里维护 —— 加新语言：Excel 加列 → ColumnMap 加一行。
    /// </summary>
    public class LubanLocalizationProvider : ILocalizationDataProvider
    {
        private const string DefaultLanguageCode = "CN";

        private static readonly Dictionary<string, Func<Language, string>> ColumnMap = new()
        {
            ["CN"] = e => e.CN,
            // ["US"] = e => e.US,
        };

        private readonly IConfigSystem _config;

        public LubanLocalizationProvider(IConfigSystem config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public string DefaultLanguage => DefaultLanguageCode;
        public IReadOnlyList<string> SupportedLanguages { get; } = new List<string>(ColumnMap.Keys);

        public Dictionary<string, string> LoadLanguage(string languageCode)
        {
            if (!ColumnMap.TryGetValue(languageCode, out var selector))
                return null;

            if (!_config.TryGetTable<TbLanguage>(out var table))
                throw new InvalidOperationException("Luban Language 配置表未注册。");

            var dict = new Dictionary<string, string>(table.DataList.Count);
            foreach (var entry in table.DataList)
                dict[entry.Key] = selector(entry).Replace("\\n", "\n");
            return dict;
        }
    }
}

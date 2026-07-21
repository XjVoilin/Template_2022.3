using System;
using System.Collections.Generic;
using cfg;
using July.Config;
using July.Localization;

namespace GameTemplate
{
    public sealed class LubanLocalizationProvider : ILocalizationDataProvider
    {
        private static readonly Dictionary<string, Func<Language, string>> ColumnMap = new()
        {
            ["CN"] = entry => entry.CN,
        };

        private readonly IConfigSystem _configSystem;

        public LubanLocalizationProvider(IConfigSystem configSystem) => _configSystem = configSystem;

        public string DefaultLanguage => "CN";
        public IReadOnlyList<string> SupportedLanguages { get; } = new List<string>(ColumnMap.Keys);

        public Dictionary<string, string> LoadLanguage(string languageCode)
        {
            if (!ColumnMap.TryGetValue(languageCode, out var selector)) return null;
            if (!_configSystem.TryGetTable<TbLanguage>(out var table)) return new Dictionary<string, string>();

            var values = new Dictionary<string, string>(table.DataList.Count);
            foreach (var entry in table.DataList)
                values[entry.Key] = selector(entry).Replace("\\n", "\n");
            return values;
        }
    }
}

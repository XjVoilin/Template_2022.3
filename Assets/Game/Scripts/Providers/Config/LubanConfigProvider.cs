using System;
using System.Collections.Generic;
using cfg;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core;
using JulyCore.Provider.Base;
using JulyCore.Provider.Config;
using JulyCore.Provider.Resource;
using SimpleJSON;
using UnityEngine;

namespace GameTemplate
{
    public class LubanConfigProvider : ProviderBase, IConfigProvider
    {
        public override int Priority => Frameworkconst.PriorityConfigProvider;
        protected override LogChannel LogChannel => LogChannel.Config;

        private readonly IResourceProvider _resourceProvider;
        private readonly Dictionary<Type, object> _tables = new();

        public LubanConfigProvider(IResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider;
        }

        protected override async UniTask OnInitAsync()
        {
            var jsonCache = await LoadAllJsonAsync();

            var tables = new Tables(name => jsonCache.TryGetValue(name, out var json)
                ? JSON.Parse(json)
                : throw new JulyException($"配置未找到: {name}"));

            _tables[typeof(Tables)] = tables;
            tables.RegisterTo(_tables);

            Log($"Luban 配置表初始化完成，共 {Tables.TableNames.Length} 张表");
        }

        private async UniTask<Dictionary<string, string>> LoadAllJsonAsync()
        {
            var names = Tables.TableNames;
            var jsonCache = new Dictionary<string, string>(names.Length);
            var tasks = new UniTask<(string name, string json)>[names.Length];

            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                tasks[i] = LoadSingleJsonAsync(name);
            }

            var results = await UniTask.WhenAll(tasks);
            foreach (var (name, json) in results)
                jsonCache[name] = json;

            return jsonCache;
        }

        private async UniTask<(string name, string json)> LoadSingleJsonAsync(string name)
        {
            var textAsset = await _resourceProvider.LoadAsync<TextAsset>(name);
            if (textAsset == null)
                throw new JulyException($"配置文件未找到: {name}");

            var json = textAsset.text;
            _resourceProvider.Unload(textAsset);
            return (name, json);
        }

        public bool TryGetTable<T>(out T table) where T : class
        {
            if (_tables.TryGetValue(typeof(T), out var t))
            {
                table = t as T;
                return table != null;
            }

            table = null;
            return false;
        }

        protected override void OnShutdown()
        {
            _tables.Clear();
        }
    }
}

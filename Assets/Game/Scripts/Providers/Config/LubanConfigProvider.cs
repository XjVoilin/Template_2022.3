using System;
using System.Collections.Generic;
using System.Reflection;
using cfg;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using JulyCore.Provider.Resource;
using SimpleJSON;
using UnityEngine;

namespace JulyCore.Provider.Config
{
    /// <summary>
    /// Luban 配置提供者
    /// 通过反射自动加载和注册所有 Luban 生成的配置表
    /// </summary>
    public class LubanConfigProvider : ProviderBase, IConfigProvider
    {
        public override int Priority => Frameworkconst.PriorityConfigProvider;
        protected override LogChannel LogChannel => LogChannel.Config;

        private readonly IResourceProvider _resourceProvider;
        private readonly Dictionary<Type, object> _tables = new();
        
        // 缓存反射结果，避免重复反射
        private static readonly PropertyInfo[] TableProperties = 
            typeof(Tables).GetProperties(BindingFlags.Public | BindingFlags.Instance);

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
            foreach (var prop in TableProperties)
            {
                _tables[prop.PropertyType] = prop.GetValue(tables);
            }

            Log($"Luban 配置表初始化完成，共 {TableProperties.Length} 张表");
        }

        private async UniTask<Dictionary<string, string>> LoadAllJsonAsync()
        {
            var jsonCache = new Dictionary<string, string>(TableProperties.Length);
            
            foreach (var prop in TableProperties)
            {
                var tableName = prop.Name.ToLower();
                var textAsset = await _resourceProvider.LoadAsync<TextAsset>(tableName, CancellationToken);
                
                if (textAsset == null)
                    throw new JulyException($"配置文件未找到: {tableName}");
                
                jsonCache[tableName] = textAsset.text;
                _resourceProvider.Unload(textAsset);
            }
            
            return jsonCache;
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

        protected override UniTask OnShutdownAsync()
        {
            _tables.Clear();
            return base.OnShutdownAsync();
        }
    }
}

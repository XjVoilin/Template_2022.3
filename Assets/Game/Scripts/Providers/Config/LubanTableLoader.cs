using System;
using System.Collections.Generic;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using July.Logging;
using July.Resource;
using SimpleJSON;
using UnityEngine;

namespace GameTemplate
{
    public static class LubanTableLoader
    {
        public static async UniTask<Dictionary<Type, object>> LoadAsync(IResourceSystem resourceSystem,
            CancellationToken ct = default)
        {
            var names = Tables.TableNames;
            var tasks = new UniTask<(string name, string json)>[names.Length];
            for (var i = 0; i < names.Length; i++)
                tasks[i] = LoadSingleAsync(resourceSystem, names[i], ct);

            var jsonCache = new Dictionary<string, string>(names.Length);
            var results = await UniTask.WhenAll(tasks);
            foreach (var (name, json) in results) jsonCache[name] = json;

            var tables = new Tables(name => jsonCache.TryGetValue(name, out var json)
                ? JSON.Parse(json)
                : throw new InvalidOperationException($"Config not found: {name}"));

            var registry = new Dictionary<Type, object> { [typeof(Tables)] = tables };
            tables.RegisterTo(registry);
            JLogger.Log($"[Luban] Loaded {names.Length} tables");
            return registry;
        }

        private static async UniTask<(string name, string json)> LoadSingleAsync(IResourceSystem resourceSystem,
            string name, CancellationToken ct)
        {
            using var handle = await resourceSystem.LoadAssetAsync<TextAsset>(name, ct);
            if (handle?.Asset == null) throw new InvalidOperationException($"Config asset not found: {name}");
            return (name, handle.Asset.text);
        }
    }
}

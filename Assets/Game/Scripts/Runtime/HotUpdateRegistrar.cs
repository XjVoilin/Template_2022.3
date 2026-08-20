using System;
using System.Collections.Generic;
using System.Threading;
using cfg;
using Cysharp.Threading.Tasks;
using Game.Aot;
using July.Arch;
using July.Audio;
using July.Launch;
using July.Config;
using July.Fsm;
using July.Input;
using July.Localization;
using July.Logging;
using July.Persistence;
using July.Pooling;
using July.Resource;
using July.Scene;
using July.Time;
using July.UI;
using SimpleJSON;
using UnityEngine;

namespace Game
{
    public sealed class HotUpdateRegistrar : IHotUpdateRegistrar, ICanGetSystem
    {
        public void Register()
        {
            var context = ArchContext.Current;
            var gameConfig = SeedServices.Resolve<GameConfig>();
            context.RegisterSystem(new PoolSystem());

            var uiSystem = new UISystem();
            uiSystem.Configure(gameConfig.UI);
            uiSystem.ConfigureTip(gameConfig.Tip);
            context.RegisterSystem(uiSystem);

            var audioSystem = new AudioSystem();
            audioSystem.Configure(gameConfig.Audio);
            context.RegisterSystem(audioSystem);

            context.RegisterSystem(new ConfigSystem());
            context.RegisterSystem(new JsonSerializeSystem());
            context.RegisterSystem(new NoEncryptionSystem());
            context.RegisterSystem(new LocalFileSaveSystem());
            context.RegisterSystem(new SceneSystem());
            context.RegisterSystem(new UnityInputSystem());
            context.RegisterSystem(new FsmSystem());
            context.RegisterSystem(new TimeSystem());
            context.RegisterSystem(new LocalizationSystem());
        }

        public async UniTask PreInitializeAsync(CancellationToken ct = default)
        {
            await LoadLubanTablesAsync(ct);
            SetupLocalization();
        }
        
        private void SetupLocalization()
        {
            var config = this.GetSystem<IConfigSystem>();
            this.GetSystem<ILocalizationSystem>().SetMainProvider(new LubanLocalizationProvider(config));
        }
        
        private async UniTask LoadLubanTablesAsync(CancellationToken ct)
        {
            var resource = this.GetSystem<IResourceSystem>();
            var names = Tables.TableNames;
            var jsonCache = new Dictionary<string, string>(names.Length);
            var tasks = new UniTask<(string name, string json)>[names.Length];

            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                tasks[i] = LoadSingleJsonAsync(resource, name);
            }

            var results = await UniTask.WhenAll(tasks);
            foreach (var (name, json) in results)
                jsonCache[name] = json;

            var tables = new Tables(name => jsonCache.TryGetValue(name, out var json)
                ? JSON.Parse(json)
                : throw new Exception($"配置未找到: {name}"));

            var tableDict = new Dictionary<Type, object>();
            tableDict[typeof(Tables)] = tables;
            tables.RegisterTo(tableDict);

            this.GetSystem<IConfigSystem>().SetMainProvider(new DictionaryConfigProvider(tableDict));

            JLogger.Log($"[HotUpdateRegistrar] Luban 配置表加载完成，共 {names.Length} 张表");
        }

        private static async UniTask<(string name, string json)> LoadSingleJsonAsync(
            IResourceSystem resource, string name)
        {
            using var handle = await resource.LoadAssetAsync<TextAsset>(name);
            if (handle?.Asset == null)
                throw new Exception($"配置文件未找到: {name}");
            return (name, handle.Asset.text);
        }

        public async UniTask OnGameLaunch()
        {
            this.GetSystem<IUISystem>().SetMainProvider(new LubanUIWindowProvider());
            await this.GetSystem<ISceneSystem>().SwitchSceneAsync("Main");
        }
    }
}

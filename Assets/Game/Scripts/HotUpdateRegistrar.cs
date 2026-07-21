using System.Threading;
using Cysharp.Threading.Tasks;
using GameTemplate.Aot;
using July.Arch;
using July.Audio;
using July.Launch;
using July.Config;
using July.Fsm;
using July.Localization;
using July.Persistence;
using July.Pooling;
using July.Resource;
using July.Scene;
using July.Time;
using July.UI;

namespace GameTemplate
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
            context.RegisterSystem(new InputSystem());
            context.RegisterSystem(new FsmSystem());
            context.RegisterSystem(new TimeSystem());
            context.RegisterSystem(new LocalizationSystem());
        }

        public async UniTask PreInitializeAsync(CancellationToken ct = default)
        {
            var tables = await LubanTableLoader.LoadAsync(this.GetSystem<IResourceSystem>(), ct);
            var configSystem = this.GetSystem<IConfigSystem>();
            configSystem.SetMainProvider(new DictionaryConfigProvider(tables));
            this.GetSystem<ILocalizationSystem>().SetMainProvider(new LubanLocalizationProvider(configSystem));
        }

        public async UniTask OnGameLaunch()
        {
            this.GetSystem<IUISystem>().SetMainProvider(new LubanUIWindowProvider());
            await this.GetSystem<ISceneSystem>().SwitchSceneAsync("Main");
        }
    }
}

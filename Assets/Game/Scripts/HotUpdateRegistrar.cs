using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCore;
using JulyCore.Provider.Config;
using JulyCore.Provider.Localization;
using JulyCore.Provider.Resource;
using JulyCore.Provider.UI;
using JulyCore.Provider.Audio;
using JulyCore.Provider.Pool;
using JulyGame;
#if JULYGF_DEBUG
using JulyCore.Provider.GM;
#endif

namespace GameTemplate
{
    public class HotUpdateRegistrar : IHotUpdateRegistrar, IArchNode
    {
        public IArchContext GetArchitecture() => GameArch.Context;

        public void Register()
        {
            RegisterProviders();
            RegisterStores();
            RegisterSystems();
        }

        private void RegisterProviders()
        {
            var resourceProvider = GF.Resolve<IResourceProvider>();
            var poolProvider = GF.Resolve<IPoolProvider>();

            var configProvider = new LubanConfigProvider(resourceProvider);
            GF.RegisterProvider<IConfigProvider>(configProvider);

            GF.RegisterProvider<ILocalizationProvider>(new LubanLocalizationProvider(configProvider));
            GF.RegisterProvider<IUIProvider>(new UIProvider(resourceProvider, poolProvider));
            GF.RegisterProvider<IAudioProvider>(new UnityAudioProvider(resourceProvider, poolProvider));

#if JULYGF_DEBUG
            RegisterGMCommands();
#endif
        }

#if JULYGF_DEBUG
        private static void RegisterGMCommands()
        {
        }
#endif

        private void RegisterStores()
        {
        }

        private void RegisterSystems()
        {
        }

        public async UniTask OnGameLaunch()
        {
            ConfigureUI();
            await GF.Scene.SwitchAsync("Main");
        }

        private static void ConfigureUI()
        {
            GF.UI.SetWindowConfig(new LubanUIWindowConfigProvider());
        }
    }
}

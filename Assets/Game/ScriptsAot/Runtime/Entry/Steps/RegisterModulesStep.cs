using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCore;
using JulyCore.Core;
using JulyCore.Core.Launch;
using JulyCore.Module.Audio;
using JulyCore.Module.Config;
using JulyCore.Module.Localization;
using JulyCore.Module.Resource;
using JulyCore.Module.Save;
using JulyCore.Module.Scene;
using JulyCore.Module.UI;

namespace GameTemplate.Aot
{
    public class RegisterModulesStep : ILaunchStep
    {
        public string Name => "Register App Modules";

        public UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            ctx.RegisterModule<ResourceModule>();
            ctx.RegisterModule<SceneModule>();
            ctx.RegisterModule<LocalizationModule>();
            ctx.RegisterModule<UIModule>();
            ctx.RegisterModule<AudioModule>();
            ctx.RegisterModule<SaveModule>();
            ctx.RegisterModule<ConfigModule>();

            var gameContext = new GameContext();
            AppArch.Context = gameContext;

            var registrar = FindRegistrar();
            if (registrar != null)
                registrar.Register(gameContext);

            ctx.Registry.Register(gameContext);
            if (registrar != null)
                ctx.Registry.Register(registrar);

            return UniTask.FromResult(true);
        }

        private static IHotUpdateRegistrar FindRegistrar()
        {
            const string typeFullName = "GameTemplate.HotUpdateRegistrar";
            try
            {
                var assembly = System.Reflection.Assembly.Load("Assembly-CSharp");
                var type = assembly.GetType(typeFullName);
                if (type != null)
                    return (IHotUpdateRegistrar)System.Activator.CreateInstance(type);
            }
            catch (System.Exception e)
            {
                JLogger.LogWarning($"[RegisterModules] HotUpdateRegistrar not found: {e.Message}");
            }

            return null;
        }
    }
}

using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCore.Core.Launch;

namespace GameTemplate.Aot
{
    public class InitModulesStep : ILaunchStep
    {
        public string Name => "Init App Modules";

        public async UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            await ctx.InitProvidersAsync();
            await ctx.InitModulesAsync();

            var gameContext = ctx.Registry.Resolve<GameContext>();
            await gameContext.InitializeAsync(ctx.Token);

            return true;
        }
    }
}

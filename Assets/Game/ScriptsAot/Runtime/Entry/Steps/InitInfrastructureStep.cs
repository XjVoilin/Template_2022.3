using Cysharp.Threading.Tasks;
using JulyCore.Core.Launch;

namespace GameTemplate.Aot
{
    public class InitInfrastructureStep : ILaunchStep
    {
        public string Name => "Init Infrastructure";

        public async UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            await ctx.InitProvidersAsync();
            await ctx.InitModulesAsync();
            ctx.OnCoreReady?.Invoke();
            return true;
        }
    }
}

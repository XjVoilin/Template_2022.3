using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCore.Core.Launch;

namespace GameTemplate.Aot
{
    public class LaunchGameStep : ILaunchStep
    {
        public string Name => "Launch Game";

        public async UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            if (ctx.Registry.TryResolve<IHotUpdateRegistrar>(out var registrar))
                await registrar.OnGameLaunch();

            return true;
        }
    }
}

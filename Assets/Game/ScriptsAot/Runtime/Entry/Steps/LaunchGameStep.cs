using System.Threading;
using Cysharp.Threading.Tasks;
using July.Launch;

namespace Game.Aot
{
    public sealed class LaunchGameStep : ILaunchStep
    {
        public string Name => "Launch Game";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await SeedServices.Resolve<IHotUpdateRegistrar>().OnGameLaunch();
            return true;
        }
    }
}

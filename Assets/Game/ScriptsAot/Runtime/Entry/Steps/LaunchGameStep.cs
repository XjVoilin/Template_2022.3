using System.Threading;
using Cysharp.Threading.Tasks;
using JulyBoot;
using JulyCommon;

namespace GameTemplate.Aot
{
    public sealed class LaunchGameStep : ILaunchStep
    {
        public string Name => "Launch Game";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await JulyDI.Resolve<IHotUpdateRegistrar>().OnGameLaunch();
            return true;
        }
    }
}

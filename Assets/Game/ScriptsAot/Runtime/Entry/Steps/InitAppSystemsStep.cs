using System.Threading;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Launch;
using July.Logging;

namespace GameTemplate.Aot
{
    public sealed class InitAppSystemsStep : ILaunchStep
    {
        public string Name => "Init App Systems";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            var registrar = SeedServices.Resolve<IHotUpdateRegistrar>();
            await ArchContext.Current.InitializeAsync(ct);
            await registrar.PreInitializeAsync(ct);
            return true;
        }
    }
}

using System.Threading;
using Cysharp.Threading.Tasks;
using JulyArch;
using JulyBoot;
using JulyCommon;

namespace GameTemplate.Aot
{
    public sealed class InitAppSystemsStep : ILaunchStep
    {
        public string Name => "Init App Systems";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            var registrar = JulyDI.Resolve<IHotUpdateRegistrar>();
            await registrar.PreInitializeAsync(ct);
            await ArchContext.Current.InitializeAsync(ct);
            return true;
        }
    }
}

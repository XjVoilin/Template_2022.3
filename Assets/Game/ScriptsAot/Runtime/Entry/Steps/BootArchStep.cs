using System.Threading;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Launch;
using July.Logging;

namespace GameTemplate.Aot
{
    public sealed class BootArchStep : ILaunchStep
    {
        private readonly BootConfig _bootConfig;

        public BootArchStep(BootConfig bootConfig)
        {
            _bootConfig = bootConfig ?? new BootConfig();
        }

        public string Name => "Boot Architecture";

        public UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _ = new ArchContext();
            SeedServices.Register(_bootConfig);
            SeedServices.Register(string.IsNullOrWhiteSpace(_bootConfig.CdnUrl)
                ? CDNEndpoints.Empty
                : new CDNEndpoints(_bootConfig.CdnUrl.TrimEnd('/')));
            return UniTask.FromResult(true);
        }
    }
}

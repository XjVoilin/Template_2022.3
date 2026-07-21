using System.Threading;
using Cysharp.Threading.Tasks;
using July.Analytics;
using July.Arch;
using July.Launch;
using July.Platform;

namespace GameTemplate.Aot
{
    public sealed class RegisterIntegrationsStep : ILaunchStep
    {
        private readonly BootConfig _bootConfig;

        public RegisterIntegrationsStep(BootConfig bootConfig)
        {
            _bootConfig = bootConfig ?? new BootConfig();
        }

        public string Name => "Register Integrations";

        public UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var context = ArchContext.Current;
            context.RegisterSystem(new PlatformSystem(IntegrationFactory.CreatePlatformAdapter()));
            context.RegisterSystem(new AnalyticsSystem(IntegrationFactory.CreateAnalyticsChannels(_bootConfig)));
            return UniTask.FromResult(true);
        }
    }
}

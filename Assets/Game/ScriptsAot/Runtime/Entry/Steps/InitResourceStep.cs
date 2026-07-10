using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyArch;
using JulyBoot;
using JulyCommon;

namespace GameTemplate.Aot
{
    public sealed class InitResourceStep : ILaunchStep
    {
        public string Name => "Init Resource";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
#if UNITY_YOOASSET
            try
            {
                var backend = new YooAssetBackend(JulyDI.Resolve<BootConfig>(), JulyDI.Resolve<CDNEndpoints>());
                await backend.InitAsync();
                var resourceSystem = new YooAssetResourceSystem();
                resourceSystem.Boot(backend);
                ArchContext.Current.RegisterSystem(resourceSystem);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                JLogger.LogError($"[InitResource] {ex.Message}");
                return false;
            }
#endif
            return true;
        }
    }
}

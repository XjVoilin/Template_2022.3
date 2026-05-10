using System;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core.Launch;
using JulyCore.Provider.Resource;

namespace GameTemplate.Aot
{
    public class InitResourceStep : ILaunchStep
    {
        public string Name => "Init Resource";

        private bool _providerRegistered;

        public async UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
#if UNITY_YOOASSET
            try
            {
                if (!_providerRegistered)
                {
                    var resourceProvider = new YooAssetResourceProvider(ctx.Config);
                    ctx.RegisterProvider<IResourceProvider>(resourceProvider);
                    _providerRegistered = true;
                }

                await ctx.InitProvidersAsync();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                GF.LogError($"[InitResource] {ex.Message}");
                return false;
            }
#endif
            return true;
        }
    }
}

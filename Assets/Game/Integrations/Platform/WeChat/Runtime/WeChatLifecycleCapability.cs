using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameTemplate.Aot;
using UnityEngine.Scripting;
using WeChatWASM;

namespace GameTemplate.Integrations.WeChat
{
    [Preserve]
    public sealed class WeChatLifecycleCapability : IPlatformLifecycleCapability
    {
        private Action<OnShowListenerResult> _onShow;

        public PlatformLaunchContext ColdContext { get; private set; }
        public PlatformLaunchContext LatestContext { get; private set; }

        public event Action<PlatformLaunchContext> Shown;

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var launch = WX.GetLaunchOptionsSync();
            ColdContext = new PlatformLaunchContext(true, launch.scene.ToString("0"), launch.query);
            LatestContext = ColdContext;

            _onShow = result =>
            {
                LatestContext = new PlatformLaunchContext(false,
                    result.scene.ToString("0"), result.query);
                Shown?.Invoke(LatestContext);
            };
            WX.OnShow(_onShow);
            return UniTask.CompletedTask;
        }

        public void Shutdown()
        {
            if (_onShow != null) WX.OffShow(_onShow);
            _onShow = null;
            Shown = null;
        }
    }
}

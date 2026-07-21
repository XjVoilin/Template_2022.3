using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameTemplate.Aot;
using TTSDK;
using UnityEngine.Scripting;

namespace GameTemplate.Integrations.Douyin
{
    [Preserve]
    public sealed class DouyinLifecycleCapability : IPlatformLifecycleCapability
    {
        private TTAppLifeCycle.OnShowEventWithDict _onShow;

        public PlatformLaunchContext ColdContext { get; private set; }
        public PlatformLaunchContext LatestContext { get; private set; }

        public event Action<PlatformLaunchContext> Shown;

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var launch = TT.GetLaunchOptionsSync();
            ColdContext = new PlatformLaunchContext(true, launch.Scene, launch.Query);
            LatestContext = ColdContext;

            _onShow = result =>
            {
                var sceneId = string.Empty;
                var query = new Dictionary<string, string>();
                foreach (var pair in result)
                {
                    if (pair.Value is not string value) continue;
                    if (pair.Key == "scene") sceneId = value;
                    else query[pair.Key] = value;
                }

                LatestContext = new PlatformLaunchContext(false, sceneId, query);
                Shown?.Invoke(LatestContext);
            };
            TT.GetAppLifeCycle().OnShow += _onShow;
            return UniTask.CompletedTask;
        }

        public void Shutdown()
        {
            if (_onShow != null) TT.GetAppLifeCycle().OnShow -= _onShow;
            _onShow = null;
            Shown = null;
        }
    }
}

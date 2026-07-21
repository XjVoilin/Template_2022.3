using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameTemplate.Aot;
using July.Platform;
using July.UI;
using TTSDK;
using UnityEngine;
using UnityEngine.Scripting;

namespace GameTemplate.Integrations.Douyin
{
    [Preserve]
    public sealed class DouyinPlatformAdapter : IPlatformAdapter
    {
        public const int DouyinPlatformType = 4;

        private readonly Func<Rect> _safeAreaProvider;

        public DouyinPlatformAdapter()
        {
            _safeAreaProvider = GetSafeArea;
        }

        public int PlatformType => DouyinPlatformType;

        public async UniTask ConfigureAsync(PlatformCapabilityRegistry registry,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new UniTaskCompletionSource();
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled());
            TT.InitSDK((_, _) => completion.TrySetResult());
            await completion.Task;

            SafeAreaAdapter.SafeAreaOverride = _safeAreaProvider;
            registry.Register<ILoginCapability>(new DouyinLoginCapability());
            registry.Register<IPlatformLifecycleCapability>(new DouyinLifecycleCapability());
            registry.Register<IShareCapability>(new DouyinShareCapability());
            registry.Register<IAdvertisementCapability>(new DouyinAdvertisementCapability());
        }

        public void VibrateShort(July.Platform.VibrateType type)
        {
            TT.VibrateShort(new VibrateShortParam());
        }

        public void VibrateLong()
        {
            TT.VibrateLong(new VibrateLongParam());
        }

        public void Shutdown()
        {
            if (SafeAreaAdapter.SafeAreaOverride == _safeAreaProvider)
                SafeAreaAdapter.SafeAreaOverride = null;
        }

        private static Rect GetSafeArea()
        {
            var info = TT.GetSystemInfo();
            var safeArea = info.safeArea;
            if (safeArea.width <= 0 || safeArea.height <= 0) return Screen.safeArea;

            var ratio = info.pixelRatio > 0 ? info.pixelRatio : 1d;
            return new Rect(
                (float)(safeArea.left * ratio),
                (float)((info.screenHeight - safeArea.bottom) * ratio),
                (float)(safeArea.width * ratio),
                (float)(safeArea.height * ratio));
        }
    }
}

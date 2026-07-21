using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameTemplate.Aot;
using July.Platform;
using July.UI;
using UnityEngine;
using UnityEngine.Scripting;
using WeChatWASM;

namespace GameTemplate.Integrations.WeChat
{
    [Preserve]
    public sealed class WeChatPlatformAdapter : IPlatformAdapter
    {
        public const int WeChatPlatformType = 3;

        private readonly Func<Rect> _safeAreaProvider;

        public WeChatPlatformAdapter()
        {
            _safeAreaProvider = GetSafeArea;
        }

        public int PlatformType => WeChatPlatformType;

        public async UniTask ConfigureAsync(PlatformCapabilityRegistry registry,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new UniTaskCompletionSource();
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled());
            WX.InitSDK(_ => completion.TrySetResult());
            await completion.Task;

            SafeAreaAdapter.SafeAreaOverride = _safeAreaProvider;
            registry.Register<ILoginCapability>(new WeChatLoginCapability());
            registry.Register<IPlatformLifecycleCapability>(new WeChatLifecycleCapability());
            registry.Register<IShareCapability>(new WeChatShareCapability());
            registry.Register<IAdvertisementCapability>(new WeChatAdvertisementCapability());
        }

        public void VibrateShort(July.Platform.VibrateType type)
        {
            string[] names = { "light", "medium", "heavy" };
            var index = Mathf.Clamp((int)type, 0, names.Length - 1);
            WX.VibrateShort(new VibrateShortOption { type = names[index] });
        }

        public void VibrateLong()
        {
            WX.VibrateLong(new VibrateLongOption());
        }

        public void Shutdown()
        {
            if (SafeAreaAdapter.SafeAreaOverride == _safeAreaProvider)
                SafeAreaAdapter.SafeAreaOverride = null;
        }

        private static Rect GetSafeArea()
        {
            var info = WX.GetWindowInfo();
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

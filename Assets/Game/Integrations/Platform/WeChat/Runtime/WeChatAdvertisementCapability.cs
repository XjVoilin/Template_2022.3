using System.Threading;
using Cysharp.Threading.Tasks;
using July.Platform;
using UnityEngine;
using UnityEngine.Scripting;
using WeChatWASM;

namespace GameTemplate.Integrations.WeChat
{
    [Preserve]
    public sealed class WeChatAdvertisementCapability : IAdvertisementCapability
    {
        private WXRewardedVideoAd _rewardedAd;
        private string _placement;
        private bool _loaded;
        private UniTaskCompletionSource<bool> _pending;

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public async UniTask<bool> ShowRewardedAsync(string placement,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(placement)) return false;
            if (_pending != null)
            {
                Debug.LogWarning("[WeChatAds] A rewarded ad request is already pending");
                return false;
            }

            EnsureRewardedAd(placement);
            var completion = new UniTaskCompletionSource<bool>();
            _pending = completion;
            using var registration = cancellationToken.Register(() => Complete(false));

            if (_loaded) ShowRewarded();
            else _rewardedAd.Load(null, _ => Complete(false));
            return await completion.Task;
        }

        public UniTask<bool> ShowInterstitialAsync(string placement,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Debug.LogWarning("[WeChatAds] Interstitial ads are not configured by the seed template");
            return UniTask.FromResult(false);
        }

        public void Shutdown()
        {
            Complete(false);
            _rewardedAd = null;
            _loaded = false;
        }

        private void EnsureRewardedAd(string placement)
        {
            if (_rewardedAd != null && _placement == placement) return;
            _placement = placement;
            _loaded = false;
            _rewardedAd = WX.CreateRewardedVideoAd(
                new WXCreateRewardedVideoAdParam { adUnitId = placement });
            _rewardedAd.OnLoad(_ =>
            {
                _loaded = true;
                if (_pending != null) ShowRewarded();
            });
            _rewardedAd.OnError(result =>
            {
                _loaded = false;
                Debug.LogWarning($"[WeChatAds] {result?.errCode} {result?.errMsg}");
                Complete(false);
            });
            _rewardedAd.OnClose(result =>
            {
                _loaded = false;
                Complete(result != null && result.isEnded);
                _rewardedAd.Load(null, null);
            });
        }

        private void ShowRewarded()
        {
            _loaded = false;
            _rewardedAd.Show(_ => { }, result =>
            {
                Debug.LogWarning($"[WeChatAds] Show failed: {result?.errMsg}");
                Complete(false);
            });
        }

        private void Complete(bool result)
        {
            var completion = _pending;
            _pending = null;
            completion?.TrySetResult(result);
        }
    }
}

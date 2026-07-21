using System.Threading;
using Cysharp.Threading.Tasks;
using July.Platform;
using TTSDK;
using UnityEngine;
using UnityEngine.Scripting;

namespace GameTemplate.Integrations.Douyin
{
    [Preserve]
    public sealed class DouyinAdvertisementCapability : IAdvertisementCapability
    {
        private TTRewardedVideoAd _rewardedAd;
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
                Debug.LogWarning("[DouyinAds] A rewarded ad request is already pending");
                return false;
            }

            EnsureRewardedAd(placement);
            var completion = new UniTaskCompletionSource<bool>();
            _pending = completion;
            using var registration = cancellationToken.Register(() => Complete(false));

            if (_loaded) ShowRewarded();
            else _rewardedAd.Load();
            return await completion.Task;
        }

        public UniTask<bool> ShowInterstitialAsync(string placement,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Debug.LogWarning("[DouyinAds] Interstitial ads are not configured by the seed template");
            return UniTask.FromResult(false);
        }

        public void Shutdown()
        {
            Complete(false);
            DetachRewardedAd();
            _rewardedAd = null;
            _loaded = false;
        }

        private void EnsureRewardedAd(string placement)
        {
            if (_rewardedAd != null && _placement == placement) return;
            DetachRewardedAd();
            _placement = placement;
            _loaded = false;
            _rewardedAd = TT.CreateRewardedVideoAd(
                new CreateRewardedVideoAdParam { AdUnitId = placement });
            _rewardedAd.OnLoad += OnLoaded;
            _rewardedAd.OnError += OnError;
            _rewardedAd.OnClose += OnClosed;
        }

        private void DetachRewardedAd()
        {
            if (_rewardedAd == null) return;
            _rewardedAd.OnLoad -= OnLoaded;
            _rewardedAd.OnError -= OnError;
            _rewardedAd.OnClose -= OnClosed;
        }

        private void OnLoaded()
        {
            _loaded = true;
            if (_pending != null) ShowRewarded();
        }

        private void ShowRewarded()
        {
            _loaded = false;
            _rewardedAd.Show();
        }

        private void OnClosed(bool isComplete, int multitonCount)
        {
            Complete(isComplete);
            _rewardedAd.Load();
        }

        private void OnError(int errorCode, string errorMessage)
        {
            _loaded = false;
            Debug.LogWarning($"[DouyinAds] {errorCode} {errorMessage}");
            Complete(false);
        }

        private void Complete(bool result)
        {
            var completion = _pending;
            _pending = null;
            completion?.TrySetResult(result);
        }
    }
}

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Platform;
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
using UnityEngine.Scripting;

namespace GameTemplate.Integrations.Douyin
{
    [Preserve]
    public sealed class DouyinShareCapability : IShareCapability
    {
        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public async UniTask<bool> ShareAsync(string title, string imageUrl,
            IReadOnlyDictionary<string, string> extras, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = new JsonData { ["title"] = title };
            if (!string.IsNullOrWhiteSpace(imageUrl)) payload["imageUrl"] = imageUrl;
            if (extras != null)
                foreach (var pair in extras)
                    payload[pair.Key] = pair.Value;

            var completion = new UniTaskCompletionSource<bool>();
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled());
            TT.ShareAppMessage(payload,
                _ => completion.TrySetResult(true),
                _ => completion.TrySetResult(false),
                () => completion.TrySetResult(false));
            return await completion.Task;
        }

        public void Shutdown()
        {
        }
    }
}

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Platform;
using UnityEngine.Scripting;
using WeChatWASM;

namespace GameTemplate.Integrations.WeChat
{
    [Preserve]
    public sealed class WeChatShareCapability : IShareCapability
    {
        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public UniTask<bool> ShareAsync(string title, string imageUrl,
            IReadOnlyDictionary<string, string> extras, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string query = null;
            string templateId = null;
            extras?.TryGetValue("query", out query);
            extras?.TryGetValue("templateId", out templateId);

            var option = new ShareAppMessageOption
            {
                title = title,
                query = query
            };
            if (!string.IsNullOrWhiteSpace(imageUrl)) option.imageUrl = imageUrl;
            if (!string.IsNullOrWhiteSpace(templateId)) option.imageUrlId = templateId;
            WX.ShareAppMessage(option);
            return UniTask.FromResult(true);
        }

        public void Shutdown()
        {
        }
    }
}

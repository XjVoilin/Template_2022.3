using System.Threading;
using Cysharp.Threading.Tasks;
using July.Platform;
using UnityEngine.Scripting;
using WeChatWASM;

namespace GameTemplate.Integrations.WeChat
{
    [Preserve]
    public sealed class WeChatLoginCapability : ILoginCapability
    {
        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public async UniTask<PlatformLoginResult> LoginAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new UniTaskCompletionSource<PlatformLoginResult>();
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled());
            WX.Login(new LoginOption
            {
                success = result => completion.TrySetResult(
                    new PlatformLoginResult(true, result.code)),
                fail = result => completion.TrySetResult(
                    new PlatformLoginResult(false, null, result.errMsg))
            });
            return await completion.Task;
        }

        public void Shutdown()
        {
        }
    }
}

using System.Threading;
using Cysharp.Threading.Tasks;
using July.Platform;
using TTSDK;
using UnityEngine.Scripting;

namespace GameTemplate.Integrations.Douyin
{
    [Preserve]
    public sealed class DouyinLoginCapability : ILoginCapability
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
            TT.Login(
                (code, _, isLogin) => completion.TrySetResult(
                    new PlatformLoginResult(isLogin, code, isLogin ? null : "Douyin login was not established")),
                error => completion.TrySetResult(
                    new PlatformLoginResult(false, null, error)));
            return await completion.Task;
        }

        public void Shutdown()
        {
        }
    }
}

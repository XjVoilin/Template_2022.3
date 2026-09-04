using System.Threading;
using Cysharp.Threading.Tasks;
using July.Launch;

namespace Game.Aot
{
    /// <summary>启动初始化前先让 Unity 显示 Launch 场景。</summary>
    public sealed class PresentLaunchFrameStep : ILaunchStep
    {
        public string Name => "Present Launch Frame";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            await UniTask.NextFrame(ct);
            return true;
        }
    }
}

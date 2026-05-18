using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCore;
using JulyCore.Data.Save;

namespace GooseMarket
{
    /// <summary>
    /// 可持久化 Store 基类 — 继承后自动获得 Save 能力。
    /// 纯内存 Store 继续使用 StoreBase。
    /// </summary>
    public abstract class SavableStoreBase<TData> : StoreBase<TData>, IAsyncLoadable
        where TData : class, ISaveData, new()
    {
        protected abstract string SaveKey { get; }

        async UniTask IAsyncLoadable.LoadAsync()
        {
            Data = await GF.Save.LoadAndRegisterAsync<TData>(SaveKey);
        }

        protected void MarkDirty()
        {
            GF.Save.MarkDirty(SaveKey);
        }

        protected override void OnShutdown()
        {
            GF.Save.Unregister(SaveKey);
        }
    }
}

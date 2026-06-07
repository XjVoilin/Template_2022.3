using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCore;
using JulyCore.Data.Save;

namespace GooseMarket
{
    public abstract class SavableStoreBase<TData> : StoreBase<TData>, IAsyncLoadable
        where TData : class, ISaveData, new()
    {
        protected abstract string SaveKey { get; }

        async UniTask IAsyncLoadable.OnLoadAsync()
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

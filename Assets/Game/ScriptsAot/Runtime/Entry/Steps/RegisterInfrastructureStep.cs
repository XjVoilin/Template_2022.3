using Cysharp.Threading.Tasks;
using JulyCore.Core.Launch;
using JulyCore.Module.Data;
using JulyCore.Module.Fsm;
using JulyCore.Module.Input;
using JulyCore.Module.Pool;
using JulyCore.Module.Time;
using JulyCore.Provider.Data;
using JulyCore.Provider.Encryption;
using JulyCore.Provider.Fsm;
using JulyCore.Provider.Input;
using JulyCore.Provider.Pool;
using JulyCore.Provider.Time;
#if JULYGF_DEBUG
using JulyCore.Provider.GM;
#endif

namespace GameTemplate.Aot
{
    public class RegisterInfrastructureStep : ILaunchStep
    {
        public string Name => "Register Infrastructure";

        public UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            ctx.RegisterModule<InputModule>();
            ctx.RegisterModule<TimeModule>();
            ctx.RegisterModule<SerializeModule>();
            ctx.RegisterModule<PoolModule>();
            ctx.RegisterModule<FsmModule>();

            ctx.RegisterProvider<IInputProvider>(new UnityInputProvider());
            ctx.RegisterProvider<ITimeProvider>(new UnityTimeProvider());
            ctx.RegisterProvider<ISerializeProvider>(new JsonSerializeProvider());
            ctx.RegisterProvider<IPoolProvider>(new PoolProvider());
            ctx.RegisterProvider<IFsmProvider>(new FsmProvider());
            ctx.RegisterProvider<IEncryptionProvider>(new NoEncryptionProvider());

#if JULYGF_DEBUG
            ctx.RegisterProvider<IGMProvider>(new GMProvider());
#endif

            return UniTask.FromResult(true);
        }
    }
}

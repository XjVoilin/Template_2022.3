using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Launch;
using July.Logging;

namespace GameTemplate.Aot
{
    public sealed class RegisterAppSystemsStep : ILaunchStep
    {
        public string Name => "Register App Systems";

        public UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var registrar = FindRegistrar();
            if (registrar == null) return UniTask.FromResult(false);
            registrar.Register();
            SeedServices.Register<IHotUpdateRegistrar>(registrar);
            return UniTask.FromResult(true);
        }

        private static IHotUpdateRegistrar FindRegistrar()
        {
            const string typeFullName = "GameTemplate.HotUpdateRegistrar";
            try
            {
                var type = Assembly.Load("Assembly-CSharp").GetType(typeFullName);
                if (type != null) return (IHotUpdateRegistrar)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                JLogger.LogError($"[RegisterAppSystems] {ex.Message}");
            }
            JLogger.LogError($"[RegisterAppSystems] Registrar not found: {typeFullName}");
            return null;
        }
    }
}

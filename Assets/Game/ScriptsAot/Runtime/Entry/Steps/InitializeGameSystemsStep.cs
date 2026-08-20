using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Arch;
using July.Launch;

namespace Game.Aot
{
    public sealed class InitializeGameSystemsStep : ILaunchStep
    {
        private const string AssemblyName = "Game.Runtime";
        private const string RegistrarTypeName = "Game.HotUpdateRegistrar";

        public string Name => "Initialize Game Systems";

        public async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var registrar = CreateRegistrar();
            registrar.Register();
            SeedServices.Register(registrar);
            await registrar.PreInitializeAsync(ct);
            await ArchContext.Current.InitializeAsync(ct);
            return true;
        }

        private static IHotUpdateRegistrar CreateRegistrar()
        {
            var type = Assembly.Load(AssemblyName).GetType(RegistrarTypeName, true);
            if (!typeof(IHotUpdateRegistrar).IsAssignableFrom(type))
                throw new InvalidOperationException(
                    $"{RegistrarTypeName} 必须实现 {nameof(IHotUpdateRegistrar)}。");

            return Activator.CreateInstance(type) as IHotUpdateRegistrar ??
                   throw new InvalidOperationException(
                       $"无法创建热更注册器 {RegistrarTypeName}。");
        }
    }
}

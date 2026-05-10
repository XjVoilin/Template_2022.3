using JulyArch;
using JulyCore.Core;
using JulyCore.Core.Launch;
using UnityEngine;

namespace GameTemplate.Aot
{
    public class GameEntry : JulyGameEntry
    {
        protected override void ConfigurePipeline(LaunchPipeline pipeline)
        {
#if !JULYGF_DEBUG
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif

            pipeline.Add(new RegisterInfrastructureStep());
            pipeline.Add(new InitInfrastructureStep());
            pipeline.Add(new InitResourceStep());
            pipeline.Add(new RegisterModulesStep());
            pipeline.Add(new InitModulesStep());
            pipeline.Add(new LaunchGameStep());
        }

        protected override void Update()
        {
            base.Update();
            if (!IsInitialized) return;
            AppArch.Context?.Update(Time.deltaTime);
        }

        protected override void OnDestroy()
        {
            AppArch.Context?.Shutdown();
            base.OnDestroy();
        }
    }
}

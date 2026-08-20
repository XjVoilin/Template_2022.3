using July.Arch;
using July.Launch;
using UnityEngine;

namespace Game.Aot
{
    public class GameEntry : JulyGameEntry
    {
        [SerializeField] private GameConfig _gameConfig = new();

        protected override void ConfigurePipeline(LaunchPipeline pipeline)
        {
            SeedServices.Register(_gameConfig);

#if !JULYGF_DEBUG
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif

            pipeline.Add(new InitializeAotSystemsStep());
            pipeline.Add(new InitializeResourceSystemStep());
            pipeline.Add(new HotUpdateStep());
            pipeline.Add(new InitializeGameSystemsStep());
            pipeline.Add(new LaunchGameStep());
        }

        private void Update()
        {
            if (!IsInitialized) return;
            ArchContext.Current.Update(Time.deltaTime);
        }

        protected override void OnDestroy()
        {
            SeedServices.Clear();
            base.OnDestroy();
        }
    }
}

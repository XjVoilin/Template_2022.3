using July.Arch;
using July.Launch;
using July.Logging;
using UnityEngine;

namespace GameTemplate.Aot
{
    public class GameEntry : JulyGameEntry
    {
        [SerializeField] private BootConfig _bootConfig = new();
        [SerializeField] private GameConfig _gameConfig;

        private GameConfig _runtimeGameConfig;

        protected override void ConfigurePipeline(LaunchPipeline pipeline)
        {
            var gameConfig = _gameConfig;
            if (gameConfig == null)
            {
                _runtimeGameConfig = ScriptableObject.CreateInstance<GameConfig>();
                gameConfig = _runtimeGameConfig;
                JLogger.LogWarning("[GameEntry] GameConfig is not assigned; using runtime defaults");
            }

            SeedServices.Register(gameConfig);

#if !JULYGF_DEBUG
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif

            pipeline.Add(new BootArchStep(_bootConfig));
            pipeline.Add(new InitResourceStep());
            pipeline.Add(new LoadHotUpdateAssembliesStep());
            pipeline.Add(new RegisterProvidersStep(_bootConfig));
            pipeline.Add(new RegisterAppSystemsStep());
            pipeline.Add(new InitAppSystemsStep());
            pipeline.Add(new LaunchGameStep());
        }

        private void Update()
        {
            if (!IsInitialized) return;
            ArchContext.Current?.Update(Time.deltaTime);
        }

        protected override void OnDestroy()
        {
            SeedServices.Clear();
            base.OnDestroy();

            if (_runtimeGameConfig != null)
                Destroy(_runtimeGameConfig);
        }
    }
}

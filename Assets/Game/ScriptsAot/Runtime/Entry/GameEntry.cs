using System;
using July.Bootstrap;
using July.Arch;
using July.Launch;
using UnityEngine;

namespace Game.Aot
{
    public class GameEntry : BootstrapGameEntry
    {
        [SerializeField] private GameConfig _gameConfig;
        [SerializeField] private LaunchView _launchView;

        protected override void ConfigurePipeline(LaunchPipeline pipeline)
        {
            if (_launchView == null) throw new InvalidOperationException("GameEntry 未指定启动画面。");
            ArchContext.Current.RegisterStore(new LaunchStore(_gameConfig));
#if !JULYGF_DEBUG
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif
            Bootstrap.Configure(pipeline, _gameConfig.Bootstrap, _launchView,
                AOTGenericReferences.PatchedAOTAssemblyList);
        }

        protected override void OnShutdown()
        {
            base.OnShutdown();
            if (_launchView != null) Destroy(_launchView.gameObject);
        }
    }
}

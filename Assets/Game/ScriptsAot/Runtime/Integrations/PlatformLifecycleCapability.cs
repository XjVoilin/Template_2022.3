using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using July.Platform;

namespace GameTemplate.Aot
{
    public sealed class PlatformLaunchContext
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyQuery =
            new Dictionary<string, string>();

        public PlatformLaunchContext(bool isColdStart, string sceneId,
            IReadOnlyDictionary<string, string> query)
        {
            IsColdStart = isColdStart;
            SceneId = sceneId ?? string.Empty;
            Query = query ?? EmptyQuery;
        }

        public bool IsColdStart { get; }
        public string SceneId { get; }
        public IReadOnlyDictionary<string, string> Query { get; }
    }

    public interface IPlatformLifecycleCapability : IPlatformCapability
    {
        PlatformLaunchContext ColdContext { get; }
        PlatformLaunchContext LatestContext { get; }
        event Action<PlatformLaunchContext> Shown;
    }
}

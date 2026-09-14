using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Game.Aot;
using July.Arch;
using July.Bootstrap;
using July.Networking;
using July.Persistence;
using July.Release.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YooAsset.Editor;
using Object = UnityEngine.Object;

namespace Game.Template.Tests
{
    public class TemplateStartupTests
    {
        [Test]
        public void ReleaseUsesTheRuntimeConfigAndCodeCollectors()
        {
            var game = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Settings/GameConfig.asset");
            var build = AssetDatabase.LoadAssetAtPath<BuildConfig>("Assets/Settings/BuildConfig.asset");
            Assert.That(game, Is.Not.Null);
            Assert.That(build, Is.Not.Null);
            Assert.That(build.bootConfig, Is.SameAs(game));
            Assert.That(build.collectorSettings, Is.Not.Null);
            var groups = build.collectorSettings.Packages.Single(p => p.PackageName == game.Bootstrap.Resource.PackageName).Groups;
            foreach (var code in new[] { ("HotFix", "HotFix"), ("AOTMeta", "AotMeta") })
            {
                var group = groups.Single(g => g.GroupName == code.Item1);
                Assert.That(group.Collectors, Has.Count.EqualTo(1));
                var tags = (group.AssetTags + ";" + group.Collectors[0].AssetTags).Split(';');
                Assert.That(tags, Does.Contain(code.Item2));
                Assert.That(AssetDatabase.IsValidFolder(group.Collectors[0].CollectPath), Is.True);
            }
            foreach (var collector in groups.SelectMany(g => g.Collectors))
                Assert.That(collector.CollectPath, Is.Not.EqualTo("Assets/Game/Scenes").And.Not.EndsWith("Launch.unity"));
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/Launch.unity")), Is.True);
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LaunchSceneReachesMainAndReleasesView()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Launch.unity");
            yield return new EnterPlayMode();
            var deadline = Time.realtimeSinceStartup + 60;
            while (SceneManager.GetActiveScene().name != "Main" || Object.FindObjectOfType<LaunchView>() != null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "标准启动未在 60 秒内进入 Main 并关闭画面。");
                yield return null;
            }
            Assert.That(ArchContext.Current, Is.Not.Null);
            var config = ArchContext.Current.GetStore<LaunchStore>().GetProjectConfig<GameConfig>();
            Assert.That(config.name, Is.EqualTo("GameConfig"));
            Assert.That(ArchContext.Current.GetSystem<IHttpSystem>(), Is.Not.Null);
            Assert.That(ArchContext.Current.GetStore<HttpPendingQueueStore>(), Is.Not.Null);
            Assert.That(ArchContext.Current.GetSystem<ISaveSystem>(), Is.TypeOf<PlatformPreferencesSaveSystem>());
            Assert.That(Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length, Is.EqualTo(1));
            yield return new ExitPlayMode();
            Assert.That(ArchContext.Current, Is.Null, "退出后应释放 Arch。");
        }

        [UnityTest]
        public IEnumerator FailureViewReturnsAllowedActionAndCancelsPendingWait()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            ExerciseFailureView();
            yield return null;
            yield return new ExitPlayMode();
        }

        private static void ExerciseFailureView()
        {
            var view = new GameObject("TestLaunchView").AddComponent<LaunchView>();
            var retry = view.ShowFailureAsync(new LaunchFailure("download", true), CancellationToken.None);
            view.GetComponentInChildren<Button>(true).onClick.Invoke();
            Assert.That(retry.GetAwaiter().GetResult(), Is.EqualTo(LaunchFailureAction.Retry));
            var restart = view.ShowFailureAsync(new LaunchFailure("assemblies", false), CancellationToken.None);
            view.GetComponentInChildren<Button>(true).onClick.Invoke();
            Assert.That(restart.GetAwaiter().GetResult(), Is.EqualTo(LaunchFailureAction.Restart));
            using var cancellation = new CancellationTokenSource();
            var pending = view.ShowFailureAsync(new LaunchFailure("download", true), cancellation.Token);
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            Assert.That(view.GetComponentInChildren<Button>(true).gameObject.activeSelf, Is.False);
            Object.Destroy(view.gameObject);
        }
    }
}

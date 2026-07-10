#if UNITY_YOOASSET
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyArch;
using JulyGame;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameTemplate.Aot
{
    /// <summary>
    /// 基于 YooAsset 的资源系统实现。
    /// 内部委托给 YooAssetBackend 执行实际资源操作（句柄、引用计数）。
    /// 由 InitResourceStep 创建并 Boot，注册到 ArchContext.Current。
    /// </summary>
    public sealed class YooAssetResourceSystem : SystemBase, IResourceSystem
    {
        private YooAssetBackend _backend;
        private bool _booted;

        internal void Boot(YooAssetBackend backend)
        {
            if (_booted) return;
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _booted = true;
        }

        protected override UniTask OnInitializeAsync()
        {
            if (!_booted)
                throw new InvalidOperationException(
                    "[YooAssetResourceSystem] 未调用 Boot()，请确保 InitResourceStep 已执行");
            return UniTask.CompletedTask;
        }

        protected override void OnShutdown()
        {
            _backend?.Shutdown();
            _backend = null;
            _booted = false;
        }

        #region Asset Loading

        public UniTask<ResourceHandle<T>> LoadAssetAsync<T>(string fileName, CancellationToken ct = default)
            where T : UnityEngine.Object
        {
            EnsureProvider();
            return _backend.LoadAssetAsync<T>(fileName, ct);
        }

        public async UniTask<T> LoadAsync<T>(string fileName, GameObject bindTo, CancellationToken ct = default)
            where T : UnityEngine.Object
        {
            EnsureProvider();
            var handle = await _backend.LoadAssetAsync<T>(fileName, ct);
            if (handle == null || !handle.IsValid)
                return null;
            handle.BindTo(bindTo);
            return handle.Asset;
        }

        public async UniTask<TResult> LoadScopedAsync<T, TResult>(string fileName, Func<T, TResult> use,
            CancellationToken ct = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            using var handle = await _backend.LoadAssetAsync<T>(fileName, ct);
            if (handle == null || !handle.IsValid)
                return default;
            return use(handle.Asset);
        }

        public async UniTask<ResourceHandle<T>[]> LoadBatchAsync<T>(IReadOnlyList<string> fileNames,
            CancellationToken ct = default) where T : UnityEngine.Object
        {
            EnsureProvider();
            if (fileNames == null || fileNames.Count == 0)
                return Array.Empty<ResourceHandle<T>>();

            var handles = new ResourceHandle<T>[fileNames.Count];
            try
            {
                var tasks = new UniTask<ResourceHandle<T>>[fileNames.Count];
                for (int i = 0; i < fileNames.Count; i++)
                    tasks[i] = _backend.LoadAssetAsync<T>(fileNames[i], ct);

                var results = await UniTask.WhenAll(tasks);
                for (int i = 0; i < results.Length; i++)
                    handles[i] = results[i];

                return handles;
            }
            catch
            {
                for (int i = 0; i < handles.Length; i++)
                    handles[i]?.Dispose();
                throw;
            }
        }

        public bool HasAsset(string fileName)
        {
            return _backend?.HasAsset(fileName) ?? false;
        }

        #endregion

        #region Instantiate

        public async UniTask<GameObject> InstantiateAsync(string fileName, Transform parent = null,
            CancellationToken ct = default)
        {
            EnsureProvider();
            var handle = await _backend.LoadAssetAsync<GameObject>(fileName, ct);
            if (handle == null || !handle.IsValid)
                return null;
            var instance = UnityEngine.Object.Instantiate(handle.Asset, parent);
            handle.BindTo(instance);
            return instance;
        }

        public async UniTask<T> InstantiateAsync<T>(string fileName, Transform parent = null,
            CancellationToken ct = default) where T : Component
        {
            var instance = await InstantiateAsync(fileName, parent, ct);
            if (instance == null) return null;
            var component = instance.GetComponent<T>();
            if (component == null)
            {
                Debug.LogWarning(
                    $"[YooAssetResourceSystem] Prefab '{fileName}' 上未找到组件 {typeof(T).Name}，销毁实例");
                UnityEngine.Object.Destroy(instance);
            }
            return component;
        }

        #endregion

        #region Download

        public UniTask<bool> DownloadByTagAsync(string tag, CancellationToken ct = default)
        {
            EnsureProvider();
            return _backend.DownloadByTagAsync(tag, ct);
        }

        public UniTask<bool> DownloadByTagWithRetryAsync(string tag, int maxRetries = 3,
            CancellationToken ct = default)
        {
            EnsureProvider();
            return _backend.DownloadByTagWithRetryAsync(tag, maxRetries, ct);
        }

        #endregion

        #region Unload

        public UniTask UnloadUnusedAssetsAsync()
        {
            EnsureProvider();
            return _backend.UnloadUnusedAssetsAsync();
        }

        #endregion

        #region Scene

        public UniTask<Scene> LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single,
            CancellationToken ct = default)
        {
            EnsureProvider();
            return _backend.LoadSceneAsync(sceneName, mode, ct);
        }

        public UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken ct = default)
        {
            EnsureProvider();
            return _backend.UnloadSceneAsync(sceneName, ct);
        }

        #endregion

        private void EnsureProvider()
        {
            if (_backend == null)
                throw new InvalidOperationException(
                    "[YooAssetResourceSystem] Backend 未初始化，请确保 InitResourceStep 已执行");
        }
    }
}
#endif


#if UNITY_YOOASSET
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core;
using JulyCore.Core.Config;
using JulyCore.Provider.Base;
using JulyCore.Provider.Resource;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using Object = UnityEngine.Object;

namespace GameTemplate.Aot
{
    internal class YooAssetResourceProvider : ProviderBase, IResourceProvider
    {
        private readonly EPlayMode _playMode;

        public YooAssetResourceProvider(FrameworkConfig config)
        {
#if UNITY_EDITOR
            _playMode = config.PlayMode switch
            {
                JPlayMode.EditorSimulateMode => EPlayMode.EditorSimulateMode,
                JPlayMode.OfflinePlayMode => EPlayMode.OfflinePlayMode,
                JPlayMode.HostPlayMode => EPlayMode.HostPlayMode,
                _ => EPlayMode.EditorSimulateMode
            };
#else
            _playMode = config.PlayMode switch
            {
                JPlayMode.HostPlayMode => EPlayMode.HostPlayMode,
                _ => EPlayMode.OfflinePlayMode
            };
#endif
        }

        private ResourcePackage _resourcePackage;

        private readonly Dictionary<Object, int> _refCounts = new();
        private readonly Dictionary<string, Object> _locationToResourceCache = new();
        private readonly Dictionary<Object, string> _objectToLocation = new();
        private readonly Dictionary<string, List<AssetHandle>> _locationToHandles = new();
        private readonly Dictionary<string, AssetHandle> _preloadHandles = new();

        protected override LogChannel LogChannel => LogChannel.Resource;

        protected override async UniTask OnInitAsync()
        {
            try
            {
                var packageName = "DefaultPackage";
                YooAssets.Initialize();

                var package = YooAssets.CreatePackage(packageName);
                YooAssets.SetDefaultPackage(package);
                _resourcePackage = package;

                InitializationOperation initOp;

                if (_playMode == EPlayMode.EditorSimulateMode)
                {
                    var buildResult = EditorSimulateModeHelper.SimulateBuild(packageName);
                    var packageRoot = buildResult.PackageRootDirectory;
                    var createParameters = new EditorSimulateModeParameters();
                    createParameters.EditorFileSystemParameters =
                        FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
                    initOp = package.InitializeAsync(createParameters);
                }
                else if (_playMode == EPlayMode.OfflinePlayMode)
                {
                    var createParameters = new OfflinePlayModeParameters();
                    createParameters.BuildinFileSystemParameters =
                        FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                    initOp = package.InitializeAsync(createParameters);
                }
                else if (_playMode == EPlayMode.HostPlayMode)
                {
                    var createParameters = new HostPlayModeParameters
                    {
                        BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(),
                        CacheFileSystemParameters =
                            FileSystemParameters.CreateDefaultCacheFileSystemParameters(new RemoteServices(""))
                    };
                    initOp = package.InitializeAsync(createParameters);
                }
                else
                {
                    throw new InvalidOperationException($"[{Name}] 不支持的运行模式: {_playMode}");
                }

                await initOp;

                if (initOp.Status != EOperationStatus.Succeed)
                    throw new InvalidOperationException($"[{Name}] 资源包初始化失败: {initOp.Error}");

                GF.Log($"[{Name}] YooAsset 资源包初始化成功（{_playMode}）");

                await UpdateManifestAsync();
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                throw;
            }
        }

        public async UniTask UpdateManifestAsync()
        {
            var requestVersionOp = _resourcePackage.RequestPackageVersionAsync(appendTimeTicks: true);
            await requestVersionOp;

            if (requestVersionOp.Status != EOperationStatus.Succeed)
                throw new InvalidOperationException($"[{Name}] 请求资源版本失败: {requestVersionOp.Error}");

            var version = requestVersionOp.PackageVersion;
            GF.Log($"[{Name}] 资源版本: {version}");

            var updateManifestOp = _resourcePackage.UpdatePackageManifestAsync(version);
            await updateManifestOp;

            if (updateManifestOp.Status != EOperationStatus.Succeed)
                throw new InvalidOperationException($"[{Name}] 资源清单加载失败: {updateManifestOp.Error}");

            GF.Log($"[{Name}] 资源清单更新成功 (版本: {version})");
        }

        public async UniTask<bool> DownloadByTagAsync(string tag, CancellationToken ct = default)
        {
            var downloader = _resourcePackage.CreateResourceDownloader(tag, 10, 3);
            if (downloader.TotalDownloadCount <= 0)
            {
                GF.Log($"[{Name}] [{tag}] 无需下载");
                return true;
            }

            GF.Log($"[{Name}] [{tag}] 需要下载 {downloader.TotalDownloadCount} 个文件，共 {downloader.TotalDownloadBytes / 1048576f:F2} MB");

            downloader.BeginDownload();

            try
            {
                await UniTask.WaitUntil(() => downloader.Status != EOperationStatus.Processing,
                    cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                downloader.CancelDownload();
                return false;
            }

            return downloader.Status == EOperationStatus.Succeed;
        }

        private void EnsurePackage()
        {
            if (_resourcePackage == null)
                throw new InvalidOperationException($"[{Name}] 资源包未找到。");

            if (_resourcePackage.InitializeStatus == EOperationStatus.None)
                throw new InvalidOperationException($"[{Name}] 资源包尚未初始化。");
        }

        #region 资源加载

        public async UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default)
            where T : Object
        {
            if (string.IsNullOrEmpty(fileName))
            {
                GF.LogWarning($"[{Name}] 资源文件名不能为空");
                return null;
            }

            EnsurePackage();

            if (TryGetCachedResource<T>(fileName, out var cachedResource))
            {
                IncrementRefCount(cachedResource);
                return cachedResource;
            }

            AssetHandle handle = null;
            if (_preloadHandles.TryGetValue(fileName, out var preloadHandle))
            {
                _preloadHandles.Remove(fileName);
                handle = await WaitForPreload(fileName, preloadHandle, cancellationToken);
            }

            if (handle == null)
            {
                try
                {
                    handle = _resourcePackage.LoadAssetAsync<T>(fileName);
                    await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    handle?.Release();
                    return null;
                }
            }

            try
            {
                if (!handle.IsValid)
                {
                    GF.LogWarning($"[{Name}] 资源加载失败: {fileName}");
                    handle.Release();
                    return null;
                }

                var resource = handle.AssetObject as T;
                if (resource == null)
                {
                    GF.LogWarning($"[{Name}] 资源类型不匹配: {fileName}");
                    handle.Release();
                    return null;
                }

                RecordResourceMapping(fileName, handle, resource);
                IncrementRefCount(resource);
                return resource;
            }
            catch (Exception ex)
            {
                handle?.Release();
                GF.LogWarning($"[{Name}] 加载资源异常: {fileName}");
                GF.LogException(ex);
                return null;
            }
        }

        public async UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName, bool captureStackTrace = false,
            CancellationToken cancellationToken = default) where T : Object
        {
            var asset = await LoadAsync<T>(fileName, cancellationToken);
            if (asset == null) return null;
            return new ResourceHandle<T>(asset, fileName, this, captureStackTrace);
        }

        public async UniTask<List<T>> LoadBatchAsync<T>(IEnumerable<string> fileNames,
            CancellationToken cancellationToken = default) where T : Object
        {
            var results = new List<T>();
            foreach (var fileName in fileNames)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                var resource = await LoadAsync<T>(fileName, cancellationToken);
                results.Add(resource);
            }

            return results;
        }

        public async UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default)
            where T : Object
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            EnsurePackage();

            if (_preloadHandles.ContainsKey(fileName) || _locationToResourceCache.ContainsKey(fileName))
                return true;

            try
            {
                var handle = _resourcePackage.LoadAssetAsync<T>(fileName);
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);

                if (handle.IsValid)
                {
                    _preloadHandles[fileName] = handle;
                    return true;
                }

                handle.Release();
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                GF.LogWarning($"[{Name}] 预加载资源异常: {fileName}");
                GF.LogException(ex);
                return false;
            }
        }

        public async UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName,
            CancellationToken cancellationToken = default) where T : Object
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(assetName))
                return null;

            EnsurePackage();

            SubAssetsHandle handle = null;
            try
            {
                handle = _resourcePackage.LoadSubAssetsAsync<T>(fileName);
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);

                if (!handle.IsValid)
                {
                    handle.Release();
                    return null;
                }

                var targetAsset = handle.GetSubAssetObject<T>(assetName);
                handle.Release();
                return targetAsset;
            }
            catch (OperationCanceledException)
            {
                handle?.Release();
                return null;
            }
            catch (Exception ex)
            {
                handle?.Release();
                GF.LogException(ex);
                return null;
            }
        }

        public async UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName,
            CancellationToken cancellationToken = default) where T : Object
        {
            if (string.IsNullOrEmpty(fileName))
                return new List<T>();

            EnsurePackage();

            AllAssetsHandle handle = null;
            try
            {
                handle = _resourcePackage.LoadAllAssetsAsync<T>(fileName);
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);

                if (!handle.IsValid)
                {
                    handle.Release();
                    return new List<T>();
                }

                var results = new List<T>();
                foreach (var asset in handle.AllAssetObjects)
                {
                    if (asset is T t)
                        results.Add(t);
                }

                handle.Release();
                return results;
            }
            catch (OperationCanceledException)
            {
                handle?.Release();
                return new List<T>();
            }
            catch (Exception ex)
            {
                handle?.Release();
                GF.LogException(ex);
                return new List<T>();
            }
        }

        /// <summary>
        /// 按 Tag 下载资源（含整体重试）。单次下载内部由 YooAsset 处理单文件重试，
        /// 此方法在整体失败时按递增延迟重试整个下载批次。
        /// </summary>
        public async UniTask<bool> DownloadByTagWithRetryAsync(string tag, int maxRetries = 3,
            CancellationToken ct = default)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                var success = await DownloadByTagAsync(tag, ct);
                if (success) return true;

                GF.LogWarning($"[{Name}] [{tag}] 下载失败，第 {i + 1}/{maxRetries} 次重试");
                if (i < maxRetries - 1)
                    await UniTask.Delay(1000 * (i + 1), cancellationToken: ct);
            }

            return false;
        }

        public bool HasAsset(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            EnsurePackage();
            var info = _resourcePackage.GetAssetInfo(fileName);
            return info != null;
        }

        #endregion

        #region 资源卸载

        public void Unload(Object obj)
        {
            if (obj == null) return;

            if (!_objectToLocation.TryGetValue(obj, out var location))
                return;

            if (DecrementRefCount(obj))
            {
                ReleaseResourceHandles(location);
                CleanupResourceMappings(location, obj);
            }
        }

        public void UnloadAll()
        {
            foreach (var kvp in _locationToHandles)
            {
                foreach (var handle in kvp.Value)
                {
                    if (handle != null && handle.IsValid)
                        handle.Release();
                }
            }

            foreach (var kvp in _preloadHandles)
            {
                if (kvp.Value != null && kvp.Value.IsValid)
                    kvp.Value.Release();
            }

            _locationToHandles.Clear();
            _objectToLocation.Clear();
            _locationToResourceCache.Clear();
            _refCounts.Clear();
            _preloadHandles.Clear();

            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region 场景加载

        private readonly Dictionary<string, SceneHandle> _sceneHandles = new();

        public async UniTask<Scene> LoadSceneAsync(
            string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("场景名称不能为空", nameof(sceneName));

            EnsurePackage();

            var existingScene = SceneManager.GetSceneByName(sceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
                return existingScene;

            SceneHandle sceneHandle = null;
            try
            {
                sceneHandle = _resourcePackage.LoadSceneAsync(sceneName, loadSceneMode);
                await UniTask.WaitUntil(() => sceneHandle.IsDone, cancellationToken: cancellationToken);

                if (!sceneHandle.IsValid)
                    throw new InvalidOperationException($"[{Name}] 场景 {sceneName} 加载失败");

                _sceneHandles[sceneName] = sceneHandle;
                return sceneHandle.SceneObject;
            }
            catch (OperationCanceledException)
            {
                sceneHandle?.Release();
                throw;
            }
        }

        public async UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("场景名称不能为空", nameof(sceneName));

            if (!_sceneHandles.Remove(sceneName, out var sceneHandle))
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid() || !scene.isLoaded)
                    return false;

                var asyncOp = SceneManager.UnloadSceneAsync(scene);
                if (asyncOp == null)
                    return false;

                await UniTask.WaitUntil(() => asyncOp.isDone, cancellationToken: cancellationToken);
                return true;
            }

            if (sceneHandle != null && sceneHandle.IsValid)
            {
                var unloadOp = sceneHandle.UnloadAsync();
                await UniTask.WaitUntil(() => unloadOp.IsDone, cancellationToken: cancellationToken);
                sceneHandle.Release();
                return true;
            }

            return false;
        }

        #endregion

        protected override void OnShutdown()
        {
            foreach (var kvp in _sceneHandles)
            {
                if (kvp.Value != null && kvp.Value.IsValid)
                    kvp.Value.Release();
            }

            _sceneHandles.Clear();
            UnloadAll();
        }

        #region Private Methods

        private async UniTask<AssetHandle> WaitForPreload(string fileName, AssetHandle preloadHandle,
            CancellationToken cancellationToken)
        {
            if (!preloadHandle.IsDone)
            {
                try
                {
                    await UniTask.WaitUntil(() => preloadHandle.IsDone, cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _preloadHandles[fileName] = preloadHandle;
                    return null;
                }
            }

            if (preloadHandle.IsValid)
                return preloadHandle;

            preloadHandle.Release();
            return null;
        }

        private bool TryGetCachedResource<T>(string location, out T resource) where T : Object
        {
            resource = null;
            if (_locationToResourceCache.TryGetValue(location, out var cachedObj))
            {
                if (cachedObj == null)
                {
                    _locationToResourceCache.Remove(location);
                    return false;
                }

                if (cachedObj is T t)
                {
                    resource = t;
                    return true;
                }
            }

            return false;
        }

        private void IncrementRefCount(Object obj)
        {
            _refCounts[obj] = _refCounts.TryGetValue(obj, out var count) ? count + 1 : 1;
        }

        private bool DecrementRefCount(Object obj)
        {
            if (!_refCounts.TryGetValue(obj, out var count))
                return false;

            if (count <= 1)
            {
                _refCounts.Remove(obj);
                return true;
            }

            _refCounts[obj] = count - 1;
            return false;
        }

        private void ReleaseResourceHandles(string location)
        {
            if (!_locationToHandles.Remove(location, out var handles))
                return;

            foreach (var handle in handles)
            {
                if (handle != null && handle.IsValid)
                    handle.Release();
            }
        }

        private void CleanupResourceMappings(string location, Object obj)
        {
            _locationToResourceCache.Remove(location);
            _objectToLocation.Remove(obj);
        }

        private void RecordResourceMapping(string location, AssetHandle handle, Object resource)
        {
            if (!_locationToHandles.TryGetValue(location, out var handles))
            {
                handles = new List<AssetHandle>(1);
                _locationToHandles[location] = handles;
            }

            handles.Add(handle);
            _objectToLocation[resource] = location;
            _locationToResourceCache[location] = resource;
        }

        #endregion

        private class RemoteServices : IRemoteServices
        {
            private readonly string _defaultHostServer;

            public RemoteServices(string defaultHostServer)
            {
                _defaultHostServer = defaultHostServer;
            }

            string IRemoteServices.GetRemoteMainURL(string fileName)
            {
                return $"{_defaultHostServer}/{fileName}";
            }

            string IRemoteServices.GetRemoteFallbackURL(string fileName)
            {
                return $"{_defaultHostServer}/{fileName}";
            }
        }
    }
}
#endif

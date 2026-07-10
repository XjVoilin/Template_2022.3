#if UNITY_YOOASSET
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using Object = UnityEngine.Object;

namespace GameTemplate.Aot
{
    /// <summary>
    /// YooAsset 适配后端：把 YooAsset SDK 包成 ResourceHandle/UniTask 风格的 API。
    /// 仅供 <see cref="YooAssetResourceSystem"/> 内部使用，业务层不直接持有。
    /// </summary>
    internal class YooAssetBackend
    {
        public string Name => nameof(YooAssetBackend);

        private readonly EPlayMode _playMode;
        private readonly CDNEndpoints _cdnEndpoints;

        /// <summary>
        /// 是否为远程模式（需要远端 CDN 配置后手动初始化）
        /// </summary>
        public bool IsRemoteMode => _playMode == EPlayMode.HostPlayMode || _playMode == EPlayMode.WebPlayMode;

        public YooAssetBackend(BootConfig config, CDNEndpoints cdnEndpoints)
        {
            _cdnEndpoints = cdnEndpoints;
#if UNITY_EDITOR
            _playMode = config.PlayMode switch
            {
                JPlayMode.EditorSimulateMode => EPlayMode.EditorSimulateMode,
                JPlayMode.OfflinePlayMode => EPlayMode.OfflinePlayMode,
                JPlayMode.HostPlayMode => EPlayMode.HostPlayMode,
                JPlayMode.WebPlayMode => EPlayMode.WebPlayMode,
                JPlayMode.CustomPlayMode => EPlayMode.CustomPlayMode,
                _ => EPlayMode.EditorSimulateMode
            };
#elif JULYGF_WX_MINIGAME || JULYGF_DY_MINIGAME
            _playMode = EPlayMode.WebPlayMode;
#else
            _playMode = config.PlayMode switch
            {
                JPlayMode.HostPlayMode => EPlayMode.HostPlayMode,
                JPlayMode.WebPlayMode => EPlayMode.WebPlayMode,
                JPlayMode.CustomPlayMode => EPlayMode.CustomPlayMode,
                _ => EPlayMode.OfflinePlayMode
            };
#endif
        }

        private ResourcePackage _resourcePackage;

        public async UniTask InitAsync()
        {
            try
            {
                var packageName = "DefaultPackage";
                YooAssets.Initialize();

                var package = YooAssets.CreatePackage(packageName);
                YooAssets.SetDefaultPackage(package);
                _resourcePackage = package;

                InitializationOperation initOp;

                if (IsRemoteMode)
                {
                    if (string.IsNullOrEmpty(_cdnEndpoints.MainURL))
                        throw new InvalidOperationException($"[{Name}] 远程模式要求 CDNEndpoints 在 OnPreLaunch 中已注入");

                    initOp = InitializeRemotePackage(_cdnEndpoints.MainURL);
                }
                else if (_playMode == EPlayMode.EditorSimulateMode)
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
                else if (_playMode == EPlayMode.CustomPlayMode)
                {
                    Debug.LogWarning($"[{Name}] CustomPlayMode 模式需要外部设置资源包，请调用 SetResourcePackage 方法");
                    return;
                }
                else
                {
                    throw new InvalidOperationException($"[{Name}] 不支持的运行模式: {_playMode}");
                }

                await initOp;

                if (initOp.Status != EOperationStatus.Succeed)
                    throw new InvalidOperationException($"[{Name}] 资源包初始化失败: {initOp.Error}");

                Debug.Log($"[{Name}] YooAsset 资源包初始化成功（{_playMode}）");

                if (_playMode != EPlayMode.CustomPlayMode)
                {
                    await UpdateManifestAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw;
            }
        }

        private InitializationOperation InitializeRemotePackage(string mainURL)
        {
            IRemoteServices remoteServices = new RemoteServices(mainURL);

            Debug.Log($"[{Name}] 远程初始化 — Main: {mainURL}");

            if (_playMode == EPlayMode.HostPlayMode)
            {
                var createParameters = new HostPlayModeParameters
                {
                    BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(),
                    CacheFileSystemParameters =
                        FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices)
                };
                return _resourcePackage.InitializeAsync(createParameters);
            }

            // WebPlayMode
            var webParameters = new WebPlayModeParameters();

#if UNITY_WEBGL && JULYGF_WX_MINIGAME
            var cdnUri = new System.Uri(mainURL);
            WeChatWASM.WX.SetDataCDN($"{cdnUri.Scheme}://{cdnUri.Authority}/");
            string packageRoot = $"{WeChatWASM.WX.env.USER_DATA_PATH}/__GAME_FILE_CACHE{cdnUri.AbsolutePath}";
            webParameters.WebServerFileSystemParameters =
                WechatFileSystemCreater.CreateFileSystemParameters(packageRoot, remoteServices);
#elif UNITY_WEBGL && JULYGF_DY_MINIGAME
        string packageRoot = "yoo";
        webParameters.WebServerFileSystemParameters =
            TiktokFileSystemCreater.CreateFileSystemParameters(packageRoot, remoteServices);
#else
            webParameters.WebServerFileSystemParameters =
                FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
#endif

            return _resourcePackage.InitializeAsync(webParameters);
        }

        /// <summary>
        /// 请求资源版本号并更新清单。appendTimeTicks 防止 CDN 缓存。
        /// </summary>
        public async UniTask UpdateManifestAsync()
        {
            var requestVersionOp = _resourcePackage.RequestPackageVersionAsync(appendTimeTicks: true);
            await requestVersionOp;

            if (requestVersionOp.Status != EOperationStatus.Succeed)
                throw new InvalidOperationException($"[{Name}] 请求资源版本失败: {requestVersionOp.Error}");

            var version = requestVersionOp.PackageVersion;
            Debug.Log($"[{Name}] 资源版本: {version}");

            var updateManifestOp = _resourcePackage.UpdatePackageManifestAsync(version);
            await updateManifestOp;

            if (updateManifestOp.Status != EOperationStatus.Succeed)
                throw new InvalidOperationException($"[{Name}] 资源清单加载失败: {updateManifestOp.Error}");

            Debug.Log($"[{Name}] 资源清单更新成功 (版本: {version})");
        }

        #region 下载

        /// <summary>
        /// 按 Tag 下载资源。
        /// </summary>
        /// <param name="tag">资源标签（如 "lobby"、"game_101"）</param>
        /// <param name="ct">取消令牌（用户返回大厅时可 cancel 小游戏下载）</param>
        /// <returns>下载是否成功</returns>
        public async UniTask<bool> DownloadByTagAsync(string tag, CancellationToken ct = default)
        {
            var downloader = _resourcePackage.CreateResourceDownloader(tag, 10, 3);
            if (downloader.TotalDownloadCount <= 0)
            {
                Debug.Log($"[{Name}] [{tag}] 无需下载");
                return true;
            }

            var totalBytes = downloader.TotalDownloadBytes;
            var totalCount = downloader.TotalDownloadCount;
            Debug.Log($"[{Name}] [{tag}] 需要下载 {totalCount} 个文件，共 {totalBytes / 1048576f:F2} MB");

            downloader.DownloadErrorCallback = data =>
            {
                Debug.LogError($"[{Name}] [{tag}] 下载出错: {data.FileName}, {data.ErrorInfo}");
            };

            downloader.BeginDownload();

            try
            {
                await UniTask.WaitUntil(() => downloader.Status != EOperationStatus.Processing,
                    cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                downloader.CancelDownload();
                Debug.Log($"[{Name}] [{tag}] 下载已取消");
                return false;
            }

            var success = downloader.Status == EOperationStatus.Succeed;

            if (success)
                Debug.Log($"[{Name}] [{tag}] 下载完成");
            else
                Debug.LogError($"[{Name}] [{tag}] 下载失败");

            return success;
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

                Debug.LogWarning($"[{Name}] [{tag}] 下载失败，第 {i + 1}/{maxRetries} 次重试");
                if (i < maxRetries - 1)
                    await UniTask.Delay(1000 * (i + 1), cancellationToken: ct);
            }

            return false;
        }

        #endregion

        public async UniTask UnloadUnusedAssetsAsync()
        {
            EnsurePackage();
            var operation = _resourcePackage.UnloadUnusedAssetsAsync();
            await UniTask.WaitUntil(() => operation.IsDone);
        }

        private void EnsurePackage()
        {
            if (_resourcePackage == null)
            {
                throw new InvalidOperationException(
                    $"[{Name}] 资源包未找到。请确保已创建并初始化 YooAsset 资源包，或调用 SetResourcePackage 方法设置资源包。");
            }

            if (_resourcePackage.InitializeStatus == EOperationStatus.None)
            {
                throw new InvalidOperationException($"[{Name}] 资源包尚未初始化。");
            }
        }

        #region 资源加载

        public async UniTask<ResourceHandle<T>> LoadAssetAsync<T>(string fileName,
            CancellationToken cancellationToken = default) where T : Object
        {
            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogWarning($"[{Name}] 资源文件名不能为空");
                return null;
            }

            EnsurePackage();

            AssetHandle handle = null;
            try
            {
                handle = _resourcePackage.LoadAssetAsync<T>(fileName);
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                handle?.Release();
                Debug.LogWarning($"[{Name}] 资源加载已取消: {fileName}");
                return null;
            }
            catch (Exception ex)
            {
                handle?.Release();
                Debug.LogWarning($"[{Name}] 加载资源异常: {fileName}");
                Debug.LogException(ex);
                return null;
            }

            if (!handle.IsValid)
            {
                Debug.LogWarning($"[{Name}] 资源加载失败: {fileName}");
                handle.Release();
                return null;
            }

            var resource = handle.AssetObject as T;
            if (resource == null)
            {
                Debug.LogWarning($"[{Name}] 资源类型不匹配: {fileName}");
                handle.Release();
                return null;
            }

            var captured = handle;
            return new ResourceHandle<T>(resource, () => captured.Release());
        }

        public bool HasAsset(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            EnsurePackage();

            var info = _resourcePackage.GetAssetInfo(fileName);
            return info != null;
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
            {
                throw new ArgumentException("场景名称不能为空", nameof(sceneName));
            }

            EnsurePackage();

            var existingScene = SceneManager.GetSceneByName(sceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                Debug.LogWarning($"[{Name}] 场景 {sceneName} 已加载，直接返回");
                return existingScene;
            }

            SceneHandle sceneHandle = null;
            try
            {
                sceneHandle = _resourcePackage.LoadSceneAsync(sceneName, loadSceneMode);
                await UniTask.WaitUntil(() => sceneHandle.IsDone, cancellationToken: cancellationToken);

                if (!sceneHandle.IsValid)
                {
                    throw new Exception($"[{Name}] 场景 {sceneName} 加载失败");
                }

                _sceneHandles[sceneName] = sceneHandle;
                Debug.Log($"[{Name}] 场景 {sceneName} 加载完成");
                return sceneHandle.SceneObject;

            }
            catch (OperationCanceledException)
            {
                sceneHandle?.Release();
                throw;
            }
            catch (Exception ex)
            {
                sceneHandle?.Release();
                Debug.LogWarning($"[{Name}] 场景 {sceneName} 加载异常");
                Debug.LogException(ex);
                throw;
            }
        }

        public async UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                throw new ArgumentException("场景名称不能为空", nameof(sceneName));
            }

            if (!_sceneHandles.Remove(sceneName, out var sceneHandle))
            {
                Debug.LogWarning($"[{Name}] 场景 {sceneName} 未通过 YooAsset 加载，尝试使用 SceneManager 卸载");

                var scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    Debug.LogWarning($"[{Name}] 场景 {sceneName} 未加载，无需卸载");
                    return false;
                }

                var asyncOp = SceneManager.UnloadSceneAsync(scene);
                if (asyncOp == null)
                {
                    return false;
                }

                await UniTask.WaitUntil(() => asyncOp.isDone, cancellationToken: cancellationToken);
                Debug.Log($"[{Name}] 场景 {sceneName} 卸载完成（SceneManager）");
                return true;
            }

            if (sceneHandle != null && sceneHandle.IsValid)
            {
                var unloadOp = sceneHandle.UnloadAsync();
                await UniTask.WaitUntil(() => unloadOp.IsDone, cancellationToken: cancellationToken);
                sceneHandle.Release();
                Debug.Log($"[{Name}] 场景 {sceneName} 卸载完成");
                return true;
            }

            return false;
        }

        #endregion

        public void Shutdown()
        {
            foreach (var kvp in _sceneHandles)
            {
                if (kvp.Value != null && kvp.Value.IsValid)
                {
                    kvp.Value.Release();
                }
            }

            _sceneHandles.Clear();
            Debug.Log($"[{Name}] YooAsset 后端已关闭");
        }

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


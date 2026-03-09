#if UNITY_YOOASSET
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// YooAsset资源提供者实现
/// 使用引用计数管理资源生命周期，简化设计，覆盖传统手游常见需求
/// 
/// 核心设计：
/// 1. 使用文件名加载：所有资源通过文件名（不含扩展名）进行加载，自动提取文件名
/// 2. 使用引用计数：每次 LoadAsync 增加引用计数，Unload 减少引用计数
/// 3. 引用计数为 0 时释放 Handle，真正卸载资源
/// 4. 资源对象缓存：同一 location 的资源对象会被复用
/// 5. 预加载：不增加引用计数，仅将资源加载到内存
/// 
/// 注意：YooAsset使用文件名作为定位地址，通常不需要扩展名
/// </summary>
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
            JPlayMode.WebPlayMode => EPlayMode.WebPlayMode,
            JPlayMode.CustomPlayMode => EPlayMode.CustomPlayMode,
            _ => EPlayMode.EditorSimulateMode
        };
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

    // 资源包实例
    private ResourcePackage _resourcePackage;

    // 资源对象到引用计数的映射（业务层引用计数）
    private readonly ConcurrentDictionary<Object, int> _refCounts = new();

    // Location到资源对象的映射（用于缓存和复用，直接存储Object，引用计数管理生命周期）
    private readonly ConcurrentDictionary<string, Object> _locationToResourceCache = new();

    // 对象到Location的映射（用于通过对象卸载）
    private readonly ConcurrentDictionary<Object, string> _objectToLocation = new();

    // Handle到Location的映射（用于追踪所有Handle，在UnloadAll时释放）
    private readonly ConcurrentDictionary<AssetHandle, string> _handleToLocation = new();

    // 预加载的资源Handle（不增加引用计数）
    private readonly ConcurrentDictionary<string, AssetHandle> _preloadHandles = new();

    // 资源使用历史记录
    private readonly List<ResourceUsageHistory> _usageHistory = new List<ResourceUsageHistory>();
    private readonly object _usageHistoryLock = new object();
    private const int MaxUsageHistorySize = 1000; // 最多保留1000条历史记录

    // 资源使用频率统计（资源位置 -> 使用频率信息）
    private readonly Dictionary<string, ResourceUsageFrequency> _usageFrequency = new();

    private readonly object _usageFrequencyLock = new();

    protected override LogChannel LogChannel => LogChannel.Resource;

    /// <summary>
    /// 初始化Provider
    /// 注意：YooAsset资源包需要在外部先初始化，或者通过 SetResourcePackage 方法设置已初始化的包
    /// </summary>
    protected override async UniTask OnInitAsync()
    {
        InitializationOperation initializationOperation;
        try
        {
            var packageName = "DefaultPackage";
            // 初始化 YooAssets
            YooAssets.Initialize();

            // 创建资源包
            var package = YooAssets.CreatePackage(packageName);
            YooAssets.SetDefaultPackage(package);

            // 保存资源包引用
            _resourcePackage = package;

            // 编辑器下的模拟模式
            if (_playMode == EPlayMode.EditorSimulateMode)
            {
                var buildResult = EditorSimulateModeHelper.SimulateBuild(packageName);
                var packageRoot = buildResult.PackageRootDirectory;
                var createParameters = new EditorSimulateModeParameters();
                createParameters.EditorFileSystemParameters =
                    FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
                initializationOperation = package.InitializeAsync(createParameters);
            }
            // 单机运行模式
            else if (_playMode == EPlayMode.OfflinePlayMode)
            {
                var createParameters = new OfflinePlayModeParameters();
                createParameters.BuildinFileSystemParameters =
                    FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                initializationOperation = package.InitializeAsync(createParameters);
            }
            // 联机运行模式
            else if (_playMode == EPlayMode.HostPlayMode)
            {
                string defaultHostServer = GetHostServerURL();
                string fallbackHostServer = GetHostServerURL();
                IRemoteServices remoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
                var createParameters = new HostPlayModeParameters();
                createParameters.BuildinFileSystemParameters =
                    FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                createParameters.CacheFileSystemParameters =
                    FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices);
                initializationOperation = package.InitializeAsync(createParameters);
            }
            // WebGL运行模式
            else if (_playMode == EPlayMode.WebPlayMode)
            {
#if UNITY_WEBGL && WEIXINMINIGAME && !UNITY_EDITOR
                    var createParameters = new WebPlayModeParameters();
                    string defaultHostServer = GetHostServerURL();
                    string fallbackHostServer = GetHostServerURL();
                    string packageRoot = $"{WeChatWASM.WX.env.USER_DATA_PATH}/__GAME_FILE_CACHE"; //注意：如果有子目录，请修改此处！
                    IRemoteServices remoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
                    createParameters.WebServerFileSystemParameters =
                        WechatFileSystemCreater.CreateFileSystemParameters(packageRoot, remoteServices);
                    initializationOperation = package.InitializeAsync(createParameters);
#else
                var createParameters = new WebPlayModeParameters();
                createParameters.WebServerFileSystemParameters =
                    FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
                initializationOperation = package.InitializeAsync(createParameters);
#endif
            }
            // 自定义运行模式
            else if (_playMode == EPlayMode.CustomPlayMode)
            {
                // 自定义模式需要外部设置资源包，这里只记录日志
                GF.LogWarning($"[{Name}] CustomPlayMode 模式需要外部设置资源包，请调用 SetResourcePackage 方法");
                return;
            }
            else
            {
                throw new InvalidOperationException($"[{Name}] 不支持的运行模式: {_playMode}");
            }

            // 确保初始化操作已创建
            if (initializationOperation == null)
            {
                throw new InvalidOperationException($"[{Name}] 初始化操作创建失败，运行模式: {_playMode}");
            }

            // 等待初始化完成
            await initializationOperation;

            // 检查初始化结果
            if (initializationOperation.Status == EOperationStatus.Succeed)
            {
                GF.Log($"[{Name}] YooAsset 资源包初始化成功");

                // 在编辑器模拟模式下，需要加载 manifest
                if (_playMode == EPlayMode.EditorSimulateMode)
                {
                    // 请求资源版本（编辑器模拟模式下通常返回 "Simulate"）
                    var requestVersionOp = package.RequestPackageVersionAsync();
                    await requestVersionOp;

                    if (requestVersionOp.Status == EOperationStatus.Succeed)
                    {
                        // 更新资源清单
                        var updateManifestOp = package.UpdatePackageManifestAsync(requestVersionOp.PackageVersion);
                        await updateManifestOp;

                        if (updateManifestOp.Status == EOperationStatus.Succeed)
                        {
                            GF.Log($"[{Name}] YooAsset 资源清单加载成功 (版本: {requestVersionOp.PackageVersion})");
                        }
                        else
                        {
                            var error = updateManifestOp.Error;
                            GF.LogError($"[{Name}] YooAsset 资源清单加载失败: {error}");
                            throw new InvalidOperationException($"[{Name}] 资源清单加载失败: {error}");
                        }
                    }
                    else
                    {
                        var error = requestVersionOp.Error;
                        GF.LogError($"[{Name}] YooAsset 请求资源版本失败: {error}");
                        throw new InvalidOperationException($"[{Name}] 请求资源版本失败: {error}");
                    }
                }
                // 在单机运行模式下，manifest 应该已经在 StreamingAssets 中，需要加载
                else if (_playMode == EPlayMode.OfflinePlayMode)
                {
                    // 请求资源版本
                    var requestVersionOp = package.RequestPackageVersionAsync();
                    await requestVersionOp;

                    if (requestVersionOp.Status == EOperationStatus.Succeed)
                    {
                        // 更新资源清单
                        var updateManifestOp = package.UpdatePackageManifestAsync(requestVersionOp.PackageVersion);
                        await updateManifestOp;

                        if (updateManifestOp.Status == EOperationStatus.Succeed)
                        {
                            GF.Log($"[{Name}] YooAsset 资源清单加载成功 (版本: {requestVersionOp.PackageVersion})");
                        }
                        else
                        {
                            var error = updateManifestOp.Error;
                            GF.LogError($"[{Name}] YooAsset 资源清单加载失败: {error}");
                            throw new InvalidOperationException($"[{Name}] 资源清单加载失败: {error}");
                        }
                    }
                    else
                    {
                        var error = requestVersionOp.Error;
                        GF.LogError($"[{Name}] YooAsset 请求资源版本失败: {error}");
                        throw new InvalidOperationException($"[{Name}] 请求资源版本失败: {error}");
                    }
                }
            }
            else
            {
                var error = initializationOperation.Error;
                GF.LogError($"[{Name}] YooAsset 资源包初始化失败: {error}");
                throw new InvalidOperationException($"[{Name}] 资源包初始化失败: {error}");
            }
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 初始化失败: {ex.Message}");
            GF.LogError($"[{Name}] 请确保 YooAsset 已正确安装，并且资源包已初始化。");
            throw;
        }
    }

    /// <summary>
    /// 设置资源包（可选）
    /// 用于设置已初始化的 YooAsset 资源包
    /// </summary>
    public void SetResourcePackage(ResourcePackage package)
    {
        if (package == null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        _resourcePackage = package;
        GF.Log($"[{Name}] 资源包已设置: {package.PackageName}");
    }

    /// <summary>
    /// 确认当前的Package是否存在且已初始化
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void EnsurePackage()
    {
        if (_resourcePackage == null)
        {
            throw new InvalidOperationException(
                $"[{Name}] 资源包未找到。请确保已创建并初始化 YooAsset 资源包，或调用 SetResourcePackage 方法设置资源包。");
        }

        // 检查包是否已初始化
        if (_resourcePackage.InitializeStatus == EOperationStatus.None)
        {
            throw new InvalidOperationException($"[{Name}] 资源包尚未初始化。请先调用资源包的 InitializeAsync 方法进行初始化。");
        }
    }

    /// <summary>
    /// 异步加载资源（推荐使用）
    /// 使用引用计数管理，多次加载同一资源会增加引用计数
    /// </summary>
    /// <param name="fileName">资源文件名（不含扩展名，如 "prefab_name"）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default)
        where T : Object
    {
        if (string.IsNullOrEmpty(fileName))
        {
            GF.LogWarning($"[{Name}] 资源文件名不能为空");
            return null;
        }

        EnsurePackage();

        // 提取文件名（不含扩展名和路径）
        var location = ExtractFileName(fileName);

        // 检查缓存
        if (TryGetCachedResource<T>(location, out var cachedResource))
        {
            IncrementRefCount(cachedResource);
            return cachedResource;
        }

        var handle = await LoadFromPreload(location, cancellationToken);

        // 如果没有从预加载获取，则重新加载
        if (handle == null)
        {
            try
            {
                handle = _resourcePackage.LoadAssetAsync<T>(location);
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                handle?.Release();
                GF.LogWarning($"[{Name}] 资源加载已取消: {fileName}");
                return null;
            }
        }

        try
        {
            if (!handle.IsValid)
            {
                GF.LogWarning($"[{Name}] 资源加载失败: {fileName} (location: {location})");
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

            // 记录资源映射并增加引用计数
            RecordResourceMapping(location, handle, resource);
            IncrementRefCount(resource);

            // 记录使用历史
            RecordUsageHistory(location, "Load", _refCounts.GetValueOrDefault(resource, 0));

            return resource;
        }
        catch (Exception ex)
        {
            handle?.Release();
            GF.LogError($"[{Name}] 加载资源异常: {fileName}, 错误: {ex.Message}");
            return null;
        }
    }

    public UniTask<ResourceHandle<T>> LoadWithHandleAsync<T>(string fileName, bool captureStackTrace = false,
        CancellationToken cancellationToken = default) where T : Object
    {
        throw new NotImplementedException();
    }

    private async UniTask<AssetHandle> LoadFromPreload(string fileName,
        CancellationToken cancellationToken = default)
    {
        // 检查预加载的Handle
        AssetHandle handle = null;
        if (_preloadHandles.TryRemove(fileName, out var preloadHandle))
        {
            // 等待预加载完成（如果还未完成）
            if (!preloadHandle.IsDone)
            {
                try
                {
                    await UniTask.WaitUntil(() => preloadHandle.IsDone, cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // 取消时，将Handle放回预加载列表
                    _preloadHandles[fileName] = preloadHandle;
                    GF.LogWarning($"[{Name}] 资源加载已取消: {fileName}");
                    return null;
                }
            }

            if (preloadHandle.IsValid)
            {
                handle = preloadHandle;
                GF.Log($"[{Name}] 从预加载获取资源: {fileName})");
            }
            else
            {
                // 预加载的Handle无效，释放它
                preloadHandle.Release();
            }
        }

        return handle;
    }

    /// <summary>
    /// 卸载资源（减少引用计数，计数为0时真正释放）
    /// </summary>
    public void Unload(Object obj)
    {
        if (obj == null)
        {
            return;
        }

        try
        {
            if (!_objectToLocation.TryGetValue(obj, out var location))
            {
                GF.LogWarning($"[{Name}] 未找到资源对象: {obj.name}");
                return;
            }

            // 减少引用计数
            var refCountBefore = _refCounts.GetValueOrDefault(obj, 0);
            if (DecrementRefCount(obj))
            {
                // 引用计数为0，释放资源
                ReleaseResourceHandles(location);
                CleanupResourceMappings(location);
                GF.Log($"[{Name}] 资源已卸载: {location}");

                // 记录使用历史（卸载后引用计数为0）
                RecordUsageHistory(location, "Unload", 0);
            }
            else
            {
                // 记录使用历史（卸载后引用计数减少但未归零）
                RecordUsageHistory(location, "Unload", _refCounts.GetValueOrDefault(obj, 0));
            }
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 卸载资源异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 预加载资源（不增加引用计数，仅将资源加载到内存）
    /// </summary>
    public async UniTask<bool> PreloadAsync<T>(string fileName, CancellationToken cancellationToken = default)
        where T : Object
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        EnsurePackage();

        var location = ExtractFileName(fileName);

        // 检查是否已预加载
        if (_preloadHandles.ContainsKey(location))
        {
            return true;
        }

        // 检查资源是否已经通过LoadAsync加载（已在缓存中）
        if (_locationToResourceCache.ContainsKey(location))
        {
            // 资源已加载，不需要预加载
            return true;
        }

        try
        {
            var handle = _resourcePackage.LoadAssetAsync<T>(location);
            await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);

            if (handle.IsValid)
            {
                _preloadHandles[location] = handle;
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
            GF.LogError($"[{Name}] 预加载资源异常: {fileName}, 错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 批量加载资源
    /// </summary>
    public async UniTask<List<T>> LoadBatchAsync<T>(IEnumerable<string> fileNames,
        CancellationToken cancellationToken = default) where T : Object
    {
        var results = new List<T>();
        foreach (var fileName in fileNames)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var resource = await LoadAsync<T>(fileName, cancellationToken);
            results.Add(resource);
        }

        return results;
    }

    /// <summary>
    /// 加载子资源（如SpriteAtlas中的Sprite、AudioClip等）
    /// 注意：子资源加载后，Handle会被立即释放，因为子资源通常不需要引用计数管理
    /// </summary>
    public async UniTask<T> LoadSubAssetAsync<T>(string fileName, string assetName,
        CancellationToken cancellationToken = default) where T : Object
    {
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(assetName))
        {
            GF.LogWarning($"[{Name}] 子资源参数不能为空");
            return null;
        }

        EnsurePackage();

        SubAssetsHandle handle = null;
        try
        {
            var location = ExtractFileName(fileName);
            handle = _resourcePackage.LoadSubAssetsAsync<T>(location);
            await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);

            if (!handle.IsValid)
            {
                handle.Release();
                return null;
            }

            // 从所有子资源中查找指定名称的资源
            var targetAsset = handle.GetSubAssetObject<T>(assetName);
            if (targetAsset != null)
            {
                // 子资源加载后立即释放Handle（子资源通常不需要引用计数管理）
                // 注意：如果需要管理子资源的生命周期，可以在这里记录映射关系
                handle.Release();
                return targetAsset;
            }

            handle.Release();
            GF.LogWarning($"[{Name}] 未找到子资源: {fileName}/{assetName}");
            return null;
        }
        catch (OperationCanceledException)
        {
            handle?.Release();
            GF.LogWarning($"[{Name}] 加载子资源已取消: {fileName}/{assetName}");
            return null;
        }
        catch (Exception ex)
        {
            handle?.Release();
            GF.LogError($"[{Name}] 加载子资源异常: {fileName}/{assetName}, 错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 批量加载子资源
    /// 注意：子资源加载后，Handle会被立即释放，因为子资源通常不需要引用计数管理
    /// </summary>
    public async UniTask<List<T>> LoadAllSubAssetsAsync<T>(string fileName,
        CancellationToken cancellationToken = default) where T : Object
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return new List<T>();
        }

        EnsurePackage();

        AllAssetsHandle handle = null;
        try
        {
            var location = ExtractFileName(fileName);
            handle = _resourcePackage.LoadAllAssetsAsync<T>(location);
            await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellationToken);

            if (!handle.IsValid)
            {
                handle.Release();
                return new List<T>();
            }

            var subAssets = handle.AllAssetObjects;
            var results = new List<T>();
            foreach (var asset in subAssets)
            {
                if (asset is T t)
                {
                    results.Add(t);
                }
            }

            // 子资源加载后立即释放Handle（子资源通常不需要引用计数管理）
            handle.Release();
            return results;
        }
        catch (OperationCanceledException)
        {
            handle?.Release();
            GF.LogWarning($"[{Name}] 加载所有子资源已取消: {fileName}");
            return new List<T>();
        }
        catch (Exception ex)
        {
            handle?.Release();
            GF.LogError($"[{Name}] 加载所有子资源异常: {fileName}, 错误: {ex.Message}");
            return new List<T>();
        }
    }

    /// <summary>
    /// 检查资源是否存在
    /// </summary>
    public bool HasAsset(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        EnsurePackage();

        var location = ExtractFileName(fileName);
        var info = _resourcePackage.GetAssetInfo(location);
        return info != null;
    }

    /// <summary>
    /// 获取资源的引用计数
    /// </summary>
    public int GetRefCount(Object obj)
    {
        return obj == null ? 0 : _refCounts.GetValueOrDefault(obj, 0);
    }

    /// <summary>
    /// 卸载所有资源
    /// </summary>
    public void UnloadAll()
    {
        try
        {
            // 释放所有资源Handle
            int releasedCount = 0;
            foreach (var kvp in _handleToLocation)
            {
                if (kvp.Key != null && kvp.Key.IsValid)
                {
                    kvp.Key.Release();
                    releasedCount++;
                }
            }

            // 释放所有预加载Handle
            foreach (var kvp in _preloadHandles)
            {
                if (kvp.Value != null && kvp.Value.IsValid)
                {
                    kvp.Value.Release();
                }
            }

            // 清理所有映射
            _handleToLocation.Clear();
            _objectToLocation.Clear();
            _locationToResourceCache.Clear();
            _refCounts.Clear();
            _preloadHandles.Clear();

            // 清理未使用的资源
            Resources.UnloadUnusedAssets();
            GC.Collect();

            GF.Log($"[{Name}] 所有资源已卸载 (释放了 {releasedCount} 个 Handle)");
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 卸载所有资源异常: {ex.Message}");
        }
    }

    #region 场景加载

    // 已加载的场景 Handle 映射
    private readonly ConcurrentDictionary<string, SceneHandle> _sceneHandles = new();

    /// <summary>
    /// 异步加载场景
    /// </summary>
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

        var location = ExtractFileName(sceneName);

        // 检查场景是否已加载
        var existingScene = SceneManager.GetSceneByName(location);
        if (existingScene.IsValid() && existingScene.isLoaded)
        {
            GF.LogWarning($"[{Name}] 场景 {sceneName} 已加载，直接返回");
            return existingScene;
        }

        try
        {
            // 使用 YooAsset 加载场景
            var sceneHandle = _resourcePackage.LoadSceneAsync(location, loadSceneMode);
            
            // 等待加载完成
            while (!sceneHandle.IsDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    GF.LogWarning($"[{Name}] 场景 {sceneName} 加载被取消");
                    sceneHandle.Release();
                    throw new OperationCanceledException("场景加载被取消", cancellationToken);
                }

                await UniTask.Yield();
            }

            if (!sceneHandle.IsValid)
            {
                throw new JulyException($"[{Name}] 场景 {sceneName} 加载失败");
            }

            // 记录 Handle
            _sceneHandles[location] = sceneHandle;

            var scene = sceneHandle.SceneObject;
            GF.Log($"[{Name}] 场景 {sceneName} 加载完成");
            return scene;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 场景 {sceneName} 加载异常: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 异步卸载场景
    /// </summary>
    public async UniTask<bool> UnloadSceneAsync(string sceneName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            throw new ArgumentException("场景名称不能为空", nameof(sceneName));
        }

        var location = ExtractFileName(sceneName);

        // 检查是否有对应的 Handle
        if (!_sceneHandles.TryRemove(location, out var sceneHandle))
        {
            GF.LogWarning($"[{Name}] 场景 {sceneName} 未通过 YooAsset 加载，尝试使用 SceneManager 卸载");
            
            // 尝试使用 SceneManager 卸载
            var scene = SceneManager.GetSceneByName(location);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                GF.LogWarning($"[{Name}] 场景 {sceneName} 未加载，无需卸载");
                return false;
            }

            var asyncOp = SceneManager.UnloadSceneAsync(scene);
            if (asyncOp == null)
            {
                return false;
            }

            while (!asyncOp.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("场景卸载被取消", cancellationToken);
                }
                await UniTask.Yield();
            }

            GF.Log($"[{Name}] 场景 {sceneName} 卸载完成（SceneManager）");
            return true;
        }

        // 使用 YooAsset 的 Handle 卸载场景
        if (sceneHandle != null && sceneHandle.IsValid)
        {
            // YooAsset 的 SceneHandle.UnloadAsync 会卸载场景
            var unloadOp = sceneHandle.UnloadAsync();
            
            while (!unloadOp.IsDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("场景卸载被取消", cancellationToken);
                }
                await UniTask.Yield();
            }

            sceneHandle.Release();
            GF.Log($"[{Name}] 场景 {sceneName} 卸载完成");
            return true;
        }

        return false;
    }

    #endregion

    /// <summary>
    /// 关闭Provider
    /// </summary>
    protected override UniTask OnShutdownAsync()
    {
        // 释放所有场景 Handle
        foreach (var kvp in _sceneHandles)
        {
            if (kvp.Value != null && kvp.Value.IsValid)
            {
                kvp.Value.Release();
            }
        }
        _sceneHandles.Clear();

        UnloadAll();
        GF.Log($"[{Name}] YooAsset资源提供者已关闭");
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// 提取文件名（不含扩展名和路径）
    /// YooAsset使用文件名作为定位地址，通常不需要扩展名
    /// </summary>
    private string ExtractFileName(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // 移除路径，只保留文件名
        var fileName = System.IO.Path.GetFileName(input);

        // 移除扩展名（YooAsset通常使用文件名作为定位地址，不需要扩展名）
        return System.IO.Path.GetFileNameWithoutExtension(fileName);
    }

    /// <summary>
    /// 尝试从缓存获取资源
    /// </summary>
    private bool TryGetCachedResource<T>(string location, out T resource) where T : Object
    {
        resource = null;
        if (_locationToResourceCache.TryGetValue(location, out var cachedObj))
        {
            // 检查资源是否已被Unity销毁
            if (cachedObj == null)
            {
                // 资源已被销毁，清理无效缓存
                _locationToResourceCache.TryRemove(location, out _);
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

    /// <summary>
    /// 增加资源引用计数
    /// </summary>
    private void IncrementRefCount(Object obj)
    {
        _refCounts.AddOrUpdate(obj, 1, (key, oldValue) => oldValue + 1);
    }

    /// <summary>
    /// 减少资源引用计数
    /// </summary>
    /// <returns>如果引用计数为0返回true，否则返回false</returns>
    private bool DecrementRefCount(Object obj)
    {
        if (!_refCounts.TryGetValue(obj, out var count))
        {
            return false;
        }

        if (count <= 1)
        {
            _refCounts.TryRemove(obj, out _);
            return true;
        }

        _refCounts[obj] = count - 1;
        return false;
    }

    /// <summary>
    /// 释放资源的所有Handle
    /// </summary>
    private void ReleaseResourceHandles(string location)
    {
        var handlesToRelease = new List<AssetHandle>();
        foreach (var kvp in _handleToLocation)
        {
            if (kvp.Value == location)
            {
                handlesToRelease.Add(kvp.Key);
            }
        }

        foreach (var handle in handlesToRelease)
        {
            if (handle != null && handle.IsValid)
            {
                handle.Release();
                _handleToLocation.TryRemove(handle, out _);
            }
        }
    }

    /// <summary>
    /// 清理资源映射关系
    /// </summary>
    private void CleanupResourceMappings(string location)
    {
        // 清理缓存
        _locationToResourceCache.TryRemove(location, out _);

        // 清理object映射（通过location查找所有相关对象）
        var objectsToRemove = new List<Object>();
        foreach (var kvp in _objectToLocation)
        {
            if (kvp.Value == location)
            {
                objectsToRemove.Add(kvp.Key);
            }
        }

        foreach (var obj in objectsToRemove)
        {
            _objectToLocation.TryRemove(obj, out _);
        }
    }

    /// <summary>
    /// 记录资源映射关系
    /// </summary>
    private void RecordResourceMapping(string location, AssetHandle handle, Object resource)
    {
        // 记录Handle → Location映射（用于UnloadAll时释放所有Handle）
        _handleToLocation[handle] = location;

        // 记录Object → Location映射（用于Unload时通过对象查找location）
        _objectToLocation[resource] = location;

        // 更新缓存（直接存储Object，引用计数管理生命周期）
        _locationToResourceCache[location] = resource;
    }

    /// <summary>
    /// 获取资源服务器地址
    /// </summary>
    private string GetHostServerURL()
    {
        //string hostServerIP = "http://10.0.2.2"; //安卓模拟器地址
        string hostServerIP = "http://127.0.0.1";
        string appVersion = "v1.0";

#if UNITY_EDITOR
        if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
            return $"{hostServerIP}/CDN/Android/{appVersion}";
        else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
            return $"{hostServerIP}/CDN/IPhone/{appVersion}";
        else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WebGL)
            return $"{hostServerIP}/CDN/WebGL/{appVersion}";
        else
            return $"{hostServerIP}/CDN/PC/{appVersion}";
#else
            if (Application.platform == RuntimePlatform.Android)
                return $"{hostServerIP}/CDN/Android/{appVersion}";
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
                return $"{hostServerIP}/CDN/IPhone/{appVersion}";
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
                return $"{hostServerIP}/CDN/WebGL/{appVersion}";
            else
                return $"{hostServerIP}/CDN/PC/{appVersion}";
#endif
    }

    /// <summary>
    /// 获取资源使用统计信息
    /// </summary>
    public ResourceStatistics GetStatistics()
    {
        var stats = new ResourceStatistics
        {
            TotalResources = _refCounts.Count,
            TotalRefCount = _refCounts.Values.Sum(),
            ActiveHandles = _handleToLocation.Count,
            PreloadedResources = _preloadHandles.Count,
            CachedResources = _locationToResourceCache.Count
        };

        return stats;
    }

    /// <summary>
    /// 检测资源泄漏
    /// </summary>
    public List<ResourceLeakInfo> DetectLeaks(int leakThreshold = 10)
    {
        var leaks = new List<ResourceLeakInfo>();

        foreach (var kvp in _refCounts)
        {
            var obj = kvp.Key;
            var refCount = kvp.Value;

            // 检查引用计数是否超过阈值
            if (refCount > leakThreshold)
            {
                // 检查对象是否已被Unity销毁
                if (obj == null)
                {
                    continue;
                }

                // 获取资源位置
                string location = "Unknown";
                if (_objectToLocation.TryGetValue(obj, out var loc))
                {
                    location = loc;
                }

                leaks.Add(new ResourceLeakInfo
                {
                    Location = location,
                    ResourceName = obj.name,
                    RefCount = refCount,
                    ResourceType = obj.GetType().Name
                });
            }
        }

        return leaks;
    }

    public List<ActiveHandleInfo> GetActiveHandles()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取所有已加载资源的详细信息
    /// </summary>
    public List<ResourceInfo> GetAllResources()
    {
        var resources = new List<ResourceInfo>();

        // 统计每个location的Handle数量
        var locationHandleCount = new Dictionary<string, int>();
        foreach (var kvp in _handleToLocation)
        {
            var location = kvp.Value;
            locationHandleCount.TryGetValue(location, out var count);
            locationHandleCount[location] = count + 1;
        }

        // 遍历所有有引用计数的资源
        foreach (var kvp in _refCounts)
        {
            var obj = kvp.Key;
            var refCount = kvp.Value;

            // 检查对象是否已被Unity销毁
            if (obj == null)
            {
                continue;
            }

            // 获取资源位置
            string location = "Unknown";
            if (_objectToLocation.TryGetValue(obj, out var loc))
            {
                location = loc;
            }

            // 检查是否在缓存中
            bool isCached = _locationToResourceCache.ContainsKey(location);

            // 获取Handle数量
            int handleCount = locationHandleCount.GetValueOrDefault(location, 0);

            resources.Add(new ResourceInfo
            {
                Location = location,
                ResourceName = obj.name,
                RefCount = refCount,
                ResourceType = obj.GetType().Name,
                IsCached = isCached,
                HandleCount = handleCount
            });
        }

        return resources;
    }

    /// <summary>
    /// 获取资源使用历史
    /// </summary>
    public List<ResourceUsageHistory> GetResourceUsageHistory(string location = null, int maxCount = 100)
    {
        lock (_usageHistoryLock)
        {
            var query = _usageHistory.AsEnumerable();

            if (!string.IsNullOrEmpty(location))
            {
                query = query.Where(h => h.Location == location);
            }

            return query
                .OrderByDescending(h => h.Timestamp)
                .Take(maxCount)
                .ToList();
        }
    }

    /// <summary>
    /// 获取资源使用频率统计
    /// </summary>
    public List<ResourceUsageFrequency> GetResourceUsageFrequency(int topN = 20)
    {
        lock (_usageFrequencyLock)
        {
            return _usageFrequency.Values
                .OrderByDescending(f => f.TotalUsageCount)
                .ThenByDescending(f => f.LastUsedTime)
                .Take(topN)
                .ToList();
        }
    }

    /// <summary>
    /// 记录资源使用历史
    /// </summary>
    private void RecordUsageHistory(string location, string operation, int refCount)
    {
        lock (_usageHistoryLock)
        {
            _usageHistory.Add(new ResourceUsageHistory
            {
                Location = location,
                Operation = operation,
                Timestamp = DateTime.Now,
                RefCount = refCount
            });

            // 限制历史记录数量
            if (_usageHistory.Count > MaxUsageHistorySize)
            {
                _usageHistory.RemoveAt(0);
            }
        }

        // 更新使用频率统计
        lock (_usageFrequencyLock)
        {
            if (!_usageFrequency.TryGetValue(location, out var frequency))
            {
                frequency = new ResourceUsageFrequency
                {
                    Location = location
                };
                _usageFrequency[location] = frequency;
            }

            if (operation == "Load")
            {
                frequency.LoadCount++;
            }
            else if (operation == "Unload")
            {
                frequency.UnloadCount++;
            }

            frequency.LastUsedTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 远端资源地址查询服务类
    /// </summary>
    private class RemoteServices : IRemoteServices
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;

        public RemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            _defaultHostServer = defaultHostServer;
            _fallbackHostServer = fallbackHostServer;
        }

        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return $"{_defaultHostServer}/{fileName}";
        }

        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return $"{_fallbackHostServer}/{fileName}";
        }
    }
}
#endif
#if HYBRID_CLR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core;
using JulyCore.Data.HotUpdate;
using JulyCore.Provider.Base;
using JulyCore.Provider.HotUpdate;
using JulyCore.Provider.Resource;

#if !UNITY_EDITOR
using HybridCLR;
using UnityEngine;
#endif

/// <summary>
/// HybridCLR热更新提供者实现
/// 纯技术执行层：负责使用HybridCLR加载热更新程序集和AOT元数据
/// </summary>
internal class HybridCLRHotUpdateProvider : ProviderBase, IHotUpdateProvider
{
    private IResourceProvider _resourceProvider;
    private readonly List<Assembly> _loadedAssemblies = new List<Assembly>();
    private bool _isHotUpdateLoaded;

    /// <summary>
    /// 是否已加载热更新程序集
    /// </summary>
    public bool IsHotUpdateLoaded => _isHotUpdateLoaded;

    /// <summary>
    /// 已加载的热更新程序集列表
    /// </summary>
    public IReadOnlyList<Assembly> LoadedAssemblies => _loadedAssemblies;

    protected override UniTask OnInitAsync(CancellationToken cancellationToken)
    {
        try
        {
            _resourceProvider = _context.ProviderService.GetProvider<IResourceProvider>();
            if (_resourceProvider == null)
            {
                throw new JulyException($"[{Name}] 需要IResourceProvider，请先注册IResourceProvider");
            }

            GF.Log($"[{Name}] HybridCLR热更新提供者初始化完成");
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 初始化失败: {ex.Message}");
            throw;
        }

        return UniTask.CompletedTask;
    }

    protected override UniTask OnShutdownAsync(CancellationToken cancellationToken)
    {
        _loadedAssemblies.Clear();
        _isHotUpdateLoaded = false;
        _resourceProvider = null;

        GF.Log($"[{Name}] HybridCLR热更新提供者已关闭");
        return UniTask.CompletedTask;
    }

    public async UniTask<HotUpdateResult> LoadHotUpdateAsync(
        HotUpdateConfig config,
        Action<HotUpdateProgress> progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            return HotUpdateResult.Failure("配置不能为空");
        }

        if (_isHotUpdateLoaded)
        {
            GF.LogWarning($"[{Name}] 热更新已加载，跳过重复加载");
            return HotUpdateResult.Success(_loadedAssemblies);
        }

#if UNITY_EDITOR
        if (config.SkipInEditor)
        {
            GF.Log($"[{Name}] 编辑器模式下跳过热更新加载");
            ReportProgress(progressCallback, HotUpdateStage.Completed, 1f, "编辑器模式", "跳过热更新加载");
            return HotUpdateResult.Success(new List<Assembly>());
        }
#endif

        try
        {
            ReportProgress(progressCallback, HotUpdateStage.Preparing, 0f, "", "准备加载热更新...");

            var result = new HotUpdateResult { IsSuccess = true };

            // 1. 加载AOT补充元数据
            if (config.AOTMetaAssemblyNames != null && config.AOTMetaAssemblyNames.Count > 0)
            {
                ReportProgress(progressCallback, HotUpdateStage.LoadingAOTMetadata, 0.1f, "", "加载AOT元数据...");

                var aotResults = await LoadAOTMetadataAsync(
                    config.AOTMetaAssemblyNames,
                    config.AOTMetaDllPathPrefix,
                    cancellationToken);

                result.AOTMetaLoadResults = aotResults;

                // 检查是否有AOT元数据加载失败
                foreach (var kvp in aotResults)
                {
                    if (!kvp.Value)
                    {
                        GF.LogWarning($"[{Name}] AOT元数据 {kvp.Key} 加载失败，可能影响泛型方法调用");
                    }
                }
            }

            // 2. 加载热更新程序集
            if (config.HotUpdateAssemblyNames != null && config.HotUpdateAssemblyNames.Count > 0)
            {
                ReportProgress(progressCallback, HotUpdateStage.LoadingHotUpdateAssemblies, 0.4f, "", "加载热更新程序集...");

                var assemblies = await LoadAssembliesAsync(
                    config.HotUpdateAssemblyNames,
                    config.HotUpdateDllPathPrefix,
                    cancellationToken);

                if (assemblies == null || assemblies.Count == 0)
                {
                    ReportProgress(progressCallback, HotUpdateStage.Failed, 1f, "", "热更新程序集加载失败");
                    return HotUpdateResult.Failure("热更新程序集加载失败");
                }

                result.LoadedAssemblies = assemblies;
            }

            // 3. 执行入口方法
            if (!string.IsNullOrEmpty(config.EntryClassName) && !string.IsNullOrEmpty(config.EntryMethodName))
            {
                ReportProgress(progressCallback, HotUpdateStage.ExecutingEntry, 0.9f, config.EntryClassName,
                    "执行热更新入口...");

                bool entrySuccess = ExecuteEntry(config.EntryClassName, config.EntryMethodName);
                if (!entrySuccess)
                {
                    GF.LogWarning($"[{Name}] 入口方法执行失败，但程序集已加载");
                }
            }

            _isHotUpdateLoaded = true;
            ReportProgress(progressCallback, HotUpdateStage.Completed, 1f, "", "热更新加载完成");

            GF.Log($"[{Name}] 热更新加载完成，共加载 {result.LoadedAssemblies.Count} 个程序集");
            return result;
        }
        catch (OperationCanceledException)
        {
            ReportProgress(progressCallback, HotUpdateStage.Failed, 1f, "", "热更新加载已取消");
            return HotUpdateResult.Failure("加载已取消");
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 热更新加载失败: {ex.Message}");
            GF.LogException(ex);
            ReportProgress(progressCallback, HotUpdateStage.Failed, 1f, "", $"加载失败: {ex.Message}");
            return HotUpdateResult.Failure(ex.Message);
        }
    }

    public async UniTask<Dictionary<string, bool>> LoadAOTMetadataAsync(
        List<string> aotAssemblyNames,
        string pathPrefix,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, bool>();

        if (aotAssemblyNames == null || aotAssemblyNames.Count == 0)
        {
            return results;
        }

#if UNITY_EDITOR
        GF.Log($"[{Name}] 编辑器模式下跳过AOT元数据加载");
        foreach (var name in aotAssemblyNames)
        {
            results[name] = true;
        }

        return results;
#else
            GF.Log($"[{Name}] 开始加载 {aotAssemblyNames.Count} 个AOT元数据程序集");

            foreach (var assemblyName in aotAssemblyNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var dllPath = $"{pathPrefix}{assemblyName}.dll.bytes";
                    var textAsset = await _resourceProvider.LoadAsync<TextAsset>(dllPath, cancellationToken);

                    if (textAsset == null || textAsset.bytes == null || textAsset.bytes.Length == 0)
                    {
                        GF.LogWarning($"[{Name}] AOT元数据加载失败，资源不存在: {dllPath}");
                        results[assemblyName] = false;
                        continue;
                    }

                    // 使用HybridCLR加载AOT元数据
                    var loadResult =
 RuntimeApi.LoadMetadataForAOTAssembly(textAsset.bytes, HomologousImageMode.SuperSet);

                    bool success = loadResult == LoadImageErrorCode.OK;
                    results[assemblyName] = success;

                    if (success)
                    {
                        GF.Log($"[{Name}] AOT元数据加载成功: {assemblyName}");
                    }
                    else
                    {
                        GF.LogWarning($"[{Name}] AOT元数据加载失败: {assemblyName}, 错误码: {loadResult}");
                    }
                }
                catch (Exception ex)
                {
                    GF.LogError($"[{Name}] AOT元数据加载异常: {assemblyName}, 错误: {ex.Message}");
                    results[assemblyName] = false;
                }
            }

            return results;
#endif
    }

    public async UniTask<List<Assembly>> LoadAssembliesAsync(
        List<string> assemblyNames,
        string pathPrefix,
        CancellationToken cancellationToken = default)
    {
        var assemblies = new List<Assembly>();

        if (assemblyNames == null || assemblyNames.Count == 0)
        {
            return assemblies;
        }

#if UNITY_EDITOR
        // 编辑器模式下直接从AppDomain获取程序集
        GF.Log($"[{Name}] 编辑器模式下从AppDomain获取程序集");
        foreach (var assemblyName in assemblyNames)
        {
            var assembly = GetAssemblyFromAppDomain(assemblyName);
            if (assembly != null)
            {
                assemblies.Add(assembly);
                GF.Log($"[{Name}] 从AppDomain获取程序集: {assemblyName}");
            }
            else
            {
                GF.LogWarning($"[{Name}] 未在AppDomain中找到程序集: {assemblyName}");
            }
        }

        return assemblies;
#else
            GF.Log($"[{Name}] 开始加载 {assemblyNames.Count} 个热更新程序集");

            foreach (var assemblyName in assemblyNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var dllPath = $"{pathPrefix}{assemblyName}.dll.bytes";
                    var textAsset = await _resourceProvider.LoadAsync<TextAsset>(dllPath, cancellationToken);

                    if (textAsset == null || textAsset.bytes == null || textAsset.bytes.Length == 0)
                    {
                        GF.LogError($"[{Name}] 热更新程序集加载失败，资源不存在: {dllPath}");
                        continue;
                    }

                    // 加载程序集
                    var assembly = Assembly.Load(textAsset.bytes);
                    if (assembly != null)
                    {
                        assemblies.Add(assembly);
                        _loadedAssemblies.Add(assembly);
                        GF.Log($"[{Name}] 热更新程序集加载成功: {assemblyName} ({textAsset.bytes.Length} bytes)");
                    }
                }
                catch (Exception ex)
                {
                    GF.LogError($"[{Name}] 热更新程序集加载异常: {assemblyName}, 错误: {ex.Message}");
                    GF.LogException(ex);
                }
            }

            return assemblies;
#endif
    }

    public bool ExecuteEntry(string entryClassName, string entryMethodName, object[] parameters = null)
    {
        if (string.IsNullOrEmpty(entryClassName) || string.IsNullOrEmpty(entryMethodName))
        {
            GF.LogWarning($"[{Name}] 入口类名或方法名为空");
            return false;
        }

        try
        {
            var entryType = GetHotUpdateType(entryClassName);
            if (entryType == null)
            {
                GF.LogError($"[{Name}] 未找到入口类: {entryClassName}");
                return false;
            }

            // 查找静态方法
            var method = entryType.GetMethod(entryMethodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null)
            {
                GF.LogError($"[{Name}] 未找到入口方法: {entryClassName}.{entryMethodName}");
                return false;
            }

            // 执行入口方法
            method.Invoke(null, parameters);
            GF.Log($"[{Name}] 入口方法执行成功: {entryClassName}.{entryMethodName}");
            return true;
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 入口方法执行失败: {ex.Message}");
            GF.LogException(ex);
            return false;
        }
    }

    public Type GetHotUpdateType(string typeFullName)
    {
        if (string.IsNullOrEmpty(typeFullName))
        {
            return null;
        }

#if UNITY_EDITOR
        // 编辑器模式下从所有程序集查找
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeFullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
#else
            // 运行时从已加载的热更新程序集查找
            foreach (var assembly in _loadedAssemblies)
            {
                var type = assembly.GetType(typeFullName);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
#endif
    }

    public List<Type> GetHotUpdateTypes(Func<Type, bool> predicate = null)
    {
        var types = new List<Type>();

#if UNITY_EDITOR
        // 编辑器模式下从特定程序集查找
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var assemblyTypes = assembly.GetTypes();
                foreach (var type in assemblyTypes)
                {
                    if (predicate == null || predicate(type))
                    {
                        types.Add(type);
                    }
                }
            }
            catch
            {
                // 某些程序集可能无法获取类型，忽略
            }
        }
#else
            // 运行时从已加载的热更新程序集查找
            foreach (var assembly in _loadedAssemblies)
            {
                try
                {
                    var assemblyTypes = assembly.GetTypes();
                    foreach (var type in assemblyTypes)
                    {
                        if (predicate == null || predicate(type))
                        {
                            types.Add(type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    GF.LogWarning($"[{Name}] 获取程序集类型失败: {assembly.FullName}, 错误: {ex.Message}");
                }
            }
#endif

        return types;
    }

    #region 私有辅助方法

    private void ReportProgress(Action<HotUpdateProgress> callback, HotUpdateStage stage, float progress,
        string currentItem, string description)
    {
        callback?.Invoke(new HotUpdateProgress
        {
            Stage = stage,
            Progress = progress,
            CurrentItem = currentItem,
            Description = description
        });
    }

#if UNITY_EDITOR
    private Assembly GetAssemblyFromAppDomain(string assemblyName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name == assemblyName)
            {
                return assembly;
            }
        }

        return null;
    }
#endif

    #endregion
}
#endif
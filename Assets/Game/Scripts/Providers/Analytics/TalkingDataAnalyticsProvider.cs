using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core;
using JulyCore.Provider.Analytics;
using JulyCore.Provider.Base;

/// <summary>
/// 数数（TalkingData）数据统计提供者实现
/// 纯技术执行层：负责数据统计事件的收集和上报
/// 不包含任何业务语义，不维护业务状态
/// 直接调用数数SDK进行上报
/// </summary>
public class TalkingDataAnalyticsProvider : ProviderBase, IAnalyticsProvider
{
    /// <summary>
    /// App ID（数数SDK初始化时使用）
    /// </summary>
    public string AppId { get; set; }

    /// <summary>
    /// 渠道ID（数数SDK初始化时使用）
    /// </summary>
    public string ChannelId { get; set; }

    /// <summary>
    /// 当前用户ID
    /// </summary>
    private string _userId;

    /// <summary>
    /// 用户属性
    /// </summary>
    private Dictionary<string, object> _userProperties = new Dictionary<string, object>();

    protected override LogChannel LogChannel => LogChannel.Analytics;

    protected override UniTask OnInitAsync()
    {
        if (string.IsNullOrEmpty(AppId))
        {
            GF.LogWarning($"[{Name}] AppId未设置，数据统计功能可能无法正常工作");
        }

        // TODO: 调用数数SDK初始化
        // TDGA.Init(AppId, ChannelId);
        // 或使用其他初始化方法，根据实际SDK文档

        GF.Log($"[{Name}] 数数统计Provider初始化完成，AppId: {AppId}, ChannelId: {ChannelId}");
        return base.OnInitAsync();
    }

    /// <summary>
    /// 上报单个事件
    /// </summary>
    public async UniTask<bool> TrackEventAsync(AnalyticsEvent evt, CancellationToken cancellationToken = default)
    {
        if (evt == null)
        {
            GF.LogWarning($"[{Name}] 事件对象为空，跳过上报");
            return false;
        }

        if (string.IsNullOrEmpty(evt.EventName))
        {
            GF.LogWarning($"[{Name}] 事件名称为空，跳过上报");
            return false;
        }

        try
        {
            // 合并用户属性到事件参数
            var finalParameters = evt.Parameters ?? new Dictionary<string, object>();
            if (_userProperties.Count > 0)
            {
                foreach (var kvp in _userProperties)
                {
                    if (!finalParameters.ContainsKey(kvp.Key))
                    {
                        finalParameters[kvp.Key] = kvp.Value;
                    }
                }
            }

            // 设置用户ID（如果事件中没有）
            if (string.IsNullOrEmpty(evt.UserId))
            {
                evt.UserId = _userId;
            }

            // TODO: 调用数数SDK上报事件
            // TDGA.OnEvent(evt.EventName, finalParameters);
            // 或使用其他方法，根据实际SDK文档
            // 注意：数数SDK可能是同步调用，这里使用UniTask.FromResult包装

            GF.Log($"[{Name}] 上报事件: {evt.EventName}, 参数数量: {finalParameters.Count}");
            return await UniTask.FromResult(true);
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 上报事件失败: {ex.Message}");
            GF.LogException(ex);
            return false;
        }
    }

    /// <summary>
    /// 批量上报事件
    /// </summary>
    public async UniTask<bool> TrackEventsAsync(List<AnalyticsEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (events == null || events.Count == 0)
        {
            return true;
        }

        try
        {
            // 遍历所有事件并上报
            foreach (var evt in events)
            {
                // 合并用户属性
                var finalParameters = evt.Parameters ?? new Dictionary<string, object>();
                if (_userProperties.Count > 0)
                {
                    foreach (var kvp in _userProperties)
                    {
                        if (!finalParameters.ContainsKey(kvp.Key))
                        {
                            finalParameters[kvp.Key] = kvp.Value;
                        }
                    }
                }

                // TODO: 调用数数SDK上报事件
                // TDGA.OnEvent(evt.EventName, finalParameters);
                // 或使用其他方法，根据实际SDK文档
            }

            GF.Log($"[{Name}] 批量上报事件: {events.Count} 个");
            return await UniTask.FromResult(true);
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 批量上报事件失败: {ex.Message}");
            GF.LogException(ex);
            return false;
        }
    }

    /// <summary>
    /// 设置用户ID
    /// </summary>
    public void SetUserId(string userId)
    {
        _userId = userId;

        // TODO: 调用数数SDK设置账号
        // TDGA.SetAccount(userId);
        // 或使用其他方法，根据实际SDK文档
        // 可能还需要设置账号类型：TDGA.SetAccountType(AccountType.REGISTERED);

        GF.Log($"[{Name}] 设置用户ID: {userId}");
    }

    /// <summary>
    /// 设置用户属性
    /// </summary>
    public void SetUserProperties(Dictionary<string, object> properties)
    {
        if (properties == null)
        {
            return;
        }

        foreach (var kvp in properties)
        {
            _userProperties[kvp.Key] = kvp.Value;
        }

        // TODO: 调用数数SDK设置用户属性
        // TDGA.SetAccountName(userName); // 如果属性中包含用户名
        // TDGA.SetAccountType(accountType); // 如果属性中包含账号类型
        // 或其他用户属性设置方法，根据实际SDK文档

        GF.Log($"[{Name}] 设置用户属性，数量: {properties.Count}");
    }

    /// <summary>
    /// 刷新上报（立即上报缓存的事件）
    /// 注意：数数SDK通常是实时上报，此方法主要用于确保数据已发送
    /// </summary>
    public async UniTask<bool> FlushAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: 调用数数SDK刷新上报
            // TDGA.Flush();
            // 或使用其他刷新方法，根据实际SDK文档
            // 如果SDK没有刷新方法，可以返回true（因为SDK通常是实时上报）

            GF.Log($"[{Name}] 刷新上报完成");
            return await UniTask.FromResult(true);
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 刷新上报失败: {ex.Message}");
            GF.LogException(ex);
            return false;
        }
    }

    /// <summary>
    /// 获取待上报事件数量
    /// 注意：数数SDK通常是实时上报，此方法可能始终返回0
    /// </summary>
    public int GetPendingEventCount()
    {
        // TODO: 如果数数SDK有获取待上报事件数量的方法，可以调用
        // 否则返回0（因为SDK通常是实时上报）
        return 0;
    }

    protected override UniTask OnShutdownAsync()
    {
        // TODO: 关闭时可能需要调用数数SDK的清理方法
        // TDGA.OnKill();
        // 或使用其他清理方法，根据实际SDK文档

        GF.Log($"[{Name}] 数数统计Provider已关闭");
        return base.OnShutdownAsync();
    }
}
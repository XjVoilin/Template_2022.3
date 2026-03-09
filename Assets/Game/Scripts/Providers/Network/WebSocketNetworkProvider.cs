using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core;
using JulyCore.Data.Network;
using JulyCore.Provider.Base;
using JulyCore.Provider.Network;
using UnityEngine.Networking;

#if UNITY_WEB_SOCKET
using UnityWebSocket;

/// <summary>
/// WebSocket连接包装器
/// </summary>
internal class WebSocketConnection
{
    public string Name { get; set; }
    public WebSocketConfig Config { get; set; }
    public IWebSocket Socket { get; set; }
    public NetworkConnectionState State { get; set; } = NetworkConnectionState.Disconnected;
    public NetworkConnectionInfo Info { get; set; } = new NetworkConnectionInfo();
    public int ReconnectAttempts { get; set; }
    public float CurrentReconnectInterval { get; set; }
    public bool IsReconnecting { get; set; }
    public CancellationTokenSource ReconnectCts { get; set; }
    public Queue<NetworkMessage> PendingMessages { get; set; } = new Queue<NetworkMessage>();
    public DateTime? LastPingTime { get; set; }
    public DateTime? LastPongTime { get; set; }
}

/// <summary>
/// 基于UnityWebSocket的网络提供者实现
/// 提供WebSocket和HTTP网络通信能力
/// </summary>
public class WebSocketNetworkProvider : ProviderBase, INetworkProvider
{
    private const string DefaultConnectionName = "default";

    private readonly Dictionary<string, WebSocketConnection> _connections =
        new Dictionary<string, WebSocketConnection>();

    private readonly object _lock = new object();
    private readonly NetworkStatistics _statistics = new NetworkStatistics();

    private HttpConfig _httpConfig = new HttpConfig();

    #region Events

    public event Action<string> OnOpen;
    public event Action<string, int, string> OnClose;
    public event Action<string, string> OnTextMessage;
    public event Action<string, byte[]> OnBinaryMessage;
    public event Action<string, string> OnError;

    #endregion

    #region Properties

    public NetworkConnectionState ConnectionState => GetConnectionState(DefaultConnectionName);

    public bool IsConnected => IsConnectionConnected(DefaultConnectionName);

    #endregion

    #region WebSocket 连接管理

    public NetworkConnectionState GetConnectionState(string connectionName)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(connectionName ?? DefaultConnectionName, out var conn))
            {
                return conn.State;
            }

            return NetworkConnectionState.Disconnected;
        }
    }

    public bool IsConnectionConnected(string connectionName)
    {
        return GetConnectionState(connectionName) == NetworkConnectionState.Connected;
    }

    public async UniTask<bool> ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        var config = new WebSocketConfig
        {
            Name = DefaultConnectionName,
            ServerUrl = url
        };
        return await ConnectAsync(config, cancellationToken);
    }

    public async UniTask<bool> ConnectAsync(WebSocketConfig config, CancellationToken cancellationToken = default)
    {
        if (config == null || string.IsNullOrEmpty(config.ServerUrl))
        {
            GF.LogError($"[{Name}] WebSocket配置无效");
            return false;
        }

        var connectionName = config.Name ?? DefaultConnectionName;

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionName, out var existingConn))
            {
                if (existingConn.State == NetworkConnectionState.Connected ||
                    existingConn.State == NetworkConnectionState.Connecting)
                {
                    GF.LogWarning($"[{Name}] 连接 {connectionName} 已存在且处于 {existingConn.State} 状态");
                    return existingConn.State == NetworkConnectionState.Connected;
                }
            }
        }

        var connection = new WebSocketConnection
        {
            Name = connectionName,
            Config = config,
            State = NetworkConnectionState.Connecting,
            Info = new NetworkConnectionInfo
            {
                ConnectionId = Guid.NewGuid().ToString("N"),
                ServerUrl = config.ServerUrl,
                State = NetworkConnectionState.Connecting
            }
        };

        lock (_lock)
        {
            _connections[connectionName] = connection;
        }

        try
        {
            // 创建WebSocket实例
            var socket = new WebSocket(config.ServerUrl);
            connection.Socket = socket;

            // 绑定事件
            socket.OnOpen += () => HandleWebSocketOpen(connectionName);
            socket.OnClose += (code, reason) => HandleWebSocketClose(connectionName, (int)code, reason);
            socket.OnMessage += (data) => HandleWebSocketMessage(connectionName, data);
            socket.OnError += (error) => HandleWebSocketError(connectionName, error);

            // 连接
            socket.ConnectAsync();

            // 等待连接完成或超时
            var timeoutSeconds = config.ConnectTimeoutSeconds > 0 ? config.ConnectTimeoutSeconds : 10f;
            var startTime = UnityEngine.Time.realtimeSinceStartup;

            while (connection.State == NetworkConnectionState.Connecting)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await DisconnectInternalAsync(connectionName);
                    return false;
                }

                if (Time.realtimeSinceStartup - startTime > timeoutSeconds)
                {
                    GF.LogError($"[{Name}] 连接 {connectionName} 超时");
                    await DisconnectInternalAsync(connectionName);
                    return false;
                }

                await UniTask.Yield();
            }

            if (connection.State == NetworkConnectionState.Connected)
            {
                _statistics.TotalConnectCount++;
                GF.Log($"[{Name}] WebSocket连接成功: {connectionName}");

                // 发送队列中的消息
                FlushPendingMessages(connectionName);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] WebSocket连接异常: {ex.Message}");
            connection.State = NetworkConnectionState.Disconnected;
            return false;
        }
    }

    public async UniTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectAsync(DefaultConnectionName, cancellationToken);
    }

    public async UniTask DisconnectAsync(string connectionName, CancellationToken cancellationToken = default)
    {
        await DisconnectInternalAsync(connectionName ?? DefaultConnectionName, DisconnectReason.Manual);
    }

    public async UniTask DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        List<string> connectionNames;
        lock (_lock)
        {
            connectionNames = new List<string>(_connections.Keys);
        }

        foreach (var name in connectionNames)
        {
            await DisconnectInternalAsync(name, DisconnectReason.Manual);
        }
    }

    private async UniTask DisconnectInternalAsync(string connectionName, DisconnectReason reason =
        DisconnectReason.Normal)
    {
        WebSocketConnection connection;
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionName, out connection))
            {
                return;
            }
        }

        // 取消重连
        connection.ReconnectCts?.Cancel();
        connection.ReconnectCts?.Dispose();
        connection.ReconnectCts = null;
        connection.IsReconnecting = false;

        if (connection.Socket != null)
        {
            try
            {
                connection.State = NetworkConnectionState.Closing;
                connection.Socket.CloseAsync();

                // 等待关闭完成
                var startTime = Time.realtimeSinceStartup;
                while (connection.State == NetworkConnectionState.Closing &&
                       Time.realtimeSinceStartup - startTime < 3f)
                {
                    await UniTask.Yield();
                }
            }
            catch (Exception ex)
            {
                GF.LogWarning($"[{Name}] 关闭WebSocket时异常: {ex.Message}");
            }
        }

        connection.State = NetworkConnectionState.Disconnected;
        connection.Info.State = NetworkConnectionState.Disconnected;
        connection.Info.DisconnectedTime = DateTime.UtcNow;
        connection.Info.DisconnectReason = reason;

        GF.Log($"[{Name}] WebSocket已断开: {connectionName}, 原因: {reason}");
    }

    public NetworkConnectionInfo GetConnectionInfo(string connectionName = null)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(connectionName ?? DefaultConnectionName, out var conn))
            {
                return conn.Info;
            }

            return null;
        }
    }

    public IReadOnlyList<string> GetActiveConnectionNames()
    {
        lock (_lock)
        {
            var result = new List<string>();
            foreach (var kvp in _connections)
            {
                if (kvp.Value.State == NetworkConnectionState.Connected)
                {
                    result.Add(kvp.Key);
                }
            }

            return result;
        }
    }

    #endregion

    #region WebSocket 事件处理

    private void HandleWebSocketOpen(string connectionName)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(connectionName, out var conn))
            {
                conn.State = NetworkConnectionState.Connected;
                conn.Info.State = NetworkConnectionState.Connected;
                conn.Info.ConnectedTime = DateTime.UtcNow;
                conn.Info.LastActivityTime = DateTime.UtcNow;
                conn.ReconnectAttempts = 0;
                conn.CurrentReconnectInterval = conn.Config.ReconnectIntervalSeconds;
            }
        }

        OnOpen?.Invoke(connectionName);
    }

    private void HandleWebSocketClose(string connectionName, int code, string reason)
    {
        WebSocketConnection connection;
        bool shouldReconnect = false;

        lock (_lock)
        {
            if (_connections.TryGetValue(connectionName, out connection))
            {
                var previousState = connection.State;
                connection.State = NetworkConnectionState.Disconnected;
                connection.Info.State = NetworkConnectionState.Disconnected;
                connection.Info.DisconnectedTime = DateTime.UtcNow;

                // 判断是否需要自动重连
                if (connection.Config.AutoReconnect &&
                    previousState != NetworkConnectionState.Closing &&
                    !connection.IsReconnecting)
                {
                    if (connection.Config.MaxReconnectAttempts == 0 ||
                        connection.ReconnectAttempts < connection.Config.MaxReconnectAttempts)
                    {
                        shouldReconnect = true;
                    }
                }
            }
        }

        OnClose?.Invoke(connectionName, code, reason);

        if (shouldReconnect)
        {
            StartReconnect(connectionName);
        }
    }

    private void HandleWebSocketMessage(string connectionName, MessageEventArgs args)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(connectionName, out var conn))
            {
                conn.Info.LastActivityTime = DateTime.UtcNow;
                conn.Info.ReceivedMessageCount++;

                if (args.IsBinary)
                {
                    conn.Info.ReceivedBytes += args.RawData?.Length ?? 0;
                    _statistics.TotalReceivedBytes += args.RawData?.Length ?? 0;
                }
                else
                {
                    var textBytes = Encoding.UTF8.GetByteCount(args.Data ?? string.Empty);
                    conn.Info.ReceivedBytes += textBytes;
                    _statistics.TotalReceivedBytes += textBytes;
                }

                _statistics.TotalReceivedMessages++;
            }
        }

        if (args.IsBinary)
        {
            OnBinaryMessage?.Invoke(connectionName, args.RawData);
        }
        else
        {
            OnTextMessage?.Invoke(connectionName, args.Data);
        }
    }

    private void HandleWebSocketError(string connectionName, string error)
    {
        GF.LogError($"[{Name}] WebSocket错误 [{connectionName}]: {error}");
        OnError?.Invoke(connectionName, error);
    }

    #endregion

    #region 自动重连

    private void StartReconnect(string connectionName)
    {
        WebSocketConnection connection;
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionName, out connection))
            {
                return;
            }

            if (connection.IsReconnecting)
            {
                return;
            }

            connection.IsReconnecting = true;
            connection.State = NetworkConnectionState.Reconnecting;
            connection.Info.State = NetworkConnectionState.Reconnecting;
            connection.ReconnectCts = new CancellationTokenSource();
        }

        ReconnectLoopAsync(connectionName, connection.ReconnectCts.Token).Forget();
    }

    private async UniTaskVoid ReconnectLoopAsync(string connectionName, CancellationToken cancellationToken)
    {
        WebSocketConnection connection;
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionName, out connection))
            {
                return;
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            connection.ReconnectAttempts++;
            _statistics.TotalReconnectCount++;
            connection.Info.ReconnectCount++;

            GF.Log($"[{Name}] 正在重连 {connectionName}... (第 {connection.ReconnectAttempts} 次)");

            // 尝试重连
            var socket = new WebSocket(connection.Config.ServerUrl);
            connection.Socket = socket;

            var connectTcs = new UniTaskCompletionSource<bool>();

            socket.OnOpen += () => { connectTcs.TrySetResult(true); };
            socket.OnClose += (code, reason) => { connectTcs.TrySetResult(false); };
            socket.OnMessage += (data) => HandleWebSocketMessage(connectionName, data);
            socket.OnError += (error) =>
            {
                HandleWebSocketError(connectionName, error);
                connectTcs.TrySetResult(false);
            };

            socket.ConnectAsync();

            // 等待连接结果
            var timeoutTask = UniTask.Delay(
                TimeSpan.FromSeconds(connection.Config.ConnectTimeoutSeconds),
                cancellationToken: cancellationToken);

            var (hasTimeout, result) = await UniTask.WhenAny(
                connectTcs.Task,
                timeoutTask.SuppressCancellationThrow().ContinueWith(_ => false)
            );

            if (result)
            {
                // 重连成功
                lock (_lock)
                {
                    connection.State = NetworkConnectionState.Connected;
                    connection.Info.State = NetworkConnectionState.Connected;
                    connection.Info.ConnectedTime = DateTime.UtcNow;
                    connection.Info.LastActivityTime = DateTime.UtcNow;
                    connection.IsReconnecting = false;
                }

                OnOpen?.Invoke(connectionName);
                GF.Log($"[{Name}] 重连成功 {connectionName}");

                // 发送队列中的消息
                FlushPendingMessages(connectionName);
                return;
            }

            // 检查是否达到最大重连次数
            if (connection.Config.MaxReconnectAttempts > 0 &&
                connection.ReconnectAttempts >= connection.Config.MaxReconnectAttempts)
            {
                GF.LogError($"[{Name}] 重连失败 {connectionName}: 达到最大重连次数");

                lock (_lock)
                {
                    connection.State = NetworkConnectionState.Disconnected;
                    connection.Info.State = NetworkConnectionState.Disconnected;
                    connection.Info.DisconnectReason = DisconnectReason.ReconnectFailed;
                    connection.IsReconnecting = false;
                }

                OnClose?.Invoke(connectionName, 0, "重连失败");
                return;
            }

            // 计算下次重连间隔（指数退避）
            connection.CurrentReconnectInterval = Math.Min(
                connection.CurrentReconnectInterval * connection.Config.ReconnectBackoffMultiplier,
                connection.Config.MaxReconnectIntervalSeconds
            );

            GF.Log($"[{Name}] {connection.CurrentReconnectInterval} 秒后重试...");
            await UniTask.Delay(TimeSpan.FromSeconds(connection.CurrentReconnectInterval),
                cancellationToken: cancellationToken);
        }
    }

    #endregion

    #region WebSocket 消息发送

    public bool SendText(string text)
    {
        return SendText(DefaultConnectionName, text);
    }

    public bool SendText(string connectionName, string text)
    {
        var message = NetworkMessage.CreateText(text);
        return Send(connectionName, message);
    }

    public bool SendBinary(byte[] data)
    {
        return SendBinary(DefaultConnectionName, data);
    }

    public bool SendBinary(string connectionName, byte[] data)
    {
        var message = NetworkMessage.CreateBinary(data);
        return Send(connectionName, message);
    }

    public bool Send(NetworkMessage message)
    {
        return Send(DefaultConnectionName, message);
    }

    public bool Send(string connectionName, NetworkMessage message)
    {
        if (message == null)
        {
            GF.LogWarning($"[{Name}] 发送消息为空");
            return false;
        }

        WebSocketConnection connection;
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionName ?? DefaultConnectionName, out connection))
            {
                GF.LogWarning($"[{Name}] 连接 {connectionName} 不存在");
                return false;
            }
        }

        if (connection.State != NetworkConnectionState.Connected)
        {
            // 如果启用了消息队列，将消息加入队列
            if (connection.Config.EnableMessageQueue)
            {
                lock (_lock)
                {
                    if (connection.PendingMessages.Count < connection.Config.MessageQueueCapacity)
                    {
                        connection.PendingMessages.Enqueue(message);
                        GF.Log($"[{Name}] 消息已加入队列，当前队列大小: {connection.PendingMessages.Count}");
                        return true;
                    }
                    else
                    {
                        GF.LogWarning($"[{Name}] 消息队列已满，丢弃消息");
                        return false;
                    }
                }
            }

            return false;
        }

        return SendMessageInternal(connection, message);
    }

    public UniTask<bool> SendAsync(NetworkMessage message, CancellationToken cancellationToken = default)
    {
        return SendAsync(DefaultConnectionName, message, cancellationToken);
    }

    public async UniTask<bool> SendAsync(string connectionName, NetworkMessage message,
        CancellationToken cancellationToken
            = default)
    {
        // 当前实现中Send是同步的，直接返回结果
        var result = Send(connectionName, message);
        await UniTask.Yield();
        return result;
    }

    private bool SendMessageInternal(WebSocketConnection connection, NetworkMessage message)
    {
        try
        {
            if (message.Type == NetworkMessageType.Binary && message.BinaryData != null)
            {
                connection.Socket.SendAsync(message.BinaryData);
                connection.Info.SentBytes += message.BinaryData.Length;
                _statistics.TotalSentBytes += message.BinaryData.Length;
            }
            else if (!string.IsNullOrEmpty(message.TextData))
            {
                connection.Socket.SendAsync(message.TextData);
                var textBytes = Encoding.UTF8.GetByteCount(message.TextData);
                connection.Info.SentBytes += textBytes;
                _statistics.TotalSentBytes += textBytes;
            }
            else
            {
                return false;
            }

            connection.Info.SentMessageCount++;
            connection.Info.LastActivityTime = DateTime.UtcNow;
            _statistics.TotalSentMessages++;

            return true;
        }
        catch (Exception ex)
        {
            GF.LogError($"[{Name}] 发送消息失败: {ex.Message}");
            return false;
        }
    }

    private void FlushPendingMessages(string connectionName)
    {
        WebSocketConnection connection;
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionName, out connection))
            {
                return;
            }
        }

        while (connection.PendingMessages.Count > 0 && connection.State == NetworkConnectionState.Connected)
        {
            NetworkMessage message;
            lock (_lock)
            {
                if (connection.PendingMessages.Count == 0) break;
                message = connection.PendingMessages.Dequeue();
            }

            SendMessageInternal(connection, message);
        }
    }

    #endregion

    #region HTTP 请求

    public void ConfigureHttp(HttpConfig config)
    {
        _httpConfig = config ?? new HttpConfig();
    }

    public async UniTask<HttpResponse> GetAsync(string url, Dictionary<string, string> headers =
        null, CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync("GET", url, null, headers, cancellationToken);
    }

    public async UniTask<HttpResponse> PostAsync(string url, byte[] data, Dictionary<string, string> headers =
        null, CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync("POST", url, data, headers, cancellationToken);
    }

    public async UniTask<HttpResponse> PostJsonAsync(string url, string jsonData, Dictionary<string, string> headers
        = null, CancellationToken cancellationToken = default)
    {
        headers = headers ?? new Dictionary<string, string>();
        if (!headers.ContainsKey("Content-Type"))
        {
            headers["Content-Type"] = "application/json";
        }

        var data = Encoding.UTF8.GetBytes(jsonData ?? string.Empty);
        return await PostAsync(url, data, headers, cancellationToken);
    }

    public async UniTask<HttpResponse> PutAsync(string url, byte[] data, Dictionary<string, string> headers =
        null, CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync("PUT", url, data, headers, cancellationToken);
    }

    public async UniTask<HttpResponse> DeleteAsync(string url, Dictionary<string, string> headers =
        null, CancellationToken cancellationToken = default)
    {
        return await SendHttpRequestAsync("DELETE", url, null, headers, cancellationToken);
    }

    private async UniTask<HttpResponse> SendHttpRequestAsync(string method, string url, byte[] data,
        Dictionary<string, string> headers, CancellationToken cancellationToken)
    {
        _statistics.TotalHttpRequests++;
        var startTime = Time.realtimeSinceStartup;

        // 处理BaseUrl
        if (!string.IsNullOrEmpty(_httpConfig.BaseUrl) && !url.StartsWith("http"))
        {
            url = _httpConfig.BaseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
        }

        int retryCount = 0;
        var maxRetries = _httpConfig.EnableRetry ? _httpConfig.MaxRetryCount : 0;

        while (true)
        {
            try
            {
                using var request = CreateUnityWebRequest(method, url, data);

                // 设置默认请求头
                if (_httpConfig.DefaultHeaders != null)
                {
                    foreach (var header in _httpConfig.DefaultHeaders)
                    {
                        request.SetRequestHeader(header.Key, header.Value);
                    }
                }

                // 设置自定义请求头
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.SetRequestHeader(header.Key, header.Value);
                    }
                }

                request.timeout = (int)_httpConfig.TimeoutSeconds;

                var operation = request.SendWebRequest();
                await operation.ToUniTask(cancellationToken: cancellationToken);

                var response = new HttpResponse
                {
                    StatusCode = (int)request.responseCode,
                    Data = request.downloadHandler?.data,
                    Headers = new Dictionary<string, string>(),
                    ElapsedMs = (long)((Time.realtimeSinceStartup - startTime) * 1000)
                };

                // 解析响应头
                if (request.GetResponseHeaders() != null)
                {
                    foreach (var header in request.GetResponseHeaders())
                    {
                        response.Headers[header.Key] = header.Value;
                    }
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    response.Error = request.error ?? $"HTTP错误: {request.responseCode}";

                    // 检查是否需要重试
                    if (retryCount < maxRetries && ShouldRetry((int)request.responseCode))
                    {
                        retryCount++;
                        GF.LogWarning($"[{Name}] HTTP请求失败，正在重试 ({retryCount}/{maxRetries}): {url}");
                        await UniTask.Delay(TimeSpan.FromSeconds(_httpConfig.RetryIntervalSeconds),
                            cancellationToken: cancellationToken);
                        continue;
                    }

                    _statistics.HttpFailureCount++;
                }
                else
                {
                    _statistics.HttpSuccessCount++;
                }

                return response;
            }
            catch (OperationCanceledException)
            {
                _statistics.HttpFailureCount++;
                return new HttpResponse
                {
                    StatusCode = 0,
                    Error = "请求已取消",
                    ElapsedMs = (long)((Time.realtimeSinceStartup - startTime) * 1000)
                };
            }
            catch (Exception ex)
            {
                if (retryCount < maxRetries)
                {
                    retryCount++;
                    GF.LogWarning($"[{Name}] HTTP请求异常，正在重试 ({retryCount}/{maxRetries}): {ex.Message}");
                    await UniTask.Delay(TimeSpan.FromSeconds(_httpConfig.RetryIntervalSeconds),
                        cancellationToken: cancellationToken);
                    continue;
                }

                _statistics.HttpFailureCount++;
                GF.LogError($"[{Name}] HTTP请求失败: {ex.Message}");
                return new HttpResponse
                {
                    StatusCode = 0,
                    Error = ex.Message,
                    ElapsedMs = (long)((Time.realtimeSinceStartup - startTime) * 1000)
                };
            }
        }
    }

    private UnityWebRequest CreateUnityWebRequest(string method, string url, byte[] data)
    {
        switch (method.ToUpper())
        {
            case "GET":
                return UnityWebRequest.Get(url);
            case "POST":
                var postRequest = new UnityWebRequest(url, "POST");
                postRequest.uploadHandler = new UploadHandlerRaw(data);
                postRequest.downloadHandler = new DownloadHandlerBuffer();
                return postRequest;
            case "PUT":
                var putRequest = new UnityWebRequest(url, "PUT");
                putRequest.uploadHandler = new UploadHandlerRaw(data);
                putRequest.downloadHandler = new DownloadHandlerBuffer();
                return putRequest;
            case "DELETE":
                return UnityWebRequest.Delete(url);
            default:
                throw new ArgumentException($"不支持的HTTP方法: {method}");
        }
    }

    private bool ShouldRetry(int statusCode)
    {
        return _httpConfig.RetryStatusCodes?.Contains(statusCode) ?? false;
    }

    #endregion

    #region 统计

    public NetworkStatistics GetStatistics()
    {
        return _statistics;
    }

    public void ResetStatistics()
    {
        _statistics.Reset();
    }

    #endregion

    #region 生命周期

    protected override UniTask OnInitAsync(CancellationToken cancellationToken)
    {
        GF.Log($"[{Name}] WebSocket网络提供者初始化完成");
        return UniTask.CompletedTask;
    }

    protected override async UniTask OnShutdownAsync(CancellationToken cancellationToken)
    {
        // 断开所有连接
        await DisconnectAllAsync(cancellationToken);

        lock (_lock)
        {
            _connections.Clear();
        }

        GF.Log($"[{Name}] WebSocket网络提供者已关闭");
    }

    #endregion
}
#endif
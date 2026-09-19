using System.Net;
using System.Text;
using System.Text.Json;

namespace cn.lixiaotuan.notifyisland.Services;

/// <summary>
/// NotifyIsland 的统一 HTTP 服务。
/// <para>
/// 同时提供两类接口：
/// <list type="bullet">
/// <item><c>POST /api/notify</c>：JSON 触发 ClassIsland 提醒（NotifyIsland 1.x 兼容）。</item>
/// <item><c>POST /&lt;id&gt;</c>：以请求体文本更新对应 ID 的文本组件（PutIsland 兼容），也可使用 <c>/api/text/&lt;id&gt;</c>。</item>
/// </list>
/// </para>
/// </summary>
public class PluginHttpServer : IDisposable
{
    /// <summary>提醒接口路径。</summary>
    public const string NotifyEndpoint = "/api/notify";

    /// <summary>文本接口的命名空间前缀。</summary>
    public const string TextEndpointPrefix = "/api/text/";

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly HttpTextComponentRegistry _registry;
    private readonly Models.ServerStatus _status;
    private readonly object _sync = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private string _token = "";
    private bool _textApiRequireToken;

    /// <inheritdoc />
    public event EventHandler<NotificationReceivedEventArgs>? NotificationReceived;

    /// <summary>
    /// 初始化 HTTP 服务。
    /// </summary>
    public PluginHttpServer(HttpTextComponentRegistry registry, Models.ServerStatus status)
    {
        _registry = registry;
        _status = status;
    }

    /// <summary>服务是否正在监听。</summary>
    public bool IsRunning { get; private set; }

    /// <summary>当前监听前缀。</summary>
    public string Prefix { get; private set; } = "";

    /// <summary>最近一次错误。</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// 将主机与端口转换为 <see cref="HttpListener"/> 监听前缀。
    /// </summary>
    public static string BuildPrefix(string host, int port)
    {
        var normalized = NormalizeHost(host);
        return $"http://{normalized}:{port}/";
    }

    private static string NormalizeHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "localhost";
        }

        var trimmed = host.Trim();
        return trimmed switch
        {
            "0.0.0.0" => "*",
            "[::]" => "*",
            "::" => "*",
            _ => trimmed
        };
    }

    /// <summary>
    /// 启动服务（若已运行则先停止再重启）。
    /// </summary>
    public void Start(string host, int port, string token, bool textApiRequireToken)
    {
        lock (_sync)
        {
            StopCore();
            _token = token ?? "";
            _textApiRequireToken = textApiRequireToken;
            Prefix = BuildPrefix(host, port);

            var listener = new HttpListener();
            listener.Prefixes.Add(Prefix);
            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                listener.Close();
                IsRunning = false;
                LastError = ex.Message;
                SetStatus("未运行", $"未运行（{ex.Message}）");
                return;
            }

            _listener = listener;
            _cts = new CancellationTokenSource();
            IsRunning = true;
            LastError = null;
            SetStatus($"运行中（{Prefix.TrimEnd('/')}{NotifyEndpoint}）",
                $"运行中（{Prefix.TrimEnd('/')}/<组件ID>）");
            _loop = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));
        }
    }

    /// <summary>
    /// 停止服务。
    /// </summary>
    public void Stop()
    {
        lock (_sync)
        {
            StopCore();
        }
    }

    private void StopCore()
    {
        var listener = _listener;
        var cts = _cts;
        var loop = _loop;
        _listener = null;
        _cts = null;
        _loop = null;
        IsRunning = false;

        if (listener != null)
        {
            try
            {
                cts?.Cancel();
            }
            catch
            {
                // ignore
            }

            try
            {
                listener.Stop();
            }
            catch
            {
                // ignore
            }

            try
            {
                listener.Close();
            }
            catch
            {
                // ignore
            }

            try
            {
                loop?.Wait(1500);
            }
            catch
            {
                // ignore
            }

            cts?.Dispose();
        }

        SetStatus("未运行", "未运行");
    }

    private void SetStatus(string notificationApi, string textApi)
    {
        // ServerStatus 为 ObservableObject，绑定更新需要在 UI 线程上发起。
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _status.NotificationApiStatus = notificationApi;
            _status.TextApiStatus = textApi;
        });
    }

    private void SetLastError(string message)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _status.LastError = message);
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleRequestAsync(context, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    SetLastError(ex.Message);
                }
                finally
                {
                    try
                    {
                        context.Response.Close();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }, token);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
    {
        var request = context.Request;
        var rawPath = request.Url?.AbsolutePath ?? "/";
        string path;
        try
        {
            path = Uri.UnescapeDataString(rawPath).TrimEnd('/');
        }
        catch
        {
            path = rawPath.TrimEnd('/');
        }

        // 根路径：返回服务信息，便于探测服务是否可用。
        if (path.Length == 0)
        {
            await WriteJsonAsync(context, HttpStatusCode.OK, BuildInfoResponse()).ConfigureAwait(false);
            return;
        }

        if (path.Equals(NotifyEndpoint, StringComparison.OrdinalIgnoreCase))
        {
            await HandleNotifyAsync(context, token).ConfigureAwait(false);
            return;
        }

        await HandleTextAsync(context, path).ConfigureAwait(false);
    }

    private string BuildInfoResponse()
    {
        var ids = _registry.GetTokens();
        var list = string.Join(",", ids.Select(x => JsonSerializer.Serialize(x)));
        return
            "{\"success\":true,\"code\":200,\"status\":200," +
            $"\"service\":\"NotifyIsland\",\"version\":\"2.0.0.0\",\"isRunning\":{(IsRunning ? "true" : "false")}," +
            $"\"notifyEndpoint\":\"{NotifyEndpoint}\"," +
            $"\"textEndpoint\":\"/<组件ID> 或 {TextEndpointPrefix}<组件ID>\"," +
            $"\"registeredIds\":[{list}]}}";
    }

    private async Task HandleNotifyAsync(HttpListenerContext context, CancellationToken token)
    {
        var request = context.Request;
        if (!request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context, HttpStatusCode.MethodNotAllowed,
                ApiResponse.Fail(-405, "API端点仅允许POST请求")).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrEmpty(_token))
        {
            var auth = request.Headers["Authorization"];
            if (string.IsNullOrEmpty(auth))
            {
                await WriteJsonAsync(context, HttpStatusCode.Unauthorized,
                    ApiResponse.Fail(-401, "API端点需要认证")).ConfigureAwait(false);
                return;
            }

            if (!auth.StartsWith("Bearer ", StringComparison.Ordinal) ||
                !string.Equals(auth.AsSpan(7).ToString(), _token, StringComparison.Ordinal))
            {
                await WriteJsonAsync(context, HttpStatusCode.Forbidden,
                    ApiResponse.Fail(-403, "API端点认证失败")).ConfigureAwait(false);
                return;
            }
        }

        string body;
        try
        {
            body = await ReadBodyAsync(request, token).ConfigureAwait(false);
        }
        catch
        {
            await WriteJsonAsync(context, HttpStatusCode.BadRequest,
                ApiResponse.Fail(-400, "读取请求内容失败")).ConfigureAwait(false);
            return;
        }

        NotificationPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<NotificationPayload>(body, PayloadJsonOptions);
        }
        catch
        {
            payload = null;
        }

        if (payload is null)
        {
            await WriteJsonAsync(context, HttpStatusCode.BadRequest,
                ApiResponse.Fail(-400, "传入内容应为JSON格式")).ConfigureAwait(false);
            return;
        }

        NotificationReceived?.Invoke(this, new NotificationReceivedEventArgs(payload));
        await WriteJsonAsync(context, HttpStatusCode.OK, ApiResponse.Ok()).ConfigureAwait(false);
    }

    private async Task HandleTextAsync(HttpListenerContext context, string path)
    {
        var request = context.Request;
        string id;
        if (path.StartsWith(TextEndpointPrefix, StringComparison.OrdinalIgnoreCase))
        {
            id = path[TextEndpointPrefix.Length..].Trim('/');
        }
        else
        {
            id = path.TrimStart('/');
        }

        if (string.IsNullOrWhiteSpace(id) || id.Contains('/'))
        {
            await WriteJsonAsync(context, HttpStatusCode.BadRequest,
                ApiResponse.Fail(-400, "没有 Token")).ConfigureAwait(false);
            return;
        }

        if (_textApiRequireToken && !string.IsNullOrEmpty(_token))
        {
            var auth = request.Headers["Authorization"];
            if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.Ordinal) ||
                !string.Equals(auth.AsSpan(7).ToString(), _token, StringComparison.Ordinal))
            {
                await WriteJsonAsync(context, HttpStatusCode.Unauthorized,
                    ApiResponse.Fail(-401, "API端点需要认证")).ConfigureAwait(false);
                return;
            }
        }

        if (!request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
            !request.HttpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context, HttpStatusCode.MethodNotAllowed,
                ApiResponse.Fail(-405, "文本接口仅允许POST请求")).ConfigureAwait(false);
            return;
        }

        string text;
        try
        {
            text = await ReadBodyAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            await WriteJsonAsync(context, HttpStatusCode.BadRequest,
                ApiResponse.Fail(-400, "读取请求内容失败")).ConfigureAwait(false);
            return;
        }

        var updated = _registry.Update(id, text);
        if (updated == 0)
        {
            await WriteJsonAsync(context, HttpStatusCode.NotFound,
                ApiResponse.Fail(-404, $"未找到 ID 为 {id} 的文本组件")).ConfigureAwait(false);
            return;
        }

        await WriteJsonAsync(context, HttpStatusCode.OK,
            ApiResponse.OkDetail(updated, "已更新文本组件")).ConfigureAwait(false);
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request, CancellationToken token)
    {
        Encoding encoding;
        try
        {
            encoding = request.ContentEncoding ?? Encoding.UTF8;
        }
        catch
        {
            encoding = Encoding.UTF8;
        }

        await using var stream = request.InputStream;
        using var reader = new StreamReader(stream, encoding);
        return await reader.ReadToEndAsync(token).ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, HttpStatusCode statusCode, string json)
    {
        var response = context.Response;
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        await response.OutputStream.FlushAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace cn.lixiaotuan.notifyisland.Services;

/// <summary>
/// /api/notify 接口的请求载荷，字段与 NotifyIsland 1.x 保持一致。
/// </summary>
public sealed class NotificationPayload
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("title_duration")]
    public double? TitleDuration { get; set; }

    [JsonPropertyName("title_voice")]
    public string? TitleVoice { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("content_duration")]
    public double? ContentDuration { get; set; }

    [JsonPropertyName("content_voice")]
    public string? ContentVoice { get; set; }

    [JsonPropertyName("sound_enabled")]
    public bool? SoundEnabled { get; set; }

    [JsonPropertyName("effect_enabled")]
    public bool? EffectEnabled { get; set; }
}

/// <summary>
/// 表示一条已通过校验的提醒请求。
/// </summary>
public sealed class NotificationReceivedEventArgs : EventArgs
{
    public NotificationPayload Payload { get; }

    public NotificationReceivedEventArgs(NotificationPayload payload)
    {
        Payload = payload;
    }
}

/// <summary>
/// 统一的 JSON 响应结构，同时保留 code 与 status 两个字段以兼容旧客户端。
/// </summary>
public static class ApiResponse
{
    private static string Build(bool success, int code, string message)
    {
        var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        return JsonSerializer.Serialize(new ApiResponseDto(success, code, message), options);
    }

    public static string Ok(string message = "[200]已推送到ClassIsland") => Build(true, 200, message);

    public static string OkDetail(int updated, string message) => Build(true, 200, $"[200]{message}（更新了 {updated} 个组件）");

    public static string Fail(int code, string message) => Build(false, code, message);

    private sealed record ApiResponseDto(bool Success, int Code, int Status, string Message)
    {
        public ApiResponseDto(bool success, int code, string message) : this(success, code, code, message)
        {
        }
    }
}

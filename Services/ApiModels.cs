using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace cn.lixiaotuan.notifyisland.Services;

/// <summary>
/// /api/notify 接口的请求载荷。
/// <para>
/// 基础字段与 NotifyIsland 1.x 保持一致；<see cref="Type"/>、<see cref="Rolling"/>、
/// <see cref="Target"/>、<see cref="RepeatCount"/>、<see cref="RollingDuration"/> 为 2.0 新增的
/// 提醒类型控制字段，省略时行为与 1.x 完全相同（基本通知）。
/// </para>
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

    /// <summary>
    /// 提醒类型：<c>basic</c>（基本通知，默认，正文使用「简单文本」模板）或
    /// <c>rolling</c>（滚动通知，正文使用「滚动文本」模板）。
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// <see cref="Type"/> 的布尔简写：为 <c>true</c> 时使用滚动通知。
    /// 两个字段同时给出时以 <see cref="Type"/> 为准。
    /// </summary>
    [JsonPropertyName("rolling")]
    public bool? Rolling { get; set; }

    /// <summary>
    /// 滚动文本所作用的部分：<c>content</c>（正文，默认）、<c>mask</c>（遮罩）或 <c>both</c>（两者）。
    /// 仅在滚动通知时生效。
    /// </summary>
    [JsonPropertyName("target")]
    public string? Target { get; set; }

    /// <summary>
    /// 滚动文本的重复次数，省略时为 2，必须大于 0。
    /// </summary>
    [JsonPropertyName("repeat_count")]
    public int? RepeatCount { get; set; }

    /// <summary>
    /// 滚动提醒的显示时长（秒）。省略时正文使用 <see cref="ContentDuration"/>、遮罩使用
    /// <see cref="TitleDuration"/>；两者都未提供时使用 <c>ApiNotificationProvider.DefaultRollingDuration</c>。
    /// </summary>
    [JsonPropertyName("rolling_duration")]
    public double? RollingDuration { get; set; }
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

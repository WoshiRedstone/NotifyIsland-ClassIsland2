using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared.Models.Notification;
using Microsoft.Extensions.Logging;
using cn.lixiaotuan.notifyisland.Models;
using cn.lixiaotuan.notifyisland.Services;
using NotificationRequest = ClassIsland.Core.Models.Notification.NotificationRequest;

namespace cn.lixiaotuan.notifyisland.Services.NotificationProviders;

/// <summary>
/// 通过 HTTP 接口触发的提醒提供方。
/// </summary>
[NotificationProviderInfo("534B80F5-8775-A978-95A3-F6A7BC5A1166", "API提醒", "\uEDC7",
    "通过 NotifyIsland 的 HTTP 接口触发的提醒。")]
public class ApiNotificationProvider : NotificationProviderBase
{
    private readonly PluginHttpServer _server;
    private readonly ILogger<ApiNotificationProvider>? _logger;

    /// <summary>
    /// 提醒默认遮罩显示时长（秒）。
    /// </summary>
    public static double DefaultTitleDuration { get; set; } = 3;

    /// <summary>
    /// 提醒默认正文显示时长（秒）。
    /// </summary>
    public static double DefaultContentDuration { get; set; } = 5;

    /// <summary>
    /// 滚动提醒未显式指定时长（rolling_duration / title_duration / content_duration）时的默认显示时长（秒）。
    /// </summary>
    public static double DefaultRollingDuration { get; set; } = NotificationStyle.DefaultRollingDurationSeconds;

    public ApiNotificationProvider(Plugin plugin, PluginHttpServer server,
        ILogger<ApiNotificationProvider>? logger = null)
    {
        Plugin = plugin;
        _server = server;
        _logger = logger;
        _server.NotificationReceived += OnNotificationReceived;
    }

    private Plugin Plugin { get; }

    private void OnNotificationReceived(object? sender, NotificationReceivedEventArgs e)
    {
        // NotificationContent / 图标模板必须在 UI 线程上构建（Avalonia 对象线程限制）。
        var payload = e.Payload;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                ShowNotification(BuildRequest(payload));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "处理 HTTP 提醒请求时出错");
            }
        });
    }

    /// <summary>
    /// 将 HTTP 载荷转换为 ClassIsland 提醒请求。
    /// <para>
    /// 正文使用哪种提醒内容模板由载荷中的 <c>type</c>（或布尔简写 <c>rolling</c>）字段决定：
    /// <c>basic</c> 使用「简单文本」模板，<c>rolling</c> 使用「滚动文本」模板；
    /// 省略该字段时按 <c>basic</c> 处理，与 NotifyIsland 1.x 行为一致。
    /// </para>
    /// </summary>
    public static NotificationRequest BuildRequest(NotificationPayload payload)
    {
        var title = payload.Title ?? "";
        var content = payload.Content ?? "";
        var titleDuration = TimeSpan.FromSeconds(payload.TitleDuration ?? DefaultTitleDuration);
        var contentDuration = TimeSpan.FromSeconds(payload.ContentDuration ?? DefaultContentDuration);
        var titleSpeech = string.IsNullOrWhiteSpace(payload.TitleVoice) ? title : payload.TitleVoice!;
        var contentSpeech = string.IsNullOrWhiteSpace(payload.ContentVoice) ? content : payload.ContentVoice!;

        // 提醒类型相关字段由 HTTP 接口提前校验，这里解析失败时退回默认值，保证任何情况下都不会抛异常。
        if (!payload.TryResolveDisplayType(out var displayType, out _))
        {
            displayType = NotificationDisplayType.Basic;
        }

        if (!payload.TryResolveRollingTarget(out var target, out _))
        {
            target = RollingTextTarget.Content;
        }

        var rolling = displayType == NotificationDisplayType.Rolling;
        var rollingMask = rolling && (target is RollingTextTarget.Mask or RollingTextTarget.Both);
        var rollingContent = rolling && (target is RollingTextTarget.Content or RollingTextTarget.Both);

        var request = new NotificationRequest
        {
            MaskContent = rollingMask
                ? BuildRollingTextContent(payload, title, titleSpeech, payload.TitleDuration ?? DefaultRollingDuration)
                : NotificationContent.CreateTwoIconsMask(title, rightIcon: "lucide(\ue378)", factory: c =>
                {
                    c.Duration = titleDuration;
                    c.SpeechContent = titleSpeech;
                })
        };

        // 滚动通知未提供正文时改为滚动显示标题，避免出现空白的滚动条。
        var rollingText = content;
        var rollingTextSpeech = contentSpeech;
        if (rollingContent && string.IsNullOrEmpty(content))
        {
            rollingText = title;
            rollingTextSpeech = titleSpeech;
        }

        if (rollingContent)
        {
            if (!string.IsNullOrEmpty(rollingText))
            {
                request.OverlayContent = BuildRollingTextContent(payload, rollingText, rollingTextSpeech,
                    payload.ContentDuration ?? DefaultRollingDuration);
            }
        }
        else if (!string.IsNullOrEmpty(content))
        {
            request.OverlayContent = NotificationContent.CreateSimpleTextContent(content, c =>
            {
                c.Duration = contentDuration;
                c.SpeechContent = contentSpeech;
            });
        }

        request.RequestNotificationSettings = new NotificationSettings
        {
            IsSettingsEnabled = true,
            IsNotificationSoundEnabled = payload.SoundEnabled ?? false,
            IsNotificationEffectEnabled = payload.EffectEnabled ?? true
        };
        return request;
    }

    /// <summary>
    /// 构建滚动文本提醒内容（ClassIsland 的「滚动文本」提醒内容模板）。
    /// </summary>
    /// <param name="payload">HTTP 载荷，用于读取 repeat_count / rolling_duration。</param>
    /// <param name="text">滚动显示的文本。</param>
    /// <param name="speech">语音播报内容。</param>
    /// <param name="fallbackDurationSeconds">未提供 rolling_duration 时使用的显示时长（秒）。</param>
    private static NotificationContent BuildRollingTextContent(NotificationPayload payload, string text,
        string speech, double fallbackDurationSeconds)
    {
        if (!payload.TryResolveRepeatCount(out var repeatCount, out _))
        {
            repeatCount = NotificationStyle.DefaultRepeatCount;
        }

        var fallback = TimeSpan.FromSeconds(fallbackDurationSeconds);
        if (!payload.TryResolveRollingDuration(fallback, out var duration, out _))
        {
            duration = fallback;
        }

        return NotificationContent.CreateRollingTextContent(text, duration, repeatCount, c =>
        {
            c.Duration = duration;
            c.SpeechContent = speech;
        });
    }
}

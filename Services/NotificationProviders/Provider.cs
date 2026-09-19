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
    /// </summary>
    public static NotificationRequest BuildRequest(NotificationPayload payload)
    {
        var title = payload.Title ?? "";
        var content = payload.Content ?? "";
        var titleDuration = TimeSpan.FromSeconds(payload.TitleDuration ?? DefaultTitleDuration);
        var contentDuration = TimeSpan.FromSeconds(payload.ContentDuration ?? DefaultContentDuration);
        var titleSpeech = string.IsNullOrWhiteSpace(payload.TitleVoice) ? title : payload.TitleVoice!;
        var contentSpeech = string.IsNullOrWhiteSpace(payload.ContentVoice) ? content : payload.ContentVoice!;

        var request = new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(title, rightIcon: "lucide(\ue378)", factory: c =>
            {
                c.Duration = titleDuration;
                c.SpeechContent = titleSpeech;
            })
        };

        if (!string.IsNullOrEmpty(content))
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
}

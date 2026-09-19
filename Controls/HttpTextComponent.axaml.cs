using System.ComponentModel;
using Avalonia;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using cn.lixiaotuan.notifyisland.Models;
using cn.lixiaotuan.notifyisland.Services;

namespace cn.lixiaotuan.notifyisland.Controls;

/// <summary>
/// 可通过 HTTP 接口修改内容的文本组件（与 PutIsland 的用法一致）。
/// </summary>
[ComponentInfo("8A5C6D6E-9F1B-4C2A-B0D3-7E4A5F6B7C8D", "HTTP 文本", "lucide(\ue378)",
    "显示可通过 HTTP POST 请求更新的文本。请求 POST http://<监听地址>:<端口>/<组件ID> 即可更新内容。")]
public partial class HttpTextComponent : ComponentBase<HttpTextComponentSettings>
{
    private readonly HttpTextComponentRegistry _registry;
    private string? _registeredToken;

    public HttpTextComponent(HttpTextComponentRegistry registry)
    {
        _registry = registry;
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Settings is not null)
        {
            Settings.PropertyChanged += OnSettingsChanged;
        }

        ApplyAppearance();
        RefreshRegistration();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Settings is not null)
        {
            Settings.PropertyChanged -= OnSettingsChanged;
        }

        ReleaseRegistration();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HttpTextComponentSettings.Token))
        {
            RefreshRegistration();
        }

        if (e.PropertyName is nameof(HttpTextComponentSettings.FontColor)
            or nameof(HttpTextComponentSettings.UseCustomFontColor)
            or nameof(HttpTextComponentSettings.FontSize))
        {
            ApplyAppearance();
        }
    }

    private void RefreshRegistration()
    {
        ReleaseRegistration();
        var token = Settings?.Token?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        _registry.Register(token, this);
        _registeredToken = token;
    }

    private void ReleaseRegistration()
    {
        if (string.IsNullOrWhiteSpace(_registeredToken))
        {
            return;
        }

        _registry.Unregister(_registeredToken, this);
        _registeredToken = null;
    }

    private void ApplyAppearance()
    {
        var settings = Settings;
        if (settings is null)
        {
            return;
        }

        Color color;
        try
        {
            color = Color.Parse(settings.FontColor);
        }
        catch
        {
            color = Colors.White;
        }

        MainTextBlock.Foreground = settings.UseCustomFontColor ? new SolidColorBrush(color) : null;
    }
}

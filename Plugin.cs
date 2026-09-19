using System.IO;
using System.Threading;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using cn.lixiaotuan.notifyisland.Controls;
using cn.lixiaotuan.notifyisland.Models;
using cn.lixiaotuan.notifyisland.Services;
using cn.lixiaotuan.notifyisland.Services.NotificationProviders;
using cn.lixiaotuan.notifyisland.Views;

namespace cn.lixiaotuan.notifyisland;

[PluginEntrance]
public class Plugin : PluginBase
{
    private const int RestartDebounceMilliseconds = 400;

    private readonly object _restartLock = new();
    private Timer? _restartTimer;

    /// <summary>
    /// 插件设置。
    /// </summary>
    public PluginSettings Settings { get; private set; } = new();

    /// <summary>
    /// HTTP 服务运行状态。
    /// </summary>
    public ServerStatus Status { get; } = new();

    /// <summary>
    /// HTTP 文本组件注册表。
    /// </summary>
    public HttpTextComponentRegistry Registry { get; } = new();

    /// <summary>
    /// HTTP 服务实例。
    /// </summary>
    public PluginHttpServer Server { get; }

    public Plugin()
    {
        Server = new PluginHttpServer(Registry, Status);
    }

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var settingsPath = Path.Combine(PluginConfigFolder, "settings.json");
        Settings = ConfigureFileHelper.LoadConfig<PluginSettings>(settingsPath);
        Settings.PropertyChanged += (_, _) =>
            ConfigureFileHelper.SaveConfig(settingsPath, Settings);

        services.AddSingleton(Settings);
        services.AddSingleton(Status);
        services.AddSingleton(Registry);
        services.AddSingleton(Server);
        services.AddSingleton(this);

        services.AddNotificationProvider<ApiNotificationProvider>();
        services.AddComponent<HttpTextComponent, HttpTextComponentSettingsControl>();
        services.AddSettingsPage<NotifyIslandSettingsPage>();

        AppBase.Current.AppStarted += OnAppStarted;
        AppBase.Current.AppStopping += OnAppStopping;
        Settings.PropertyChanged += OnSettingsChanged;
    }

    private void OnAppStarted(object? sender, EventArgs e)
    {
        ApplyServerSettings();
    }

    private void OnAppStopping(object? sender, EventArgs e)
    {
        Settings.PropertyChanged -= OnSettingsChanged;
        _restartTimer?.Dispose();
        _restartTimer = null;
        Server.Dispose();
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PluginSettings.Enabled):
            case nameof(PluginSettings.Host):
            case nameof(PluginSettings.Port):
            case nameof(PluginSettings.Token):
            case nameof(PluginSettings.TextApiRequireToken):
                ScheduleServerRestart();
                break;
        }
    }

    private void ScheduleServerRestart()
    {
        lock (_restartLock)
        {
            _restartTimer?.Dispose();
            _restartTimer = new Timer(_ =>
                Dispatcher.UIThread.Post(ApplyServerSettings), null,
                RestartDebounceMilliseconds, Timeout.Infinite);
        }
    }

    private void ApplyServerSettings()
    {
        try
        {
            if (Settings.Enabled)
            {
                Server.Start(Settings.Host, Settings.Port, Settings.Token, Settings.TextApiRequireToken);
            }
            else
            {
                Server.Stop();
            }
        }
        catch (Exception ex)
        {
            Status.LastError = ex.Message;
        }
    }
}

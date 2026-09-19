using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using cn.lixiaotuan.notifyisland.Models;

namespace cn.lixiaotuan.notifyisland.Views;

/// <summary>
/// NotifyIsland 设置页面。
/// </summary>
[SettingsPageInfo("notifyisland.settingspage", "NotifyIsland", "\uE904", "\uE905", SettingsPageCategory.External)]
public partial class NotifyIslandSettingsPage : SettingsPageBase
{
    /// <summary>
    /// 插件设置。
    /// </summary>
    public PluginSettings Settings { get; }

    /// <summary>
    /// 服务运行状态。
    /// </summary>
    public ServerStatus Status { get; }

    public NotifyIslandSettingsPage(Plugin plugin)
    {
        Settings = plugin.Settings;
        Status = plugin.Status;
        InitializeComponent();
        DataContext = this;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace cn.lixiaotuan.notifyisland.Models;

/// <summary>
/// HTTP 服务运行状态，用于在设置界面显示。
/// </summary>
public partial class ServerStatus : ObservableObject
{
    /// <summary>
    /// 提醒接口状态描述。
    /// </summary>
    [ObservableProperty]
    private string _notificationApiStatus = "未启动";

    /// <summary>
    /// 文本组件接口状态描述。
    /// </summary>
    [ObservableProperty]
    private string _textApiStatus = "未启动";

    /// <summary>
    /// 最近一次错误信息。
    /// </summary>
    [ObservableProperty]
    private string _lastError = "";
}

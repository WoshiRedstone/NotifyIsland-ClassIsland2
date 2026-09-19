using CommunityToolkit.Mvvm.ComponentModel;

namespace cn.lixiaotuan.notifyisland.Models;

/// <summary>
/// NotifyIsland 插件全局设置。
/// </summary>
public partial class PluginSettings : ObservableObject
{
    /// <summary>
    /// 是否启用 HTTP 服务（同时控制提醒接口与文本组件接口）。
    /// </summary>
    [ObservableProperty]
    private bool _enabled = true;

    /// <summary>
    /// 监听主机。localhost 无需管理员权限；* 或 + 为通配所有网卡，需要管理员权限。
    /// </summary>
    [ObservableProperty]
    private string _host = "localhost";

    /// <summary>
    /// 监听端口。
    /// </summary>
    [ObservableProperty]
    private int _port = 1379;

    /// <summary>
    /// 提醒接口 /api/notify 使用的 Bearer 密钥，留空表示不校验。
    /// </summary>
    [ObservableProperty]
    private string _token = "";

    /// <summary>
    /// 文本组件接口（/&lt;id&gt;）是否也要求 Bearer 密钥。默认关闭以保持与 PutIsland 用法一致。
    /// </summary>
    [ObservableProperty]
    private bool _textApiRequireToken = false;
}

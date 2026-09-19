using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;

namespace cn.lixiaotuan.notifyisland.Models;

/// <summary>
/// HTTP 文本组件的设置。
/// </summary>
public partial class HttpTextComponentSettings : ObservableObject
{
    /// <summary>
    /// 组件 ID，用于 HTTP 接口寻址：POST http://&lt;host&gt;:&lt;port&gt;/&lt;id&gt;
    /// </summary>
    [ObservableProperty]
    private string _token = "";

    /// <summary>
    /// 当前显示的文本。
    /// </summary>
    [ObservableProperty]
    private string _text = "";

    /// <summary>
    /// 文本字号。
    /// </summary>
    [ObservableProperty]
    private double _fontSize = 16;

    /// <summary>
    /// 自定义文本颜色（"#RRGGBBAA"）。
    /// </summary>
    [ObservableProperty]
    private string _fontColor = "#FFFFFFFF";

    /// <summary>
    /// 是否使用自定义文本颜色。关闭时使用主题默认前景色。
    /// </summary>
    [ObservableProperty]
    private bool _useCustomFontColor = false;
}

namespace cn.lixiaotuan.notifyisland.Services;

/// <summary>
/// 提醒的显示类型，对应 ClassIsland 内置的提醒内容模板。
/// </summary>
public enum NotificationDisplayType
{
    /// <summary>
    /// 基本通知：正文使用「简单文本」提醒内容模板（与 NotifyIsland 1.x 行为一致）。
    /// </summary>
    Basic,

    /// <summary>
    /// 滚动通知：正文使用「滚动文本」提醒内容模板。
    /// </summary>
    Rolling
}

/// <summary>
/// 滚动文本所作用的提醒部分。
/// </summary>
public enum RollingTextTarget
{
    /// <summary>正文（OverlayContent），默认。</summary>
    Content,

    /// <summary>遮罩（MaskContent）。</summary>
    Mask,

    /// <summary>遮罩与正文都使用滚动文本。</summary>
    Both
}

/// <summary>
/// 提醒类型（基本通知 / 滚动通知）相关字段的取值名称、别名与解析规则。
/// </summary>
public static class NotificationStyle
{
    /// <summary>基本通知在 JSON 中的标准名称。</summary>
    public const string BasicName = "basic";

    /// <summary>滚动通知在 JSON 中的标准名称。</summary>
    public const string RollingName = "rolling";

    /// <summary>滚动文本的默认重复次数。</summary>
    public const int DefaultRepeatCount = 2;

    /// <summary>滚动文本未显式指定时长时的默认显示时长（秒）。</summary>
    public const double DefaultRollingDurationSeconds = 20;

    private static readonly string[] BasicAliases =
        { "basic", "simple", "normal", "default", "static", "基本", "普通", "简单" };

    private static readonly string[] RollingAliases =
        { "rolling", "roll", "scroll", "scrolling", "marquee", "滚动" };

    private static readonly string[] ContentAliases =
        { "content", "overlay", "body", "正文" };

    private static readonly string[] MaskAliases =
        { "mask", "title", "遮罩", "标题" };

    private static readonly string[] BothAliases =
        { "both", "all", "全部", "两者" };

    /// <summary>
    /// JSON 中 type 字段的说明，用于错误提示与文档。
    /// </summary>
    public const string TypeFieldDescription =
        "type：basic（基本通知，默认，正文为简单文本）、rolling（滚动通知，正文为滚动文本）";

    private static bool Matches(string value, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (string.Equals(value, alias, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 解析提醒类型：<paramref name="type"/> 字段优先，其次使用 <paramref name="rolling"/> 简写字段。
    /// </summary>
    public static bool TryParseDisplayType(string? type, bool? rolling,
        out NotificationDisplayType displayType, out string? error)
    {
        error = null;
        var value = type?.Trim();
        if (!string.IsNullOrEmpty(value))
        {
            if (Matches(value, BasicAliases))
            {
                displayType = NotificationDisplayType.Basic;
                return true;
            }

            if (Matches(value, RollingAliases))
            {
                displayType = NotificationDisplayType.Rolling;
                return true;
            }

            displayType = NotificationDisplayType.Basic;
            error = $"不支持的通知类型 {type}，可选值：{BasicName}、{RollingName}";
            return false;
        }

        displayType = rolling == true ? NotificationDisplayType.Rolling : NotificationDisplayType.Basic;
        return true;
    }

    /// <summary>
    /// 解析滚动文本作用的部分，默认为正文。
    /// </summary>
    public static bool TryParseRollingTarget(string? target, out RollingTextTarget result, out string? error)
    {
        error = null;
        var value = target?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            result = RollingTextTarget.Content;
            return true;
        }

        if (Matches(value, ContentAliases))
        {
            result = RollingTextTarget.Content;
            return true;
        }

        if (Matches(value, MaskAliases))
        {
            result = RollingTextTarget.Mask;
            return true;
        }

        if (Matches(value, BothAliases))
        {
            result = RollingTextTarget.Both;
            return true;
        }

        result = RollingTextTarget.Content;
        error = $"不支持的滚动位置 {target}，可选值：content（正文）、mask（遮罩）、both（两者）";
        return false;
    }
}

/// <summary>
/// <see cref="NotificationPayload"/> 中提醒类型相关字段的解析与校验。
/// </summary>
public static class NotificationPayloadStyleExtensions
{
    /// <summary>
    /// 解析提醒类型。
    /// </summary>
    public static bool TryResolveDisplayType(this NotificationPayload payload,
        out NotificationDisplayType displayType, out string? error)
    {
        return NotificationStyle.TryParseDisplayType(payload.Type, payload.Rolling, out displayType, out error);
    }

    /// <summary>
    /// 解析滚动文本作用的部分。
    /// </summary>
    public static bool TryResolveRollingTarget(this NotificationPayload payload,
        out RollingTextTarget target, out string? error)
    {
        return NotificationStyle.TryParseRollingTarget(payload.Target, out target, out error);
    }

    /// <summary>
    /// 解析滚动文本的重复次数，默认为 <see cref="NotificationStyle.DefaultRepeatCount"/>。
    /// </summary>
    public static bool TryResolveRepeatCount(this NotificationPayload payload, out int repeatCount, out string? error)
    {
        error = null;
        if (payload.RepeatCount is not { } value)
        {
            repeatCount = NotificationStyle.DefaultRepeatCount;
            return true;
        }

        if (value < 1)
        {
            repeatCount = NotificationStyle.DefaultRepeatCount;
            error = $"repeat_count 必须大于 0，当前值为 {value}";
            return false;
        }

        repeatCount = value;
        return true;
    }

    /// <summary>
    /// 解析滚动提醒的显示时长：优先 rolling_duration，其次由调用方给出的兜底时长。
    /// </summary>
    public static bool TryResolveRollingDuration(this NotificationPayload payload, TimeSpan fallback,
        out TimeSpan duration, out string? error)
    {
        error = null;
        var seconds = payload.RollingDuration ?? fallback.TotalSeconds;
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
        {
            duration = fallback;
            error = $"rolling_duration 必须大于 0，当前值为 {payload.RollingDuration}";
            return false;
        }

        duration = TimeSpan.FromSeconds(seconds);
        return true;
    }

    /// <summary>
    /// 校验提醒类型相关字段，供 HTTP 接口在推送提醒前拒绝非法请求。
    /// 字段未出现时一律使用默认值，保证与旧客户端兼容。
    /// </summary>
    public static bool TryValidateStyle(this NotificationPayload payload, out string? error)
    {
        if (!payload.TryResolveDisplayType(out var displayType, out error))
        {
            return false;
        }

        if (!payload.TryResolveRollingTarget(out _, out error))
        {
            return false;
        }

        if (!payload.TryResolveRepeatCount(out _, out error))
        {
            return false;
        }

        if (payload.RollingDuration is not null &&
            !payload.TryResolveRollingDuration(TimeSpan.FromSeconds(NotificationStyle.DefaultRollingDurationSeconds),
                out _, out error))
        {
            return false;
        }

        // 基本通知下 target / repeat_count / rolling_duration 不会生效，但取值非法时仍然提示使用者。
        _ = displayType;
        return true;
    }
}

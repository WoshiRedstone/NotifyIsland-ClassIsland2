using System.Text;

namespace cn.lixiaotuan.notifyisland.Services;

/// <summary>
/// 供 HTTP 服务寻址的文本组件注册表。
/// </summary>
/// <remarks>
/// 组件在附加到可视树时注册自身，分离时注销。同一个 ID 允许注册多个组件（多组件同时侦听）。
/// </remarks>
public class HttpTextComponentRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, List<WeakReference<Controls.HttpTextComponent>>> _map =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 注册一个组件实例。
    /// </summary>
    public void Register(string token, Controls.HttpTextComponent component)
    {
        if (string.IsNullOrWhiteSpace(token) || component is null)
        {
            return;
        }

        lock (_sync)
        {
            if (!_map.TryGetValue(token, out var list))
            {
                list = [];
                _map[token] = list;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].TryGetTarget(out var existing) && ReferenceEquals(existing, component))
                {
                    return;
                }
            }

            list.Add(new WeakReference<Controls.HttpTextComponent>(component));
        }
    }

    /// <summary>
    /// 注销一个组件实例。
    /// </summary>
    public void Unregister(string token, Controls.HttpTextComponent component)
    {
        if (string.IsNullOrWhiteSpace(token) || component is null)
        {
            return;
        }

        lock (_sync)
        {
            if (!_map.TryGetValue(token, out var list))
            {
                return;
            }

            list.RemoveAll(w => !w.TryGetTarget(out var existing) || ReferenceEquals(existing, component));
            if (list.Count == 0)
            {
                _map.Remove(token);
            }
        }
    }

    /// <summary>
    /// 获取当前已注册的所有组件 ID。
    /// </summary>
    public List<string> GetTokens()
    {
        lock (_sync)
        {
            PruneLocked();
            return _map.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>
    /// 更新指定 ID 的所有组件文本，返回被更新的组件数量。
    /// </summary>
    public int Update(string token, string text)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return 0;
        }

        List<Controls.HttpTextComponent> targets;
        lock (_sync)
        {
            if (!_map.TryGetValue(token, out var list))
            {
                return 0;
            }

            list.RemoveAll(w => !w.TryGetTarget(out _));
            targets = list.Select(w => w.TryGetTarget(out var c) ? c : null).Where(c => c is not null)
                .Cast<Controls.HttpTextComponent>().ToList();
        }

        foreach (var component in targets)
        {
            var target = component;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (target.Settings is { } settings)
                    {
                        settings.Text = text;
                    }
                }
                catch
                {
                    // 组件可能已被释放，忽略。
                }
            });
        }

        return targets.Count;
    }

    private void PruneLocked()
    {
        foreach (var key in _map.Keys.ToList())
        {
            if (_map.TryGetValue(key, out var list))
            {
                list.RemoveAll(w => !w.TryGetTarget(out _));
                if (list.Count == 0)
                {
                    _map.Remove(key);
                }
            }
        }
    }
}

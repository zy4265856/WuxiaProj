using System;
using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// 事件总线。维护 HookPoint 类型 → 排序 IBuffHook 列表的映射。
/// Buff 施加/移除时 Register/Unregister，OpExecutor 执行时 Fire。
/// </summary>
public class HookBus
{
    /// <summary>
    /// Type = typeof(BeforeHpModifyHook) 等 → 按 Priority 降序排列的 hook 集合。
    /// </summary>
    private readonly Dictionary<Type, SortedSet<RegisteredHook>> _hooks = new();

    /// <summary>
    /// 注册一个 Buff Hook 到指定 HookPoint 类型。
    /// </summary>
    public void Register<TContext>(IBuffHook hook) where TContext : HookContext
    {
        var hookType = typeof(TContext);
        if (!_hooks.TryGetValue(hookType, out var set))
        {
            set = new SortedSet<RegisteredHook>(RegisteredHookComparer.Instance);
            _hooks[hookType] = set;
        }
        set.Add(new RegisteredHook(hook));
    }

    /// <summary>
    /// 从指定 HookPoint 类型注销一个 Buff Hook。
    /// </summary>
    public void Unregister<TContext>(IBuffHook hook) where TContext : HookContext
    {
        var hookType = typeof(TContext);
        if (_hooks.TryGetValue(hookType, out var set))
            set.Remove(new RegisteredHook(hook));
    }

    /// <summary>
    /// 触发指定 HookPoint 类型的所有注册 Hook。按 Priority 降序调用。
    /// IsCancelled 后仍继续调用剩余 hook。
    /// </summary>
    public void Fire<TContext>(TContext context) where TContext : HookContext
    {
        var hookType = typeof(TContext);
        if (!_hooks.TryGetValue(hookType, out var set))
            return;

        foreach (var registered in set)
        {
            registered.Hook.OnHook(context);
        }
    }

    private sealed class RegisteredHook
    {
        public IBuffHook Hook { get; }
        public int Priority { get; }

        public RegisteredHook(IBuffHook hook)
        {
            Hook = hook;
            Priority = hook.Priority;
        }
    }

    private sealed class RegisteredHookComparer : IComparer<RegisteredHook>
    {
        public static readonly RegisteredHookComparer Instance = new();

        public int Compare(RegisteredHook? x, RegisteredHook? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // 降序：Priority 大的排前面
            var cmp = y.Priority.CompareTo(x.Priority);
            if (cmp != 0) return cmp;

            // Priority 相同时按 HashCode 区分，确保不丢弃同优先级条目
            return x.Hook.GetHashCode().CompareTo(y.Hook.GetHashCode());
        }
    }
}

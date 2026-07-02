using System;
using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// 事件总线。维护 (contextType, phase) → 排序 IBuffHook 列表的映射。
/// Buff 施加/移除时注册/注销，HookContext.BeforeOpExecute/AfterOpExecute 触发。
/// </summary>
public class HookBus
{
    /// <summary>
    /// (contextType, phase) → 按 Priority 降序排列的 hook 集合。
    /// </summary>
    private readonly Dictionary<(Type ContextType, HookPhase Phase), SortedSet<RegisteredHook>> _hooks = new();

    /// <summary>
    /// 注册一个 Buff Hook 到指定 contextType 的指定 phase。
    /// </summary>
    public void Register(Type contextType, HookPhase phase, IBuffHook hook)
    {
        var key = (contextType, phase);
        if (!_hooks.TryGetValue(key, out var set))
        {
            set = new SortedSet<RegisteredHook>(RegisteredHookComparer.Instance);
            _hooks[key] = set;
        }
        set.Add(new RegisteredHook(hook));
    }

    /// <summary>
    /// 从指定 contextType 的指定 phase 注销一个 Buff Hook。
    /// </summary>
    public void Unregister(Type contextType, HookPhase phase, IBuffHook hook)
    {
        var key = (contextType, phase);
        if (_hooks.TryGetValue(key, out var set))
            set.Remove(new RegisteredHook(hook));
    }

    /// <summary>
    /// 触发指定 contextType + phase 的所有注册 Hook。按 Priority 降序调用。
    /// </summary>
    public void Fire(Type contextType, HookPhase phase, HookContext context)
    {
        var key = (contextType, phase);
        if (!_hooks.TryGetValue(key, out var set))
            return;

        foreach (var registered in set)
            registered.Hook.OnHook(context);
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

            var cmp = y.Priority.CompareTo(x.Priority);
            if (cmp != 0) return cmp;

            return x.Hook.GetHashCode().CompareTo(y.Hook.GetHashCode());
        }
    }
}

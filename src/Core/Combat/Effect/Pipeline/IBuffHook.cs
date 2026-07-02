namespace WuxiaProj.Combat;

/// <summary>
/// Buff Hook 接口。Buff 实现此接口以注册到特定 (contextType, phase) 拦截点。
/// </summary>
public interface IBuffHook
{
    int Priority { get; }
    void OnHook(HookContext context);
}

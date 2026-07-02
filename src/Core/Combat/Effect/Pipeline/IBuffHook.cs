namespace WuxiaProj.Combat;

/// <summary>
/// Buff Hook 非泛型接口。HookBus 内部以此接口统一存储和调用所有 Hook。
/// </summary>
public interface IBuffHook
{
    int Priority { get; }
    void OnHook(HookContext context);
}

/// <summary>
/// 类型化 Buff Hook 接口。手写 Buff 逻辑时实现此泛型版本获得编译期类型安全。
/// 编译生成的 Buff 直接实现 IBuffHook（非泛型）。
/// </summary>
public interface IBuffHook<TContext> : IBuffHook where TContext : HookContext
{
    void OnHook(TContext context);
}

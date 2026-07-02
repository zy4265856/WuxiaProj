namespace WuxiaProj.Combat;

/// <summary>
/// Hook 类型标记基类。每个子类代表管线上一个拦截点，
/// 携带专属上下文类型 TContext，用作 HookBus 的类型键。
/// </summary>
public abstract class HookPoint<TContext> where TContext : HookContext { }

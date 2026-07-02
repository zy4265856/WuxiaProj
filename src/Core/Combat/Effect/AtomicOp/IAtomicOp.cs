namespace WuxiaProj.Combat;

/// <summary>
/// 原子操作非泛型接口。OpExecutor 通过此接口调度所有 IAtomicOp。
/// </summary>
public interface IAtomicOp
{
    void Execute(HookContext context);
}

/// <summary>
/// 原子操作泛型接口。新运算符实现此接口获得编译期类型安全的 Execute(TContext)。
/// </summary>
public interface IAtomicOp<TContext> : IAtomicOp where TContext : HookContext
{
    void Execute(TContext context);
}

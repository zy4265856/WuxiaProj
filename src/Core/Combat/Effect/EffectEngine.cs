using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// 效果引擎。持有 OpExecutor 和 HookBus，
/// 对外提供统一的效果执行入口。
/// </summary>
public class EffectEngine
{
    public HookBus HookBus { get; }
    public OpExecutor Executor { get; }

    public EffectEngine()
    {
        HookBus = new HookBus();
        Executor = new OpExecutor(HookBus);
    }

    /// <summary>
    /// 执行一条原子操作，使用给定的上下文。
    /// </summary>
    public void ExecuteOp(IAtomicOp op, HookContext context)
    {
        Executor.Execute(op, context);
    }

    /// <summary>
    /// 批量执行一组原子操作，每个操作走完整管线。
    /// </summary>
    public void ExecuteOps(IEnumerable<IAtomicOp> ops, HookContext sharedContext)
    {
        foreach (var op in ops)
            Executor.Execute(op, sharedContext);
    }
}

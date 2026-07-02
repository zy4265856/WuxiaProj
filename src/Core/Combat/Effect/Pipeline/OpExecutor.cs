using System;
using System.Linq;
using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 原子操作管线调度器。每个 IAtomicOp 执行时经过：
///   组装上下文 → ctx.BeforeOpExecute → 检查 Cancel → Execute → ctx.AfterOpExecute → 执行追加指令
/// </summary>
public class OpExecutor
{
    private readonly HookBus _hookBus;
    private const int MaxRecursionDepth = 5;

    public OpExecutor(HookBus hookBus)
    {
        _hookBus = hookBus;
    }

    /// <summary>
    /// 执行一个原子操作，走完整管线。
    /// </summary>
    public void Execute(IAtomicOp op, HookContext context)
    {
        // 1. Before 阶段：上下文自己触发 HookBus
        context.BeforeOpExecute(_hookBus);

        // 2. 检查阻断
        if (context.IsCancelled)
            return;

        // 3. 执行原子操作
        op.Execute(context);

        // 4. After 阶段：上下文自己触发 HookBus
        context.AfterOpExecute(_hookBus);

        // 5. 执行追加指令
        if (context.AppendedOps.Count == 0)
            return;

        if (context.RecursionDepth >= MaxRecursionDepth)
        {
            GD.PushWarning(
                $"[OpExecutor] 递归深度超限 ({MaxRecursionDepth})，" +
                $"丢弃 {context.AppendedOps.Count} 条追加指令");
            return;
        }

        var appendedOps = context.AppendedOps.ToArray();
        context.AppendedOps.Clear();

        foreach (var appendedOp in appendedOps)
        {
            var childContext = CreateChildContext(context);
            Execute(appendedOp, childContext);
        }
    }

    /// <summary>
    /// 为追加指令创建子上下文——继承黑板快照，递增递归深度。
    /// </summary>
    private static HookContext CreateChildContext(HookContext parent)
    {
        var child = (HookContext)Activator.CreateInstance(parent.GetType())!;

        child.GetType().GetProperty(nameof(HookContext.Blackboard))!
            .SetValue(child, parent.Blackboard.Snapshot());

        child.GetType().GetProperty(nameof(HookContext.RecursionDepth))!
            .SetValue(child, parent.RecursionDepth + 1);

        child.GetType().GetProperty(nameof(HookContext.SourceUnit))!
            .SetValue(child, parent.SourceUnit);
        child.GetType().GetProperty(nameof(HookContext.TargetUnit))!
            .SetValue(child, parent.TargetUnit);
        child.GetType().GetProperty(nameof(HookContext.SourceDiceFaceId))!
            .SetValue(child, parent.SourceDiceFaceId);

        return child;
    }
}

using System;
using System.Linq;
using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 原子操作管线调度器。每个 IAtomicOp 执行时经过：
///   组装上下文 → Fire Before → 检查 Cancel → Execute → Fire After → 执行追加指令
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
        // 1. Fire Before Hook
        FireHook(context, isBefore: true);

        // 2. 检查阻断
        if (context.IsCancelled)
            return;

        // 3. 执行原子操作
        op.Execute(context);

        // 4. Fire After Hook
        FireHook(context, isBefore: false);

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
    /// 通过反射找到 context 类型对应的 BeforeXxxHook / AfterXxxHook 并 Fire。
    /// 约定：context 类名 "HpModifyContext" → "BeforeHpModifyHook" / "AfterHpModifyHook"
    /// </summary>
    private void FireHook(HookContext context, bool isBefore)
    {
        var contextType = context.GetType();
        var prefix = isBefore ? "Before" : "After";
        var baseName = contextType.Name.Replace("Context", "");
        var hookTypeName = $"WuxiaProj.Combat.{prefix}{baseName}Hook";
        var hookType = contextType.Assembly.GetType(hookTypeName);

        if (hookType == null)
        {
            GD.PushWarning($"[OpExecutor] 找不到 Hook 类型: {hookTypeName}");
            return;
        }

        var fireMethod = typeof(HookBus).GetMethod("Fire")!.MakeGenericMethod(hookType);
        fireMethod.Invoke(_hookBus, new object[] { context });
    }

    /// <summary>
    /// 为追加指令创建子上下文——继承黑板快照，递增递归深度。
    /// </summary>
    private static HookContext CreateChildContext(HookContext parent)
    {
        var child = (HookContext)Activator.CreateInstance(parent.GetType())!;

        // 拷贝黑板快照
        child.GetType().GetProperty(nameof(HookContext.Blackboard))!
            .SetValue(child, parent.Blackboard.Snapshot());

        // 递增递归深度
        child.GetType().GetProperty(nameof(HookContext.RecursionDepth))!
            .SetValue(child, parent.RecursionDepth + 1);

        // 拷贝元信息
        child.GetType().GetProperty(nameof(HookContext.SourceUnit))!
            .SetValue(child, parent.SourceUnit);
        child.GetType().GetProperty(nameof(HookContext.TargetUnit))!
            .SetValue(child, parent.TargetUnit);
        child.GetType().GetProperty(nameof(HookContext.SourceDiceFaceId))!
            .SetValue(child, parent.SourceDiceFaceId);

        return child;
    }
}

using System;
using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// Hook 上下文基类。携带一次原子操作执行所需的全部可变数据，
/// 以及 Blackboard 供 Buff 间传递临时标记。
/// 子类通过覆写 BeforeHookType / AfterHookType 声明自身对应的 Hook 类型。
/// </summary>
public abstract class HookContext
{
    public bool IsCancelled { get; set; }

    public List<IAtomicOp> AppendedOps { get; } = new();

    // -- 元信息（管线填充，只读） --
    public CombatUnitId SourceUnit { get; init; }
    public CombatUnitId TargetUnit { get; init; }
    public string? SourceDiceFaceId { get; init; }
    public int RecursionDepth { get; init; }

    public Blackboard Blackboard { get; init; } = new();

    // -- 预计算标记（管线填充） --
    public bool TargetHasBleed { get; init; }
    public bool TargetHasPoison { get; init; }

    /// <summary>
    /// 子类覆写：返回此上下文对应的 Before Hook 类型。
    /// </summary>
    public abstract Type BeforeHookType { get; }

    /// <summary>
    /// 子类覆写：返回此上下文对应的 After Hook 类型。
    /// </summary>
    public abstract Type AfterHookType { get; }
}

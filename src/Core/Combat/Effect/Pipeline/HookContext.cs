using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// Hook 上下文基类。携带一次原子操作执行所需的全部可变数据，
/// 以及 Blackboard 供 Buff 间传递临时标记。
/// foreach 迭代源由具体上下文子类按需提供。
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
}

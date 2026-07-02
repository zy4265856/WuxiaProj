using System;
using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// Hook 上下文基类。携带一次原子操作执行所需的全部可变数据与黑板。
/// 子类通过 BeforeOpExecute / AfterOpExecute 控制自身在管线中的调度行为。
/// 子类须在静态构造器中调用 RegisterContextType() 登记短名 → Type 映射。
/// </summary>
public abstract class HookContext
{
    private static readonly Dictionary<string, Type> ContextTypeRegistry = new();

    /// <summary>
    /// 子类静态构造器中调用，登记短名（如 "HpModifyContext"）到 Type 的映射。
    /// </summary>
    protected static void RegisterContextType(string name, Type contextType)
    {
        ContextTypeRegistry[name] = contextType;
    }

    /// <summary>
    /// 根据短名查找上下文类型。BuffEffectCompiler 在编译阶段调用。
    /// </summary>
    internal static Type? ResolveContextType(string name)
    {
        return ContextTypeRegistry.TryGetValue(name, out var type) ? type : null;
    }
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
    /// Before 阶段：在原子操作执行前调用。默认向 HookBus 触发本上下文类型的 Before 事件。
    /// 子类可不覆写（默认行为），或覆写以自定义调度逻辑。
    /// </summary>
    public virtual void BeforeOpExecute(HookBus bus)
    {
        bus.Fire(GetType(), HookPhase.Before, this);
    }

    /// <summary>
    /// After 阶段：在原子操作执行后调用。默认向 HookBus 触发本上下文类型的 After 事件。
    /// </summary>
    public virtual void AfterOpExecute(HookBus bus)
    {
        bus.Fire(GetType(), HookPhase.After, this);
    }
}

/// <summary>
/// Hook 阶段枚举。Before = 执行前（可修改/阻断），After = 执行后（可响应/追加）。
/// </summary>
public enum HookPhase
{
    Before,
    After
}

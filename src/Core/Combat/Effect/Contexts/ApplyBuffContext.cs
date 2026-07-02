namespace WuxiaProj.Combat;

public sealed class BeforeApplyBuffHook : HookPoint<ApplyBuffContext> { }
public sealed class AfterApplyBuffHook  : HookPoint<ApplyBuffContext> { }

/// <summary>
/// 施加/移除 Buff 操作的上下文。
/// </summary>
public class ApplyBuffContext : HookContext
{
    public string BuffConfigId { get; init; } = "";
    public int Duration { get; set; }
    public int StackCount { get; set; } = 1;
}

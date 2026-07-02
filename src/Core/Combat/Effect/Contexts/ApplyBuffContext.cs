namespace WuxiaProj.Combat;

/// <summary>
/// 施加/移除 Buff 操作的上下文。
/// </summary>
public class ApplyBuffContext : HookContext
{
    public string BuffConfigId { get; init; } = "";
    public int Duration { get; set; }
    public int StackCount { get; set; } = 1;
}

namespace WuxiaProj.Combat;

/// <summary>
/// Buff 运行时状态。每个施加在单位上的 Buff 持有一个实例。
/// </summary>
public class BuffState
{
    public CombatBuffId Id { get; init; }
    public string ConfigId { get; init; } = "";
    public BuffDelegate? CompiledEffects { get; init; }
    public int RemainingDuration { get; set; }
    public int CurrentStacks { get; set; } = 1;
    public CombatUnitId SourceUnit { get; init; }

    public bool IsExpired => RemainingDuration == 0;
}

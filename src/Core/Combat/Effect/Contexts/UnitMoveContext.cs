using Godot;

namespace WuxiaProj.Combat;

public sealed class BeforeUnitMoveHook : HookPoint<UnitMoveContext> { }
public sealed class AfterUnitMoveHook  : HookPoint<UnitMoveContext> { }

/// <summary>
/// 单位移动操作的上下文。
/// </summary>
public class UnitMoveContext : HookContext
{
    public Vector2I From { get; init; }
    public Vector2I To { get; set; }
    public int Distance { get; set; }
}

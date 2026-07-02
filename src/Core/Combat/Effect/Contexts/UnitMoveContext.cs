using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 单位移动操作的上下文。
/// </summary>
public class UnitMoveContext : HookContext
{
    static UnitMoveContext()
    {
        RegisterContextType("UnitMoveContext", typeof(UnitMoveContext));
    }
    public Vector2I From { get; init; }
    public Vector2I To { get; set; }
    public int Distance { get; set; }
}

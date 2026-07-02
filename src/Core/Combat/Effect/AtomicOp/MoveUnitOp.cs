using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 单位移动的原子操作。
/// </summary>
public class MoveUnitOp : IAtomicOp
{
    public Vector2I Delta { get; init; }

    public void Execute(HookContext context)
    {
        var ctx = (UnitMoveContext)context;
        GD.Print($"[MoveUnitOp] {ctx.SourceUnit}: {ctx.From} → {ctx.To}");
    }
}

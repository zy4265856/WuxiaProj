using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 单位移动的原子操作。
/// </summary>
public class MoveUnitOp : IAtomicOp<UnitMoveContext>
{
    public Vector2I Delta { get; init; }

    void IAtomicOp.Execute(HookContext context) => Execute((UnitMoveContext)context);

    public void Execute(UnitMoveContext context)
    {
        GD.Print($"[MoveUnitOp] {context.SourceUnit}: {context.From} → {context.To}");
    }
}

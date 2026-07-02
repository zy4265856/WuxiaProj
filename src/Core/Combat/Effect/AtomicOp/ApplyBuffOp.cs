using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 施加 Buff 的原子操作。
/// </summary>
public class ApplyBuffOp : IAtomicOp
{
    public string BuffConfigId { get; init; } = "";

    public void Execute(HookContext context)
    {
        var ctx = (ApplyBuffContext)context;
        GD.Print($"[ApplyBuffOp] {ctx.TargetUnit} 获得 Buff: {ctx.BuffConfigId}");
    }
}

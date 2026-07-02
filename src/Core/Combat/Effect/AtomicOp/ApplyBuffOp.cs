using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 施加 Buff 的原子操作。
/// </summary>
public class ApplyBuffOp : IAtomicOp<ApplyBuffContext>
{
    public string BuffConfigId { get; init; } = "";

    void IAtomicOp.Execute(HookContext context) => Execute((ApplyBuffContext)context);

    public void Execute(ApplyBuffContext context)
    {
        GD.Print($"[ApplyBuffOp] {context.TargetUnit} 获得 Buff: {context.BuffConfigId}");
    }
}

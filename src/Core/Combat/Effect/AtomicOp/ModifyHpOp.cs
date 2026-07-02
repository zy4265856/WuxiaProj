using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 修改 HP 的原子操作。正 Amount = 治疗，负 = 伤害。
/// </summary>
public class ModifyHpOp : IAtomicOp
{
    public int Amount { get; init; }
    public string DamageType { get; init; } = "pure";

    public void Execute(HookContext context)
    {
        var ctx = (HpModifyContext)context;
        GD.Print($"[ModifyHpOp] {ctx.SourceUnit} → {ctx.TargetUnit}: " +
                 $"HP {ctx.Amount:+#;-#} ({ctx.DamageType})");
    }
}

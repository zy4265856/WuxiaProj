using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 修改 HP 的原子操作。正 Amount = 治疗，负 = 伤害。
/// </summary>
public class ModifyHpOp : IAtomicOp<HpModifyContext>
{
    public int Amount { get; init; }
    public string DamageType { get; init; } = "pure";

    void IAtomicOp.Execute(HookContext context) => Execute((HpModifyContext)context);

    public void Execute(HpModifyContext context)
    {
        GD.Print($"[ModifyHpOp] {context.SourceUnit} → {context.TargetUnit}: " +
                 $"HP {context.Amount:+#;-#} ({context.DamageType})");
    }
}

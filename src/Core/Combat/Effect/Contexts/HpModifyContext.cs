using System.Collections.Generic;

namespace WuxiaProj.Combat;

public sealed class BeforeHpModifyHook : HookPoint<HpModifyContext> { }
public sealed class AfterHpModifyHook  : HookPoint<HpModifyContext> { }

/// <summary>
/// HP 修改操作的上下文。Amount 正值=治疗，负值=伤害。
/// </summary>
public class HpModifyContext : HookContext
{
    public int Amount { get; set; }
    public string DamageType { get; init; } = "pure";
    public bool CanCrit { get; set; }

    /// <summary>foreach 迭代源：相邻敌人单位 ID 列表（管线组装时填充）</summary>
    public IReadOnlyList<CombatUnitId> AdjacentEnemies { get; init; } = System.Array.Empty<CombatUnitId>();
}

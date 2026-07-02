using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// Buff 模板配置（从 JSON 加载）。
/// </summary>
public class BuffConfig
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public BuffDurationType DurationType { get; init; } = BuffDurationType.Turn;
    public int Duration { get; init; }
    public BuffStackRule StackRule { get; init; } = BuffStackRule.Replace;
    public int MaxStacks { get; init; } = 1;
    public string Tag { get; init; } = "";

    /// <summary>
    /// Hook 配置列表。每个条目声明 contextType、phase、condition、actions。
    /// 在运行时由 BuffEffectCompiler 编译为 BuffDelegate。
    /// </summary>
    public List<BuffHookEntry> Hooks { get; init; } = new();
}

/// <summary>
/// 单个 Hook 条目。对应 Buff JSON 配置中 hooks 数组的一个元素。
/// </summary>
public class BuffHookEntry
{
    /// <summary>上下文类型名，如 "HpModifyContext"</summary>
    public string ContextType { get; init; } = "";

    /// <summary>拦截阶段："Before" 或 "After"</summary>
    public string Phase { get; init; } = "Before";

    public string Condition { get; init; } = "";
    public List<object> Actions { get; init; } = new();
}

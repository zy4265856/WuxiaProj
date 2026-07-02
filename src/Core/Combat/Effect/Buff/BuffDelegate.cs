using System;
using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// 编译后的 Buff 效果委托容器。
/// Key = (contextType, HookPhase) 二元组。
/// </summary>
public class BuffDelegate
{
    public Dictionary<(Type ContextType, HookPhase Phase), Action<HookContext>> Handlers { get; } = new();
}

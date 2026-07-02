using System;
using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// 编译后的 Buff 效果委托容器。
/// 内部持有 HookPoint Type → Action&lt;HookContext&gt; 映射。
/// </summary>
public class BuffDelegate
{
    public Dictionary<Type, Action<HookContext>> Handlers { get; } = new();
}

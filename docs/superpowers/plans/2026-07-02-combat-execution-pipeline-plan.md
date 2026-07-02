# 战斗执行上下文管线 — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现带拦截点的原子操作执行管线，使 Buff 能在 Before/After 两个时点通过事件广播修改、阻断或追加战斗指令。

**Architecture:** 分层构建——先 ID 与基础类型（CombatUnitId、Blackboard、HookContext），再管线核心（HookBus、OpExecutor），然后是具体上下文与原子操作，最后是 Buff 编译系统与 BuffManager 集成。

**Tech Stack:** C# + Godot 4.6 .NET，`System.Linq.Expressions`，遵循项目代码规则（CLAUDE.md §9）

---

## 文件清单

| 文件 | 职责 |
|------|------|
| `src/Core/Combat/CombatUnitId.cs` | 战斗内单位 ID（值类型，自增） |
| `src/Core/Combat/CombatBuffId.cs` | Buff 实例 ID（值类型，自增） |
| `src/Core/Combat/Effect/Pipeline/HookPoint.cs` | HookPoint\<TContext\> 抽象基类 |
| `src/Core/Combat/Effect/Pipeline/HookContext.cs` | HookContext 抽象基类（含 Blackboard 持有） |
| `src/Core/Combat/Effect/Pipeline/Blackboard.cs` | 值类型 KV 黑板 |
| `src/Core/Combat/Effect/Pipeline/IBuffHook.cs` | Buff Hook 泛型接口 |
| `src/Core/Combat/Effect/Pipeline/HookBus.cs` | 事件总线（Type → 排序 Hook 列表） |
| `src/Core/Combat/Effect/Pipeline/OpExecutor.cs` | 管线调度器（组装上下文 → Fire → Execute → Append） |
| `src/Core/Combat/Effect/Contexts/HpModifyContext.cs` | HP 修改上下文 + Before/After Hook 类型 |
| `src/Core/Combat/Effect/Contexts/UnitMoveContext.cs` | 移动上下文 + Hook 类型 |
| `src/Core/Combat/Effect/Contexts/ApplyBuffContext.cs` | 施加 Buff 上下文 + Hook 类型 |
| `src/Core/Combat/Effect/AtomicOp/IAtomicOp.cs` | 原子操作接口 |
| `src/Core/Combat/Effect/AtomicOp/ModifyHpOp.cs` | HP 修改原子操作 |
| `src/Core/Combat/Effect/AtomicOp/MoveUnitOp.cs` | 移动原子操作 |
| `src/Core/Combat/Effect/AtomicOp/ApplyBuffOp.cs` | 施加 Buff 原子操作 |
| `src/Core/Combat/Effect/EffectEngine.cs` | 效果引擎（使用 OpExecutor） |
| `src/Core/Combat/Effect/Buff/BuffDelegate.cs` | 编译产物持有者 |
| `src/Core/Combat/Effect/Buff/BuffEffectCompiler.cs` | JSON 配置 → Expression Tree 编译器 |
| `src/Core/Combat/Effect/Buff/BuffConfig.cs` | Buff 配置数据 |
| `src/Core/Combat/Effect/Buff/BuffState.cs` | Buff 运行时状态 |
| `src/Core/Combat/Effect/Buff/BuffManager.cs` | Buff 管理器（生命周期 + Hook 注册） |
| `src/Core/Combat/Effect/Buff/BuffDurationType.cs` | Buff 持续类型枚举 |
| `src/Core/Combat/Effect/Buff/BuffStackRule.cs` | Buff 叠加规则枚举 |

---

### Task 1: 战斗 ID 类型

**Files:**
- Create: `src/Core/Combat/CombatUnitId.cs`
- Create: `src/Core/Combat/CombatBuffId.cs`

- [ ] **Step 1: 创建 CombatUnitId.cs**

```csharp
using System;

namespace WuxiaProj.Combat;

/// <summary>
/// 战斗内单位轻量标识符。值类型，战斗初始化时由 CombatManager 自增分配。
/// </summary>
public readonly struct CombatUnitId : IEquatable<CombatUnitId>
{
    public uint Value { get; }

    private static uint _nextId = 1;

    internal CombatUnitId(uint value)
    {
        Value = value;
    }

    public static CombatUnitId New()
    {
        return new CombatUnitId(_nextId++);
    }

    public bool Equals(CombatUnitId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is CombatUnitId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"CU({Value})";

    public static bool operator ==(CombatUnitId left, CombatUnitId right) => left.Equals(right);
    public static bool operator !=(CombatUnitId left, CombatUnitId right) => !left.Equals(right);
}
```

- [ ] **Step 2: 创建 CombatBuffId.cs**

```csharp
using System;

namespace WuxiaProj.Combat;

/// <summary>
/// Buff 实例轻量标识符。值类型，由 BuffManager 在 Buff 施加时自增分配。
/// </summary>
public readonly struct CombatBuffId : IEquatable<CombatBuffId>
{
    public uint Value { get; }

    private static uint _nextId = 1;

    internal CombatBuffId(uint value)
    {
        Value = value;
    }

    public static CombatBuffId New()
    {
        return new CombatBuffId(_nextId++);
    }

    public bool Equals(CombatBuffId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is CombatBuffId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"BF({Value})";

    public static bool operator ==(CombatBuffId left, CombatBuffId right) => left.Equals(right);
    public static bool operator !=(CombatBuffId left, CombatBuffId right) => !left.Equals(right);
}
```

- [ ] **Step 3: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 4: 提交**

```bash
git add src/Core/Combat/CombatUnitId.cs src/Core/Combat/CombatBuffId.cs
git commit -m "feat: 新增 CombatUnitId / CombatBuffId 战斗内轻量 ID 类型"
```

---

### Task 2: Blackboard 黑板

**Files:**
- Create: `src/Core/Combat/Effect/Pipeline/Blackboard.cs`

- [ ] **Step 1: 创建 Blackboard.cs**

```csharp
using System;
using System.Collections.Generic;
using WuxiaProj.Framework;

namespace WuxiaProj.Combat;

/// <summary>
/// 上下文级键值黑板。只允许值类型和专用 ID 的读写，不允许引用类型。
/// 子上下文通过 Snapshot() 继承父黑板，上下文结束后黑板随上下文丢弃。
/// </summary>
public class Blackboard
{
    private readonly Dictionary<string, object> _data = new();

    private static readonly HashSet<Type> AllowedTypes = new()
    {
        typeof(int), typeof(float), typeof(bool), typeof(string),
        typeof(CombatUnitId), typeof(CombatBuffId), typeof(ObjectId)
    };

    public void Set<T>(string key, T value)
    {
        var type = typeof(T);
        if (!AllowedTypes.Contains(type))
            throw new ArgumentException(
                $"[Blackboard] 不支持的类型: {type.Name}。仅允许值类型和战斗 ID 类型。");
        _data[key] = value!;
    }

    public T Get<T>(string key)
    {
        if (_data.TryGetValue(key, out var value))
            return (T)value;
        throw new KeyNotFoundException($"[Blackboard] 键不存在: {key}");
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (_data.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    public bool Has(string key) => _data.ContainsKey(key);

    public void Remove(string key) => _data.Remove(key);

    /// <summary>
    /// 深拷贝当前黑板，供子上下文继承。
    /// </summary>
    public Blackboard Snapshot()
    {
        var clone = new Blackboard();
        foreach (var (key, value) in _data)
            clone._data[key] = value; // 值类型自然拷贝，string/ID 是 immutable
        return clone;
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 3: 提交**

```bash
git add src/Core/Combat/Effect/Pipeline/Blackboard.cs
git commit -m "feat: 新增 Blackboard 上下文级值类型 KV 黑板"
```

---

### Task 3: HookPoint 与 HookContext 基类

**Files:**
- Create: `src/Core/Combat/Effect/Pipeline/HookPoint.cs`
- Create: `src/Core/Combat/Effect/Pipeline/HookContext.cs`

- [ ] **Step 1: 创建 HookPoint.cs**

```csharp
namespace WuxiaProj.Combat;

/// <summary>
/// Hook 类型标记基类。每个子类代表管线上一个拦截点，
/// 携带专属上下文类型 TContext，用作 HookBus 的类型键。
/// </summary>
public abstract class HookPoint<TContext> where TContext : HookContext { }
```

- [ ] **Step 2: 创建 HookContext.cs**

```csharp
using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// Hook 上下文基类。携带一次原子操作执行所需的全部可变数据，
/// 以及 Blackboard 供 Buff 间传递临时标记。
/// foreach 迭代源由具体上下文子类按需提供。
/// </summary>
public abstract class HookContext
{
    public bool IsCancelled { get; set; }

    public List<IAtomicOp> AppendedOps { get; } = new();

    // -- 元信息（管线填充，只读） --
    public CombatUnitId SourceUnit { get; init; }
    public CombatUnitId TargetUnit { get; init; }
    public string? SourceDiceFaceId { get; init; }
    public int RecursionDepth { get; init; }

    public Blackboard Blackboard { get; init; } = new();

    // -- 预计算标记（管线填充） --
    public bool TargetHasBleed { get; init; }
    public bool TargetHasPoison { get; init; }
}
```

> 注：`IAtomicOp` 接口尚未创建，但编译可通过——C# 允许前向引用同程序集中的类型。
> 实际上这里会有编译错误。先让 HookContext 中的 `AppendedOps` 使用 `object` 占位，Task 5 创建 IAtomicOp 后再改回来。

- [ ] **Step 3: 先创建 IAtomicOp 最小接口（跳过此步骤则 HookContext 编译报错）**

在 Task 3 中一并创建 `src/Core/Combat/Effect/AtomicOp/IAtomicOp.cs`：

```csharp
namespace WuxiaProj.Combat;

/// <summary>
/// 原子操作接口。每个实现类代表战斗中的一个最小可执行效果单元。
/// </summary>
public interface IAtomicOp
{
    void Execute(HookContext context);
}
```

> 注：spec 中原有 `EffectContext` 和 `Dictionary<string, object> @params` 两个参数，
> 现合并为 `HookContext context` 单一入口——具体上下文子类携带了原 params 中的所有数据。

- [ ] **Step 4: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 5: 提交**

```bash
git add src/Core/Combat/Effect/Pipeline/HookPoint.cs src/Core/Combat/Effect/Pipeline/HookContext.cs src/Core/Combat/Effect/AtomicOp/IAtomicOp.cs
git commit -m "feat: 新增 HookPoint/HookContext 基类及 IAtomicOp 接口"
```

---

### Task 4: IBuffHook 与 HookBus

**Files:**
- Create: `src/Core/Combat/Effect/Pipeline/IBuffHook.cs`
- Create: `src/Core/Combat/Effect/Pipeline/HookBus.cs`

- [ ] **Step 1: 创建 IBuffHook.cs**

```csharp
namespace WuxiaProj.Combat;

/// <summary>
/// Buff Hook 接口。Buff 实现此接口以注册到特定 Hook 的拦截点。
/// Priority 决定同 Hook 内多个 Buff 的执行顺序（降序）。
/// </summary>
public interface IBuffHook
{
    int Priority { get; }
    void OnHook(HookContext context);
}

/// <summary>
/// 类型化 Buff Hook 接口。手写 Buff 逻辑时实现此泛型版本获得类型安全。
/// 编译生成的 Buff 直接实现 IBuffHook（非泛型）。
/// </summary>
public interface IBuffHook<TContext> : IBuffHook where TContext : HookContext
{
    void OnHook(TContext context);
}
```

> IBuffHook（非泛型）是 HookBus 内部存储和调用的统一接口。
> IBuffHook\<TContext\> 继承它，手写 Buff 实现泛型版本时获得编译期类型安全。
> 编译生成的 CompiledBuffHook 直接实现非泛型版本。

- [ ] **Step 2: 创建 HookBus.cs**

```csharp
using System;
using System.Collections.Generic;
using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 事件总线。维护 Type → 排序 IBuffHook 列表的映射。
/// Buff 施加/移除时 Register/Unregister，OpExecutor 执行时 Fire。
/// </summary>
public class HookBus
{
    /// <summary>
    /// Type = typeof(BeforeHpModifyHook) 等 → 按 Priority 降序的 IBuffHook 列表。
    /// </summary>
    private readonly Dictionary<Type, SortedSet<RegisteredHook>> _hooks = new();

    /// <summary>
    /// 注册一个 Buff Hook 到指定 HookPoint 类型。
    /// </summary>
    public void Register<TContext>(IBuffHook hook) where TContext : HookContext
    {
        var hookType = typeof(TContext);
        if (!_hooks.TryGetValue(hookType, out var set))
        {
            set = new SortedSet<RegisteredHook>(RegisteredHookComparer.Instance);
            _hooks[hookType] = set;
        }
        set.Add(new RegisteredHook(hook));
    }

    public void Unregister<TContext>(IBuffHook hook) where TContext : HookContext
    {
        var hookType = typeof(TContext);
        if (_hooks.TryGetValue(hookType, out var set))
            set.Remove(new RegisteredHook(hook));
    }

    /// <summary>
    /// 触发指定 HookPoint 类型的所有注册 Hook。按 Priority 降序调用。
    /// IsCancelled 后仍继续调用剩余 hook。
    /// </summary>
    public void Fire<TContext>(TContext context) where TContext : HookContext
    {
        var hookType = typeof(TContext);
        if (!_hooks.TryGetValue(hookType, out var set))
            return;

        foreach (var registered in set)
        {
            registered.Hook.OnHook(context);
        }
    }

    private sealed class RegisteredHook
    {
        public IBuffHook Hook { get; }
        public int Priority { get; }

        public RegisteredHook(IBuffHook hook)
        {
            Hook = hook;
            Priority = hook.Priority;
        }
    }

    private sealed class RegisteredHookComparer : IComparer<RegisteredHook>
    {
        public static readonly RegisteredHookComparer Instance = new();

        public int Compare(RegisteredHook? x, RegisteredHook? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var cmp = y.Priority.CompareTo(x.Priority);
            if (cmp != 0) return cmp;

            return x.Hook.GetHashCode().CompareTo(y.Hook.GetHashCode());
        }
    }
}
```

- [ ] **Step 3: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 4: 提交**

```bash
git add src/Core/Combat/Effect/Pipeline/IBuffHook.cs src/Core/Combat/Effect/Pipeline/HookBus.cs
git commit -m "feat: 新增 IBuffHook 接口与 HookBus 事件总线"
```

---

### Task 5: 具体上下文类型与 Hook 定义

**Files:**
- Create: `src/Core/Combat/Effect/Contexts/HpModifyContext.cs`
- Create: `src/Core/Combat/Effect/Contexts/UnitMoveContext.cs`
- Create: `src/Core/Combat/Effect/Contexts/ApplyBuffContext.cs`

- [ ] **Step 1: 创建 HpModifyContext.cs（含 Before/After Hook 类型）**

```csharp
using System.Collections.Generic;
using Godot;

namespace WuxiaProj.Combat;

public sealed class BeforeHpModifyHook : HookPoint<HpModifyContext> { }
public sealed class AfterHpModifyHook  : HookPoint<HpModifyContext> { }

/// <summary>
/// HP 修改操作的上下文。Amount 正=治疗，负=伤害。
/// </summary>
public class HpModifyContext : HookContext
{
    public int Amount { get; set; }
    public string DamageType { get; init; } = "pure";  // slash / pierce / fire / pure
    public bool CanCrit { get; set; }

    /// <summary>foreach 迭代源：相邻敌人单位 ID 列表（管线组装时填充）</summary>
    public IReadOnlyList<CombatUnitId> AdjacentEnemies { get; init; } = System.Array.Empty<CombatUnitId>();
}
```

- [ ] **Step 2: 创建 UnitMoveContext.cs**

```csharp
using Godot;

namespace WuxiaProj.Combat;

public sealed class BeforeUnitMoveHook : HookPoint<UnitMoveContext> { }
public sealed class AfterUnitMoveHook  : HookPoint<UnitMoveContext> { }

/// <summary>
/// 单位移动操作的上下文。
/// </summary>
public class UnitMoveContext : HookContext
{
    public Vector2I From { get; init; }
    public Vector2I To { get; set; }
    public int Distance { get; set; }
}
```

- [ ] **Step 3: 创建 ApplyBuffContext.cs**

```csharp
namespace WuxiaProj.Combat;

public sealed class BeforeApplyBuffHook : HookPoint<ApplyBuffContext> { }
public sealed class AfterApplyBuffHook  : HookPoint<ApplyBuffContext> { }

/// <summary>
/// 施加/移除 Buff 操作的上下文。
/// </summary>
public class ApplyBuffContext : HookContext
{
    public string BuffConfigId { get; init; } = "";
    public int Duration { get; set; }
    public int StackCount { get; set; } = 1;
}
```

- [ ] **Step 4: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 5: 提交**

```bash
git add src/Core/Combat/Effect/Contexts/
git commit -m "feat: 新增 HpModify/UnitMove/ApplyBuff 上下文与 Before/After Hook 定义"
```

---

### Task 6: OpExecutor 管线调度器

**Files:**
- Create: `src/Core/Combat/Effect/Pipeline/OpExecutor.cs`

- [ ] **Step 1: 创建 OpExecutor.cs**

```csharp
using System;
using System.Collections.Generic;
using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// 原子操作管线调度器。每个 IAtomicOp 执行时经过：
///   组装上下文 → Fire Before → 检查 Cancel → Execute → Fire After → 执行追加指令
/// </summary>
public class OpExecutor
{
    private readonly HookBus _hookBus;
    private const int MaxRecursionDepth = 5;

    public OpExecutor(HookBus hookBus)
    {
        _hookBus = hookBus;
    }

    /// <summary>
    /// 执行一个原子操作，走完整管线。
    /// </summary>
    /// <param name="op">要执行的原子操作</param>
    /// <param name="context">操作对应的具体 HookContext 实例（由调用方组装好元信息）</param>
    public void Execute(IAtomicOp op, HookContext context)
    {
        // Step 1: Fire Before Hook（通过反射分发到具体类型）
        FireHook(context, isBefore: true);

        // Step 2: 检查阻断
        if (context.IsCancelled)
            return;

        // Step 3: 执行原子操作
        op.Execute(context);

        // Step 4: Fire After Hook
        FireHook(context, isBefore: false);

        // Step 5: 执行追加指令
        if (context.AppendedOps.Count == 0)
            return;

        if (context.RecursionDepth >= MaxRecursionDepth)
        {
            GD.PushWarning(
                $"[OpExecutor] 递归深度超限 ({MaxRecursionDepth})，" +
                $"丢弃 {context.AppendedOps.Count} 条追加指令");
            return;
        }

        var appendedOps = context.AppendedOps.ToArray();
        context.AppendedOps.Clear();

        foreach (var appendedOp in appendedOps)
        {
            // 为追加指令创建子上下文，继承黑板并递增深度
            var childContext = CreateChildContext(context);
            Execute(appendedOp, childContext);
        }
    }

    /// <summary>
    /// 通过反射找到 context 类型对应的 BeforeXxxHook / AfterXxxHook 并 Fire。
    /// 具体实现：typeof(context) → 找到 BeforeXxxHook 子类 → _hookBus.Fire<THook>
    /// </summary>
    private void FireHook(HookContext context, bool isBefore)
    {
        var contextType = context.GetType();
        var suffix = isBefore ? "BeforeHook" : "AfterHook";

        // 约定：context 类名 = "HpModifyContext" → BeforeHpModifyHook / AfterHpModifyHook
        var contextName = contextType.Name;             // "HpModifyContext"
        var baseName = contextName.Replace("Context", ""); // "HpModify"
        var hookTypeName = $"WuxiaProj.Combat.{(isBefore ? "Before" : "After")}{baseName}Hook";
        var hookType = contextType.Assembly.GetType(hookTypeName);

        if (hookType == null)
        {
            GD.PushWarning($"[OpExecutor] 找不到 Hook 类型: {hookTypeName}");
            return;
        }

        // 调用 _hookBus.GetType().GetMethod("Fire").MakeGenericMethod(hookType).Invoke(...)
        var fireMethod = typeof(HookBus).GetMethod("Fire")!.MakeGenericMethod(hookType);
        fireMethod.Invoke(_hookBus, new object[] { context });
    }

    /// <summary>
    /// 为追加指令创建子上下文——继承黑板快照，递增递归深度。
    /// 不拷贝预计算标记（子上下文由调用方重新组装）。
    /// </summary>
    private static HookContext CreateChildContext(HookContext parent)
    {
        var child = (HookContext)Activator.CreateInstance(parent.GetType())!;
        child.GetType().GetProperty(nameof(HookContext.Blackboard))!
            .SetValue(child, parent.Blackboard.Snapshot());
        child.GetType().GetProperty(nameof(HookContext.RecursionDepth))!
            .SetValue(child, parent.RecursionDepth + 1);
        // 拷贝元信息
        child.GetType().GetProperty(nameof(HookContext.SourceUnit))!
            .SetValue(child, parent.SourceUnit);
        child.GetType().GetProperty(nameof(HookContext.TargetUnit))!
            .SetValue(child, parent.TargetUnit);
        return child;
    }
}
```

> 注：FireHook 的反射实现是过渡方案。后续可改为在 HookContext 子类上标注 `[HookTypes(BeforeHpModifyHook, AfterHpModifyHook)]` 特性以消除字符串拼接。

- [ ] **Step 2: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 3: 提交**

```bash
git add src/Core/Combat/Effect/Pipeline/OpExecutor.cs
git commit -m "feat: 新增 OpExecutor 管线调度器（Before/After Hook + 追加指令）"
```

---

### Task 7: 原子操作实现

**Files:**
- Create: `src/Core/Combat/Effect/AtomicOp/ModifyHpOp.cs`
- Create: `src/Core/Combat/Effect/AtomicOp/MoveUnitOp.cs`
- Create: `src/Core/Combat/Effect/AtomicOp/ApplyBuffOp.cs`

- [ ] **Step 1: 创建 ModifyHpOp.cs**

```csharp
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
        // 实际 HP 修改由 UnitBattleState 执行，此处为管线占位
    }
}
```

- [ ] **Step 2: 创建 MoveUnitOp.cs**

```csharp
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
        // 实际位移由 CombatGrid 执行
    }
}
```

- [ ] **Step 3: 创建 ApplyBuffOp.cs**

```csharp
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
        // 实际 Buff 施加由 BuffManager 执行
    }
}
```

- [ ] **Step 4: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 5: 提交**

```bash
git add src/Core/Combat/Effect/AtomicOp/ModifyHpOp.cs src/Core/Combat/Effect/AtomicOp/MoveUnitOp.cs src/Core/Combat/Effect/AtomicOp/ApplyBuffOp.cs
git commit -m "feat: 新增 ModifyHpOp / MoveUnitOp / ApplyBuffOp 原子操作"
```

---

### Task 8: EffectEngine 效果引擎

**Files:**
- Create: `src/Core/Combat/Effect/EffectEngine.cs`

- [ ] **Step 1: 创建 EffectEngine.cs**

```csharp
using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// 效果引擎。持有 OpExecutor 和 HookBus，
/// 对外提供统一的效果执行入口。
/// </summary>
public class EffectEngine
{
    public HookBus HookBus { get; }
    public OpExecutor Executor { get; }

    public EffectEngine()
    {
        HookBus = new HookBus();
        Executor = new OpExecutor(HookBus);
    }

    /// <summary>
    /// 执行一条原子操作，使用给定的上下文。
    /// 调用方负责组装好上下文（填充 Source/Target 等元信息）。
    /// </summary>
    public void ExecuteOp(IAtomicOp op, HookContext context)
    {
        Executor.Execute(op, context);
    }

    /// <summary>
    /// 批量执行一组原子操作。每个操作走完整管线。
    /// </summary>
    public void ExecuteOps(IEnumerable<IAtomicOp> ops, HookContext sharedContext)
    {
        foreach (var op in ops)
            Executor.Execute(op, sharedContext);
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 3: 提交**

```bash
git add src/Core/Combat/Effect/EffectEngine.cs
git commit -m "feat: 新增 EffectEngine 效果引擎（集成 HookBus + OpExecutor）"
```

---

### Task 9: Buff 配置与运行时类型

**Files:**
- Create: `src/Core/Combat/Effect/Buff/BuffDurationType.cs`
- Create: `src/Core/Combat/Effect/Buff/BuffStackRule.cs`
- Create: `src/Core/Combat/Effect/Buff/BuffConfig.cs`
- Create: `src/Core/Combat/Effect/Buff/BuffState.cs`
- Create: `src/Core/Combat/Effect/Buff/BuffDelegate.cs`

- [ ] **Step 1: 创建枚举**

`BuffDurationType.cs`：

```csharp
namespace WuxiaProj.Combat;

public enum BuffDurationType
{
    Turn,    // 回合数
    Time,    // 秒数
    Permanent // -1 = 永久
}
```

`BuffStackRule.cs`：

```csharp
namespace WuxiaProj.Combat;

public enum BuffStackRule
{
    Replace,      // 覆盖（刷新持续时间和层数）
    Extend,       // 只刷新持续时间
    Independent,  // 独立存在，每次都是新实例
    Stack         // 叠加层数（不超 MaxStacks）
}
```

- [ ] **Step 2: 创建 BuffConfig.cs**

```csharp
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
    public int Duration { get; init; }  // -1 = 永久
    public BuffStackRule StackRule { get; init; } = BuffStackRule.Replace;
    public int MaxStacks { get; init; } = 1;
    public string Tag { get; init; } = "";  // bleed / poison 等标签

    /// <summary>
    /// Hook 配置列表。每个条目声明：hookType、condition、actions。
    /// 在运行时由 BuffEffectCompiler 编译为 BuffDelegate。
    /// </summary>
    public List<BuffHookEntry> Hooks { get; init; } = new();
}

/// <summary>
/// 单个 Hook 条目。对应 Buff JSON 配置中 hooks 数组的一个元素。
/// </summary>
public class BuffHookEntry
{
    public string HookType { get; init; } = "";      // "BeforeHpModifyHook"
    public string Condition { get; init; } = "";      // "ctx.TargetUnit == self"
    public List<object> Actions { get; init; } = new(); // 字符串或嵌套对象
}
```

- [ ] **Step 3: 创建 BuffDelegate.cs**

```csharp
using System;
using System.Collections.Generic;

namespace WuxiaProj.Combat;

/// <summary>
/// 编译后的 Buff 效果委托容器。
/// 内部持有 HookType → Action<HookContext> 映射。
/// </summary>
public class BuffDelegate
{
    public Dictionary<Type, Action<HookContext>> Handlers { get; } = new();
}
```

- [ ] **Step 4: 创建 BuffState.cs**

```csharp
namespace WuxiaProj.Combat;

/// <summary>
/// Buff 运行时状态。每个施加在单位上的 Buff 持有一个实例。
/// </summary>
public class BuffState
{
    public CombatBuffId Id { get; init; }
    public string ConfigId { get; init; } = "";
    public BuffDelegate? CompiledEffects { get; init; }
    public int RemainingDuration { get; set; }
    public int CurrentStacks { get; set; } = 1;
    public CombatUnitId SourceUnit { get; init; }

    public bool IsExpired => RemainingDuration == 0;
}
```

- [ ] **Step 5: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 6: 提交**

```bash
git add src/Core/Combat/Effect/Buff/BuffDurationType.cs src/Core/Combat/Effect/Buff/BuffStackRule.cs src/Core/Combat/Effect/Buff/BuffConfig.cs src/Core/Combat/Effect/Buff/BuffState.cs src/Core/Combat/Effect/Buff/BuffDelegate.cs
git commit -m "feat: 新增 Buff 配置/运行时类型与 BuffDelegate"
```

---

### Task 10: BuffEffectCompiler 表达式编译器

**Files:**
- Create: `src/Core/Combat/Effect/Buff/BuffEffectCompiler.cs`

这是本计划中最复杂的组件。分步实现核心解析能力。

- [ ] **Step 1: 创建 BuffEffectCompiler 骨架 + 简单赋值解析**

```csharp
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace WuxiaProj.Combat;

/// <summary>
/// 将 BuffConfig 中的条件字符串和动作字符串编译为 Expression Tree 委托。
/// 支持的表达式子集见 spec §6.2。
/// </summary>
public static class BuffEffectCompiler
{
    /// <summary>
    /// 编译一个 Buff 配置的所有 Hook 条目 → BuffDelegate。
    /// </summary>
    public static BuffDelegate Compile(BuffConfig config)
    {
        var result = new BuffDelegate();

        foreach (var entry in config.Hooks)
        {
            var hookType = ResolveHookType(entry.HookType);
            if (hookType == null)
                continue;

            var contextType = hookType.BaseType?.GetGenericArguments()[0];
            if (contextType == null)
                continue;

            // 构建 Expression<Action<TContext>>
            var handler = CompileEntry(entry, contextType);
            if (handler != null)
                result.Handlers[hookType] = handler;
        }

        return result;
    }

    /// <summary>
    /// 根据 Hook 类型名查找 Type。例："BeforeHpModifyHook" → typeof(BeforeHpModifyHook)
    /// </summary>
    private static Type? ResolveHookType(string hookTypeName)
    {
        var fullName = $"WuxiaProj.Combat.{hookTypeName}";
        return Type.GetType(fullName)
            ?? typeof(BuffEffectCompiler).Assembly.GetType(fullName);
    }

    /// <summary>
    /// 编译单个 Hook 条目 → Action<HookContext>。
    /// </summary>
    private static Action<HookContext>? CompileEntry(BuffHookEntry entry, Type contextType)
    {
        try
        {
            // 参数: TContext ctx
            var ctxParam = Expression.Parameter(contextType, "ctx");

            // 解析条件 → Expression (bool)
            Expression? conditionExpr = string.IsNullOrEmpty(entry.Condition)
                ? null
                : ParseCondition(entry.Condition, ctxParam);

            // 解析动作列表 → Expression
            var actionExprs = new List<Expression>();
            foreach (var action in entry.Actions)
            {
                var expr = ParseAction(action, ctxParam);
                if (expr != null)
                    actionExprs.Add(expr);
            }

            if (actionExprs.Count == 0)
                return null;

            var body = conditionExpr != null
                ? Expression.IfThen(conditionExpr, Expression.Block(actionExprs))
                : Expression.Block(actionExprs);

            // Lambda: (TContext ctx) => { body }
            var lambda = Expression.Lambda<Action<HookContext>>(
                Expression.Block(new[] { ctxParam }, body),
                ctxParam); // 这里类型不匹配——ctxParam 是 TContext 但 Lambda 要 Action<HookContext>

            return lambda.Compile();
        }
        catch (Exception ex)
        {
            Godot.GD.PushError($"[BuffEffectCompiler] 编译 Hook {entry.HookType} 失败: {ex.Message}");
            return null;
        }
    }

    // ---- 后续步骤补全 ----

    private static Expression ParseCondition(string condition, ParameterExpression ctxParam)
    {
        // TODO: 实现字符串 → Expression 的解析器
        throw new NotImplementedException();
    }

    private static Expression? ParseAction(object action, ParameterExpression ctxParam)
    {
        // TODO: 实现动作字符串/对象 → Expression 的解析器
        throw new NotImplementedException();
    }
}
```

- [ ] **Step 2: 实现 ParseCondition**

解析类 C# 条件表达式，支持：`&&`, `||`, `==`, `!=`, `<`, `>`, `!`，访问 `ctx.Property` 和 `ctx.Blackboard.Get<T>("key")`。

完整代码见本节末尾。

- [ ] **Step 3: 实现 ParseAction**

解析动作表达式，支持：
- 简单赋值：`ctx.Amount = val`
- 黑板写：`ctx.Blackboard.Set("key", value)`
- 阻断：`ctx.IsCancelled = true`
- if/else：嵌套对象 `{ "if": "...", "then": [...], "else": [...] }`
- for：`for (int i = 0; i < ctx.Blackboard.Get<int>("n"); i++) { ... }`
- while：`while (ctx.Blackboard.Get<int>("x") > 0) { ... }`
- foreach：`foreach (var u in ctx.AdjacentEnemies) { ... }`

完整代码见本节末尾。

- [ ] **Step 4: 解决 Lambda 类型转换**

`ctxParam` 是 `TContext` 类型，但 `BuffDelegate.Handlers` 的签名是 `Action<HookContext>`。需要在 Lambda 内部做一次类型转换：

```csharp
// 最终 Lambda: (HookContext ctx) => { var typed = (TContext)ctx; body using typed; }
var baseParam = Expression.Parameter(typeof(HookContext), "ctx");
var typedVar = Expression.Variable(contextType, "typed");
var cast = Expression.Assign(typedVar, Expression.Convert(baseParam, contextType));

var innerBody = conditionExpr != null
    ? Expression.IfThen(conditionExpr, Expression.Block(actionExprs))
    : Expression.Block(actionExprs);

var block = Expression.Block(
    new[] { typedVar },
    cast,
    innerBody);

var lambda = Expression.Lambda<Action<HookContext>>(block, baseParam);
return lambda.Compile();
```

- [ ] **Step 5: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

> 注：Step 5 应在 ParseCondition 和 ParseAction 具体实现完成并解决所有 NotImplementedException 后执行。

- [ ] **Step 6: 提交**

```bash
git add src/Core/Combat/Effect/Buff/BuffEffectCompiler.cs
git commit -m "feat: 新增 BuffEffectCompiler 表达式编译器（JSON → Expression Tree）"
```

---

### Task 11: BuffManager 集成 HookBus

**Files:**
- Create: `src/Core/Combat/Effect/Buff/BuffManager.cs`

- [ ] **Step 1: 创建 BuffManager.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// Buff 管理器。管理单位身上所有 Buff 的生命周期（施加/移除/Tick），
/// 并在施加/移除时自动向 HookBus 注册/注销编译后的委托。
/// </summary>
public partial class BuffManager : Node
{
    private readonly List<BuffState> _activeBuffs = new();
    private readonly HookBus _hookBus;

    public IReadOnlyList<BuffState> ActiveBuffs => _activeBuffs.AsReadOnly();

    public BuffManager(HookBus hookBus)
    {
        _hookBus = hookBus;
    }

    /// <summary>
    /// 从 BuffConfig 创建一个 BuffState 实例。
    /// 施加时自动向 HookBus 注册编译效果。
    /// </summary>
    public void Apply(BuffConfig config, CombatUnitId sourceUnit)
    {
        // 编译 Buff 效果（首次编译，后续可缓存）
        var compiled = BuffEffectCompiler.Compile(config);

        // 处理叠加规则
        HandleStackRule(config);

        var state = new BuffState
        {
            Id = CombatBuffId.New(),
            ConfigId = config.Id,
            CompiledEffects = compiled,
            RemainingDuration = config.Duration,
            CurrentStacks = 1,
            SourceUnit = sourceUnit
        };

        _activeBuffs.Add(state);
        RegisterHooks(state);

        GD.Print($"[BuffManager] 施加 Buff: {config.DisplayName} " +
                 $"(ID: {state.Id}, 持续: {config.Duration})");
    }

    /// <summary>
    /// 移除 Buff 实例，自动注销其 Hook。
    /// </summary>
    public void Remove(CombatBuffId buffId)
    {
        var state = _activeBuffs.Find(b => b.Id == buffId);
        if (state == null) return;

        UnregisterHooks(state);
        _activeBuffs.Remove(state);
        GD.Print($"[BuffManager] 移除 Buff: {state.ConfigId}");
    }

    /// <summary>
    /// 按标签移除所有匹配的 Buff。
    /// </summary>
    public void RemoveByTag(string tag, BuffConfig[] allConfigs)
    {
        var configMap = allConfigs.ToDictionary(c => c.Id);
        var toRemove = _activeBuffs
            .Where(b => configMap.TryGetValue(b.ConfigId, out var c) && c.Tag == tag)
            .ToList();

        foreach (var buff in toRemove)
            Remove(buff.Id);
    }

    /// <summary>
    /// 回合结束 Tick。Turn 型 Buff 持续 -1，到期移除。
    /// </summary>
    public void TickTurn(BuffConfig[] allConfigs)
    {
        var configMap = allConfigs.ToDictionary(c => c.Id);
        var expired = new List<CombatBuffId>();

        foreach (var buff in _activeBuffs)
        {
            if (!configMap.TryGetValue(buff.ConfigId, out var config)) continue;
            if (config.DurationType != BuffDurationType.Turn) continue;
            if (config.Duration <= 0) continue; // Permanent

            buff.RemainingDuration--;
            if (buff.RemainingDuration <= 0)
                expired.Add(buff.Id);
        }

        foreach (var id in expired)
            Remove(id);
    }

    /// <summary>
    /// 查询某属性类型的所有 Buff 修正总和。
    /// </summary>
    public int GetAttributeModifier(string attrName)
    {
        // MVP 阶段返回 0，后续接入属性修正数据
        return 0;
    }

    private void HandleStackRule(BuffConfig config)
    {
        var existing = _activeBuffs.Find(b => b.ConfigId == config.Id);
        if (existing == null) return;

        switch (config.StackRule)
        {
            case BuffStackRule.Replace:
                Remove(existing.Id);
                break;
            case BuffStackRule.Extend:
                existing.RemainingDuration = config.Duration;
                break;
            case BuffStackRule.Independent:
                break; // 不处理，新 Buff 独立追加
            case BuffStackRule.Stack:
                if (existing.CurrentStacks < config.MaxStacks)
                    existing.CurrentStacks++;
                existing.RemainingDuration = config.Duration;
                break;
        }
    }

    private void RegisterHooks(BuffState state)
    {
        if (state.CompiledEffects == null) return;

        foreach (var (hookType, handler) in state.CompiledEffects.Handlers)
        {
            var compiledHook = new CompiledBuffHook(hookType, handler, state);
            _hookBus.GetType()
                .GetMethod("Register")!
                .MakeGenericMethod(hookType)
                .Invoke(_hookBus, new object[] { compiledHook });
        }
    }

    private void UnregisterHooks(BuffState state)
    {
        if (state.CompiledEffects == null) return;

        foreach (var (hookType, handler) in state.CompiledEffects.Handlers)
        {
            var compiledHook = new CompiledBuffHook(hookType, handler, state);
            _hookBus.GetType()
                .GetMethod("Unregister")!
                .MakeGenericMethod(hookType)
                .Invoke(_hookBus, new object[] { compiledHook });
        }
    }
}

/// <summary>
/// 编译生成的 Hook 适配器。将编译后的 Action<HookContext> 包装为 IBuffHook<TContext>。
/// </summary>
internal sealed class CompiledBuffHook : IBuffHook
{
    private readonly Action<HookContext> _handler;

    public int Priority { get; }

    public CompiledBuffHook(Type hookType, Action<HookContext> handler, BuffState state)
    {
        _handler = handler;
        Priority = (int)state.Id.Value * 10;
    }

    public void OnHook(HookContext context)
    {
        _handler(context);
    }
}
```

> `CompiledBuffHook` 直接实现非泛型 `IBuffHook`。`HookBus.Register<TContext>` 的泛型参数 `TContext` 仅用于确定 Hook 类型键，实际调用走 `IBuffHook.OnHook(HookContext)`，编译委托内部已完成 `HookContext → TContext` 的类型转换。

- [ ] **Step 2: 构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 3: 提交**

```bash
git add src/Core/Combat/Effect/Buff/BuffManager.cs
git commit -m "feat: 新增 BuffManager（生命周期管理 + HookBus 自动注册/注销）"
```

---

### Task 12: 端到端集成验证

**Files:**
- Create: `src/Core/Combat/CombatTest.cs`（临时验证脚本，验证通过后删除）

- [ ] **Step 1: 创建临时验证脚本**

```csharp
using Godot;
using WuxiaProj.Combat;

namespace WuxiaProj;

/// <summary>
/// 战斗管线集成验证（开发用，非正式测试）。
/// </summary>
public partial class CombatTest : Node
{
    public override void _Ready()
    {
        GD.Print("=== Combat Pipeline Test ===");

        // 1. 创建 EffectEngine（内含 HookBus + OpExecutor）
        var engine = new EffectEngine();

        // 2. 创建 BuffManager 并挂载一个简单 Buff
        var buffManager = new BuffManager(engine.HookBus);

        // 3. 模拟一次 HP 修改操作
        var ctx = new HpModifyContext
        {
            SourceUnit = CombatUnitId.New(),
            TargetUnit = CombatUnitId.New(),
            Amount = -50,
            DamageType = "slash"
        };

        var op = new ModifyHpOp { Amount = ctx.Amount, DamageType = ctx.DamageType };

        GD.Print($"Before: Amount = {ctx.Amount}");
        engine.ExecuteOp(op, ctx);
        GD.Print($"After: Amount = {ctx.Amount}, Cancelled = {ctx.IsCancelled}");

        GD.Print("=== Pipeline Test Complete ===");
    }
}
```

- [ ] **Step 2: 构建并运行**

```bash
dotnet build WuxiaProj.csproj
```

预期：编译通过，无运行时错误。控制台输出管线执行日志。

- [ ] **Step 3: 删除验证脚本**

```bash
rm src/Core/Combat/CombatTest.cs
```

- [ ] **Step 4: 最终构建验证**

```bash
dotnet build WuxiaProj.csproj
```

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "test: 战斗管线端到端集成验证"
```

---

### 后续迭代（本计划范围外）

| 内容 | 说明 |
|------|------|
| `BuffEffectCompiler` 完整解析器 | Task 10 中 ParseCondition/ParseAction 需实现具体解析逻辑（当前为骨架）。这是本设计最大的独立工作量，建议单独开 spec/plan。 |
| `OpExecutor.FireHook` 去反射 | 当前使用反射调用 HookBus.Fire，后续可用 Source Generator 或特性标注消除。 |
| 属性修正接入 | BuffManager.GetAttributeModifier 当前返回 0，需接入属性系统。 |
| UnitBattleState 集成 | 当前原子操作仅打印日志，需接入实际的单位战斗状态。 |
| JSON 配置加载 | BuffConfig 从 ConfigDataManager 加载 JSON 的管线。 |
| 更多原子操作 | KnockbackOp、PullOp、ModifyQiOp 等按需新增。 |

---

最后更新：2026-07-02

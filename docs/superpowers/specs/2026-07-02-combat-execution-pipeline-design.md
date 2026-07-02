# 战斗执行上下文管线设计

> 本文档定义战斗系统中**指令执行管线**与 **Buff 拦截机制**的架构设计。基于 [战斗系统代码框架设计](../plans/2026-07-02-combat-system-code-framework-design.md) 中已定义的 `IAtomicOp`、`EffectEngine`、`BuffManager` 等基础概念，在其上建立带拦截点的执行上下文管线。

## 一、概述

### 1.1 目标

将现有直线式"命令 → 效果引擎 → 原子操作"执行模型升级为**带拦截点的上下文管线**，使 Buff 能够：

- 在执行前**修改**上下文参数（如减半伤害）
- **阻断**原子操作的执行（如护盾挡掉伤害）
- 在执行后**追加**新指令（如受伤后反击、吸血）
- 通过**黑板**在 Buff 间传递临时标记

### 1.2 核心设计决策

| 决策 | 选择 |
|------|------|
| 拦截粒度 | 原子操作级（每个 `IAtomicOp` 独立上下文） |
| Buff 介入模式 | 事件广播（Event Bus）+ 优先级调度 |
| Hook 组织 | 类型化 Hook Point（`HookPoint<TContext>`） |
| 时点拆分 | Before / After 双 Hook |
| Buff 效果执行 | JSON 配置 → `Expression Tree` → 编译委托 |
| 跨 Buff 通信 | 上下文级黑板（值类型 KV，上下文结束即清空） |
| 战斗内 ID | 轻量 `CombatUnitId` / `CombatBuffId`（自增值类型） |

---

## 二、执行管线

### 2.1 管线流程

每条 `IAtomicOp` 执行时经过统一管线：

```
OpExecutor.Execute(op, params)
  │
  ├─ 1. 组装上下文（TContext）
  │     填充 Source/Target 元信息、可变参数、黑板快照、预计算标记
  │
  ├─ 2. Fire Before Hook
  │     HookBus.Fire<BeforeXxxHook>(context)
  │     授权：修改字段、设为 Cancel、SetTag、AppendOp、读写黑板
  │
  ├─ 3. 检查 IsCancelled → 跳过 DoExecute
  │
  ├─ 4. op.DoExecute(context)
  │     原子操作消费上下文中的数据
  │
  ├─ 5. Fire After Hook
  │     HookBus.Fire<AfterXxxHook>(context)
  │     授权：观察结果、AppendOp、读写黑板
  │     已完成执行的字段（如 Amount 已扣血）设为只读语义，
  │     修改它们不会回滚已发生的效果，只能通过追加指令来补偿
  │
  └─ 6. 执行追加指令
        foreach (appended in context.AppendedOps)
          OpExecutor.Execute(appended, ...)    // 递归走同一管线
```

### 2.2 递归安全

追加指令通过同一管线执行，天然支持链式反应。为防止无限递归，设计层面做两项约束：

1. **来源标记**：每个 `IAtomicOp` 在入管线时标记 `RecursionDepth`（从 0 开始，每次 Append 的执行 depth+1）。Buff 可通过条件判断 `ctx.RecursionDepth > 0` 自行决定是否在嵌套调用中响应，避免"吸血后的治疗再次触发吸血"。
2. **硬上限兜底**：深度上限 5，超出时追加指令被丢弃并 `GD.PushWarning`。

设计原则：正常 Buff 组合不应触碰深度上限；如果触发了，说明策划配置了循环触发链，应在配置层面调整，而非依赖上限硬吃。

---

## 三、类型体系

### 3.1 HookPoint\<TContext\>

每个拦截点定义为一个类型标记，携带其专属上下文类型：

```csharp
// 基类 — 空标记，用于 HookBus 的类型键
public abstract class HookPoint<TContext> where TContext : HookContext { }

// 具体定义 — 每个 IAtomicOp 对应一对 Before/After
public sealed class BeforeHpModifyHook  : HookPoint<HpModifyContext> { }
public sealed class AfterHpModifyHook   : HookPoint<HpModifyContext> { }
public sealed class BeforeUnitMoveHook  : HookPoint<UnitMoveContext> { }
public sealed class AfterUnitMoveHook   : HookPoint<UnitMoveContext> { }
public sealed class BeforeApplyBuffHook : HookPoint<ApplyBuffContext> { }
public sealed class AfterApplyBuffHook  : HookPoint<ApplyBuffContext> { }
// ... 每个 IAtomicOp 各一对
```

### 3.2 HookContext（基类）

```csharp
public abstract class HookContext
{
    // -- 阻断 --
    public bool IsCancelled { get; set; }

    // -- 追加 --
    public List<IAtomicOp> AppendedOps { get; }

    // -- 元信息（管线填充，只读） --
    public CombatUnitId SourceUnit { get; init; }
    public CombatUnitId TargetUnit { get; init; }
    public string SourceDiceFaceId { get; init; }  // 可为 null
    public int RecursionDepth { get; init; }

    // -- 黑板 --
    public Blackboard Blackboard { get; init; }

    // -- 预计算标记（管线填充） --
    public bool TargetHasBleed { get; init; }
    public bool TargetHasPoison { get; init; }
    // ... 按需扩展

    // foreach 迭代源由具体上下文子类提供，不在基类中定义
    // 例：HpModifyContext.AdjacentEnemies, UnitMoveContext.BlockedPaths 等
}
```

### 3.3 具体上下文示例

```csharp
public class HpModifyContext : HookContext
{
    public int Amount { get; set; }          // 正=治疗，负=伤害
    public string DamageType { get; init; }  // slash / pierce / fire / pure ...
    public bool CanCrit { get; set; }
}

public class UnitMoveContext : HookContext
{
    public Vector2I From { get; init; }
    public Vector2I To { get; set; }
    public int Distance { get; set; }
}

public class ApplyBuffContext : HookContext
{
    public string BuffConfigId { get; init; }
    public int Duration { get; set; }        // Buff 可修改持续时间
    public int StackCount { get; set; }
}
```

### 3.4 IBuffHook\<TContext\>

```csharp
public interface IBuffHook<TContext> where TContext : HookContext
{
    int Priority { get; }
    void OnHook(TContext context);
}
```

`Priority` 由 BuffManager 在注册时自动分配（基于 Buff 施加顺序 + 类型权重），策划可在 BuffConfig 中手动覆盖。

### 3.5 HookBus

```csharp
public class HookBus
{
    // 维护 Type → SortedList<IBuffHook> 映射
    // Type = typeof(BeforeHpModifyHook) 等

    public void Register<TContext>(IBuffHook<TContext> hook);
    public void Unregister<TContext>(IBuffHook<TContext> hook);
    public void Fire<TContext>(TContext context) where TContext : HookContext;
}
```

`Fire` 内部：查表 → 按 Priority 降序 → 依次调用 `OnHook`。设置 `IsCancelled` 后**仍继续**调用剩余 hook（高优先级 hook 可再次修改），最终由管线检查 `IsCancelled` 决定是否执行 `DoExecute`。

---

## 四、黑板（Blackboard）

### 4.1 设计

```csharp
public class Blackboard
{
    public T Get<T>(string key);
    public void Set<T>(string key, T value);
    public bool TryGet<T>(string key, out T value);
    public bool Has(string key);
    public void Remove(string key);
    public Blackboard Snapshot();  // 深拷贝，供子上下文继承
}
```

| 特性 | 规则 |
|------|------|
| 可读写类型 | `int`、`float`、`bool`、`string`、`CombatUnitId`、`CombatBuffId`、`ObjectId` |
| 不可存放 | 引用类型 — `Set` 时抛 `ArgumentException` |
| 子上下文继承 | 追加指令的新上下文调用 `Snapshot()` 继承父黑板当前值 |
| 生命周期 | 上下文执行完毕后随上下文丢弃 |

### 4.2 典型用法

多个 Buff 通过黑板传递标记，避免互相覆盖：

```
// Buff「铁布衫」(BeforeHpModify)：减半伤害 → 做标记
ctx.Amount = ctx.Amount / 2;
ctx.Blackboard.Set("damage_reduced", true);

// Buff「荆棘光环」(AfterHpModify)：检查标记，未被减伤才反弹
if (!ctx.Blackboard.TryGet<bool>("damage_reduced", out var reduced) || !reduced)
    ctx.AppendedOps.Add(new ModifyHpOp(...));  // 反弹伤害
```

---

## 五、战斗 ID 系统

战斗内使用轻量自增值类型，与全局 `ObjectId` 共存：

```csharp
public readonly struct CombatUnitId : IEquatable<CombatUnitId>
{
    public uint Value { get; }
    internal CombatUnitId(uint value) => Value = value;
}

public readonly struct CombatBuffId : IEquatable<CombatBuffId>
{
    public uint Value { get; }
    internal CombatBuffId(uint value) => Value = value;
}
```

| ID 类型 | 分配者 | 生命周期 |
|--------|--------|----------|
| `CombatUnitId` | `CombatManager` | 进入战斗 → 战斗结束 |
| `CombatBuffId` | `BuffManager` | Buff 施加 → Buff 移除 |

不需要全局锁（战斗内单线程执行），可安全放入黑板（值类型）。

---

## 六、Buff 编译系统

### 6.1 配置格式（JSON）

```json
{
  "id": "buff_iron_skin",
  "displayName": "铁布衫",
  "durationType": "turn",
  "duration": 3,
  "stackRule": "replace",
  "hooks": [
    {
      "hookType": "BeforeHpModifyHook",
      "condition": "ctx.TargetUnit == self && ctx.DamageType != 'pure'",
      "actions": [
        "ctx.Amount = (int)(ctx.Amount * 0.5)",
        "ctx.Blackboard.Set('damage_reduced', true)"
      ]
    },
    {
      "hookType": "AfterHpModifyHook",
      "condition": "ctx.SourceUnit == self && ctx.Amount < 0",
      "actions": [
        "ctx.Blackboard.Set('damage_dealt', -ctx.Amount)",
        {
          "if": "ctx.Blackboard.Get<int>('damage_dealt') > 10",
          "then": [
            "ctx.AppendOp('ApplyBuff', { target: ctx.TargetUnit, buffId: 'buff_heavy_strike' })"
          ]
        }
      ]
    }
  ]
}
```

### 6.2 表达式子集

配置中的 `condition` 和 `actions` 编译为 `Expression<Action<TContext>>`，支持以下能力：

| 能力 | 示例 |
|------|------|
| 读上下文字段 | `ctx.Amount`、`ctx.DamageType` |
| 写可修改字段 | `ctx.Amount = val`、`ctx.IsCancelled = true` |
| 算术 | `+` `-` `*` `/` `%` |
| 比较/逻辑 | `==` `!=` `<` `>` `&&` `\|\|` `!` |
| 黑板读写 | `ctx.Blackboard.Get<int>("key")`、`.Set("k", v)` |
| 追加指令 | `ctx.AppendOp("ModifyHp", { target, amount })` |
| 读取元信息 | `ctx.SourceUnit`、`ctx.TargetUnit`、`ctx.RecursionDepth` |
| 条件分支 | `if (cond) { ... } else { ... }` |
| foreach | `foreach (var u in ctx.AdjacentEnemies) { ... }` |
| for（黑板计数） | `for (int i = 0; i < ctx.Blackboard.Get<int>("n"); i++) { ... }` |
| while（黑板条件） | `while (ctx.Blackboard.Get<int>("remaining") > 0) { ... }` |

**循环边界：**
- `foreach`：集合由上下文提供，长度自然限制
- `for`：循环次数在首次迭代时取值**快照**，之后黑板变化不影响循环次数
- `while`：每轮从黑板重新读取，值 ≤0 退出；硬上限 1000 次（兜底告警）

### 6.3 编译流程

```
BuffConfig (JSON)
  │
  ├─ BuffEffectCompiler.Compile(config)
  │
  │   对每个 hook entry:
  │     ├─ 解析 condition 字符串 → Expression<Func<TContext, bool>>
  │     ├─ 解析 actions 列表 → List<Expression>
  │     │     ├─ 简单语句 → Expression.Assign / Expression.Call ...
  │     │     ├─ if/else   → Expression.IfThenElse
  │     │     ├─ foreach   → Expression.ForEach
  │     │     ├─ for       → Expression.Loop + counter
  │     │     └─ while     → Expression.Loop + break check
  │     └─ 组合: Expression.IfThen(condition, Expression.Block(actions))
  │
  └─ Lambda.Compile() → Action<TContext>
      → 存入 BuffDelegate.Handlers[hookType]
```

### 6.4 运行时

```csharp
// Buff 施加时
public class BuffState
{
    public CombatBuffId Id { get; init; }
    public BuffDelegate CompiledEffects { get; init; }
    public int RemainingDuration { get; set; }
    public CombatUnitId SourceUnit { get; init; }
}
```

`BuffDelegate` 内部持有一个 `Dictionary<Type, Action<HookContext>>`，HookBus 的 `Fire` 被调用时遍历 Buff → 找到匹配的 Handler → 直接调用委托。

编译发生在 Buff 配置加载时（`ConfigDataManager` 加载 JSON → `BuffEffectCompiler.Compile`），运行时零解析开销。

> **实现注记**：`BuffEffectCompiler` 需实现一个从字符串到 `Expression` 的小型解析器，这是本设计的核心复杂点。解析器只支持受控的表达式子集（见 6.2），不实现完整 C# 语法。条件表达式和动作语句的语法约定将在实现阶段细化。

---

## 七、与现有系统的关系

### 7.1 对现有 IAtomicOp 的改动

现有的 `IAtomicOp.Execute(EffectContext context, Dictionary<string, object> @params)` 不变，管线在其外层包裹 Before/After Hook：

```
旧：EffectEngine → op.Execute(ctx, params)
新：OpExecutor → Hook → op.DoExecute(ctx) → Hook → Append
```

`EffectEngine` 内部改为调用 `OpExecutor` 替代直接调 `op.Execute`。

### 7.2 对现有 BuffManager 的改动

现有 `BuffManager` 仅管理 Buff 生命周期（施加/移除/Tick），需新增：
- `RegisterHook` / `UnregisterHook`：Buff 施加时自动向 `HookBus` 注册其编译后的委托
- Buff 移除时自动从 `HookBus` 注销

### 7.3 对现有 CommandQueue 的影响

无直接影响。`ICommand` 和 `CommandQueue` 照常使用，变化发生在命令被分解为原子操作后进入 `OpExecutor` 的阶段。

---

## 八、扩展点

- **新增 Hook**：定义新类继承 `HookPoint<TContext>` + 在 `OpExecutor` 中对应 `IAtomicOp` 的执行前后 Fire
- **新增预计算标记**：在 `HookContext` 基类加属性 + `OpExecutor` 组装时填充
- **新增表达式能力**：扩展 `BuffEffectCompiler` 的解析器（不影响已编译的 Buff）

---

## 九、目录结构

```
src/Core/Combat/
├── Effect/
│   ├── AtomicOp/          ← IAtomicOp, ModifyHpOp, ...（已有）
│   ├── Pipeline/
│   │   ├── OpExecutor.cs          ← 管线调度器
│   │   ├── HookPoint.cs           ← HookPoint<TContext> 基类
│   │   ├── HookContext.cs         ← HookContext 基类
│   │   ├── HookBus.cs             ← 事件总线
│   │   ├── IBuffHook.cs           ← Buff Hook 接口
│   │   └── Blackboard.cs          ← 黑板
│   ├── Buff/
│   │   ├── BuffConfig.cs          ← Buff 配置数据（已有，扩展 hook entries）
│   │   ├── BuffState.cs           ← Buff 运行时（已有，扩展 BuffDelegate）
│   │   ├── BuffManager.cs         ← Buff 管理器（已有，扩展 Hook 注册）
│   │   ├── BuffDelegate.cs        ← 编译产物持有者
│   │   └── BuffEffectCompiler.cs  ← JSON → Expression Tree 编译器
│   └── Contexts/
│       ├── HpModifyContext.cs
│       ├── UnitMoveContext.cs
│       ├── ApplyBuffContext.cs
│       └── ...（每个 IAtomicOp 一个）
├── CombatUnitId.cs                ← 战斗单位 ID
├── CombatBuffId.cs                ← Buff 实例 ID
└── ...
```

---

## 相关文档

- [[2026-07-02-combat-system-code-framework-design|战斗系统代码框架设计]] — 本文档的承载基础（IAtomicOp / BuffManager / EffectEngine）
- [[2026-06-15-combat-system-detailed-design|战斗系统详细设计]] — 战斗玩法设计（骰子、行动、Buff 概念）
- [[2026-06-25-attribute-system-design|属性系统设计]] — 属性体系定义
- [[2026-07-02-framework-scaffold-design|游戏基础框架脚手架设计]] — Manager 架构

---

最后更新：2026-07-02

# 战斗系统代码框架设计

> 本文档定义战斗系统的 C# 代码框架，基于 [战斗系统详细设计](2026-06-15-combat-system-detailed-design.md) 与 [属性系统设计](2026-06-25-attribute-system-design.md)。

## 一、架构决策

| 决策项 | 选择 | 说明 |
|--------|------|------|
| 目标范围 | 核心骨架 | 只搭建数据模型与流程骨架，具体逻辑（伤害公式、AI 等）后续填充 |
| 驱动模式 | 命令队列 | 玩家/系统操作封装为 `ICommand`，支持 Undo（移动回溯）和回放 |
| 目录组织 | 按子系统分目录 | `src/Core/Combat/Dice/`、`Effect/`、`Action/`、`Unit/`、`Grid/` |
| 数据分离 | Config/State 双轨 | 配置类用 `*Config` 后缀，运行时类用 `*State` 后缀 |
| 效果系统 | 数据驱动 | C# 侧提供原子操作引擎，具体效果由数据组合配方定义 |
| Buff 系统 | Config/State 双轨 + 数据驱动 | 复用效果引擎，框架只管理生命周期 |

---

## 二、目录结构

```
src/Core/Combat/
├── Dice/
│   ├── Config/          ← DiceFaceConfig, DiceSetConfig（骰面/骰体静态配置）
│   ├── State/           ← DiceInstanceState（运行时骰子实例）
│   ├── DiceSourceType.cs
│   └── DiceTriggerType.cs
├── Effect/
│   ├── AtomicOp/        ← IAtomicOp, ModifyHpOp, ModifyQiOp, ModifyActionPointOp, ...
│   ├── Buff/            ← BuffConfig, BuffState, BuffManager, BuffDurationType, BuffStackRule
│   ├── EffectRecipe.cs  ← 效果配方（原子操作序列 + 参数）
│   ├── EffectConfig.cs  ← 效果模板配置（ID → EffectRecipe 列表）
│   └── EffectEngine.cs  ← 解析配方 → 依次调用原子操作
├── Action/
│   ├── Command/         ← ICommand, MoveCommand, AttackCommand, SkillCommand, ...
│   ├── CommandQueue.cs  ← 命令队列（Execute / Undo）
│   ├── ActionCost.cs
│   └── ActionResult.cs
├── Unit/
│   ├── Config/          ← UnitTemplateConfig
│   ├── State/           ← UnitBattleState, AttributeSnapshot
│   └── AttributeType.cs
├── Grid/
│   ├── BattleGrid.cs
│   ├── GridCell.cs
│   ├── PathFinder.cs
│   └── BattlefieldGenerator.cs
├── Turn/
│   ├── TurnPhase.cs
│   └── TurnStateMachine.cs
├── CombatManager.cs     ← 战斗总控
└── CombatConfig.cs      ← 战斗事件配置
```

---

## 三、核心子系统

### 3.1 命令队列

所有玩家操作和系统动作封装为 `ICommand`：

```csharp
public interface ICommand
{
    List<EffectRecipe> Execute(UnitBattleState executor);
    void Undo(UnitBattleState executor);
    bool CanUndo { get; }
    ActionCost Cost { get; }
}
```

**命令类型**：
- `MoveCommand` — 移动（可 Undo，仅当后续命令非 Move 时）
- `AttackCommand` — 攻击（消耗骰子 + 行动力）
- `SkillCommand` — 武学（基础骰 + 联动骰 + 行动力）
- `ItemCommand` — 物品
- `WaitCommand` — 等待（牺牲 50% 行动条，保留骰子触发蓄力效果）
- `EndTurnCommand` — 结束回合
- `RollAllDiceCommand` — 投全部骰子（系统命令）
- `CompositeCommand` — 组合命令（基础骰 + 联动骰打包执行）

**Undo 约束**：只有 `MoveCommand` 支持 Undo。玩家执行移动后可回溯到回合开始位置；一旦执行非移动命令，Undo 栈清空，位置确认——与文档"执行一个非移动行为即确认位置"一致。

### 3.2 回合状态机

```
TurnPhase 流程：
  EnterBattle → RollPhase → ActionPhase → EndPhase → NextUnit → RollPhase...
  
  EnterBattle 只在战斗开始时执行一次（触发入场效果）
```

- `EnterBattle`：初始化网格、生成单位、触发所有骰子的入场效果
- `RollPhase`：系统投出当前单位所有骰子 → UI 展示 → 执行骰子开始效果
- `ActionPhase`：玩家提交 `ICommand` → `EffectEngine` 结算 → 循环直到行动力耗尽或玩家主动结束
- `EndPhase`：未用骰子触发留用效果 → 回收骰子 → Buff 持续 -1 → 切换下一单位

### 3.3 效果系统（数据驱动）

C# 侧只提供**原子操作引擎**，具体效果由数据配方组合定义。

#### 原子操作接口

```csharp
public interface IAtomicOp
{
    void Execute(EffectContext context, Dictionary<string, object> @params);
}
```

#### 内置原子操作

| 类名 | 功能 | 参数 |
|------|------|------|
| `ModifyHpOp` | 修改气血 | target, amount, damageType |
| `ModifyQiOp` | 修改内力 | target, amount |
| `ModifyActionPointOp` | 修改行动力 | target, amount |
| `ApplyBuffOp` | 施加 Buff | target, buffConfigId |
| `RemoveBuffOp` | 移除 Buff | target, tag |
| `MoveUnitOp` | 位移 | target, distance, direction |
| `KnockbackOp` | 击退 | target, distance, fromPosition |
| `PullOp` | 拉扯 | target, distance, towardPosition |

*后续可按需新增。*

#### 效果配方

一条效果由配方数据定义，是原子操作的组合序列：

```json
{
  "id": "attack_slash_1",
  "effects": [
    { "op": "ModifyHp", "params": { "target": "selected_enemy", "amount": -5, "damageType": "slash" } },
    { "op": "ModifyActionPoint", "params": { "target": "self", "amount": -2 } }
  ]
}
```

#### EffectEngine

```csharp
public class EffectEngine
{
    // 执行单个配方
    public void ExecuteRecipe(EffectRecipe recipe, EffectContext context);
    
    // 执行骰子触发效果（入场/开始/留用/蓄力）
    public void ExecuteTrigger(DiceTriggerType trigger, DiceInstanceState dice, EffectContext context);
    
    // 注册自定义原子操作（后续扩展用）
    public void RegisterOp(string opName, IAtomicOp op);
}
```

### 3.4 骰子数据模型

#### 配置层

**DiceFaceConfig** — 骰面模板：

```
字段：
  Id: string                          ← "attack_slash_1"
  DisplayName: string                 ← "挥砍·轻"
  Effects: List<EffectRecipe>         ← 骰面效果配方
  Cost: ActionCost                    ← 执行开销
  TriggerEffects: Dictionary<DiceTriggerType, List<EffectRecipe>>  ← 入场/开始/留用/蓄力
```

**DiceSetConfig** — 骰体预设（如 "attack_dice_D4"）：

```
字段：
  Id: string                          ← "attack_dice_D4"
  SourceType: DiceSourceType          ← 武功/装备/天赋/才华/成长奖励
  Faces: List<string>                 ← 骰面 ID 列表（初始配置）
  MaxFaces: int                       ← 最大骰面数上限
```

#### 运行时层

**DiceInstanceState** — 一枚骰子的运行时状态：

```
字段：
  ConfigId: string                    ← 引用 DiceSetConfig.Id
  CurrentFaces: List<string>          ← 当前可用骰面 ID
  CustomFaces: List<string>           ← 成长奖励追加的自定义骰面 ID
  FaceEnhancements: Dictionary<string, EnhancementData>  ← 骰面强化数据
  IsExhausted: bool                   ← 本回合是否已打出
  CurrentRollResult: string           ← 本回合投出的骰面 ID（null = 未投）
```

#### 数据关系

```
UnitBattleState
  └── DiceInstances: List<DiceInstanceState>
        └── DiceInstanceState → DiceSetConfig → DiceFaceConfig[]
              └── DiceFaceConfig 持有 EffectRecipe[] → EffectEngine 执行
```

> 骰面强化数据的具体字段待存档系统文档定义后细化。

### 3.5 单位系统

#### UnitTemplateConfig（配置）

```
字段：
  Id: string
  DisplayName: string
  BaseAttributes: Dictionary<AttributeType, int>
  DefaultDiceSetIds: List<string>
  ActionPointTable: List<StaminaStep>    ← 精力 → 行动力阶梯
  MaxActionPoint: int
  MoveDiceSetId: string                  ← 保底移动骰体（默认 "walk_dice_default"）
```

#### UnitBattleState（运行时）

```
字段：
  UnitId: string
  TemplateConfigId: string
  Attributes: AttributeSnapshot          ← 十维属性快照（战斗内不被永久修改）
  CurrentHp / MaxHp / CurrentQi / MaxQi
  ActionPoints / MaxActionPoints
  DiceInstances: List<DiceInstanceState>
  ActiveBuffs: BuffManager
  GridPosition: Vector2I
  IsAlive / IsPlayerControlled
  TeamId: int
```

#### AttributeSnapshot

```
字段：
  Values: Dictionary<AttributeType, int>  ← 基础值（不可变）

方法：
  GetEffectiveValue(attr, buffLayer): int  ← 基础值 + Buff 修正 = 最终值
```

### 3.6 战场网格

#### GridCell

MVP 阶段只有通行性：

```
字段：
  IsPassable: bool
  WorldPosition: Vector2I
```

#### BattlefieldGenerator

实现动态战场生成算法：

```
输入：参战单位坐标、最小尺寸配置、世界地图可通行性矩阵、是否预设固定战场
输出：BattleGrid

算法：
  1. 计算所有单位坐标的包围矩形
  2. 若矩形 < 最小尺寸 → 四向扩展（遇边界停止）
  3. 若配置了固定战场 → 直接使用预设区域
  4. 裁剪世界地图矩阵中对应区域的格子数据
```

#### PathFinder

标准 A* 算法，用于移动骰可达范围预览、点击移动自动寻路、AI 寻路。

### 3.7 Buff 系统

#### BuffConfig（配置）

```
字段：
  Id: string
  DisplayName: string
  DurationType: BuffDurationType      ← 回合 / 时间 / 永久
  Duration: int                       ← -1 = 永久
  StackRule: BuffStackRule            ← Replace / Extend / Independent / Stack
  MaxStacks: int
  OnApplyEffects: List<EffectRecipe>
  OnRemoveEffects: List<EffectRecipe>
  TickEffects: List<EffectRecipe>
  AttributeModifiers: Dictionary<AttributeType, int>
  Tag: string                         ← 标签（bleed/poison），用于驱散/免疫判定
```

#### BuffState（运行时）

```
字段：
  ConfigId: string
  RemainingDuration: int
  CurrentStacks: int
  SourceUnitId: string
  ElapsedTicks: int
```

#### BuffManager

挂载在每个 `UnitBattleState` 上：

```csharp
public class BuffManager
{
    public List<BuffState> ActiveBuffs { get; }

    void Apply(string buffConfigId, string sourceUnitId);    // 施加 Buff
    void RemoveByTag(string tag);                             // 按标签移除
    void TickTurn();                                          // 回合结束 Tick
    void TickTime(float deltaSeconds);                        // 时间 Tick
    int GetAttributeModifier(AttributeType attr);             // 属性修正总和

    event Action<BuffState> OnBuffApplied;
    event Action<BuffState> OnBuffRemoved;
}
```

#### 叠加规则

| 规则 | 行为 |
|------|------|
| `Replace` | 同名 Buff 覆盖（刷新持续时间和层数） |
| `Extend` | 只刷新持续时间，层数不变 |
| `Independent` | 同名 Buff 独立存在，每次施加都是新实例 |
| `Stack` | 叠加层数（不超过 MaxStacks），每层独立计时 |

#### 生命周期

```
Apply → 查配置 → 判断叠加规则 → 执行 OnApplyEffects → 加入 ActiveBuffs
TickTurn (TurnStateMachine.EndPhase 驱动)
  → 遍历 DurationType.Turn 的 Buff
    → RemainingDuration -= 1
    → >0: 执行 TickEffects
    → <=0: 执行 OnRemoveEffects → 从 ActiveBuffs 移除
```

---

## 四、CombatManager 总控

```csharp
public class CombatManager
{
    // 核心子系统
    TurnStateMachine Turn { get; }
    CommandQueue CommandQueue { get; }
    EffectEngine Effects { get; }

    // 战斗数据
    BattleGrid Grid { get; }
    List<UnitBattleState> Units { get; }
    int CurrentUnitIndex { get; }
    CombatConfig Config { get; }

    // 生命周期
    void Initialize(CombatConfig config);
    void Update(float delta);
    void End();

    // UI 事件
    event Action<BattleGrid> OnGridCreated;
    event Action<List<DiceInstanceState>> OnDiceRolled;
    event Action<UnitBattleState> OnTurnStart;
    event Action<List<EffectRecipe>> OnEffectsResolved;
    event Action<CombatResult> OnCombatEnd;
}
```

### 初始化流程

```
Initialize(CombatConfig config)
  ├─ 1. 设置 Config
  ├─ 2. BattlefieldGenerator.Generate → 发布 OnGridCreated
  ├─ 3. 创建 UnitBattleState 列表
  │     ├─ 从模板创建 AttributeSnapshot
  │     ├─ 从模板创建 DiceInstances
  │     └─ 分配 GridPosition
  ├─ 4. 按行动顺序排序（默认身法降序）
  ├─ 5. EnterBattle → 触发所有骰子的入场效果
  └─ 6. TransitionTo(RollPhase)
```

### 驱动循环

```
Update(delta)
  switch CurrentPhase:
    RollPhase:
      ├─ RollAllDiceCommand 入队执行 → OnDiceRolled
      ├─ 执行骰子开始效果
      └─ TransitionTo(ActionPhase)

    ActionPhase:
      ├─ 等待玩家输入（CommandQueue.Enqueue）
      ├─ 每收到命令：Execute → EffectEngine 结算
      └─ EndTurnCommand → TransitionTo(EndPhase)
          WaitCommand → 下一个单位

    EndPhase:
      ├─ 未用骰子触发留用效果
      ├─ 骰子回池
      ├─ BuffManager.TickTurn()
      ├─ 检查胜负条件
      └─ TransitionTo(RollPhase, nextUnit) 或 战斗结束
```

---

## 五、关键约束与设计原则

1. **属性不进入骰池**：属性只做骰面公式缩放输入与门槛判定，详见 [属性系统设计](2026-06-25-attribute-system-design.md)
2. **保底骰体**：空手无武学时持有"挥击骰体"，无轻功时持有"步行"移动骰
3. **配置驱动**：骰面、骰体、效果配方、Buff 均为配置数据，策划可独立调整
4. **命令扩展**：新增行动类型只需实现 `ICommand`，不影响已有命令
5. **效果扩展**：新增效果只需组合已有原子操作；全新机制才需新增 `IAtomicOp`
6. **Undo 约束**：仅移动命令可撤销，非移动命令确认位置后清空 Undo 栈

---

## 六、后续待定项

| 内容 | 归属 |
|------|------|
| 骰面强化数据具体字段 | [[2026-07-02-save-system-design|存档系统代码框架设计]] |
| 伤害/防御等具体公式 | 骰面公式文档（待创建） |
| 属性成长曲线与数值范围 | 成长系统文档（待创建） |
| AI 决策框架 | 本文档后续扩展 |
| Godot 场景/UI 集成 | 本文档后续扩展 |
| 网络同步框架 | 本文档后续扩展（如考虑多人） |
| 战斗回放 | 本文档后续扩展（命令队列天然支持） |

---

## 相关文档

- [[2026-06-15-combat-system-detailed-design|战斗系统详细设计]] — 战斗玩法设计基础（本文档的 C# 实现层）
- [[2026-06-25-attribute-system-design|属性系统设计]] — 属性体系定义
- [[2026-07-02-framework-scaffold-design|游戏基础框架脚手架设计]] — 五管理器架构
- [[2026-07-02-save-system-design|存档系统代码框架设计]] — 战斗状态持久化规范
- [[2026-07-02-ui-mvvm-design|UI MVVM 技术规范]] — 战斗 UI 数据绑定
- [[2026-06-12-wuxia-game-design-outline|总设计大纲]] — 游戏整体设计方向

---

最后更新：2026-07-02

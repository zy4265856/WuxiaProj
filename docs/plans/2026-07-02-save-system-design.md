# 存档系统代码框架设计

## 概述

本文档规定 WuxiaProj 存档系统的技术选型、全链路流程、SaveDataManager 接口草案，以及存档数据类的强制性编写规范。核心原则：**存档一律使用 MemoryPack 二进制格式，每个字段必须在定义中显式声明序列化次序（`[MemoryPackOrder]`）**。

---

## 一、技术选型

### 序列化库

| 项 | 值 |
|----|----|
| 库 | `Cysharp.MemoryPack`（已在 `WuxiaProj.csproj` 引入 v1.21.4） |
| 实现类 | `WuxiaProj.Framework.Serialization.MemoryPackImpl`（`ISerializer` 接口） |
| 生成模式 | `GenerateType.VersionTolerant` |
| 元数据 | `Newtonsoft.Json`（`JsonImpl`，仅用于 `metadata.json` 预览文件） |

### 选择 VersionTolerant 的理由

- **新增字段**：旧档自动以 default 填充，无需迁移代码。
- **删除字段**：旧档残留数据自动跳过，反序列化不报错。
- **代价**：仅两条硬约束——字段类型不可变、Order 序号不可复用。对存档数据结构而言这两条是可接受的控制成本。
- 替代方案对比：手写版本号 + 迁移管道开发成本高、易出错；严格模式导致旧档频繁作废，不适合长周期开发。

---

## 二、全链路流程

### 存档流程

```
Save:  SaveData Object
       → ISerializer.SerializeAsync (MemoryPackImpl)
       → byte[]
       → FileStream 写入 {saveDir}/{slotId}/save.bin
       同时写入 metadata.json（预览信息）
```

### 读档流程

```
Load:  FileStream 读取 {saveDir}/{slotId}/save.bin
       → byte[]
       → ISerializer.DeserializeAsync (MemoryPackImpl)
       → SaveData Object
```

### 存档目录结构

```
user://saves/
├── slot_0/
│   ├── save.bin          # 主存档（MemoryPack 二进制）
│   └── metadata.json     # 元数据（预览用，JSON 明文）
├── slot_1/
│   └── ...
├── slot_2/
│   └── ...
└── settings/
    └── settings.bin      # 系统设置（独立于存档槽位）
```

- `user://` 为 Godot 用户数据路径（`OS.GetUserDataDir()`），各平台自动映射。
- `metadata.json` 用 JSON 而非二进制：存档选择界面只需预览信息，不必反序列化整个存档文件。内容示例：

```json
{
  "version": 1,
  "playerName": "张三",
  "level": 12,
  "playTimeSeconds": 3725,
  "savedAt": "2026-07-02T15:30:00",
  "screenshotPath": "user://saves/slot_0/screenshot.png"
}
```

### 加密 / 压缩（预留）

当前阶段不引入。存档链路："对象 → ISerializer → byte[] → FileStream 写入"天然可插拔，后续可在 byte[] 写入文件前插入压缩/加密中间层，不改 SaveDataManager 内部逻辑。

---

## 三、SaveDataManager 接口草案

### 定位

Autoload 单例，与现有五管理器（ConfigDataManager / ResourceManager / ObjectManager / SceneManager / UiManager）同级。内部组合 `ISerializer`（通过 `ServiceLocator` 注入），不直接依赖具体序列化库。

### 目录与命名空间

- 目录：`src/Framework/Save/`（暂未创建）
- 命名空间：`WuxiaProj.Framework`

### 核心接口

```csharp
public partial class SaveDataManager : Node
{
    public static SaveDataManager Instance { get; private set; } = null!;

    // —— 槽位管理 ——
    // 枚举所有存档槽位（读 metadata.json，不反序列化主文件）
    IReadOnlyList<SaveSlotInfo> EnumerateSlots();
    // 删除指定槽位
    bool DeleteSlot(int slotId);

    // —— 存档 / 读档 ——
    Task SaveAsync(int slotId, SaveData data, CancellationToken ct = default);
    Task<SaveData?> LoadAsync(int slotId, CancellationToken ct = default);

    // —— 设置 ——
    Task SaveSettingsAsync(GameSettings settings, CancellationToken ct = default);
    Task<GameSettings?> LoadSettingsAsync(CancellationToken ct = default);
}
```

### 信号

- `Saved(int slotId)` — 存档完成
- `Loaded(int slotId, SaveData data)` — 读档完成
- `SlotDeleted(int slotId)` — 槽位删除

### 初始化顺序

SaveDataManager 依赖 `ISerializer`（来自 ServiceLocator），而 ISerializer 无场景树依赖——故可在 ConfigDataManager 之后就绪，排在 ResourceManager 之前或之后均可（存档不依赖 Godot Resource 系统）。

---

## 四、存档数据类编写规范（硬性规则）

### 4.1 类型声明模板

```csharp
using MemoryPack;

namespace WuxiaProj.Save;

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class XxxSaveData
{
    // 字段定义...
}
```

**强制要求：**

- 必须是 `partial class`（MemoryPack Source Generator 编译期生成序列化代码）。
- **不可使用 `record` 或 `struct`**——`GenerateType.VersionTolerant` 仅支持 class。
- 存档根对象命名为 `SaveData`，所有存档数据类放在命名空间 `WuxiaProj.Save`。
- 每个存档数据类必须标注 `[MemoryPackable(GenerateType.VersionTolerant)]`。

### 4.2 字段声明模板

```csharp
[MemoryPackOrder(N)]
public SomeType FieldName { get; set; } = DefaultValue;
```

**强制要求：**

- `[MemoryPackOrder(N)]` **每个字段必须写**，`N` 从 `0` 开始连续递增，不跳号。
- 属性必须有 `{ get; set; }`（含 setter，反序列化时需要赋值）。
- 必须有默认值：值类型可用 `= default` 或具体值，引用类型必须赋非 null 默认值（如 `= ""`、`= new List<T>()`）。
- 集合类型统一使用 `List<T>` / `Dictionary<K,V>`（MemoryPack 原生支持），不使用 `IList<T>` 等接口类型。

### 4.3 字段变更规则（不可破例）

| 操作 | 规则 |
|------|------|
| 新增字段 | Order 取当前最大序号 + 1，类型与默认值确定后不可再改 |
| 删除字段 | 删除字段定义，但保留 Order 序号注释，标记 `// [已废弃 vX.X]`，序号永久跳号 |
| 改字段名 | 允许（MemoryPack 按 Order 序号匹配，不按名称） |
| 改字段类型 | **禁止**。必须新增字段 + 废弃旧字段 |
| 调整序号 | **禁止**。序号即合约，改序 = 旧档永久损坏 |

**废弃字段注释示例：**

```csharp
[MemoryPackable(GenerateType.VersionTolerant)]
public partial class PlayerSaveData
{
    [MemoryPackOrder(0)]
    public string PlayerName { get; set; } = "";

    // [MemoryPackOrder(1)] [已废弃 v1.2] 改用 MaxHealth，int→float
    // public int Health { get; set; } = 100;

    [MemoryPackOrder(2)]
    public int Level { get; set; } = 1;

    [MemoryPackOrder(3)]  // ← 新增字段，序号取当前最大 + 1
    public float MaxHealth { get; set; } = 100f;
}
```

### 4.4 Godot 内建类型处理

MemoryPack 不原生支持 Godot 内建类型（`Vector2`、`Vector3`、`Godot.Collections.Dictionary` 等）。遇到这些类型必须拆解为原始类型存储。

```csharp
// —— 错误：MemoryPack 不支持 Godot.Vector2 ——
// [MemoryPackOrder(0)] public Vector2 Position { get; set; }

// —— 正确：拆解为原始类型 ——
[MemoryPackOrder(0)] public float PosX { get; set; }
[MemoryPackOrder(1)] public float PosY { get; set; }

// 提供转换属性（不参与序列化，仅运行时使用）
[MemoryPackIgnore]
public Vector2 Position
{
    get => new(PosX, PosY);
    set { PosX = value.X; PosY = value.Y; }
}
```

- `[MemoryPackIgnore]` 标记的属性不参与序列化，不会占用 Order 序号。

### 4.5 存档根对象 SaveData

```csharp
namespace WuxiaProj.Save;

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class SaveData
{
    /// <summary>存档结构版本号（手动管理，与 MemoryPack Order 无关）</summary>
    [MemoryPackOrder(0)]
    public int Version { get; set; } = 1;

    [MemoryPackOrder(1)]
    public PlayerSaveData Player { get; set; } = new();

    [MemoryPackOrder(2)]
    public WorldSaveData World { get; set; } = new();

    [MemoryPackOrder(3)]
    public InventorySaveData Inventory { get; set; } = new();

    [MemoryPackOrder(4)]
    public List<QuestSaveData> Quests { get; set; } = new();
}
```

- `Version` 字段为手动管理的语义版本号，与 MemoryPack Order 机制无关，用于极端情况下判定需要人工迁移的边界。
- 按业务模块拆分 `XxxSaveData` 子对象（Player / World / Inventory / Quest 等），每层子对象同样遵守 4.1 ~ 4.4 规范。

### 4.6 回调钩子（按需使用）

MemoryPack 提供 4 个生命周期回调：

```csharp
[MemoryPackOnSerializing]   // 序列化前
[MemoryPackOnSerialized]    // 序列化后
[MemoryPackOnDeserializing] // 反序列化前
[MemoryPackOnDeserialized]  // 反序列化后
```

适用场景：数据迁移（旧字段值换算到新字段）、运行时状态填充、DI 容器注入。

```csharp
[MemoryPackable(GenerateType.VersionTolerant)]
public partial class PlayerSaveData
{
    [MemoryPackOrder(0)] public float MaxHealth { get; set; } = 100f;
    [MemoryPackOrder(1)] public float CurrentHealth { get; set; } = 100f;

    [MemoryPackOnDeserialized]
    void OnDeserialized()
    {
        // 修正：旧档可能 CurrentHealth > MaxHealth（后续版本降低了上限）
        if (CurrentHealth > MaxHealth)
            CurrentHealth = MaxHealth;
    }
}
```

---

## 五、元数据类定义

`SaveSlotInfo` 和 `metadata.json` 的对应类使用 JSON（不走 MemoryPack），定义从简：

```csharp
namespace WuxiaProj.Save;

public class SaveSlotInfo
{
    public int SlotId { get; set; }
    public int Version { get; set; }
    public string PlayerName { get; set; } = "";
    public int Level { get; set; }
    public long PlayTimeSeconds { get; set; }
    public DateTime SavedAt { get; set; }
    public string? ScreenshotPath { get; set; }
}
```

此类不标注 `[MemoryPackable]`，仅通过 `JsonImpl` 读写 `metadata.json`。

---

## 六、扩展预留

- **压缩**：在 `SaveAsync` 中 `byte[]` 写入 FileStream 前插入 GZip/Deflate，不影响上游。
- **加密**：同上，中间层插 AES/XOR。
- **云存档**：SaveDataManager 抽象出 `ISaveStorage` 接口（LocalFile / SteamCloud / 自定义后端），后续替换不改变存档数据类。
- **自定义 MemoryPack Formatter**：如需要直接序列化 `Godot.Vector2` 等类型，可编写 MemoryPack 自定义 Formatter 注册到 `MemoryPackSerializerOptions`，但 VersionTolerant 模式下自定义 Formatter 可能产生额外约束，需仔细评估。

---

## 相关文档

- [[2026-07-02-framework-scaffold-design|游戏基础框架脚手架设计]] — 五管理器架构与初始化顺序（SaveDataManager 同属 Framework 层）
- [[2026-06-25-attribute-system-design|属性系统设计]] — 需持久化的属性体系
- [[2026-07-02-combat-system-code-framework-design|战斗系统代码框架设计]] — 战斗状态持久化参考
- [[2026-07-02-ui-mvvm-design|UI MVVM 技术规范]] — 存档界面 MVVM 参考

---

最后更新：2026-07-02

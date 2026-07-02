# 游戏基础框架脚手架设计

## 概述

本文档定义 WuxiaProj 五个基础管理器（配置数据、资源加载、对象管理、场景管理、UI 管理）的架构、接口骨架与协作关系。这些管理器构成游戏代码的"基础设施层"，后续所有业务系统在此之上构建。

---

## 一、架构全景

### 入口方式

采用 **Godot Autoload + 静态 Instance** 模式：

- 每个管理器是一个继承自 `Node` 的类，在 `project.godot` 中注册为 Autoload，引擎启动时自动加入场景树。
- 类内部暴露 `public static XxxManager Instance { get; private set; }`，在 `_Ready()` 中赋值。
- 其他代码通过 `UiManager.Instance.Open(...)` 等直接访问。

### 目录与命名空间

- 目录：`src/Framework/`
- 命名空间：`WuxiaProj.Framework`

### 通信模型

| 方向 | 方式 | 说明 |
|------|------|------|
| 命令/请求 | 直接方法调用 | 有返回值或需要立即结果，如 `Get<T>(id)`、`ChangeToAsync(...)` |
| 通知/事件 | Godot `[Signal]` | 纯广播，谁关心谁听，如 `SceneChanged`、`ObjectRegistered` |

### 初始化顺序

`_Ready()` 按 Autoload 注册顺序依次触发，数据依赖关系决定顺序：

1. **ConfigDataManager** — 最先就绪，其余管理器启动时可能需配置参数
2. **ResourceManager** — 依赖路径映射表（来自配置）
3. **ObjectManager** — 创建实体需加载预制（依赖 ResourceManager）
4. **SceneManager** — 加载场景需 ResourceManager，实体注册到 ObjectManager
5. **UiManager** — 最后就绪（需场景树已有 Canvas 层）

### 场景树结构

```
Godot Scene Tree
├── [Main] ← 场景入口（已有）
│
└── Autoload 单例节点（无父节点，直接挂在 root 下）:
    ├── ConfigDataManager
    ├── ResourceManager
    ├── ObjectManager
    ├── SceneManager
    └── UiManager
```

---

## 二、ConfigDataManager — 配置数据管理器

### 定位

最底层只读数据层。其余管理器不直接读文件/表格，全通过本管理器查询。

### 数据形态

策划配置以 JSON 文件存放在 `config/` 目录下，短期用 JSON（手写友好），后续可切二进制。管理器屏蔽格式差异，外部只调用 `Get<T>(id)`。

### 核心接口

```csharp
// 配置表注册（一个表对应一组数据行）
void RegisterTable<T>(string filePath) where T : IConfigRow;

// 单条查询
T? Get<T>(string id) where T : class, IConfigRow;

// 全表查询
IReadOnlyList<T> GetAll<T>() where T : class, IConfigRow;

// 条件筛选
IReadOnlyList<T> Where<T>(Func<T, bool> predicate) where T : class, IConfigRow;
```

### 内部结构

- `Dictionary<Type, Dictionary<string, object>>` — 按类型分桶、按 id 索引。
- `IConfigRow` 接口仅要求 `string Id { get; }`。
- 注册显式进行（非自动扫描），启动顺序可控，报错路径清晰。
- 只读不写，运行时状态不回流到配置层。

### 信号

- `ConfigReloaded` — 热重载完成后广播，通知需要刷新的系统。

---

## 三、ResourceManager — 资源加载器

### 定位

封装 Godot 资源加载，提供异步加载、引用计数缓存与路径抽象。其余管理器不直接调用 `GD.Load` 或 `ResourceLoader`。

### 核心接口

```csharp
// 同步加载（缓存命中直接返回，未命中走 ResourceLoader.Load）
T Load<T>(string path) where T : Resource;

// 异步加载
Task<T> LoadAsync<T>(string path) where T : Resource;

// 加载并持有引用（调用方用完须 Release）
Task<T> LoadAndRetain<T>(string path) where T : Resource;
void Release(string path);

// 批量预加载（场景切换前预热，避免卡帧）
Task PreloadBatch(IEnumerable<string> paths);

// 手动清理未引用的缓存资源
void CollectGarbage();
```

### 缓存策略

- **强引用字典**：`Dictionary<string, Resource>` — 被 `LoadAndRetain` 持有或被多个使用者引用的资源。
- **弱引用池**：只载入过但无人 Retain 的资源用 `WeakReference` 兜底，避免重复加载。
- `CollectGarbage()` 遍历弱引用池，清理已 GC 回收的条目。

### 路径抽象

内部维护"逻辑名 → 真实路径"映射表（从 ConfigDataManager 加载），业务代码仅用逻辑名：

```csharp
ResourceManager.LoadAsync<Texture2D>("icon/sword")
// → 内部查表 → "res://assets/sprites/icons/sword.png"
```

---

## 四、ObjectManager — 游戏对象管理器

### 定位

管理游戏中"活的实体"的创建、查找、回收。每个实体拥有唯一 `ObjectId`。

### 核心接口

```csharp
// 注册/注销
ObjectId Register(Node node, ObjectType type);
void Unregister(ObjectId id);

// 查询
T? Find<T>(ObjectId id) where T : Node;
IReadOnlyList<T> FindByType<T>(ObjectType type) where T : Node;

// 工厂方法（从配置创建实体，内部调 ResourceManager 加载预制）
Task<T> CreateAsync<T>(string prefabLogicName, ObjectType type) where T : Node;

// 回收（后续可改为退回对象池）
void Destroy(ObjectId id);
```

### 内部结构

```
Dictionary<ObjectId, Node>         // id → 实体节点
Dictionary<ObjectType, List<Node>> // 类型 → 实体列表（快速批量遍历）
```

### ObjectType 枚举

`Player`、`Enemy`、`Npc`、`Projectile`、`DroppedItem`、`Other`

### 对象池（预留）

`CreateAsync` / `Destroy` 接口无感——后续 `Destroy` 可改为退回池子、`CreateAsync` 可复用池中实例，调用方代码无需修改。

### 信号

- `ObjectRegistered(ObjectId id, ObjectType type)` — 新实体入场
- `ObjectUnregistered(ObjectId id, ObjectType type)` — 实体退场

### 与 SceneManager 的分工

SceneManager 管理"场景文件的加载/卸载"，ObjectManager 管理"场景内的实体"。场景加载完毕后，其内的角色/敌人通过 ObjectManager 注册到全局索引。场景卸载前，SceneManager 发射信号通知 ObjectManager 批量注销。

---

## 五、SceneManager — 场景管理器

### 定位

管理 Godot 场景树的加载/卸载/切换，是所有场景级操作的唯一入口。

### 核心接口

```csharp
// 切换场景
Task ChangeToAsync(string sceneLogicName, TransitionType transition = TransitionType.Fade);

// 叠加场景（弹窗式场景，不卸载底层）
Task OverlayAsync(string sceneLogicName);
Task RemoveOverlayAsync(string sceneLogicName);

// 当前状态
string CurrentScene { get; }
IReadOnlyList<string> ActiveOverlays { get; }
bool IsSwitching { get; }
```

### 场景路径映射

业务代码用逻辑名 `"battle/forest_01"`，管理器内部查映射表转 `"res://scenes/battle/forest_01.tscn"`。

### 过渡动画

`TransitionType` 枚举（`None`、`Fade`、`SlideLeft`，后续可扩展）。

`ChangeToAsync` 内部流程：
1. 播过渡入场动画（遮罩覆盖全屏）
2. 卸载旧场景
3. 加载新场景（调 ResourceLoader）
4. 新场景加入树
5. 播过渡出场动画
6. 发射完成信号

`OverlayAsync` 跳过步骤 2（不卸载底层），仅加载叠加场景并以更高层级加入树。

### 信号

- `SceneUnloadStarted(string sceneLogicName)` — UiManager 监听，关闭旧场景关联的 UI
- `SceneLoadCompleted(string sceneLogicName)` — UiManager 监听，打开默认 HUD
- `TransitionFinished()` — 过渡动画全部结束，允许玩家输入

### 与 ObjectManager 协作

`SceneUnloadStarted` 触发时，ObjectManager 批量注销该场景内所有已注册实体，防止引用悬挂。

---

## 六、UiManager — UI 管理器

### 定位

管理所有 UI 面板的栈式生命周期、层级排序和输入焦点。

### 核心接口

```csharp
// 打开面板（泛型，自动加载 .tscn，返回面板实例）
Task<T> OpenAsync<T>(object? data = null) where T : Panel;

// 关闭面板
void Close<T>() where T : Panel;
void Close(Panel panel);
void CloseTop();

// 查询
T? GetPanel<T>() where T : Panel;
bool IsOpen<T>() where T : Panel;
IReadOnlyList<Panel> PanelStack { get; }

// 全局提示
Task ShowToastAsync(string message, float duration = 2f);
void ShowLoading(bool show);
```

### UI 栈模型

```
┌──────────────────┐  ← 栈顶（捕获输入焦点）
│  弹窗面板 (Dialog) │
├──────────────────┤
│  二级面板 (Sub)    │     下层不响应输入
├──────────────────┤
│  主面板 (HUD)      │     栈底——常驻，不可关闭
└──────────────────┘
```

- `OpenAsync` 压入栈顶并拿到焦点，下层自动禁用输入。
- `Close` 从栈中移除该层，焦点回到新栈顶。
- 强制保留不可关闭的根层（HUD 或空根节点），栈永不为空。

### 层级管理

每层 UI 挂在一个 `CanvasLayer` 下，按栈深分配 Godot `layer` 值，渲染与输入遮罩次序一致。

### 面板路径映射

`OpenAsync<BattleHud>()` 自动查映射表：`"ui/battle_hud"` → `"res://scenes/ui/battle_hud.tscn"`。

### 信号

- `PanelOpened(Panel panel)`
- `PanelClosed(Panel panel)`
- `StackEmptied` — 所有面板关闭（仅剩根层），用于调试/异常恢复

---

## 七、扩展预留

以下能力当前不实现，但接口与架构已预留扩展点：

- **ObjectManager**：对象池（`Destroy` 退池 / `CreateAsync` 复用）
- **ResourceManager**：资源热重载（`ConfigReloaded` 信号触发重载）
- **SceneManager**：更多过渡动画类型、场景预加载
- **UiManager**：UI 动画钩子（打开/关闭动画接口）

---

最后更新：2026-07-02

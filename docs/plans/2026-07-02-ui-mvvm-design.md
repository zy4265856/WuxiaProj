# UI MVVM 技术规范

## 概述

本文档定义 WuxiaProj UI 层基于 **MVVM** 架构的技术规范。以 **R3** 作为响应式数据绑定层，在现有 `UiManager`/`UiPanel` 基础上扩展 ViewModel 和 ServiceLocator 能力。

---

## 一、架构全景

### 分层

```
┌──────────────────────────────────────────────┐
│  View 层 (Godot Scene + UiPanel 子类)         │
│  - .tscn 布局 + C# partial 订阅 R3 属性        │
│  - _Ready() 中从 ServiceLocator 获取 VM        │
│  - 用 CompositeDisposable 管理订阅生命周期      │
├──────────────────────────────────────────────┤
│  ViewModel 层 (纯 C#，不继承 Node)             │
│  - 暴露 ReactiveProperty<T> / ReactiveCommand  │
│  - 持有 Model 引用，转换数据供 View 消费        │
│  - 可使用 Godot 类型（Vector2/Texture2D/Color） │
│    ⚠️ 但不应直接操作场景树或调用 Node 方法      │
├──────────────────────────────────────────────┤
│  Model 层 (纯 C#)                             │
│  - 游戏数据：角色属性、物品、技能等             │
│  - 通过 Manager 获取数据                        │
│  - 不含 View/VM 概念                           │
└──────────────────────────────────────────────┘
```

### R3 核心类型映射

| 需求 | R3 提供 | 说明 |
|------|---------|------|
| 可绑定属性 | `ReactiveProperty<T>` | `T` 可以是 Godot 类型 |
| 命令/按钮 | `ReactiveCommand` | 绑定到 `Button.Pressed` |
| 订阅管理 | `CompositeDisposable` | `Dispose()` 一键取消全部 |

### 边界约定

- ViewModel **可以**：用 `Texture2D` 存图标、用 `Color` 存品质色、用 `Vector2` 做坐标计算。
- ViewModel **不可以**：`GetNode()`、`AddChild()`、`QueueFree()` — 操作场景树始终是 View 的职责。
- ServiceLocator 注册集中在一个 `ServiceRegistration` 启动脚本中。

---

## 二、核心基类

### 2.1 ViewModelBase

```csharp
namespace WuxiaProj.Framework;

public abstract class ViewModelBase : IDisposable
{
    protected CompositeDisposable Disposables { get; } = new();

    public virtual void Dispose()
    {
        Disposables.Dispose();
    }
}
```

- 持有 `CompositeDisposable`，VM 中所有 `ReactiveProperty` / `ReactiveCommand` / 订阅均通过 `.AddTo(Disposables)` 注入。
- VM 被 Dispose 时一键清理所有响应式资源。
- 实例不继承 `Node`，不由 Godot 管理生命周期。

### 2.2 UiPanel（升级）

```csharp
namespace WuxiaProj.Framework;

public partial class UiPanel : Control, IDisposable
{
    protected CompositeDisposable ViewDisposables { get; } = new();

    public override void _ExitTree()
    {
        Dispose();
    }

    public virtual void Dispose()
    {
        ViewDisposables.Dispose();
    }
}
```

- 新增 `IDisposable` + `CompositeDisposable ViewDisposables`，所有 R3 订阅用 `.AddTo(ViewDisposables)` 挂载。
- `_ExitTree()` 自动 Dispose — UiManager 关闭面板清理节点时触发，订阅随之自动解除。

### 2.3 ServiceLocator

```csharp
namespace WuxiaProj.Framework;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, Func<object>> _factories = new();
    private static readonly Dictionary<Type, object> _singletons = new();

    public static void Register<T>(Func<T> factory) where T : class
    public static void RegisterSingleton<T>(T instance) where T : class
    public static T Resolve<T>() where T : class
    public static bool TryResolve<T>(out T? result) where T : class
    public static void Unregister<T>() where T : class
}
```

- 支持工厂注册（每次 `Resolve` 创建新实例）和单例注册。
- ViewModel 通常 Transient，Model 通常 Singleton。
- `Unregister<T>()` 自动 Dispose 单例（若实现 `IDisposable`）。

### 2.4 典型使用流程

```
UiManager.OpenAsync<CharacterSheet>()
  → View._Ready()
    → ViewModel = ServiceLocator.Resolve<CharacterSheetViewModel>()
    → ViewModel.SomeProperty.Subscribe(v => label.Text = v)
        .AddTo(ViewDisposables)
  → 用户交互
  → UiManager.Close(panel)
    → panel.QueueFree() → _ExitTree() → Dispose()
    → ViewDisposables 清空 → 所有订阅解除
    → ViewModel.Dispose() → VM 订阅清空
```

---

## 三、R3 绑定模式

### 3.1 属性 → 控件（单向）

```csharp
// ViewModel
public ReactiveProperty<string> Name { get; } = new("未命名");

// View._Ready()
vm.Name.Subscribe(v => _nameLabel.Text = v).AddTo(ViewDisposables);
```

- `Subscribe` 立即触发一次当前值，确保初始化控件已有正确值。
- 字符串、数值、Color（→Modulate）、Texture（→TextureRect）均适用。

### 3.2 属性 → 控件（双向）

```csharp
// ViewModel
public ReactiveProperty<int> Level { get; } = new(1);

// View: Slider → VM
_slider.ValueChanged += v => vm.Level.Value = (int)v;
// View: VM → Slider
vm.Level.Subscribe(v => _slider.Value = v).AddTo(ViewDisposables);
```

- R3 的 `ReactiveProperty` 本身不提供双向绑定语法糖，双向需手动搭两条线。
- 封装为轻量工具方法 `BindToSlider` 等（见第四节）。

### 3.3 ReactiveCommand → 按钮

```csharp
// ViewModel
public ReactiveCommand OnUpgrade { get; } = new();
OnUpgrade.Subscribe(_ => Level.Value++).AddTo(Disposables);

// View
_upgradeButton.Pressed += () => vm.OnUpgrade.Execute(Unit.Default);
```

- `ReactiveCommand` 在构造时可传入 `IObservable<bool>` 作为 `CanExecute` 条件。
- 联动：`level.Select(l => l < maxLevel)` 作为构造参数控制按钮灰显。

---

## 四、绑定工具类 `UiBinding`

### 4.1 双向绑定

```csharp
namespace WuxiaProj.Framework;

public static class UiBinding
{
    public static IDisposable BindToSlider(
        this ReactiveProperty<int> property, Slider slider)
    {
        // Slider → VM
        slider.ValueChanged += v => property.Value = (int)v;
        // VM → Slider
        var sub = property.Subscribe(v => slider.Value = v);
        return Disposable.Create(() =>
        {
            sub.Dispose();
        });
    }
}
```

用法：

```csharp
vm.Level.BindToSlider(_levelSlider).AddTo(ViewDisposables);
```

### 4.2 按钮命令绑定

```csharp
public static IDisposable BindCommand(
    this Button button,
    ReactiveCommand command)
{
    button.Pressed += () => command.Execute(Unit.Default);
    return Disposable.Create(() => { });
}

### 4.3 工具类总览

| 方法 | 方向 | 场景 |
|------|------|------|
| `BindToSlider` | ↔ | Slider ↔ ReactiveProperty&lt;int&gt; |
| `BindCommand` | → | Button → ReactiveCommand（点击执行） |
| `Subscribe` | ← | 通用单向（R3 原生，不封装） |

---

## 五、完整示例：CharacterSheet 角色属性面板

### 5.1 Model 层

```csharp
// src/UI/Models/CharacterSheetModel.cs
namespace WuxiaProj.UI.Models;

public class CharacterSheetModel
{
    public CharacterData LoadCharacter(ObjectId id) => /* ... */;
}

public class CharacterData
{
    public string Name { get; init; } = "";
    public int Level { get; init; }
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Mp { get; init; }
    public int MaxMp { get; init; }
    public int Qi { get; init; }
    public int InnerBreath { get; init; }
    public int Physique { get; init; }
    public int Comprehension { get; init; }
    public int Agility { get; init; }
    public int Willpower { get; init; }
    public int Fortune { get; init; }
    public int Charisma { get; init; }
    public int Vigor { get; init; }
    public int Precision { get; init; }
    public List<SkillInfo> Skills { get; init; } = new();
}
```

### 5.2 ViewModel 层

```csharp
// src/UI/ViewModels/CharacterSheetViewModel.cs
namespace WuxiaProj.UI.ViewModels;

public class CharacterSheetViewModel : ViewModelBase
{
    public ReactiveProperty<string> Name { get; } = new("");
    public ReactiveProperty<int> Level { get; } = new(1);
    public ReactiveProperty<int> Hp { get; } = new(100);
    public ReactiveProperty<int> MaxHp { get; } = new(100);
    public ReactiveProperty<float> HpRatio { get; } = new(1f);

    public Dictionary<string, ReactiveProperty<int>> Attributes { get; } = new();
    public List<SkillSlotViewModel> Skills { get; } = new();
    public ReactiveCommand OnClose { get; } = new();

    public CharacterSheetViewModel(CharacterSheetModel model, ObjectId characterId)
    {
        var data = model.LoadCharacter(characterId);

        Name.Value = data.Name;
        Level.Value = data.Level;
        Hp.Value = data.Hp;
        MaxHp.Value = data.MaxHp;

        // 血量联动：Hp / MaxHp → HpRatio
        Observable.CombineLatest(Hp, MaxHp)
            .Select(t => (float)t[0] / t[1])
            .Subscribe(v => HpRatio.Value = v)
            .AddTo(Disposables);

        Attributes["Qi"] = new ReactiveProperty<int>(data.Qi).AddTo(Disposables);
        Attributes["InnerBreath"] = new ReactiveProperty<int>(data.InnerBreath).AddTo(Disposables);
        // ... 其余八维（Physique/Comprehension/Agility/Willpower/Fortune/Charisma/Vigor/Precision）

        foreach (var skill in data.Skills)
            Skills.Add(new SkillSlotViewModel(skill));
    }
}

public class SkillSlotViewModel
{
    public ReactiveProperty<string> Name { get; }
    public ReactiveProperty<Texture2D?> Icon { get; }
}
```

### 5.3 View 层

```csharp
// src/UI/Views/CharacterSheet.cs
namespace WuxiaProj.UI.Views;

public partial class CharacterSheet : UiPanel
{
    private CharacterSheetViewModel _vm = null!;

    [Export] private Label _nameLabel = null!;
    [Export] private Label _levelLabel = null!;
    [Export] private Label _hpLabel = null!;
    [Export] private ProgressBar _hpBar = null!;
    [Export] private GridContainer _skillGrid = null!;
    [Export] private Button _closeButton = null!;

    public override void _Ready()
    {
        _vm = ServiceLocator.Resolve<CharacterSheetViewModel>();
        BindViewModel();
    }

    private void BindViewModel()
    {
        _vm.Name.Subscribe(v => _nameLabel.Text = v).AddTo(ViewDisposables);
        _vm.Level.Subscribe(v => _levelLabel.Text = $"Lv.{v}").AddTo(ViewDisposables);

        _vm.Hp.CombineLatest(_vm.MaxHp, (hp, maxHp) => $"{hp} / {maxHp}")
            .Subscribe(v => _hpLabel.Text = v)
            .AddTo(ViewDisposables);

        _vm.HpRatio.Subscribe(v => _hpBar.Value = v).AddTo(ViewDisposables);

        foreach (var skill in _vm.Skills)
            _skillGrid.AddChild(CreateSkillView(skill));

        _closeButton.BindCommand(_vm.OnClose).AddTo(ViewDisposables);
        _vm.OnClose.Subscribe(_ => UiManager.Instance.Close(this))
            .AddTo(ViewDisposables);
    }

    private Control CreateSkillView(SkillSlotViewModel vm) { /* ... */ }
}
```

### 5.4 注册

```csharp
// 游戏启动时
ServiceLocator.RegisterSingleton(new CharacterSheetModel());
ServiceLocator.Register(() => new CharacterSheetViewModel(
    ServiceLocator.Resolve<CharacterSheetModel>(),
    currentCharacterId
));
```

### 5.5 数据流全景

```
ObjectManager → CharacterSheetModel(LoadCharacter)
  → CharacterSheetViewModel(R3属性转换+联动)
    → CharacterSheet(View, Subscribe绑定Godot节点)
      → 玩家看到UI
      → 按钮 → ReactiveCommand → VM逻辑 → 属性更新 → View自动刷新
      → 关闭 → UiManager.Close → QueueFree → _ExitTree → Dispose → 全部订阅解除
```

---

## 六、目录结构

```
src/
├── Framework/
│   ├── UiManager.cs          # 已有（不需修改）
│   ├── UiPanel.cs            # 升级：+IDisposable +ViewDisposables
│   ├── ViewModelBase.cs      # 新增
│   ├── ServiceLocator.cs     # 新增
│   └── UiBinding.cs          # 新增
└── UI/
    ├── Models/
    │   └── CharacterSheetModel.cs
    ├── ViewModels/
    │   ├── CharacterSheetViewModel.cs
    │   └── SkillSlotViewModel.cs
    └── Views/
        └── CharacterSheet.cs
```

命名空间：`WuxiaProj.Framework`（基类/工具） + `WuxiaProj.UI.Models` / `WuxiaProj.UI.ViewModels` / `WuxiaProj.UI.Views`（业务层）。

---

## 七、扩展预留

以下能力当前不实现，但架构已预留扩展点：

- **动画绑定**：`ReactiveProperty` 变化驱动 `Tween` 动画（View 层在 Subscribe 回调中触发 `CreateTween()`）
- **全局消息总线**：跨面板通信（如"角色升级"通知 HUD 刷新），通过 R3 `Subject<T>` 注册到 ServiceLocator
- **UI 过渡动画钩子**：`UiPanel.OnOpen` / `OnClose` 虚方法，供子类播入场/退场动画

---

最后更新：2026-07-02

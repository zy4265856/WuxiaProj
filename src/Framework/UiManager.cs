using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace WuxiaProj.Framework;

/// <summary>
/// UI 管理器 — 管理所有 UI 面板的栈式生命周期、层级排序和输入焦点。
/// 每层 UI 挂载在独立的 CanvasLayer 下，栈顶捕获输入，下层自动禁用。
/// </summary>
public partial class UiManager : Node
{
    public static UiManager Instance { get; private set; } = null!;

    [Signal]
    public delegate void PanelOpenedEventHandler(Node panel);

    [Signal]
    public delegate void PanelClosedEventHandler(Node panel);

    [Signal]
    public delegate void StackEmptiedEventHandler();

    /// <summary>UI 面板栈（栈底→栈顶）。</summary>
    private readonly List<UiPanel> _panelStack = new();

    /// <summary>UI 根 CanvasLayer，作为所有面板层的父节点。</summary>
    private CanvasLayer _rootLayer = null!;

    /// <summary>逻辑名 → 真实场景路径映射表。</summary>
    private Dictionary<string, string> _pathMap = new();

    public IReadOnlyList<UiPanel> PanelStack => _panelStack.AsReadOnly();

    public override void _Ready()
    {
        Instance = this;
        _rootLayer = new CanvasLayer { Name = "UiRoot", Layer = 0 };
        AddChild(_rootLayer);
        LoadPathMap();
        GD.Print("[UiManager] 已就绪");
    }

    /// <summary>
    /// 打开泛型 UI 面板。通过类型名查找对应场景，加载后压入栈顶。
    /// 返回面板实例供调用方填充数据。
    /// </summary>
    public async Task<T> OpenAsync<T>(object? data = null) where T : UiPanel
    {
        // 通过类型名推导逻辑名
        var logicName = typeof(T).Name;

        var scenePath = ResolvePath(logicName);
        var packed = await ResourceManager.Instance.LoadAsync<PackedScene>(scenePath);
        var panel = packed.Instantiate<T>();

        // 创建独立 CanvasLayer 并设定层级（栈越高 layer 越大）
        var layer = new CanvasLayer();
        layer.Layer = _panelStack.Count + 1;
        layer.AddChild(panel);
        _rootLayer.AddChild(layer);

        // 禁用前一个栈顶的输入
        if (_panelStack.Count > 0)
            _panelStack[^1].ProcessMode = ProcessModeEnum.Disabled;

        _panelStack.Add(panel);
        EmitSignal(SignalName.PanelOpened, panel);
        GD.Print($"[UiManager] 打开面板: {typeof(T).Name} (栈深: {_panelStack.Count})");

        return panel;
    }

    /// <summary>
    /// 按类型关闭面板。
    /// </summary>
    public void Close<T>() where T : UiPanel
    {
        var panel = _panelStack.Find(p => p is T);
        if (panel != null)
            Close(panel);
    }

    /// <summary>
    /// 关闭指定面板实例。
    /// </summary>
    public void Close(UiPanel panel)
    {
        var index = _panelStack.IndexOf(panel);
        if (index < 0)
            return;

        // 从栈中移除
        _panelStack.RemoveAt(index);

        // 恢复新栈顶的输入
        if (_panelStack.Count > 0)
            _panelStack[^1].ProcessMode = ProcessModeEnum.Inherit;

        // 清理节点（面板 + 其 CanvasLayer 父节点）
        panel.GetParent()?.QueueFree();

        GD.Print($"[UiManager] 关闭面板: {panel.GetType().Name} (栈深: {_panelStack.Count})");
        EmitSignal(SignalName.PanelClosed, panel);

        if (_panelStack.Count == 0)
            EmitSignal(SignalName.StackEmptied);
    }

    /// <summary>
    /// 关闭栈顶面板。
    /// </summary>
    public void CloseTop()
    {
        if (_panelStack.Count > 0)
            Close(_panelStack[^1]);
    }

    /// <summary>
    /// 按类型获取已打开的面板实例。
    /// </summary>
    public T? GetPanel<T>() where T : UiPanel
    {
        return _panelStack.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// 检查某类型面板是否已打开。
    /// </summary>
    public bool IsOpen<T>() where T : UiPanel
    {
        return _panelStack.Any(p => p is T);
    }

    /// <summary>
    /// 显示全局 Toast 提示。
    /// </summary>
    public async Task ShowToastAsync(string message, float duration = 2f)
    {
        // TODO: 实现轻量 Toast Label 弹出 + 自动消失
        GD.Print($"[Toast] {message}");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 显示/隐藏全局 Loading 遮罩。
    /// </summary>
    public void ShowLoading(bool show)
    {
        // TODO: 实现半透明遮罩 + 转圈动画
        GD.Print($"[Loading] {(show ? "显示" : "隐藏")}");
    }

    private string ResolvePath(string logicName)
        => _pathMap.TryGetValue(logicName, out var realPath) ? realPath : logicName;

    private void LoadPathMap()
    {
        // TODO: 从 ConfigDataManager 加载 UI 面板路径映射表
    }
}

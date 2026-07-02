using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace WuxiaProj.Framework;

/// <summary>
/// 场景管理器 — 管理 Godot 场景树的加载/卸载/切换。
/// 支持主场景切换（ChangeToAsync）与叠加场景（OverlayAsync）。
/// </summary>
public partial class SceneManager : Node
{
    public static SceneManager Instance { get; private set; } = null!;

    [Signal]
    public delegate void SceneUnloadStartedEventHandler(string sceneLogicName);

    [Signal]
    public delegate void SceneLoadCompletedEventHandler(string sceneLogicName);

    [Signal]
    public delegate void TransitionFinishedEventHandler();

    /// <summary>当前主场景的逻辑名。</summary>
    public string CurrentScene { get; private set; } = string.Empty;

    /// <summary>当前活动的叠加场景列表。</summary>
    public IReadOnlyList<string> ActiveOverlays => _overlays.AsReadOnly();

    /// <summary>场景切换 / 过渡动画进行中，屏蔽输入。</summary>
    public bool IsSwitching { get; private set; }

    private readonly List<string> _overlays = new();
    private Dictionary<string, string> _pathMap = new();

    public override void _Ready()
    {
        Instance = this;
        LoadPathMap();
        GD.Print("[SceneManager] 已就绪");
    }

    /// <summary>
    /// 切换到新的主场景。卸载当前场景 → 加载新场景 → 过渡动画。
    /// </summary>
    public async Task ChangeToAsync(string sceneLogicName, TransitionType transition = TransitionType.Fade)
    {
        if (IsSwitching)
            return;

        IsSwitching = true;

        // 1. 通知旧场景即将卸载
        if (!string.IsNullOrEmpty(CurrentScene))
            EmitSignal(SignalName.SceneUnloadStarted, CurrentScene);

        // 2. 过渡入场动画
        if (transition != TransitionType.None)
            await PlayTransitionAsync(transition, entering: true);

        // 3. 清理所有叠加场景
        foreach (var overlay in _overlays.ToArray())
            await RemoveOverlayInternal(overlay);

        // 4. 卸载当前场景
        GetTree().CurrentScene?.QueueFree();

        // 5. 加载新场景
        var scenePath = ResolvePath(sceneLogicName);
        var packed = await ResourceManager.Instance.LoadAsync<PackedScene>(scenePath);
        var newScene = packed.Instantiate();
        GetTree().Root.AddChild(newScene);
        GetTree().CurrentScene = newScene;
        CurrentScene = sceneLogicName;

        // 6. 通知新场景就绪
        EmitSignal(SignalName.SceneLoadCompleted, CurrentScene);

        // 7. 过渡出场动画
        if (transition != TransitionType.None)
            await PlayTransitionAsync(transition, entering: false);

        EmitSignal(SignalName.TransitionFinished);
        IsSwitching = false;
    }

    /// <summary>
    /// 叠加一个弹窗式场景（如小游戏、子界面），不卸载底层主场景。
    /// </summary>
    public async Task OverlayAsync(string sceneLogicName)
    {
        var scenePath = ResolvePath(sceneLogicName);
        var packed = await ResourceManager.Instance.LoadAsync<PackedScene>(scenePath);
        var overlay = packed.Instantiate();
        GetTree().Root.AddChild(overlay);
        _overlays.Add(sceneLogicName);
    }

    /// <summary>
    /// 移除一个叠加场景。
    /// </summary>
    public async Task RemoveOverlayAsync(string sceneLogicName)
    {
        await RemoveOverlayInternal(sceneLogicName);
    }

    /// <summary>
    /// 内部：移除叠加场景并回收节点。
    /// </summary>
    private Task RemoveOverlayInternal(string sceneLogicName)
    {
        // TODO: 通过逻辑名精确查找并移除对应的 overlay 节点
        _overlays.Remove(sceneLogicName);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 播放过渡动画。当前使用 Timer 模拟延迟，后续替换为实际动画。
    /// </summary>
    private async Task PlayTransitionAsync(TransitionType type, bool entering)
    {
        // TODO: 实现 ColorRect 遮罩淡入/淡出或滑屏动画
        var phase = entering ? "入场" : "出场";
        GD.Print($"[SceneManager] 过渡动画: {type} - {phase}");
        await ToSignal(GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
    }

    private string ResolvePath(string logicName)
        => _pathMap.TryGetValue(logicName, out var realPath) ? realPath : logicName;

    private void LoadPathMap()
    {
        // TODO: 从 ConfigDataManager 加载场景路径映射表
    }
}

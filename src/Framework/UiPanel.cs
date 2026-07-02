using Godot;
using R3;

namespace WuxiaProj.Framework;

/// <summary>
/// UI 面板基类。所有 UI 面板（HUD、弹窗、菜单等）继承自此类型。
/// 注意：类名 UiPanel 用于避免与 Godot 内置 Panel 类冲突。
/// GodotObject 已实现 IDisposable，此处用 new 显式隐藏基类 Dispose，
/// 在退出场景树时自动清理所有 R3 订阅。
/// </summary>
public partial class UiPanel : Control
{
    /// <summary>
    /// View 层 CompositeDisposable。所有 R3 订阅通过 .AddTo(ViewDisposables) 挂载，
    /// 面板退出场景树时自动清理。
    /// </summary>
    protected CompositeDisposable ViewDisposables { get; } = new();

    public override void _ExitTree()
    {
        Dispose();
    }

    public new virtual void Dispose()
    {
        ViewDisposables.Dispose();
        base.Dispose();
    }
}

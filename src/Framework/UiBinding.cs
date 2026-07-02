using System;
using Godot;
using R3;

namespace WuxiaProj.Framework;

/// <summary>
/// UI 绑定工具类。为 R3 响应式类型提供 Godot 控件绑定的薄封装。
/// </summary>
public static class UiBinding
{
    /// <summary>
    /// ReactiveProperty&lt;int&gt; 双向绑定到 Slider。
    /// </summary>
    public static IDisposable BindToSlider(
        this ReactiveProperty<int> property, Slider slider)
    {
        // Slider → VM
        slider.ValueChanged += v => property.Value = (int)v;
        // VM → Slider
        var sub = property.Subscribe(v => slider.Value = v);
        return Disposable.Create(() =>
        {
            slider.ValueChanged -= v => property.Value = (int)v;
            sub.Dispose();
        });
    }

    /// <summary>
    /// Button 绑定到 ReactiveCommand。绑定点击 → 执行命令。
    /// CanExecute 灰显逻辑在 View 中手写。
    /// </summary>
    public static IDisposable BindCommand(
        this Button button,
        ReactiveCommand command)
    {
        button.Pressed += OnPressed;
        return Disposable.Create(() => button.Pressed -= OnPressed);
        void OnPressed() => command.Execute(Unit.Default);
    }
}

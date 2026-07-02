using R3;

namespace WuxiaProj.Framework;

/// <summary>
/// ViewModel 基类。持有 CompositeDisposable，所有 R3 资源通过 .AddTo(Disposables) 注入。
/// 不继承 Godot.Node，由 ServiceLocator 或 View 管理生命周期。
/// </summary>
public abstract class ViewModelBase : System.IDisposable
{
    /// <summary>
    /// VM 层 CompositeDisposable。ReactiveProperty / ReactiveCommand / 内部订阅均挂载于此。
    /// </summary>
    protected CompositeDisposable Disposables { get; } = new();

    public virtual void Dispose()
    {
        Disposables.Dispose();
    }
}

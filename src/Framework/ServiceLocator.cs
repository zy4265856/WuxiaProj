using System;
using System.Collections.Generic;

namespace WuxiaProj.Framework;

/// <summary>
/// 轻量服务定位器。支持工厂注册与单例注册。
/// ViewModel 通常 Transient（工厂），Model 通常 Singleton。
/// 注册集中在 ServiceRegistration 启动脚本中。
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, Func<object>> _factories = new();
    private static readonly Dictionary<Type, object> _singletons = new();

    /// <summary>
    /// 注册工厂方法。每次 Resolve 创建一个新实例。
    /// </summary>
    public static void Register<T>(Func<T> factory) where T : class
    {
        var type = typeof(T);
        if (_factories.ContainsKey(type) || _singletons.ContainsKey(type))
        {
            Godot.GD.PushWarning($"[ServiceLocator] {type.Name} 已注册，将被覆盖");
            _factories.Remove(type);
            _singletons.Remove(type);
        }
        _factories[type] = () => factory();
    }

    /// <summary>
    /// 注册单例。Resolve 始终返回同一实例。
    /// </summary>
    public static void RegisterSingleton<T>(T instance) where T : class
    {
        var type = typeof(T);
        if (_factories.ContainsKey(type) || _singletons.ContainsKey(type))
        {
            Godot.GD.PushWarning($"[ServiceLocator] {type.Name} 已注册，将被覆盖");
            _factories.Remove(type);
            _singletons.Remove(type);
        }
        _singletons[type] = instance;
    }

    /// <summary>
    /// 解析服务。未注册时抛出 InvalidOperationException。
    /// </summary>
    public static T Resolve<T>() where T : class
    {
        var type = typeof(T);

        if (_singletons.TryGetValue(type, out var singleton))
            return (T)singleton;

        if (_factories.TryGetValue(type, out var factory))
            return (T)factory();

        throw new InvalidOperationException($"[ServiceLocator] 未注册: {type.Name}");
    }

    /// <summary>
    /// 尝试解析。未注册时返回 false（不抛异常）。
    /// </summary>
    public static bool TryResolve<T>(out T? result) where T : class
    {
        var type = typeof(T);

        if (_singletons.TryGetValue(type, out var singleton))
        {
            result = (T)singleton;
            return true;
        }

        if (_factories.TryGetValue(type, out var factory))
        {
            result = (T)factory();
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// 注销服务。若单例实现了 IDisposable，自动调用 Dispose。
    /// </summary>
    public static void Unregister<T>() where T : class
    {
        var type = typeof(T);
        if (_singletons.Remove(type, out var instance) && instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _factories.Remove(type);
    }
}

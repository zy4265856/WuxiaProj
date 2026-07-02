using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace WuxiaProj.Framework;

/// <summary>
/// 资源管理器 — 封装 Godot 资源加载，提供异步加载、引用计数缓存与路径抽象。
/// 其余管理器不直接调 GD.Load / ResourceLoader，全部通过本管理器。
/// </summary>
public partial class ResourceManager : Node
{
    public static ResourceManager Instance { get; private set; } = null!;

    /// <summary>强引用缓存：路径 → 资源</summary>
    private readonly Dictionary<string, Resource> _cache = new();

    /// <summary>弱引用池：无人 Retain 的资源兜底复用</summary>
    private readonly Dictionary<string, WeakReference<Resource>> _weakCache = new();

    /// <summary>引用计数：路径 → 持有方数量</summary>
    private readonly Dictionary<string, int> _refCounts = new();

    /// <summary>逻辑名 → 真实路径映射表（从 ConfigDataManager 加载）</summary>
    private Dictionary<string, string> _pathMap = new();

    public override void _Ready()
    {
        Instance = this;
        LoadPathMap();
        GD.Print("[ResourceManager] 已就绪");
    }

    /// <summary>
    /// 同步加载资源。缓存命中直接返回，未命中走 ResourceLoader.Load。
    /// </summary>
    public T Load<T>(string path) where T : Resource
    {
        var realPath = ResolvePath(path);

        if (_cache.TryGetValue(realPath, out var cached))
            return (T)cached;

        var resource = ResourceLoader.Load<T>(realPath);
        if (resource == null)
            throw new InvalidOperationException($"[ResourceManager] 资源加载失败或类型不匹配: {path}");
        _cache[realPath] = resource;
        return resource;
    }

    /// <summary>
    /// 异步加载资源。内部使用 ResourceLoader.LoadThreadedRequest 避免卡主线程。
    /// </summary>
    public async Task<T> LoadAsync<T>(string path) where T : Resource
    {
        var realPath = ResolvePath(path);

        // 强引用缓存命中
        if (_cache.TryGetValue(realPath, out var cached))
            return (T)cached;

        // 弱引用池命中 → 提升为强引用
        if (_weakCache.TryGetValue(realPath, out var weakRef) && weakRef.TryGetTarget(out var weakRes))
        {
            _cache[realPath] = weakRes;
            _weakCache.Remove(realPath);
            return (T)weakRes;
        }

        var error = ResourceLoader.LoadThreadedRequest(realPath);
        if (error != Error.Ok)
            throw new InvalidOperationException($"[ResourceManager] 无法开始加载: {path}");

        // 轮询等待加载完成
        while (true)
        {
            var status = ResourceLoader.LoadThreadedGetStatus(realPath);
            if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                break;
            if (status == ResourceLoader.ThreadLoadStatus.Failed)
                throw new InvalidOperationException($"[ResourceManager] 资源加载失败: {path}");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var resource = ResourceLoader.LoadThreadedGet(realPath) as T
            ?? throw new InvalidOperationException($"[ResourceManager] 资源类型不匹配: {path} (期望 {typeof(T).Name})");

        _cache[realPath] = resource;
        return resource;
    }

    /// <summary>
    /// 加载资源并持有引用。调用方用完必须调用 Release，防止缓存误清。
    /// </summary>
    public async Task<T> LoadAndRetain<T>(string path) where T : Resource
    {
        var resource = await LoadAsync<T>(path);
        var realPath = ResolvePath(path);
        _refCounts.TryGetValue(realPath, out int count);
        _refCounts[realPath] = count + 1;
        return resource;
    }

    /// <summary>
    /// 释放一个引用。计数归零时将资源从强引用缓存移入弱引用池。
    /// </summary>
    public void Release(string path)
    {
        var realPath = ResolvePath(path);
        if (!_refCounts.TryGetValue(realPath, out int count) || count <= 0)
            return;

        count--;
        if (count <= 0)
        {
            _refCounts.Remove(realPath);
            if (_cache.TryGetValue(realPath, out var res))
            {
                _cache.Remove(realPath);
                _weakCache[realPath] = new WeakReference<Resource>(res);
            }
        }
        else
        {
            _refCounts[realPath] = count;
        }
    }

    /// <summary>
    /// 批量预加载资源。场景切换前调用以减少卡帧。
    /// </summary>
    public async Task PreloadBatch(IEnumerable<string> paths)
    {
        var tasks = paths.Select(p => LoadAsync<Resource>(p)).ToList();
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 清理弱引用池中已被 GC 回收的条目。
    /// </summary>
    public void CollectGarbage()
    {
        var deadKeys = new List<string>();
        foreach (var (key, weakRef) in _weakCache)
        {
            if (!weakRef.TryGetTarget(out _))
                deadKeys.Add(key);
        }

        foreach (var key in deadKeys)
            _weakCache.Remove(key);

        if (deadKeys.Count > 0)
            GD.Print($"[ResourceManager] 清理了 {deadKeys.Count} 个弱引用条目");
    }

    /// <summary>
    /// 将逻辑名解析为真实文件路径。
    /// </summary>
    private string ResolvePath(string logicName)
        => _pathMap.TryGetValue(logicName, out var realPath) ? realPath : logicName;

    /// <summary>
    /// 从 ConfigDataManager 加载路径映射表。（待实现）
    /// </summary>
    private void LoadPathMap()
    {
        // TODO: 从 ConfigDataManager 加载 "逻辑名 → 资源路径" 映射表
    }
}

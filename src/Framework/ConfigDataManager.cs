using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WuxiaProj.Framework;

/// <summary>
/// 配置数据管理器 — 最底层只读数据层。
/// 负责加载/缓存策划配置表（JSON），提供泛型查询接口。
/// Autoload 单例，在其他管理器之前就绪。
/// </summary>
public partial class ConfigDataManager : Node
{
    public static ConfigDataManager Instance { get; private set; } = null!;

    /// <summary>
    /// 按类型分桶的配置表存储：Type → (Id → Row)
    /// </summary>
    private readonly Dictionary<Type, Dictionary<string, object>> _tables = new();

    [Signal]
    public delegate void ConfigReloadedEventHandler();

    public override void _Ready()
    {
        Instance = this;
        GD.Print("[ConfigDataManager] 已就绪");
    }

    /// <summary>
    /// 注册一个配置表。filePath 指向 config/ 下的 JSON 文件。
    /// 目前仅建立空表占位，实际 JSON 反序列化留待后续实现。
    /// </summary>
    public void RegisterTable<T>(string filePath) where T : IConfigRow
    {
        var type = typeof(T);
        if (_tables.ContainsKey(type))
        {
            GD.PushWarning($"[ConfigDataManager] 配置表 {type.Name} 已注册，将被覆盖");
        }
        _tables[type] = new Dictionary<string, object>();
        GD.Print($"[ConfigDataManager] 注册配置表: {type.Name} ← {filePath}");
    }

    /// <summary>
    /// 按 id 查询单条配置。
    /// </summary>
    public T? Get<T>(string id) where T : class, IConfigRow
    {
        if (_tables.TryGetValue(typeof(T), out var table) && table.TryGetValue(id, out var row))
            return (T)row;
        return null;
    }

    /// <summary>
    /// 获取某张配置表的所有行。
    /// </summary>
    public IReadOnlyList<T> GetAll<T>() where T : class, IConfigRow
    {
        if (_tables.TryGetValue(typeof(T), out var table))
            return table.Values.Cast<T>().ToList();
        return Array.Empty<T>();
    }

    /// <summary>
    /// 按条件筛选配置行。
    /// </summary>
    public IReadOnlyList<T> Where<T>(Func<T, bool> predicate) where T : class, IConfigRow
    {
        if (_tables.TryGetValue(typeof(T), out var table))
            return table.Values.Cast<T>().Where(predicate).ToList();
        return Array.Empty<T>();
    }
}

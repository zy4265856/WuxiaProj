using System;
using System.Collections.Generic;
using WuxiaProj.Framework;

namespace WuxiaProj.Combat;

/// <summary>
/// 上下文级键值黑板。只允许值类型和专用 ID 的读写，不允许引用类型。
/// 子上下文通过 Snapshot() 继承父黑板，上下文结束后黑板随上下文丢弃。
/// </summary>
public class Blackboard
{
    private readonly Dictionary<string, object> _data = new();

    private static readonly HashSet<Type> AllowedTypes = new()
    {
        typeof(int), typeof(float), typeof(bool), typeof(string),
        typeof(CombatUnitId), typeof(CombatBuffId), typeof(ObjectId)
    };

    public void Set<T>(string key, T value)
    {
        var type = typeof(T);
        if (!AllowedTypes.Contains(type))
            throw new ArgumentException(
                $"[Blackboard] 不支持的类型: {type.Name}。仅允许值类型和战斗 ID 类型。");
        _data[key] = value!;
    }

    public T Get<T>(string key)
    {
        if (_data.TryGetValue(key, out var value))
            return (T)value;
        throw new KeyNotFoundException($"[Blackboard] 键不存在: {key}");
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (_data.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    public bool Has(string key) => _data.ContainsKey(key);

    public void Remove(string key) => _data.Remove(key);

    /// <summary>
    /// 深拷贝当前黑板，供子上下文继承。
    /// </summary>
    public Blackboard Snapshot()
    {
        var clone = new Blackboard();
        foreach (var (key, value) in _data)
            clone._data[key] = value;
        return clone;
    }
}

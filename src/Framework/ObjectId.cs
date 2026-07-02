using System;

namespace WuxiaProj.Framework;

/// <summary>
/// 游戏对象的全局唯一标识符。线程安全的自增 ID。
/// </summary>
public readonly struct ObjectId : IEquatable<ObjectId>
{
    public ulong Value { get; }

    private static ulong _nextId = 1;
    private static readonly object _lock = new();

    public ObjectId(ulong value)
    {
        Value = value;
    }

    public static ObjectId New()
    {
        lock (_lock)
        {
            return new ObjectId(_nextId++);
        }
    }

    public bool Equals(ObjectId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ObjectId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"ObjectId({Value})";

    public static bool operator ==(ObjectId left, ObjectId right) => left.Equals(right);
    public static bool operator !=(ObjectId left, ObjectId right) => !left.Equals(right);
}

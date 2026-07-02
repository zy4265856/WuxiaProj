using System;

namespace WuxiaProj.Combat;

/// <summary>
/// Buff 实例轻量标识符。值类型，由 BuffManager 在 Buff 施加时自增分配。
/// </summary>
public readonly struct CombatBuffId : IEquatable<CombatBuffId>
{
    public uint Value { get; }

    private static uint _nextId = 1;

    internal CombatBuffId(uint value)
    {
        Value = value;
    }

    public static CombatBuffId New()
    {
        return new CombatBuffId(_nextId++);
    }

    public bool Equals(CombatBuffId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is CombatBuffId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"BF({Value})";

    public static bool operator ==(CombatBuffId left, CombatBuffId right) => left.Equals(right);
    public static bool operator !=(CombatBuffId left, CombatBuffId right) => !left.Equals(right);
}

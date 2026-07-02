using System;

namespace WuxiaProj.Combat;

/// <summary>
/// 战斗内单位轻量标识符。值类型，战斗初始化时由 CombatManager 自增分配。
/// </summary>
public readonly struct CombatUnitId : IEquatable<CombatUnitId>
{
    public uint Value { get; }

    private static uint _nextId = 1;

    internal CombatUnitId(uint value)
    {
        Value = value;
    }

    public static CombatUnitId New()
    {
        return new CombatUnitId(_nextId++);
    }

    public bool Equals(CombatUnitId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is CombatUnitId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"CU({Value})";

    public static bool operator ==(CombatUnitId left, CombatUnitId right) => left.Equals(right);
    public static bool operator !=(CombatUnitId left, CombatUnitId right) => !left.Equals(right);
}

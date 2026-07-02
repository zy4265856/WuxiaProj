using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WuxiaProj.Combat;

/// <summary>
/// Buff 管理器。管理单位身上所有 Buff 的生命周期（施加/移除/Tick），
/// 并在施加/移除时自动向 HookBus 注册/注销编译后的委托。
/// </summary>
public partial class BuffManager : Node
{
    private readonly List<BuffState> _activeBuffs = new();
    private readonly HookBus _hookBus;

    public IReadOnlyList<BuffState> ActiveBuffs => _activeBuffs.AsReadOnly();

    public BuffManager(HookBus hookBus)
    {
        _hookBus = hookBus;
    }

    /// <summary>
    /// 从 BuffConfig 创建一个 BuffState 实例。
    /// 施加时自动向 HookBus 注册编译效果。
    /// </summary>
    public void Apply(BuffConfig config, CombatUnitId sourceUnit)
    {
        var compiled = BuffEffectCompiler.Compile(config);
        HandleStackRule(config);

        var state = new BuffState
        {
            Id = CombatBuffId.New(),
            ConfigId = config.Id,
            CompiledEffects = compiled,
            RemainingDuration = config.Duration,
            CurrentStacks = 1,
            SourceUnit = sourceUnit
        };

        _activeBuffs.Add(state);
        RegisterHooks(state);

        GD.Print($"[BuffManager] 施加 Buff: {config.DisplayName} " +
                 $"(ID: {state.Id}, 持续: {config.Duration})");
    }

    /// <summary>
    /// 移除 Buff 实例，自动注销其 Hook。
    /// </summary>
    public void Remove(CombatBuffId buffId)
    {
        var state = _activeBuffs.Find(b => b.Id == buffId);
        if (state == null) return;

        UnregisterHooks(state);
        _activeBuffs.Remove(state);
        GD.Print($"[BuffManager] 移除 Buff: {state.ConfigId}");
    }

    /// <summary>
    /// 按标签移除所有匹配的 Buff。
    /// </summary>
    public void RemoveByTag(string tag, BuffConfig[] allConfigs)
    {
        var configMap = allConfigs.ToDictionary(c => c.Id);
        var toRemove = _activeBuffs
            .Where(b => configMap.TryGetValue(b.ConfigId, out var c) && c.Tag == tag)
            .ToList();

        foreach (var buff in toRemove)
            Remove(buff.Id);
    }

    /// <summary>
    /// 回合结束 Tick。Turn 型 Buff 持续 -1，到期移除。
    /// </summary>
    public void TickTurn(BuffConfig[] allConfigs)
    {
        var configMap = allConfigs.ToDictionary(c => c.Id);
        var expired = new List<CombatBuffId>();

        foreach (var buff in _activeBuffs)
        {
            if (!configMap.TryGetValue(buff.ConfigId, out var config)) continue;
            if (config.DurationType != BuffDurationType.Turn) continue;
            if (config.Duration <= 0) continue;

            buff.RemainingDuration--;
            if (buff.RemainingDuration <= 0)
                expired.Add(buff.Id);
        }

        foreach (var id in expired)
            Remove(id);
    }

    /// <summary>
    /// 查询某属性类型的所有 Buff 修正总和。
    /// </summary>
    public int GetAttributeModifier(string attrName)
    {
        return 0;
    }

    private void HandleStackRule(BuffConfig config)
    {
        var existing = _activeBuffs.Find(b => b.ConfigId == config.Id);
        if (existing == null) return;

        switch (config.StackRule)
        {
            case BuffStackRule.Replace:
                Remove(existing.Id);
                break;
            case BuffStackRule.Extend:
                existing.RemainingDuration = config.Duration;
                break;
            case BuffStackRule.Independent:
                break;
            case BuffStackRule.Stack:
                if (existing.CurrentStacks < config.MaxStacks)
                    existing.CurrentStacks++;
                existing.RemainingDuration = config.Duration;
                break;
        }
    }

    private void RegisterHooks(BuffState state)
    {
        if (state.CompiledEffects == null) return;

        foreach (var (hookType, handler) in state.CompiledEffects.Handlers)
        {
            var compiledHook = new CompiledBuffHook(handler, state);
            RegisterHookToBus(hookType, compiledHook);
        }
    }

    private void UnregisterHooks(BuffState state)
    {
        if (state.CompiledEffects == null) return;

        foreach (var (hookType, handler) in state.CompiledEffects.Handlers)
        {
            var compiledHook = new CompiledBuffHook(handler, state);
            UnregisterHookFromBus(hookType, compiledHook);
        }
    }

    private void RegisterHookToBus(Type hookType, CompiledBuffHook hook)
    {
        typeof(HookBus)
            .GetMethod("Register")!
            .MakeGenericMethod(hookType)
            .Invoke(_hookBus, new object[] { hook });
    }

    private void UnregisterHookFromBus(Type hookType, CompiledBuffHook hook)
    {
        typeof(HookBus)
            .GetMethod("Unregister")!
            .MakeGenericMethod(hookType)
            .Invoke(_hookBus, new object[] { hook });
    }
}

/// <summary>
/// 编译生成的 Hook 适配器。将编译后的 Action&lt;HookContext&gt; 包装为 IBuffHook。
/// </summary>
internal sealed class CompiledBuffHook : IBuffHook
{
    private readonly Action<HookContext> _handler;

    public int Priority { get; }

    public CompiledBuffHook(Action<HookContext> handler, BuffState state)
    {
        _handler = handler;
        Priority = (int)state.Id.Value * 10;
    }

    public void OnHook(HookContext context)
    {
        _handler(context);
    }
}

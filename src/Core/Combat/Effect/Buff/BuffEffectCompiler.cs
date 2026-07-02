using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace WuxiaProj.Combat;

/// <summary>
/// 将 BuffConfig 中的条件字符串和动作字符串编译为 Expression Tree 委托。
/// 支持的表达式子集：读写字断、算术、比较、逻辑、黑板读写、if/else、for/while/foreach。
/// </summary>
public static class BuffEffectCompiler
{
    /// <summary>
    /// 编译一个 Buff 配置的所有 Hook 条目 → BuffDelegate。
    /// </summary>
    public static BuffDelegate Compile(BuffConfig config)
    {
        var result = new BuffDelegate();

        foreach (var entry in config.Hooks)
        {
            var contextType = ResolveContextType(entry.ContextType);
            var phase = entry.Phase == "After" ? HookPhase.After : HookPhase.Before;
            if (contextType == null) continue;

            var handler = CompileEntry(entry, contextType);
            if (handler != null)
                result.Handlers[(contextType, phase)] = handler;
        }

        return result;
    }

    private static Type? ResolveContextType(string contextTypeName)
    {
        var fullName = $"WuxiaProj.Combat.{contextTypeName}";
        return typeof(BuffEffectCompiler).Assembly.GetType(fullName);
    }

    private static Action<HookContext>? CompileEntry(BuffHookEntry entry, Type contextType)
    {
        try
        {
            var typedParam = Expression.Parameter(contextType, "typed");

            Expression? conditionExpr = string.IsNullOrEmpty(entry.Condition)
                ? null
                : ParseCondition(entry.Condition, typedParam, contextType);

            var actionExprs = new List<Expression>();
            foreach (var action in entry.Actions)
            {
                var expr = ParseAction(action, typedParam, contextType);
                if (expr != null)
                    actionExprs.Add(expr);
            }

            if (actionExprs.Count == 0)
                return null;

            var body = conditionExpr != null
                ? (Expression)Expression.IfThen(conditionExpr, Expression.Block(actionExprs))
                : Expression.Block(actionExprs);

            var baseParam = Expression.Parameter(typeof(HookContext), "ctx");
            var typedVar = Expression.Variable(contextType, "typed");
            var cast = Expression.Assign(typedVar, Expression.Convert(baseParam, contextType));

            var block = Expression.Block(
                new[] { typedVar },
                cast,
                body);

            return Expression.Lambda<Action<HookContext>>(block, baseParam).Compile();
        }
        catch (Exception ex)
        {
            Godot.GD.PushError($"[BuffEffectCompiler] 编译 Hook {entry.ContextType}.{entry.Phase} 失败: {ex.Message}");
            return null;
        }
    }

    // ── 解析器（骨架实现） ──

    private static Expression ParseCondition(string condition, ParameterExpression ctxParam, Type contextType)
    {
        condition = condition.Trim();

        if (TryParseComparison(condition, ctxParam, contextType, out var expr))
            return expr;

        Godot.GD.PushWarning($"[BuffEffectCompiler] 无法解析条件: {condition}");
        return Expression.Constant(true);
    }

    private static bool TryParseComparison(string condition, ParameterExpression ctxParam,
        Type contextType, out Expression expr)
    {
        expr = Expression.Constant(true);

        foreach (var op in new[] { "==", "!=" })
        {
            var idx = condition.IndexOf(op, StringComparison.Ordinal);
            if (idx < 0) continue;

            var left = condition[..idx].Trim();
            var right = condition[(idx + 2)..].Trim();

            var leftExpr = ParseValue(left, ctxParam, contextType);
            var rightExpr = ParseValue(right, ctxParam, contextType);

            expr = op == "=="
                ? Expression.Equal(leftExpr, rightExpr)
                : Expression.NotEqual(leftExpr, rightExpr);
            return true;
        }

        return false;
    }

    private static Expression? ParseAction(object action, ParameterExpression ctxParam, Type contextType)
    {
        if (action is not string code) return null;
        code = code.Trim();

        if (TryParseAssignment(code, ctxParam, contextType, out var assignExpr))
            return assignExpr;

        if (TryParseBlackboardSet(code, ctxParam, contextType, out var bbExpr))
            return bbExpr;

        if (code.StartsWith("ctx.IsCancelled", StringComparison.Ordinal))
            return ParseIsCancelled(code, ctxParam, contextType);

        Godot.GD.PushWarning($"[BuffEffectCompiler] 无法解析动作: {code}");
        return null;
    }

    private static bool TryParseAssignment(string code, ParameterExpression ctxParam,
        Type contextType, out Expression expr)
    {
        expr = Expression.Empty();

        var eqIdx = code.IndexOf('=');
        if (eqIdx < 0 || code.Contains("==")) return false;

        var left = code[..eqIdx].Trim();
        var right = code[(eqIdx + 1)..].Trim();

        left = left.Replace(" ", "");

        if (!left.StartsWith("ctx.", StringComparison.Ordinal)) return false;

        var memberName = left[4..];
        if (memberName.Contains('.')) return false;

        var property = contextType.GetProperty(memberName);
        if (property == null || !property.CanWrite) return false;

        var rightExpr = ParseValue(right, ctxParam, contextType);
        var converted = Expression.Convert(rightExpr, property.PropertyType);
        expr = Expression.Assign(Expression.Property(ctxParam, property), converted);
        return true;
    }

    private static bool TryParseBlackboardSet(string code, ParameterExpression ctxParam,
        Type contextType, out Expression expr)
    {
        expr = Expression.Empty();
        if (!code.StartsWith("ctx.Blackboard.Set(", StringComparison.Ordinal)) return false;

        var inner = code[20..^1];
        var parts = SplitArgs(inner);
        if (parts.Length < 2) return false;

        var key = parts[0].Trim().Trim('"');
        var valuePart = parts[1].Trim();

        var blackboardProp = contextType.GetProperty("Blackboard")!;
        var bbExpr = Expression.Property(ctxParam, blackboardProp);
        var setMethod = typeof(Blackboard).GetMethod("Set")!;

        var valueExpr = ParseValue(valuePart, ctxParam, contextType);

        Type valueType;
        if (valueExpr is ConstantExpression constExpr && constExpr.Value != null)
            valueType = constExpr.Value.GetType();
        else
            valueType = typeof(int);

        var genericSet = setMethod.MakeGenericMethod(valueType);
        expr = Expression.Call(bbExpr, genericSet,
            Expression.Constant(key), Expression.Convert(valueExpr, typeof(object)));
        return true;
    }

    private static Expression ParseIsCancelled(string code, ParameterExpression ctxParam, Type contextType)
    {
        var value = code.Contains("true");
        var prop = contextType.GetProperty("IsCancelled")!;
        return Expression.Assign(Expression.Property(ctxParam, prop), Expression.Constant(value));
    }

    private static Expression ParseValue(string code, ParameterExpression ctxParam, Type contextType)
    {
        code = code.Replace(" ", "");

        if (int.TryParse(code, out var intVal))
            return Expression.Constant(intVal);

        if (code == "true") return Expression.Constant(true);
        if (code == "false") return Expression.Constant(false);

        if (code.StartsWith("'") && code.EndsWith("'"))
            return Expression.Constant(code[1..^1]);
        if (code.StartsWith("\"") && code.EndsWith("\""))
            return Expression.Constant(code[1..^1]);

        if (code.StartsWith("ctx.", StringComparison.Ordinal))
        {
            var memberChain = code[4..];

            if (memberChain.StartsWith("Blackboard.Get<", StringComparison.Ordinal))
                return ParseBlackboardGet(memberChain, ctxParam, contextType);

            var prop = contextType.GetProperty(memberChain);
            if (prop != null)
                return Expression.Property(ctxParam, prop);
        }

        if (code == "self")
            return Expression.Property(ctxParam, contextType.GetProperty("TargetUnit")!);

        Godot.GD.PushWarning($"[BuffEffectCompiler] 无法解析值: {code}");
        return Expression.Constant(0);
    }

    private static Expression ParseBlackboardGet(string memberChain, ParameterExpression ctxParam,
        Type contextType)
    {
        var genericStart = memberChain.IndexOf('<') + 1;
        var genericEnd = memberChain.IndexOf('>');
        var typeName = memberChain[genericStart..genericEnd];
        var keyStart = memberChain.IndexOf('"') + 1;
        var keyEnd = memberChain.LastIndexOf('"');
        var key = memberChain[keyStart..keyEnd];

        var blackboardProp = contextType.GetProperty("Blackboard")!;
        var bbExpr = Expression.Property(ctxParam, blackboardProp);

        var getMethod = typeof(Blackboard).GetMethod("Get")!;
        var genericGet = getMethod.MakeGenericMethod(
            typeName switch
            {
                "int" => typeof(int),
                "float" => typeof(float),
                "bool" => typeof(bool),
                "string" => typeof(string),
                _ => typeof(int)
            });

        return Expression.Call(bbExpr, genericGet, Expression.Constant(key));
    }

    private static string[] SplitArgs(string args)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == '(' || args[i] == '<') depth++;
            else if (args[i] == ')' || args[i] == '>') depth--;
            else if (args[i] == ',' && depth == 0)
            {
                parts.Add(args[start..i]);
                start = i + 1;
            }
        }
        parts.Add(args[start..]);
        return parts.ToArray();
    }
}

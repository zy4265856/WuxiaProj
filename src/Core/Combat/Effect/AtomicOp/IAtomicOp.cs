namespace WuxiaProj.Combat;

/// <summary>
/// 原子操作接口。每个实现类代表战斗中的一个最小可执行效果单元。
/// </summary>
public interface IAtomicOp
{
    void Execute(HookContext context);
}

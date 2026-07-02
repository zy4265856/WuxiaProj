using WuxiaProj.Framework;
using WuxiaProj.UI.Models;
using WuxiaProj.UI.ViewModels;

namespace WuxiaProj;

/// <summary>
/// 服务注册入口。在游戏启动阶段（Main._Ready()）调用 RegisterAll()，
/// 集中注册所有 Model / ViewModel 到 ServiceLocator。
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// 注册所有服务。Model → Singleton，ViewModel → Transient。
    /// </summary>
    public static void RegisterAll()
    {
        // Model（单例）
        ServiceLocator.RegisterSingleton(new CharacterSheetModel());

        // ViewModel（每次打开面板创建新实例，构造时需传入当前上下文参数）
        // 注：此处仅示例 CharacterSheetViewModel 的注册方式，
        // 实际 caller 应在 OpenAsync 前注册当前角色 ID 等上下文。
        ServiceLocator.Register(() => new CharacterSheetViewModel(
            ServiceLocator.Resolve<CharacterSheetModel>(),
            ObjectId.New() // TODO: 替换为当前选中的角色ID
        ));
    }
}

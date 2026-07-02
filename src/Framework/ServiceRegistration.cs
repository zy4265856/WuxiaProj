using WuxiaProj.DevSample.UI.Models;
using WuxiaProj.DevSample.UI.ViewModels;
using WuxiaProj.Framework;

namespace WuxiaProj;

/// <summary>
/// 服务注册入口。在游戏启动阶段（Main._Ready()）调用 RegisterAll()，
/// 集中注册所有 Model / ViewModel 到 ServiceLocator。
/// </summary>
public static class ServiceRegistration
{
    public static void RegisterAll()
    {
        // [DevTest] 示例：注册 DevTest Model / ViewModel
        ServiceLocator.RegisterSingleton(new DevTestCharacterSheetModel());

        ServiceLocator.Register(() => new DevTestCharacterSheetViewModel(
            ServiceLocator.Resolve<DevTestCharacterSheetModel>(),
            ObjectId.New() // TODO: 替换为当前选中的角色ID
        ));
    }
}

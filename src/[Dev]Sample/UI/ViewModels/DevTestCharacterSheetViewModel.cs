using System.Collections.Generic;
using R3;
using WuxiaProj.DevSample.UI.Models;
using WuxiaProj.Framework;

namespace WuxiaProj.DevSample.UI.ViewModels;

/// <summary>
/// [DevTest] 角色属性面板的 ViewModel 示例。
/// 展示如何将 Model 数据转换为 View 可直接绑定的 R3 响应式属性。
/// </summary>
public class DevTestCharacterSheetViewModel : ViewModelBase
{
    public ReactiveProperty<string> Name { get; } = new("");

    public ReactiveProperty<int> Level { get; } = new(1);

    public ReactiveProperty<int> Hp { get; } = new(100);
    public ReactiveProperty<int> MaxHp { get; } = new(100);

    public ReactiveProperty<int> Mp { get; } = new(50);
    public ReactiveProperty<int> MaxMp { get; } = new(50);

    /// <summary>血量百分比，由 Hp / MaxHp 自动派生。</summary>
    public ReactiveProperty<float> HpRatio { get; } = new(1f);

    /// <summary>内力百分比。</summary>
    public ReactiveProperty<float> MpRatio { get; } = new(1f);

    /// <summary>十维属性字典（属性名 → 值）。</summary>
    public Dictionary<string, ReactiveProperty<int>> Attributes { get; } = new();

    /// <summary>技能列表。</summary>
    public List<DevTestSkillSlotViewModel> Skills { get; } = new();

    /// <summary>关闭面板命令。</summary>
    public ReactiveCommand OnClose { get; } = new();

    public DevTestCharacterSheetViewModel(DevTestCharacterSheetModel model, ObjectId characterId)
    {
        var data = model.LoadCharacter(characterId);

        Name.Value = data.Name;
        Level.Value = data.Level;
        Hp.Value = data.Hp;
        MaxHp.Value = data.MaxHp;
        Mp.Value = data.Mp;
        MaxMp.Value = data.MaxMp;

        // 派生属性：Hp / MaxHp → HpRatio
        Observable.CombineLatest(Hp, MaxHp)
            .Select(t => MaxHp.Value > 0 ? (float)t[0] / t[1] : 0f)
            .Subscribe(v => HpRatio.Value = v)
            .AddTo(Disposables);

        // 派生属性：Mp / MaxMp → MpRatio
        Observable.CombineLatest(Mp, MaxMp)
            .Select(t => MaxMp.Value > 0 ? (float)t[0] / t[1] : 0f)
            .Subscribe(v => MpRatio.Value = v)
            .AddTo(Disposables);

        // 十维属性
        Attributes["Qi"] = new ReactiveProperty<int>(data.Qi).AddTo(Disposables);
        Attributes["InnerBreath"] = new ReactiveProperty<int>(data.InnerBreath).AddTo(Disposables);
        Attributes["Physique"] = new ReactiveProperty<int>(data.Physique).AddTo(Disposables);
        Attributes["Comprehension"] = new ReactiveProperty<int>(data.Comprehension).AddTo(Disposables);
        Attributes["Agility"] = new ReactiveProperty<int>(data.Agility).AddTo(Disposables);
        Attributes["Willpower"] = new ReactiveProperty<int>(data.Willpower).AddTo(Disposables);
        Attributes["Fortune"] = new ReactiveProperty<int>(data.Fortune).AddTo(Disposables);
        Attributes["Charisma"] = new ReactiveProperty<int>(data.Charisma).AddTo(Disposables);
        Attributes["Vigor"] = new ReactiveProperty<int>(data.Vigor).AddTo(Disposables);
        Attributes["Precision"] = new ReactiveProperty<int>(data.Precision).AddTo(Disposables);

        foreach (var skill in data.Skills)
            Skills.Add(new DevTestSkillSlotViewModel(skill));
    }
}

/// <summary>
/// [DevTest] 单个技能槽位的 ViewModel。
/// </summary>
public class DevTestSkillSlotViewModel
{
    public ReactiveProperty<string> Name { get; }

    /// <summary>技能图标逻辑名，View 层通过 ResourceManager 加载实际贴图。</summary>
    public ReactiveProperty<string> IconPath { get; }

    public DevTestSkillSlotViewModel(DevTestSkillInfo info)
    {
        Name = new ReactiveProperty<string>(info.Name);
        IconPath = new ReactiveProperty<string>(info.IconPath);
    }
}

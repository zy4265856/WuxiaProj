using System.Collections.Generic;
using WuxiaProj.Framework;

namespace WuxiaProj.DevSample.UI.Models;

/// <summary>
/// [DevTest] 角色属性面板的 Model 层示例。
/// 展示如何从 ObjectManager / ConfigDataManager 获取角色数据并转换为数据载体。
/// </summary>
public class DevTestCharacterSheetModel
{
    public DevTestCharacterData LoadCharacter(ObjectId id)
    {
        // TODO: 从 ObjectManager + ConfigDataManager 获取真实数据
        return new DevTestCharacterData
        {
            Name = "示例角色",
            Level = 10,
            Hp = 450,
            MaxHp = 500,
            Mp = 200,
            MaxMp = 280,
            Qi = 15,
            InnerBreath = 12,
            Physique = 10,
            Comprehension = 14,
            Agility = 13,
            Willpower = 11,
            Fortune = 8,
            Charisma = 9,
            Vigor = 16,
            Precision = 12,
            Skills = new List<DevTestSkillInfo>
            {
                new() { Name = "基础剑法", IconPath = "icon/skill_sword_basic" },
                new() { Name = "轻功飞跃", IconPath = "icon/skill_leap" },
                new() { Name = "内功心法", IconPath = "icon/skill_inner" },
            }
        };
    }
}

/// <summary>
/// [DevTest] 角色数据载体。
/// </summary>
public class DevTestCharacterData
{
    public string Name { get; init; } = "";
    public int Level { get; init; }
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Mp { get; init; }
    public int MaxMp { get; init; }

    public int Qi { get; init; }
    public int InnerBreath { get; init; }
    public int Physique { get; init; }
    public int Comprehension { get; init; }
    public int Agility { get; init; }
    public int Willpower { get; init; }
    public int Fortune { get; init; }
    public int Charisma { get; init; }
    public int Vigor { get; init; }
    public int Precision { get; init; }

    public List<DevTestSkillInfo> Skills { get; init; } = new();
}

/// <summary>
/// [DevTest] 技能信息。
/// </summary>
public class DevTestSkillInfo
{
    public string Name { get; init; } = "";
    public string IconPath { get; init; } = "";
}

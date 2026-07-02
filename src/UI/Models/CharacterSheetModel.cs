using System.Collections.Generic;
using WuxiaProj.Framework;

namespace WuxiaProj.UI.Models;

/// <summary>
/// 角色属性面板的 Model 层。从 ObjectManager / ConfigDataManager 获取角色数据。
/// 当前使用模拟数据，后续对接真实数据源。
/// </summary>
public class CharacterSheetModel
{
    public CharacterData LoadCharacter(ObjectId id)
    {
        // TODO: 从 ObjectManager + ConfigDataManager 获取真实数据
        return new CharacterData
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
            Skills = new List<SkillInfo>
            {
                new() { Name = "基础剑法", IconPath = "icon/skill_sword_basic" },
                new() { Name = "轻功飞跃", IconPath = "icon/skill_leap" },
                new() { Name = "内功心法", IconPath = "icon/skill_inner" },
            }
        };
    }
}

/// <summary>
/// 角色数据载体。从游戏数据层获取后传递给 ViewModel。
/// </summary>
public class CharacterData
{
    public string Name { get; init; } = "";
    public int Level { get; init; }
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Mp { get; init; }
    public int MaxMp { get; init; }

    // 十维属性
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

    public List<SkillInfo> Skills { get; init; } = new();
}

/// <summary>
/// 技能信息。
/// </summary>
public class SkillInfo
{
    public string Name { get; init; } = "";
    public string IconPath { get; init; } = "";
}

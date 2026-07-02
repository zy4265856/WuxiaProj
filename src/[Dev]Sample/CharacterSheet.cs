using Godot;
using R3;
using WuxiaProj.Framework;

namespace WuxiaProj.DevSample;

/// <summary>
/// 角色属性面板 View。需配合 CharacterSheet.tscn 场景使用，
/// Export 字段在编辑器中绑定到 Godot 节点。
/// </summary>
public partial class CharacterSheet : UiPanel
{
    private CharacterSheetViewModel _vm = null!;

    // 基本信息
    [Export] private Label _nameLabel = null!;
    [Export] private Label _levelLabel = null!;

    // 血条
    [Export] private Label _hpLabel = null!;
    [Export] private ProgressBar _hpBar = null!;

    // 内力条
    [Export] private Label _mpLabel = null!;
    [Export] private ProgressBar _mpBar = null!;

    // 属性容器
    [Export] private VBoxContainer _attrContainer = null!;

    // 技能容器
    [Export] private GridContainer _skillGrid = null!;

    // 关闭按钮
    [Export] private Button _closeButton = null!;

    public override void _Ready()
    {
        if (!ServiceLocator.TryResolve<CharacterSheetViewModel>(out var vm) || vm == null)
        {
            GD.PushError("[CharacterSheet] ViewModel 未注册");
            return;
        }

        _vm = vm;
        BindViewModel();
    }

    private void BindViewModel()
    {
        // === 基本属性 ===
        _vm.Name.Subscribe(v => _nameLabel.Text = v).AddTo(ViewDisposables);
        _vm.Level.Subscribe(v => _levelLabel.Text = $"Lv.{v}").AddTo(ViewDisposables);

        // === HP ===
        _vm.Hp.CombineLatest(_vm.MaxHp, (hp, maxHp) => $"{hp} / {maxHp}")
            .Subscribe(v => _hpLabel.Text = v)
            .AddTo(ViewDisposables);
        _vm.HpRatio.Subscribe(v => _hpBar.Value = v).AddTo(ViewDisposables);

        // === MP ===
        _vm.Mp.CombineLatest(_vm.MaxMp, (mp, maxMp) => $"{mp} / {maxMp}")
            .Subscribe(v => _mpLabel.Text = v)
            .AddTo(ViewDisposables);
        _vm.MpRatio.Subscribe(v => _mpBar.Value = v).AddTo(ViewDisposables);

        // === 属性列表 ===
        foreach (var (attrName, attrValue) in _vm.Attributes)
        {
            var row = CreateAttributeRow(attrName, attrValue);
            _attrContainer.AddChild(row);
        }

        // === 技能列表 ===
        foreach (var skill in _vm.Skills)
        {
            var skillView = CreateSkillView(skill);
            _skillGrid.AddChild(skillView);
        }

        // === 关闭按钮 ===
        _closeButton.BindCommand(_vm.OnClose).AddTo(ViewDisposables);
        _vm.OnClose.Subscribe(_ => UiManager.Instance.Close(this))
            .AddTo(ViewDisposables);
    }

    /// <summary>
    /// 创建单行属性展示：Label("气劲") + Label("15")。
    /// </summary>
    private static HBoxContainer CreateAttributeRow(string name, ReactiveProperty<int> value)
    {
        var row = new HBoxContainer();

        var nameLabel = new Label { Text = name, CustomMinimumSize = new Vector2(120, 0) };
        row.AddChild(nameLabel);

        var valueLabel = new Label();
        value.Subscribe(v => valueLabel.Text = v.ToString());
        row.AddChild(valueLabel);

        return row;
    }

    /// <summary>
    /// 创建单个技能图标展示（简化版：显示技能名）。
    /// </summary>
    private static Control CreateSkillView(SkillSlotViewModel vm)
    {
        var skillItem = new VBoxContainer();

        var iconPlaceholder = new ColorRect
        {
            Color = new Color(0.3f, 0.3f, 0.3f),
            CustomMinimumSize = new Vector2(48, 48)
        };
        skillItem.AddChild(iconPlaceholder);

        var nameLabel = new Label { Text = "" };
        vm.Name.Subscribe(v => nameLabel.Text = v);
        skillItem.AddChild(nameLabel);

        return skillItem;
    }
}

using Godot;
using R3;
using WuxiaProj.DevSample.UI.ViewModels;
using WuxiaProj.Framework;

namespace WuxiaProj.DevSample.UI.Views;

/// <summary>
/// [DevTest] 角色属性面板 View 示例。
/// 展示 MVVM 模式下 View 层如何通过 R3 Subscribe 绑定 Godot 节点。
/// 需配合 CharacterSheet.tscn 场景使用，Export 字段在编辑器中绑定。
/// </summary>
public partial class DevTestCharacterSheet : UiPanel
{
    private DevTestCharacterSheetViewModel _vm = null!;

    [Export] private Label _nameLabel = null!;
    [Export] private Label _levelLabel = null!;

    [Export] private Label _hpLabel = null!;
    [Export] private ProgressBar _hpBar = null!;

    [Export] private Label _mpLabel = null!;
    [Export] private ProgressBar _mpBar = null!;

    [Export] private VBoxContainer _attrContainer = null!;

    [Export] private GridContainer _skillGrid = null!;

    [Export] private Button _closeButton = null!;

    public override void _Ready()
    {
        if (!ServiceLocator.TryResolve<DevTestCharacterSheetViewModel>(out var vm) || vm == null)
        {
            GD.PushError("[DevTestCharacterSheet] ViewModel 未注册");
            return;
        }

        _vm = vm;
        BindViewModel();
    }

    private void BindViewModel()
    {
        _vm.Name.Subscribe(v => _nameLabel.Text = v).AddTo(ViewDisposables);
        _vm.Level.Subscribe(v => _levelLabel.Text = $"Lv.{v}").AddTo(ViewDisposables);

        _vm.Hp.CombineLatest(_vm.MaxHp, (hp, maxHp) => $"{hp} / {maxHp}")
            .Subscribe(v => _hpLabel.Text = v)
            .AddTo(ViewDisposables);
        _vm.HpRatio.Subscribe(v => _hpBar.Value = v).AddTo(ViewDisposables);

        _vm.Mp.CombineLatest(_vm.MaxMp, (mp, maxMp) => $"{mp} / {maxMp}")
            .Subscribe(v => _mpLabel.Text = v)
            .AddTo(ViewDisposables);
        _vm.MpRatio.Subscribe(v => _mpBar.Value = v).AddTo(ViewDisposables);

        foreach (var (attrName, attrValue) in _vm.Attributes)
        {
            var row = CreateAttributeRow(attrName, attrValue);
            _attrContainer.AddChild(row);
        }

        foreach (var skill in _vm.Skills)
        {
            var skillView = CreateSkillView(skill);
            _skillGrid.AddChild(skillView);
        }

        _closeButton.BindCommand(_vm.OnClose).AddTo(ViewDisposables);
        _vm.OnClose.Subscribe(_ => UiManager.Instance.Close(this))
            .AddTo(ViewDisposables);
    }

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

    private static Control CreateSkillView(DevTestSkillSlotViewModel vm)
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

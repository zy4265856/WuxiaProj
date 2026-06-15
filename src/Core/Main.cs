using Godot;

namespace WuxiaProj;

/// <summary>
/// 主场景入口脚本，挂载到 main.tscn 的根节点。
/// </summary>
public partial class Main : Node2D
{
	public override void _Ready()
	{
		GD.Print("WuxiaProj 已启动");
	}
}

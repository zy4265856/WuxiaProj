# WuxiaProj - 2D武侠游戏

一款使用骰子驱动的2D武侠题材回合制策略RPG。

## 项目简介

玩家在江湖世界中扮演武者，通过修炼武功、收集装备、结交NPC来成长。核心战斗系统采用骰子驱动的战棋玩法，探索世界时骰子决定事件结果。

## 核心特性

- **骰子驱动战斗**：每回合投掷骰子决定可用行动和效果
- **联动骰系统**：武功、装备、天赋等提供额外骰子，可自由组合
- **战棋玩法**：2D网格地图，走位和策略同样重要
- **开放世界**：多个互联区域，丰富的NPC和事件

## 技术栈

- **引擎**：Godot Engine 4.x (.NET版本)
- **语言**：C# / GDScript
- **协作**：Git + GitHub

## 开发状态

项目处于设计阶段，详见 [设计大纲](docs/plans/2026-06-12-wuxia-game-design-outline.md)

## 快速开始

### 环境准备
1. 安装 [Git](https://git-scm.com/)
2. 安装 [Godot Engine .NET](https://godotengine.org/download)
3. 克隆本项目：
```bash
git clone https://github.com/zy4265856/WuxiaProj.git
cd WuxiaProj
```

### 运行项目
1. 打开 Godot Engine
2. 导入项目目录
3. 点击运行

## 项目结构

```
WuxiaProj/
├── docs/               # 项目文档
│   ├── plans/         # 设计文档
│   └── COLLABORATION_GUIDE.md
├── assets/            # 游戏资源
├── scripts/           # 游戏脚本
└── scenes/            # 场景文件
```

## 协作指南

新成员请先阅读 [协作指南](docs/COLLABORATION_GUIDE.md)

## 贡献指南

请查看 [CONTRIBUTING.md](CONTRIBUTING.md)

## 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

## 联系方式

- **QQChannel**: #dev 频道（开发讨论）
- **GitHub Issues**: [提交问题或建议](https://github.com/zy4265856/WuxiaProj/issues)

---

最后更新：2026-06-15

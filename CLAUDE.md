# CLAUDE.md — WuxiaProj

> 本文件为 Claude Code 提供本项目的工作指引。设计与协作的**完整细节**以 `docs/` 下文档为准，这里只放操作要点与硬性要求。

## 1. 项目概述

WuxiaProj 是一款 **2D 武侠题材回合制策略 RPG**：骰子驱动战棋 + 开放世界探索。玩家扮演武者，通过修炼武功、收集装备、结交 NPC 成长。

- 仓库：https://github.com/zy4265856/WuxiaProj
- 许可证：MIT

## 2. 当前阶段

**设计 / 头脑风暴阶段**。代码仅有最小脚手架（`Game.cs`、`Main.cs`），核心系统仍在设计文档中演进。

> ⚠️ 代码结构、目录划分、命名空间等约定**尚未固化**，本阶段不写入硬性规则，后续确定后再补充。新增代码请跟随现有两个文件的写法，但**不要自行发明复杂的目录/命名规则**。

## 3. 技术栈

| 项 | 值 |
|----|----|
| 引擎 | Godot Engine 4.6（.NET 版，Forward+ 渲染） |
| 语言 | C#（`TargetFramework=net8.0`、`Nullable=enable`、`LangVersion=latest`） |
| 程序集 / 命名空间 | `WuxiaProj` |
| 规划脚本 | GDScript（snake_case，遵循 GDScript 风格指南；当前尚无 `.gd`） |

需 .NET 8 SDK。安装 Godot 时务必选 **.NET 版**（非纯 GDScript 版）。

## 4. 常用命令

> `godot` 指 Godot .NET 版可执行文件；名称依安装方式可能为 `godot` 或 `Godot_v4.x-stable_win64.exe`，建议建别名。

| 操作 | 命令 |
|------|------|
| 构建 C# 程序集（类型/语法检查） | `dotnet build WuxiaProj.csproj` |
| 运行游戏 | `godot --path .`（需先设置主场景，见第 8 节） |
| 打开编辑器 | `godot --path . -e` |
| 导出发行版 | `godot --path . --export-release "Windows Desktop"` → `build/wuxia.exe` |
| 导出调试版 | `godot --path . --export-debug "Windows Desktop"` |

日常也可在 Godot .NET 编辑器内编辑、F5 运行；上述命令行用于构建与导出。

**测试**：暂无测试框架，**待定**。纯 C# 逻辑可考虑 xUnit（`dotnet test`）；场景/UI 测试可考虑 GdUnit4。确定后再补。

## 5. 仓库布局

```
WuxiaProj/
├── project.godot              # 引擎配置
├── WuxiaProj.csproj / .sln    # .NET 工程配置（必须提交）
├── export_presets.cfg         # 导出预设（Windows Desktop → build/wuxia.exe）
├── src/Core/                  # C# 源码（Game.cs 纯逻辑 / Main.cs 场景入口）
├── scenes/main/               # Godot 场景（.tscn，文本格式可合并）
├── docs/
│   ├── plans/                 # 设计文档，命名 YYYY-MM-DD-名称.md
│   │   ├── 2026-06-12-wuxia-game-design-outline.md       # 总设计大纲
│   │   └── 2026-06-15-combat-system-detailed-design.md   # 战斗系统详设
│   ├── brainstorm/            # 头脑风暴/未定型草稿
│   └── COLLABORATION_GUIDE.md
├── assets/  (规划)            # 美术/音频：ai_generated/ 原始 + sprites/ 成品
└── scripts/ (规划)            # GDScript（尚未创建）
```

## 6. 文档与协作约定

完整流程见 `CONTRIBUTING.md` 与 `docs/COLLABORATION_GUIDE.md`。核心要点：

- **提交信息**：Conventional Commits（`feat:`/`fix:`/`docs:`/`style:`/`refactor:`/`test:`/`chore:`），描述用中文。
- **分支**：`main` 分支**始终保持可运行**；功能用 `feature/`、修复用 `bugfix/`、个人实验用 `xxx/personal`。
- **文档命名**：`docs/plans/` 下用 `YYYY-MM-DD-名称.md`，正文用中文。

## 7. 给 Claude 的协作要求（硬性）

- **(a) 实现或修改任何游戏系统前，必须先阅读 `docs/plans/` 中对应的设计文档**（如骰子 / 战斗 / 存档），确保实现与设计一致；设计存在矛盾或缺失时先提出，不要自行拍板大改。
- **(b) 提交信息采用 Conventional Commits，描述用中文。** 不在 `main` 上直接做大改，先开分支。
- **(c) 改动设计文档时**：更新文档底部「最后更新」日期（改为当天），并保持 `YYYY-MM-DD-名称.md` 命名；新增的待补充文档（如存档系统）按该命名法创建占位链接。
- **(d) 文档引用同步**（硬性）：每当 `docs/plans/` 下有设计文档新增或内容修改时，必须同步更新相关文档间的交叉引用：
  - 新文档自身必须包含「相关文档」小节，链接到与之有承接/依赖关系的已有文档。
  - 已有文档中引用了该文档的（或其内容与本次变更相关的），需在「相关文档」小节中补上反向链接。
  - 引用格式统一使用 Obsidian Wikilink：`[[YYYY-MM-DD-名称|显示名称]]`。`docs/brainstorm/` 下的草稿不纳入引用体系。

## 8. 易踩坑 / 关键规则

- **`.csproj` 与 `.sln` 必须提交**（核心工程配置）；`bin/`、`obj/`、`.vs/`、`.godot/` 已在 `.gitignore` 忽略，勿手动提交。
- **主场景**：`project.godot` 的 `run/main_scene` 指向 `res://scenes/main/main.tscn`，故 `godot --path .` 可直接启动。新增入口场景后记得同步更新此处。
- **场景文件 `.tscn`**：文本格式可合并，但多人改同一场景易冲突；优先在编辑器内操作。
- **AI 美术资源**：保留原始文件与生成参数；来源记入 `docs/ASSET_LOG.md`（待创建）与 `assets/ai_generated/`。

## 9. 参考文档

- 设计总纲：`docs/plans/2026-06-12-wuxia-game-design-outline.md`
- 战斗系统详设：`docs/plans/2026-06-15-combat-system-detailed-design.md`
- 属性系统设计：`docs/plans/2026-06-25-attribute-system-design.md`
- 武侠世界观总纲：`docs/plans/2026-06-26-worldview-design.md`
- 框架脚手架设计：`docs/plans/2026-07-02-framework-scaffold-design.md`
- UI MVVM 技术规范：`docs/plans/2026-07-02-ui-mvvm-design.md`
- 战斗系统代码框架设计：`docs/plans/2026-07-02-combat-system-code-framework-design.md`
- 存档系统代码框架设计：`docs/plans/2026-07-02-save-system-design.md`
- 贡献指南：`CONTRIBUTING.md`
- 协作指南（Git/GitHub 新手向）：`docs/COLLABORATION_GUIDE.md`
- 项目说明：`README.md`

---

最后更新：2026-07-02

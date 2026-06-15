# 武侠游戏项目协作指南

## 给新手的说明

这是一份为远程协作团队准备的指南，所有成员都应该阅读。如果你对Git/GitHub不熟悉，请按顺序阅读本文档。

---

## 一、核心概念简介

### 什么是 Git？
Git是版本控制工具，用来记录代码的所有修改历史。这样：
- 代码永远不会丢失（可以回退任何版本）
- 多人协作不会互相覆盖
- 可以并行开发不同功能

### 什么是 GitHub？
GitHub是托管Git代码的网站，提供：
- 代码存储和备份
- 问题追踪（Issues）
- 项目看板（Projects）
- 协作工具（Pull Request等）

### 基本流程图
```
你修改代码 → 提交到本地 → 推送到GitHub → 其他人看到 → 合并进项目
```

---

## 二、工具准备

### 必需工具

**Git**
- 下载：https://git-scm.com/
- 安装时一路默认即可
- 安装后打开终端，输入 `git --version` 验证

**GitHub账号**
- 访问 https://github.com
- 注册免费账号
- 完成后告诉团队你的用户名

**代码编辑器**
- 推荐VS Code：https://code.microsoft.com/
- 安装 "C#" 和 "GitLens" 扩展

**Godot引擎**
- 下载：https://godotengine.org/download
- 选择 .NET 版本（支持C#）

---

## 三、第一次设置（一次性操作）

### 1. 配置Git身份
打开终端，执行：

```bash
git config --global user.name "你的名字"
git config --global user.email "你的邮箱"
```

### 2. 克隆项目
```bash
git clone https://github.com/你的用户名/WuxiaProj.git
cd WuxiaProj
```

---

## 四、日常开发流程

### 每次开始工作前
```bash
cd WuxiaProj
git pull origin main    # 获取最新代码
```

### 修改代码后提交
```bash
git add .               # 标记所有修改
git commit -m "描述你做了什么"    # 提交到本地
git push origin 你的分支名    # 推送到GitHub
```

### 提交信息规范
写清楚你做了什么，方便其他人理解：
```
✅ 好的例子：
"添加了骰子系统的基础数据结构"
"修复了战斗时移动骰子无法消耗的bug"
"更新了NPC对话的UI布局"

❌ 不好的例子：
"修改"
"update"
"测试"
```

---

## 五、分支管理（简化版）

### 我们的分支策略

```
main —— 主分支，始终是可运行的状态
  ├── feature/xxx —— 功能开发分支
  ├── bugfix/xxx —— 修复bug的分支
  └── xxx/personal —— 个人实验分支
```

### 创建新分支
```bash
git checkout -b feature/骰子系统
```

### 完成工作后
```bash
git checkout main        # 切回主分支
git pull origin main     # 更新主分支
git merge feature/骰子系统    # 合并你的工作
git push origin main      # 推送
git branch -d feature/骰子系统    # 删除分支
```

---

## 六、解决冲突

### 当多人修改同一文件时

```bash
git pull origin main
# 如果出现冲突，Git会告诉你哪些文件冲突
```

### 手动解决冲突
1. 打开冲突的文件，会看到：
```
<<<<<<< HEAD
你的代码
=======
别人的代码
>>>>>>> main
```

2. 保留需要的代码，删除标记：
```
最终想要的代码
```

3. 标记已解决：
```bash
git add 冲突文件
git commit -m "解决了xxx冲突"
```

---

## 七、Godot项目协作注意

### 需要提交的文件
- 所有 `.cs` / `.gd` 脚本文件
- `project.godot` 项目配置
- 资源文件：场景、纹理、音频等

### 不需要提交的文件
确保 `.gitignore` 包含：
```
# Godot
.import/
.temp/
 Godot/`

# C#
.vs/
bin/
obj/
*.sln
```

### 场景文件协作
Godot的场景文件（`.tscn`）是文本格式，可以合并。
但多人同时修改同一场景时容易冲突，建议：
- 一个场景由一人主要负责
- 或拆分成更小的子场景

---

## 八、AI美术资源管理

### 目录结构建议
```
WuxiaProj/
├── assets/
│   ├── ai_generated/      # AI生成的原始资源
│   │   ├── characters/
│   │   ├── environments/
│   │   └── ui/
│   └── sprites/           # 实际使用的处理后的资源
│       ├── characters/
│       ├── environments/
│       └── ui/
```

### 版权与版本追踪
- 在 `assets/ai_generated/` 保留原始文件和生成参数记录
- 记录使用的AI工具和提示词版本
- 考虑创建 `docs/ASSET_LOG.md` 记录资源来源

---

## 九、Issue模板

### 报告Bug时请包含：
1. **发生了什么**：简短描述
2. **重现步骤**：如何触发这个bug
3. **预期行为**：应该发生什么
4. **实际行为**：实际发生了什么
5. **环境信息**：Godot版本、操作系统

### 提出新功能时请包含：
1. **功能描述**：想要什么功能
2. **使用场景**：为什么需要
3. **实现建议**：（可选）你的想法

---

## 十、紧急情况

### 代码出错了？
```bash
git log               # 查看历史
git checkout <commit-id>    # 回退到某个版本
```

### 提交错了？
```bash
git reset HEAD~1      # 撤销最后一次提交（保留修改）
git reset --hard HEAD~1  # 撤销最后一次提交（丢弃修改）
```

### 搞砸了？
```bash
git checkout main     # 切回主分支
git branch -D 搞砸的分支  # 删除出错分支
```

---

## 十一、常用命令速查

```bash
# 查看状态
git status

# 查看历史
git log

# 查看差异
git diff

# 创建分支
git branch 分支名
git checkout -b 分支名

# 切换分支
git checkout 分支名

# 合并分支
git merge 分支名

# 删除分支
git branch -d 分支名

# 推送新分支
git push -u origin 分支名
```

---

## 十二、寻求帮助

如果你遇到问题：
1. 先看本指南相关章节
2. 在QQ Channel #dev频道询问
3. 在GitHub提Issue

记住：没有问题是愚蠢的问题，我们都是新手，一起学习！

---

## 附录：团队成员

| 昵称 | GitHub用户名 | 主要职责 |
|------|-------------|---------|
| （待填写） | | |

---

最后更新：2026-06-15

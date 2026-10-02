# Emerge / 共生体

20 天单人 Game Jam 项目，主题为「涌现」。完整设计见 [项目方案](../参考资料/共生体-完整项目方案.md)。

开发任务、里程碑与验收进度统一维护在 [开发计划与进度](Docs/开发计划与进度.md)。当前 T01 已完成，下一项为 T02：拖拽、旋转与编辑模式。

Git 仓库根目录为上一级 `gamejam_1`，同时管理工程与参考资料；日常操作见 [Git 管理说明](Docs/Git管理说明.md)。

## 环境

- Unity **6000.4.7f1**，使用本机已安装版本，不升级编辑器。
- 官方 URP 2D 模板，URP **17.4.0**，2D Renderer。
- Input System **1.19.0**，只启用新输入系统。
- VS Code 为主编辑器，使用 Microsoft Unity 扩展、C# Dev Kit 和 C# 扩展；Unity IDE 集成包 **2.0.27**。
- Windows x64，Mono，.NET Standard API，1280 × 720 可调整窗口。
- 2D 重力为零，固定模拟步长为 0.02 秒。
- 文本资产序列化、可见 `.meta` 文件；Library、缓存、生成的解决方案和构建包不纳入版本管理。

实际包版本固定在 `Packages/manifest.json` 和 `Packages/packages-lock.json`；第一次创建时 Unity 将模板中的包调整为当前编辑器兼容版本。

## 打开项目

在 Unity Hub 中选择「Add / 从磁盘添加」，选择本目录 `E:\gamejam\gamejam_1\emerge`，用 6000.4.7f1 打开。

主场景为 `Assets/_Emerge/Scenes/CellLab.unity`，已加入构建场景列表。进入 Play 后显示核心与纤毛样本，可用顶部按钮或 1 / 2 添加，Backspace 重置，Clear 清空，点击细胞查看类型与半径。它目前是基础实验室，还没有物理连接和移动；操作与实测结果见 [T01 验证记录](Docs/T01-验证记录.md)。

双击 C# 脚本使用已配置的 VS Code，路径为 `D:\VS Code\Microsoft VS Code\Code.exe`。用 VS Code 打开本项目文件夹，加载生成的 `emerge.slnx`；解决方案可重新生成，无需提交。当前 IDE 集成会自动选择 SLNX 格式并更新工作区设置。

Unity 打开本项目后，在 VS Code 按 F5，使用 `Attach to Unity` 配置附加调试，再在 Unity 中进入 Play 模式。配置已提供，实际断点命中需在加入运行时脚本后验证。若代码补全未启动，确认文件夹可信、C# Dev Kit 已加载解决方案，以及 Microsoft Unity 扩展已启用。

`.vscode` 保存扩展推荐、解决方案选择与附加调试配置。仍保留名为 `Visual Studio Editor` 的 Unity 包，它同时支持 VS Code；不安装旧版 `Visual Studio Code Editor` 包。Visual Studio 2022 和已安装的 Unity 工作负载保留为备用；`.vsconfig` 记录其所需组件。

## 目录

```text
Assets/
├─ _Emerge/
│  ├─ Scenes/              CellLab
│  ├─ Scripts/             Core、Cells、World、BuildEditor、Presentation
│  ├─ Data/                细胞、区域配置与输入动作
│  ├─ Prefabs/             Cells、World
│  ├─ Art/                 Sprites、Materials
│  ├─ Audio/
│  └─ UI/
├─ Editor/                 项目配置、验证与构建脚本
└─ Settings/               官方 URP 与 2D Renderer 资源
Tools/                     本机 Unity 启动辅助脚本
Logs/                      本地配置、验证与构建报告
Builds/Windows/            本地 Windows 测试包
```

模板的 SampleScene 和示例 InputSystem_Actions 保留作为参考，不是游戏入口。后续业务资源统一放在 `_Emerge` 中。文件移动需同时保留 `.meta`，优先使用 Unity 编辑器移动。

## 输入与物理约定

`Assets/_Emerge/Data/EmergeControls.inputactions` 定义 Gameplay 动作：

| 动作 | 绑定 |
| --- | --- |
| IntentW / IntentA / IntentS / IntentD | W / A / S / D，四个独立 Button |
| ToggleEditor | Tab |
| Assimilate | E |
| Pause | Esc |

这些只是输入定义，尚未连接执行器；WASD 没有预设方向含义。未来控制器须加载、启用并订阅这份动作资源，不使用模板 Move 动作替代四个意图通道，不使用旧的 `UnityEngine.Input` API。

物理层 8～11 分别为 Cell、Nutrient、Terrain、Hazard。碰撞矩阵暂保留默认值，待真实细胞和营养碰撞体完成后按需求收束；空场景不能作为物理稳定性验证。

## 配置与打包

Unity 菜单提供：

- `Emerge > Environment > Configure Project`：重新应用基准设置，已有 CellLab 和输入资源不会重建。
- `Emerge > Environment > Validate Project`：验证 URP、场景、零重力与 Windows 构建支持，写入 `Logs/environment-report.txt`。
- `Emerge > Build > Windows Development`：构建 `Builds/Windows/Emerge.exe`，写入构建报告。

重新配置会覆盖本文列出的玩家、物理与层设置；后续改动这些设置时须同步维护配置脚本。编辑器处于运行状态时不要同时启动批处理操作。

也可在关闭 Unity 后从项目目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Unity.ps1 -Action Configure
powershell -ExecutionPolicy Bypass -File .\Tools\Unity.ps1 -Action Validate
powershell -ExecutionPolicy Bypass -File .\Tools\Unity.ps1 -Action Build
```

脚本中编辑器路径仅适用于当前电脑；换机器后按实际安装位置调整。命令行创建和执行方法参考 [Unity 官方命令行文档](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor)，构建接口参考 [BuildPipeline](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/buildpipeline)，Visual Studio 组件标识参考 [Microsoft 官方列表](https://learn.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-community?view=visualstudio)。

## 当前边界

工程初始化与 T01 基础细胞实验室已完成；自由融合、物理关节、信号传播、营养闭环尚未开发。环境记录见 [环境配置记录](Docs/环境配置记录.md)，当前任务状态见开发进度表。

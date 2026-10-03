# Emerge / 共生体

新增组合实验库：Play 后点击右下角 **组合实验 [B]**，载入收缩移动、收缩膜口、360°旋转吸入及对照，保存和更新自己的布局与记录。Unity 顶部 **Emerge → 组合实验** 可创建、复制和精确编辑实验资产。操作、保存位置与实测见 [组合实验指南](Docs/组合实验库与测试指南.md)。

20 天单人 Game Jam 项目，主题为「涌现」。完整设计见 [项目方案](../参考资料/共生体-完整项目方案.md)。

开发任务、里程碑与验收进度统一维护在 [开发计划与进度](Docs/开发计划与进度.md)。当前 T01～T10 已完成，M1 工程验收与用户试玩通过，真实颗粒输送、定向滤食与正资源收益已验证；膜协同验证通过，吸收优化与提前执行的 T12 收缩已完成，下一项仍为 T11：温度、损伤与核心死亡。规模验证见 [T05 验证记录](Docs/T05-验证记录.md)。

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

主场景为 `Assets/_Emerge/Scenes/CellLab.unity`，已加入构建场景列表。进入 Play 后显示核心与纤毛样本，可用顶部按钮或 1 / 2 添加，Backspace 重置，按钮清空；点击并拖动细胞调整位置，Q / E 或滚轮旋转，Tab 切换编辑 / 游动模式。拖近其他细胞后松开可圆周吸附连接，已连接身体整体拖动；Shift 点击接触细胞补边，X 拆开，Delete 删除。只点击主核心直连的连接桥选择一个 W/A/S/D 出口信号；后续连接和纤毛自动跟随，无需配置通道或损耗，蓝箭头表示推水、橙箭头表示反作用力；Tab 进入游动后按对应键，收到核心出口信号的纤毛会通过真实刚体和关节推进身体。再次 Tab 暂停物理并清零速度以继续编辑。编辑时按住 WASD 预览激活、主动合力和转矩，身体保持静止；紫箭头为合力。示例按钮或 3 循环载入直行、偏转、反向、摄食、滤食、膜导流、收缩七种示例，会替换当前身体。按 4 添加吸收细胞，按 5 投放实验营养；游动模式才摄食、代谢和耗能，缺能会减弱推力，摄食后恢复。顶部显示中文资源统计，摄食示例流程见 [T08 记录](Docs/T08-验证记录.md)。滤食示例按 7 投放吸收区外的营养，再 Tab 游动、W 推水摄食；6 切换流场显示，编辑期间颗粒静止。对照与轨迹见 [T09 记录](Docs/T09-验证记录.md)。M1 验收与本机测试包说明见 [T07 记录](Docs/T07-M1验收记录.md)。界面为中文；核心出口规则与实测见 [T06 修正记录](Docs/T06-核心出口规则与验证.md)，后期换图见 [美术资产替换指南](Docs/美术资产替换指南.md)。

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

T04 已显式加载、克隆并订阅四个 Intent 动作，T06 在此基础上仅在主核心出口选择信号，下游自动传递，经节点保留率衰减，按收到通道的最大强度驱动真实受力；WASD 没有预设方向含义。其他动作资源尚未订阅，当前实验室模式切换使用 Tab 键；核心出口默认选 W；普通节点保留 90%，多键和多路径均不叠加放大。未使用模板 Move 或旧的 `UnityEngine.Input` API。

物理层 8～11 分别为 Cell、Nutrient、Terrain、Hazard。碰撞矩阵暂保留默认值，物理关节禁用相邻细胞碰撞，其他细胞与实验室 Terrain 边界正常碰撞；营养等后续碰撞体完成后按需求收束。T05 六组 20 / 30 细胞测试通过；当前采用刚性关节与速度 / 位置迭代 32 / 16，收缩或柔性改动须重新验证。

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

## 场景环境摆放

在 `Assets/_Emerge/Prefabs/World` 使用 FlowRegion（局部环境流场）、NutrientPatch（营养区域）、NutrientPoint（单颗放置点）；可拖进场景，也可用 `GameObject > Emerge` 菜单。CellLab 自带默认关闭的场景环境示例。活跃纤毛周围显示密集局部水流，6 切换显示；完整操作见 [场景放置指南](Docs/场景流场与营养放置指南.md)。

## 当前边界

工程初始化与 T01～T10 身体编辑、连接、物理推进、规模压力、核心出口信号、预览、营养代谢、局部流场与定向滤食已完成；膜阻挡、留存几何与导流摄食对照已完成；温度 / 损伤生存逻辑和地图尚未完成。环境记录见 [环境配置记录](Docs/环境配置记录.md)，当前任务状态见开发进度表。

## T10 膜细胞

8 添加膜；9 或「膜对照」按钮依次载入正确朝向、无膜、错误朝向的对照。Tab 游动后固定环境水流会推动颗粒，无需 WASD；白色膜片阻挡并导流，旋转改变路径。每轮观察约 13 秒，再 Tab 返回编辑切换对照。完整流程、替换资产与验证见 [T10 记录](Docs/T10-膜导流与验证.md)。

## 胃内消化与收缩

吸收细胞先兜住完整食物，等待后分摊速率逐渐消化，绿色颗粒留在体内随身体移动；顶部区分捕获与消化。0 添加收缩细胞，C 载入收缩示例；Tab 游动、按住 A 缩短相邻连接、松开恢复。只配置核心出口，其他细胞继续跟随，活动受真实供能约束。流程、边界与验证见 [吸收优化与 T12](Docs/吸收优化与T12-收缩验证.md)。

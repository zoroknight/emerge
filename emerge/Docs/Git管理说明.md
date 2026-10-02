# Git 与 GitHub 管理说明

## 仓库边界

本地仓库根目录：`E:\gamejam\gamejam_1`。`emerge` 为 Unity 项目，`参考资料` 保存设计依据。这样工程、方案和进度会在同一提交历史中。

主分支为 `main`。远程仓库由用户创建，地址为 [zoroknight/emerge](https://github.com/zoroknight/emerge)，使用 HTTPS 地址 `https://github.com/zoroknight/emerge.git` 作为 `origin`。远程与本地是否同步以 `git status` 和最新推送结果为准。

## 日常操作

在仓库根目录执行：

```powershell
git status
git diff
git add emerge/Assets emerge/Packages emerge/ProjectSettings emerge/Docs emerge/README.md 参考资料
git diff --cached --stat
git commit -m "feat: add cell lab runtime foundation"
git push
```

提交消息描述本次实际改动。一个可验收功能完成后提交一次，并同步更新开发进度；未验证功能不要在提交说明中宣称已完成。

改动范围较大时使用分支，例如：

```powershell
git switch -c feature/cell-lab
```

稳定通过阶段验收后再合并到 main。单人 Jam 的小改动可以直接提交 main，不必为每次文档更新建立 PR。提交、推送和是否合并按当次用户授权执行。

## Unity 特别约定

- 提交 Assets、Packages、ProjectSettings 及资源对应的 `.meta`。
- 文件移动优先在 Unity 编辑器内完成；不要丢弃或随意重建已有 GUID。
- 不提交 Library、Temp、Logs、UserSettings、Builds、IDE 缓存或自动生成的解决方案。
- 同时修改同一个场景或 Prefab 时应谨慎检查差异。无法确认冲突结果时重新在编辑器中验证。
- 构建包通过发布附件或赛事平台交付，不放入源码提交。
- 当前没有启用 Git LFS。大体积 PSD、音频、视频在加入 Git 历史之前评估是否启用 LFS；不要先提交超大二进制文件再补规则。
- 克隆后的编辑器路径因机器而异；本机 Tools/Unity.ps1 和配置脚本中的路径需按实际安装修改。

## 推送与登录

本地 Git 提交与 GitHub 登录是两回事。远程链接提供后，先设置 origin，再使用正常 Git HTTPS / SSH 登录方式推送。若首次推送要求登录，交由用户完成浏览器或系统凭据提示；不在源码、文档或聊天中记录令牌。

本次用户未授权读取既有 GitHub 凭据，后续不单独提取凭据；推送仅使用正常 Git 认证流程。

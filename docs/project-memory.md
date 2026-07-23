# 轻截项目记忆

这份文档保存跨对话仍应延续的产品取舍、当前实现入口和发布约定。它不是需求清单，也不替代源码、测试或 GitHub 的实时状态。涉及版本、文件大小和线上资产时，先按文末命令重新核对。

## 当前快照

- 快照日期：2026-07-23。
- 主程序版本：`1.11.2`。
- 长截图模块版本：`1.1.0`。
- 录屏模块版本：`1.7.0`，最低要求主程序 `1.10.0`。
- 本地 OCR 模块版本：`1.1.0`，最低要求主程序 `1.11.0`。
- PP-OCR Tiny 与 PP-OCR Small 模块版本：均为 `1.0.0`，最低要求主程序 `1.11.0`。
- 二维码扫描模块版本：`1.0.0`，最低要求主程序 `1.11.0`。
- GitHub 仓库：`XDIOEZ/CutCut`，默认分支 `main`。
- 当前 Release：`v1.11.2`。
- 发布首页：<https://xdioez.github.io/CutCut/>。
- 模块下载页：<https://xdioez.github.io/CutCut/modules.html>。

## 已确认的产品取舍

### 启动与设置工作台

- 首次运行，或配置中的 `lastLaunchedVersion` 与当前主程序版本不一致时，主动打开设置工作台并选中“截图设置”。这是帮助新用户完成必要配置、让升级用户检查新选项的固定行为。
- 首次/更新检查优先级高于“手动启动后最小化”和 `--background`；同一版本完成标记后，后续启动才恢复用户选择的安静启动行为。
- “开机自动启动”是重要功能：使用当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名为 `LightShotCN`，命令为带引号的当前 EXE 绝对路径加 `--background`。不要求管理员权限，登录后安静进入托盘。
- 相关入口：`Application/StartupWorkspaceService.cs`、`Core/StartupWorkspacePolicy.cs`、`Infrastructure/WindowsRunStartupEntryStore.cs`、`Application/StartupRegistrationService.cs`、`Presentation/MainForm.cs`。

### 插件与设置页

- 设置工作台包含宿主级“插件模块”页，使用纵向单列布局。用户可以查看已安装或已禁用模块，并自主启用、禁用或永久删除。
- 禁用只退役程序集并保留文件和跨重启标记；永久删除会删除该模块自己的一级目录。删除前必须明确提示不可恢复，并说明重新安装需要前往模块发布页下载。
- 模块自己的设置页仍由模块契约动态加入导航；模块禁用或删除后立即移除，重新启用后恢复。
- 轻量版和重量版预装长截图、本地 OCR、二维码扫描与录屏模块，但仍要保留独立模块包，供按需安装或永久删除后恢复。PP-OCR Tiny/Small 模型不进入这两个常规包，避免破坏 5 MiB / 90 MiB 体积约束；全插件完全版以重量版为底包，额外预装 Tiny、Small 及其模型，作为一次装齐全部插件的手动下载选项。
- 本地 OCR 保持稳定命令 ID `screenshot-tool.ocr.recognize`，使用 Windows 自带离线 OCR；识别前对原图、高清放大、灰度增强和 Otsu 二值化候选结果分别识别并择优。PP-OCR Tiny 和 Small 是稳定 ID、独立目录、独立依赖与模型的两个模块，分别偏向体积/速度和复杂场景精度。三者都不上传图片；成功后关闭截图遮罩，并由宿主在选区旁打开可编辑、可复制的独立文本结果窗。
- 三种 OCR 可以同时安装，但默认建议用户只启用其中一个。对应目录为 `Modules\Ocr`、`Modules\PaddleOcrTiny` 和 `Modules\PaddleOcrSmall`；任一模块的禁用、删除或替换不得影响另外两个。
- 二维码扫描通过稳定命令 ID `screenshot-tool.qr-code.scan` 离线扫描当前截图选区，只尝试 QR Code；成功后复用宿主侧边结果窗显示原始内容，不自动打开网址或执行二维码内容。入口 DLL、私有 `zxing.dll` 与许可文本共同位于 `Modules\QrCode`。
- 相关入口：`Infrastructure/Modules/ModuleHost.cs`、`Infrastructure/Modules/ModuleLoadContext.cs`、`Presentation/Pages/ModuleManagementPage.cs`、`ScreenshotTool.Ocr`、`ScreenshotTool.PaddleOcr*`、`ScreenshotTool.Contracts/ModuleContracts.cs`。

### 软件内更新

- 设置工作台包含宿主级“软件更新”页，使用纵向单列布局。更新只检查 `XDIOEZ/CutCut` 的 GitHub 最新正式 Release，不自动安装预发布版，也不上传截图、设置或其他本地内容。
- 当前电脑已安装 .NET 8 或更高 Windows Desktop Runtime 时下载 `complete-lightweight-win-x64.zip`；没有系统桌面运行库时下载 `complete-portable-win-x64.zip`，保证原本依靠自带运行库运行的用户更新后仍能启动。
- 更新必须验证 GitHub Release API 对资产提供的 `sha256:` digest、压缩包大小、解压路径和包内 `ScreenshotTool.exe` 文件版本。摘要缺失、版本不一致、文件超限或 ZIP 路径越界时停止安装。
- 下载和解压只发生在 `%LocalAppData%\LightShotCN\Updates`。校验完成后启动独立 PowerShell 更新进程，主程序正常退出；更新进程先备份将覆盖的文件，再原地替换并自动重启。替换失败时尝试回滚，并在重启后的工作台显示结果。安装目录不可写时请求当前操作所需的 UAC 授权。
- 整包更新只升级当前仍存在的 `Modules/<模块目录>`；用户已永久删除的模块不会因为完整包中预装该模块而被重新安装，禁用但仍保留文件的模块会正常更新且保持禁用标记。
- 软件内更新依赖每个正式 Release 同时提供稳定命名的轻量与便携完整包。用户必须先运行一次已经包含更新页的版本；再往后的正式版才可完全通过软件内按钮更新。
- 相关入口：`Abstractions/IApplicationUpdateService.cs`、`Infrastructure/GitHubReleaseApplicationUpdateService.cs`、`Presentation/Pages/ApplicationUpdatePage.cs`、`Application/CompositionRoot.cs`、`Presentation/MainForm.cs`。

### 发布页体验

- 轻量版是主下载入口，保持视觉优先级最高；它依赖目标电脑安装 .NET 8 Desktop Runtime。
- 内置 .NET 8 的重量版是次级、小尺寸按钮，避免喧宾夺主，但必须能够一键直接下载，不能把普通用户转到 Releases 自己找文件。
- 全插件完全版排在重量版之后，同样内置 .NET 8，并预装长截图、本地 OCR、PP-OCR Tiny、PP-OCR Small、二维码扫描和录屏全部模块；它是手动下载档位，不改变软件内更新对轻量版/重量版的自动选包规则。
- 模块页上的长截图、本地 OCR、PP-OCR Tiny、PP-OCR Small、二维码扫描和录屏也必须一键直接下载 ZIP。虽然独立下载使用率可能较低，但它是完整的恢复路径。
- 页面只有在 GitHub API 暂时不可用或某个未来 Release 确实没有对应资产时才允许显示回退状态。对正式承诺提供的版本，不应只改文案或放假链接；应上传真实资产。
- 首页与模块页都是 `site/` 下的纯静态页面，通过 GitHub API 读取最新 Release。只补充同一 Release 的资产时不需要重新部署 Pages，页面会自动识别；修改 `site/**` 并推送到 `main` 时由 `.github/workflows/pages.yml` 部署。
- 首页用“系统负责截一下，轻截负责当场做完”解释与 Windows 自带截图的差异。表达必须客观承认系统工具零安装、偶尔截图很方便，重点突出轻截的对象级标注编辑、双向长截图、录屏实时批注和插件自由装卸，避免贬低式比较。
- 首页功能区后使用三张真实软件截图轮播演示精准框选、对象级标注和长截图预览。轮播自动播放，但必须保留前后切换、圆点跳转、键盘方向键、悬停/聚焦暂停和减少动态效果支持；每张图都提供通往下载区的明确入口。
- 相关入口：`site/release.js`、`site/modules.js`、`site/index.html`、`site/modules.html`。

## 发布资产契约

发布页依赖下列稳定文件名；修改命名时必须同时更新发布脚本、页面识别规则、文档和线上验证：

| 资产 | 固定文件名 | 页面用途 |
| --- | --- | --- |
| 轻量完整包 | `complete-lightweight-win-x64.zip` | 首页主下载按钮 |
| 内置运行库重量完整包 | `complete-portable-win-x64.zip` | 首页次级直接下载按钮 |
| 内置运行库全插件完全包 | `complete-full-win-x64.zip` | 首页第三个完全版按钮 |
| 长截图独立模块 | `long-capture-addon-win-x64.zip` | 模块页长截图按钮 |
| 本地 OCR 独立模块 | `ocr-addon-win-x64.zip` | 模块页本地 OCR 按钮 |
| PP-OCR Tiny 独立模块 | `paddle-ocr-tiny-addon-win-x64.zip` | 模块页 PP-OCR Tiny 按钮 |
| PP-OCR Small 独立模块 | `paddle-ocr-small-addon-win-x64.zip` | 模块页 PP-OCR Small 按钮 |
| 二维码扫描独立模块 | `qr-code-addon-win-x64.zip` | 模块页二维码扫描按钮 |
| 录屏独立模块 | `screen-recording-addon-win-x64.zip` | 模块页录屏按钮 |
| 校验和 | `SHA256SUMS.txt` | Release 完整性校验 |

`v1.11.2` 起正式提供表中的全部十个固定资产。`v1.11.2` 发布候选包核对结果为：

- 轻量完整包约 `1.24 MiB`。
- 重量完整包约 `59.04 MiB`。
- 全插件完全包约 `112.25 MiB`。
- 长截图独立模块约 `0.06 MiB`。
- 本地 OCR 独立模块约 `0.02 MiB`。
- PP-OCR Tiny 独立模块约 `16.64 MiB`。
- PP-OCR Small 独立模块约 `36.57 MiB`。
- 二维码扫描独立模块约 `0.23 MiB`。
- 录屏独立模块约 `0.45 MiB`。
- 轻量版和重量版已预装长截图、本地 OCR、二维码扫描与录屏；完全版在重量版基础上再预装 PP-OCR Tiny/Small，因此包含当前全部插件。独立模块包保持程序旁 `Modules/<模块目录>/...` 的目录结构，解压到程序目录即可安装。

发布脚本 `scripts/Publish-Release.ps1` 会生成三种完整包、六个独立模块包、免解压运行目录和 `SHA256SUMS.txt`，并检查轻量版小于 `5 MiB`、重量版小于 `90 MiB`、完全版未压缩目录小于 `180 MiB` 且最终 ZIP 小于 `130 MiB`。本地 OCR 与二维码扫描的独立恢复资产名分别固定为 `ocr-addon-win-x64.zip` 和 `qr-code-addon-win-x64.zip`；PP-OCR 通过 `scripts/Get-PaddleOcrModels.ps1` 下载固定版本并核对 SHA-256，再由 `scripts/Publish-PaddleOcrModule.ps1` 分别组装 Tiny/Small。只有用户在当前需求中明确要求打包或发布时才运行发布脚本；不能因为记忆里已有 Release 就擅自发布。

## 后续开发时优先保持

- “轻截”的产品身份是轻巧、低干扰：主界面和发布页优先突出轻量版，重量版、全插件完全版与可选模块作为补充，但补充入口也必须完整可用。
- 新设置项继续采用纵向单列、一行一个设置项；不要把多个独立开关横向塞在同一行。
- 新功能优先进入独立服务、页面或模块，不继续扩大 `MainForm`、`CaptureOverlayForm` 和 `CompositionRoot` 的职责。
- 新增可选模块时，除模块实现和生命周期测试外，还要补齐模块管理显示、独立发布脚本、模块页卡片、稳定资产名和永久删除后的恢复路径。
- 发布后不要只看 GitHub Actions 成功状态：还要在线核对按钮文案、`href`、`download` 属性，并实际重新下载资产核对大小或 SHA-256。
- 软件内更新要求正式 Release 同时保留两个完整包的固定文件名和 GitHub `sha256:` digest；发布后应从更新页分别验证“有桌面运行库”和“无桌面运行库”两条选包路径。更新程序不得重新安装已永久删除的模块。

## 核对与验证入口

获取当前版本和线上资产，不要只依赖本快照：

```powershell
Select-Xml -Path .\src\ScreenshotTool\ScreenshotTool.csproj -XPath '/Project/PropertyGroup/Version'
gh release view --json tagName,name,publishedAt,assets,url
```

常规交付前至少执行：

```powershell
dotnet format .\ScreenshotTool.sln --verify-no-changes
dotnet run --project .\tests\ScreenshotTool.LogicTests\ScreenshotTool.LogicTests.csproj -c Release
$verificationRoot = Join-Path (Resolve-Path .) 'artifacts\build-verification'
dotnet build .\ScreenshotTool.sln -c Release -p:UseArtifactsOutput=true "-p:ArtifactsPath=$verificationRoot"
```

涉及设置工作台时，按变更范围运行 `ScreenshotTool.UiPreview` 对应的 smoke 参数，例如 `--screenshot-settings-page-smoke`、`--main-navigation-smoke`、`--module-management-page-smoke` 或 `--application-update-page-smoke`，并查看生成图片。涉及发布页时，除静态脚本检查外，还要在已部署网址中检查 GitHub API 填充后的真实状态。

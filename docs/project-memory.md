# 轻截项目记忆

这份文档保存跨对话仍应延续的产品取舍、当前实现入口和发布约定。它不是需求清单，也不替代源码、测试或 GitHub 的实时状态。涉及版本、文件大小和线上资产时，先按文末命令重新核对。

## 当前快照

- 快照日期：2026-07-24。
- 主程序版本：`1.11.6`。
- 长截图模块版本：`1.1.0`。
- 录屏模块版本：`1.7.0`，最低要求主程序 `1.10.0`。
- 本地 OCR 模块版本：`1.2.0`，最低要求主程序 `1.11.6`。
- PP-OCR Tiny 与 PP-OCR Small 模块版本：均为 `1.1.0`，最低要求主程序 `1.11.6`。
- 二维码扫描模块版本：`1.0.0`，最低要求主程序 `1.11.0`。
- 贴图悬浮窗模块版本：`1.0.0`，最低要求主程序 `1.11.5`。
- GitHub 仓库：`XDIOEZ/CutCut`，默认分支 `main`。
- 当前 Release：`v1.11.6`。
- 发布首页：<https://xdioez.github.io/CutCut/>。
- 模块下载页：<https://xdioez.github.io/CutCut/modules.html>。

## 已确认的产品取舍

### 启动与设置工作台

- 首次运行，或配置中的 `lastLaunchedVersion` 与当前主程序版本不一致时，主动打开设置工作台并选中“截图设置”。这是帮助新用户完成必要配置、让升级用户检查新选项的固定行为。
- 首次/更新检查优先级高于“手动启动后最小化”和 `--background`；同一版本完成标记后，后续启动才恢复用户选择的安静启动行为。
- “开机自动启动”是重要功能：使用当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名为 `LightShotCN`，命令为带引号的当前 EXE 绝对路径加 `--background`。不要求管理员权限，登录后安静进入托盘。
- 开机自启动的用户选择同时持久化到配置文件；程序每次启动都会按该选择重新同步启动项。已有稳定启动项会自动迁移到配置，程序移动或原地更新后会把启动命令修复为当前 EXE 路径，避免注册表项丢失或旧路径导致重启后无法启动。
- 相关入口：`Application/StartupWorkspaceService.cs`、`Core/StartupWorkspacePolicy.cs`、`Infrastructure/WindowsRunStartupEntryStore.cs`、`Application/StartupRegistrationService.cs`、`Presentation/MainForm.cs`。

### 查看截图与再次编辑

- “查看截图”页同时展示保存目录直属层级中的受支持图片与录屏 MP4；双击时交给系统默认图片查看器或视频播放器打开。图片右键依次提供“编辑”“复制”和“删除”，其中“复制”把原始截图像素写入系统剪贴板且不锁定源文件；视频没有编辑能力，也不提供图片像素复制，因此右键只显示“删除”。
- “查看截图”页顶部提供文件名实时搜索框和排序菜单；排序固定支持保存时间最新优先、保存时间最早优先、名称 A→Z、名称 Z→A 四种模式，按钮文字应显示当前模式。搜索忽略大小写并先筛选再排序，`Esc` 可清空当前搜索词。
- “编辑”把选中的已保存图片载入现有截图覆盖层，显示整张原图并复用矩形、椭圆、箭头、画笔、文字、马赛克、撤销、粗细和颜色等编辑能力。保存时沿用截图命名规则生成新的 PNG，不覆盖或删除原文件。
- 进入已有图片或贴图的重新编辑模式时，编辑覆盖层仍使用虚拟桌面坐标，但初始图片只在进入编辑瞬间鼠标所在的显示器范围内缩放并居中，不能以整个虚拟桌面居中而跨在多显示器接缝上。
- 箭头被单独选中时只显示箭尾起点和箭头终点两个缩放手柄，不显示外接框四边中点及另外两个边角手柄；命中与缩放也只响应这两个可见端点。
- “图片修改”设置页提供“Alt 移动元素方式”，默认保持“按住 Alt 临时移动”，也可选择“裸按一次 Alt 切换移动模式开关”。该选择统一作用于普通截图编辑、已有图片/贴图重新编辑、录屏实时批注和文字输入框；Alt+滚轮旋转等组合操作不能误触发切换。
- 新建或重新编辑文字时，无需退出文字输入模式；移动模式开启后可以从文字输入框内任意位置拖动整个输入框，松开后继续保持焦点、光标位置和输入状态。移动模式关闭时，左键拖动仍用于选择文字。
- 未处于文字编辑状态时粘贴剪贴板文本，必须在鼠标位置打开与文字工具相同的透明输入框并预填内容，继续使用当前颜色、粗细、输入、选区和撤销能力；提交后统一生成普通文字元素。不得再为粘贴文字维护独立的深色背景元素、渲染样式或重新编辑分支。粘贴图片仍生成图片贴纸。
- 文字输入框拥有独立的撤销历史；编辑文字期间按 `Ctrl+Z` 优先撤销输入、删除、剪切或粘贴造成的文字变化，并恢复当时的光标与选区，不得误触发画布级批注撤销。没有正在编辑的文字框时，`Ctrl+Z` 仍撤销上一项画布批注操作。
- “删除”必须二次确认，并将文件移入 Windows 回收站而不是永久删除；只允许管理当前截图保存目录直属层级中的受支持图片或 MP4 视频。
- 相关入口：`Presentation/Pages/ScreenshotGalleryPage.cs`、`Presentation/Pages/ScreenshotGalleryQuery.cs`、`Abstractions/ISavedScreenshotService.cs`、`Infrastructure/SavedScreenshotService.cs`、`Editing/AnnotationHandleLayout.cs`、`Editing/AnnotationMoveActivationState.cs`、`Presentation/ExistingImageEditLayout.cs`、`Presentation/TransparentTextEditorControl.cs`、`Presentation/MainForm.cs`、`Presentation/CaptureOverlayForm.cs`。

### 插件与设置页

- 设置工作台包含宿主级“插件模块”页，使用纵向单列布局。用户可以查看已安装或已禁用模块，并自主启用、禁用或永久删除。
- “插件模块”页内使用“已启用模块”和“已禁用模块”两个状态分页；分页只显示对应状态的模块，加载失败模块归入禁用分页以保留恢复入口。已启用模块名称使用绿色，已禁用或加载失败模块名称使用红色。
- 禁用只退役程序集并保留文件和跨重启标记；永久删除会删除该模块自己的一级目录。删除前必须明确提示不可恢复，并说明重新安装需要前往模块发布页下载。
- 模块自己的设置页仍由模块契约动态加入导航；模块禁用或删除后立即移除，重新启用后恢复。
- 从 `v1.11.5` 起，轻量版和重量版预装贴图悬浮窗、长截图、本地 OCR、二维码扫描与录屏模块，但仍要保留独立模块包，供按需安装或永久删除后恢复。PP-OCR Tiny/Small 模型不进入这两个常规包，避免破坏 5 MiB / 90 MiB 体积约束；轻量完全版与完全版分别以轻量版、重量版为底包，额外预装 Tiny、Small 及其模型，作为依赖系统 .NET 或自带运行库的一次装齐全部插件选项。
- 本地 OCR 保持稳定命令 ID `screenshot-tool.ocr.recognize`，使用 Windows 自带离线 OCR；识别前对原图、高清放大、灰度增强和 Otsu 二值化候选结果分别识别并择优。PP-OCR Tiny 和 Small 是稳定 ID、独立目录、独立依赖与模型的两个模块，分别偏向体积/速度和复杂场景精度。三者都不上传图片；成功后关闭截图遮罩，并由宿主在选区旁打开可编辑、可复制的独立文本结果窗。
- 本地 OCR、PP-OCR Tiny 和 PP-OCR Small 开始识别后，当前点击的工具栏按钮必须原位切换为带“识别中…”文字的连续动画进度条；没有识别到文字或执行失败时恢复原按钮，识别成功时随截图会话一起关闭。OCR 引擎没有可校准的百分比时不得显示虚假的完成百分比。
- OCR 与二维码扫描始终读取当前未标注的截图源：普通截图读取桌面选区；已有图片、贴图再次编辑或长图替换结果存在时读取当前替换图片的原始像素，不能穿透编辑窗口读取其下方桌面。
- 三种 OCR 可以同时安装，但默认建议用户只启用其中一个。对应目录为 `Modules\Ocr`、`Modules\PaddleOcrTiny` 和 `Modules\PaddleOcrSmall`；任一模块的禁用、删除或替换不得影响另外两个。
- 二维码扫描通过稳定命令 ID `screenshot-tool.qr-code.scan` 离线扫描当前截图选区，只尝试 QR Code；成功后复用宿主侧边结果窗显示原始内容，不自动打开网址或执行二维码内容。入口 DLL、私有 `zxing.dll` 与许可文本共同位于 `Modules\QrCode`。
- 贴图悬浮窗使用稳定模块 ID `screenshot-tool.pinned-image` 和命令 ID `screenshot-tool.pinned-image.pin`，位于 `Modules\PinnedImage`。点击“贴图”时使用最终导出渲染取得包含全部批注的选区位图，在原选区位置创建置顶无边框窗并结束截图会话。拖动内部可移动；拖动四边或四角默认等比缩放，按住 `Shift` 可自由改变宽高比。右键菜单固定为删除、复制、保存、编辑；编辑会把当前贴图像素作为新底图回到现有编辑窗口。禁用、删除或替换模块时必须关闭并释放其全部贴图窗口。
- 相关入口：`Infrastructure/Modules/ModuleHost.cs`、`Infrastructure/Modules/ModuleLoadContext.cs`、`Infrastructure/Modules/ModuleImageHostProxy.cs`、`Presentation/Pages/ModuleManagementPage.cs`、`ScreenshotTool.PinnedImage`、`ScreenshotTool.Ocr`、`ScreenshotTool.PaddleOcr*`、`ScreenshotTool.Contracts/ModuleContracts.cs`。

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
- 轻量完全版依赖系统已安装 .NET 8 Desktop Runtime，并预装全部模块；完全版提供同样的插件集合并内置 .NET 8。两者都是手动下载档位，不改变软件内更新对轻量版/重量版的自动选包规则。
- 模块页上的贴图悬浮窗、长截图、本地 OCR、PP-OCR Tiny、PP-OCR Small、二维码扫描和录屏也必须一键直接下载 ZIP。虽然独立下载使用率可能较低，但它是完整的恢复路径。
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
| 依赖系统运行库全插件轻量完全包 | `complete-lightweight-full-win-x64.zip` | 首页第三个轻量完全版按钮 |
| 内置运行库全插件完全包 | `complete-full-win-x64.zip` | 首页第四个完全版按钮 |
| 贴图悬浮窗独立模块 | `pinned-image-addon-win-x64.zip` | 模块页贴图悬浮窗按钮 |
| 长截图独立模块 | `long-capture-addon-win-x64.zip` | 模块页长截图按钮 |
| 本地 OCR 独立模块 | `ocr-addon-win-x64.zip` | 模块页本地 OCR 按钮 |
| PP-OCR Tiny 独立模块 | `paddle-ocr-tiny-addon-win-x64.zip` | 模块页 PP-OCR Tiny 按钮 |
| PP-OCR Small 独立模块 | `paddle-ocr-small-addon-win-x64.zip` | 模块页 PP-OCR Small 按钮 |
| 二维码扫描独立模块 | `qr-code-addon-win-x64.zip` | 模块页二维码扫描按钮 |
| 录屏独立模块 | `screen-recording-addon-win-x64.zip` | 模块页录屏按钮 |
| 校验和 | `SHA256SUMS.txt` | Release 完整性校验 |

表中除贴图悬浮窗之外的十一个固定资产从 `v1.11.3` 起正式提供；贴图悬浮窗从 `v1.11.5` 起成为第十二项固定资产。`v1.11.4` 发布包的历史核对结果为：

- 轻量完整包约 `1.24 MiB`。
- 重量完整包约 `59.04 MiB`。
- 轻量完全包约 `54.45 MiB`（解压目录约 `94.23 MiB`）。
- 全插件完全包约 `112.25 MiB`。
- 长截图独立模块约 `0.06 MiB`。
- 本地 OCR 独立模块约 `0.02 MiB`。
- PP-OCR Tiny 独立模块约 `16.64 MiB`。
- PP-OCR Small 独立模块约 `36.57 MiB`。
- 二维码扫描独立模块约 `0.23 MiB`。
- 录屏独立模块约 `0.45 MiB`。
- `v1.11.5` 的轻量版和重量版预装贴图悬浮窗、长截图、本地 OCR、二维码扫描与录屏；轻量完全版和完全版分别在轻量版、重量版基础上再预装 PP-OCR Tiny/Small，因此包含全部七个模块。独立模块包保持程序旁 `Modules/<模块目录>/...` 的目录结构，解压到程序目录即可安装。

发布脚本 `scripts/Publish-Release.ps1` 会生成四种完整包、七个独立模块包、免解压运行目录和 `SHA256SUMS.txt`，并检查轻量版小于 `5 MiB`、重量版小于 `90 MiB`、轻量完全版未压缩目录小于 `110 MiB` 且 ZIP 小于 `80 MiB`、完全版未压缩目录小于 `180 MiB` 且 ZIP 小于 `130 MiB`。项目级 Skill `.agents/skills/publish-cutcut-release` 使用快速直发流程：用户明确要求打包或发布后，更新版本并直接提交到 `main`，只生成一次正式资产并上传，不再单独运行格式、测试、UI、候选包、`ValidateOnly`、PR/CI 或发布后复核步骤。贴图、本地 OCR 与二维码扫描的独立恢复资产名分别固定为 `pinned-image-addon-win-x64.zip`、`ocr-addon-win-x64.zip` 和 `qr-code-addon-win-x64.zip`；PP-OCR 通过 `scripts/Get-PaddleOcrModels.ps1` 下载固定版本并核对 SHA-256，再由 `scripts/Publish-PaddleOcrModule.ps1` 分别组装 Tiny/Small。

## 本地最新测试包

- 固定测试打包根目录为 `测试打包`，其中只保留一个可直接运行的 `轻截-最新测试版` 文件夹；不在该目录积累历史版本、ZIP、日志或更新脚本。
- 在仓库根目录运行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Update-LatestTestPackage.ps1`，会从当前工作树使用 `LightweightWinX64` 配置重新构建，并在验证成功后替换上述唯一目录。构建期间使用 `artifacts` 下的独立中间目录，不要求关闭正在运行的轻截。
- 该测试包依赖系统已安装 .NET 8 Desktop Runtime，并始终携带 `PinnedImage`、`LongCapture`、`Ocr`、`PaddleOcrTiny`、`PaddleOcrSmall`、`QrCode`、`ScreenRecording` 七个模块及两套 PP-OCR 模型。脚本会检查模块集合、关键文件、禁用标记、目录唯一性和 `110 MiB` 未压缩体积上限。
- 测试包内的 `测试包信息.txt` 记录版本、生成时间、源码提交和是否包含未提交改动。它只用于本地验收，不创建正式版本、不更新 `Relase`、不上传 GitHub Release。
- 完成会影响可执行程序或模块的功能改动后，如需保持测试包与工作树同步，应再次运行该脚本；PP-OCR 模型缓存在 `artifacts\latest-test-package-model-cache`，后续更新会复用已通过 SHA-256 校验的模型。

## 后续开发时优先保持

- “轻截”的产品身份是轻巧、低干扰：主界面和发布页优先突出轻量版，重量版、轻量完全版、完全版与可选模块作为补充，但补充入口也必须完整可用。
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

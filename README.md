# 轻截

截一张图，圈出重点，加两句说明，然后发出去。

轻截是一款 Windows 截图工具，把框选、标注、贴图、长截图和录屏放在一起。平时收在系统托盘里，需要时按下 `Ctrl + Shift + X` 就能开始。

[下载轻截](https://xdioez.github.io/CutCut/#download) · [查看发布版本](https://github.com/XDIOEZ/CutCut/releases) · [挑选插件](https://xdioez.github.io/CutCut/modules.html) · [反馈问题](https://github.com/XDIOEZ/CutCut/issues)

## 用轻截做点什么

### 把重点直接画在图上

框选后就能添加箭头、方框、文字、画笔和马赛克，也可以把剪贴板里的图片或文字贴进来。

这些标注都能选中后继续调整：移动、缩放、旋转，或者多选后一起排齐。文字写错了，双击就能接着改。截图完成前，可以慢慢把意思表达清楚。

### 把参考图留在手边

点击“贴图”，截图就会变成置顶的小窗口。对照设计稿、抄一段信息、比较两处内容时，可以一边看图一边操作其他程序。贴图支持移动、缩放，右键还能保存或重新编辑。

### 一屏放不下，就继续往下截

网页、聊天记录、代码都可能超出一屏。框选滚动区域后点击“长截图”，向上或向下滚动，旁边的预览窗会显示拼接结果。完成后还能继续加箭头、文字和马赛克。

### 需要演示时，直接录下来

录屏沿用截图选区，可以录制系统声音和麦克风，也能边录边画重点。停止后保存为 MP4，适合演示操作、说明问题或记录一个复现过程。

### 用到哪些功能，就留下哪些插件

长截图、贴图、收藏夹、文字识别、二维码和录屏都以插件提供。完整包已带上常用插件，可以直接开始用；不需要的功能，可以在“插件模块”页禁用或删除。

## 下载与运行

面向 Windows 10 / 11 的 x64 电脑，下载后解压运行即可。

| 版本 | 适合什么情况 | 需要预装 .NET 8 桌面运行库 |
| --- | --- | --- |
| **轻量版** | 想让下载包小一些，使用截图和常用插件 | 需要 |
| **重量版** | 想解压后直接运行，省去单独安装运行库 | 不需要 |
| **轻量完全版** | 想使用全部插件，包括两套 PP-OCR 模型 | 需要 |
| **完全版** | 想一次下载运行库和全部插件 | 不需要 |

轻量版和重量版都包含贴图、收藏夹、长截图、本地 OCR、二维码扫描和录屏。两种完全版额外包含 PP-OCR Tiny / Small。

下载轻量版后，如果系统提示缺少运行库，请安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) 的 Windows x64 版本。

> 本文按当前 `main` 分支说明功能。刚提交的改动可能尚未打包发布，下载版的具体变化请以 [Release 说明](https://github.com/XDIOEZ/CutCut/releases) 为准。

## 先截一张试试

1. **启动轻截。** 可以先到“保存路径”选好截图目录和 PNG / JPEG 格式，其余设置按需要再调整。
2. **按 `Ctrl + Shift + X`。** 移动鼠标后单击可截取窗口，按住左键拖动则自由框选。
3. **加一点说明。** 用工具栏画箭头、加文字，或者按 `Ctrl + V` 粘贴图片和文字。
4. **复制或保存。** 按 `Ctrl + Shift + C` 复制整张截图；按 `Ctrl + S` 保存到文件夹，同时复制到剪贴板。

保存成功后，点击右下角通知就能找到文件。关掉主窗口后，轻截仍会留在托盘里；需要完全退出时，在托盘菜单中选择“退出”。

## 常用快捷键

先记住截图、复制和保存就够用了，其他操作用到时再查。

| 想做什么 | 操作 |
| --- | --- |
| 开始截图 | `Ctrl + Shift + X`，可在“截图设置”修改 |
| 复制整张截图并结束 | `Ctrl + Shift + C`，或在非文字输入状态下按 `Enter` |
| 保存截图并复制 | `Ctrl + S` |
| 复制选中的编辑元素 | `Ctrl + C` |
| 粘贴元素、图片或文字 | `Ctrl + V` |
| 撤销 / 删除选中元素 | `Ctrl + Z` / `Delete` |
| 移动编辑元素 | 默认按住 `Alt + 左键` 拖动 |
| 多选 / 取消某个元素的选中 | `Ctrl + 左键` 单击 |
| 缩放 / 旋转鼠标下的元素 | `Ctrl + 滚轮` / `Alt + 滚轮` |
| 刷新当前区域的截图底图 | 鼠标放在普通截图框内，按 `Ctrl + R` |
| 退出当前编辑状态或取消截图 | `Esc` |

**Ctrl+C 复制的是选中元素，Ctrl+Shift+C 复制的是整张截图。** 没选中元素时，Ctrl+C 不会结束截图。同一次截图里复制再粘贴，元素仍可分别编辑；粘贴到其他程序或另一次截图时，会使用图片形式。

正在输入文字时，Ctrl+C 复制所选文字，`Enter` 完成输入，`Ctrl + Enter` 换行。

<details>
<summary>更多框选与编辑操作</summary>

### 调整截图范围

- 拖动截图框的边或角，可以微调大小；在框内按住右键拖动，可以移动整个截图框。
- `Ctrl + A` 优先全选编辑元素；继续按会扩展到鼠标所在显示器，再扩展到全部显示器。没有元素时，直接从当前显示器开始。
- 普通截图框选完成后，框外仍可操作其他程序。需要重新框选时，按住 `Ctrl` 从截图框外左键拖动。
- 想更新画面而保留标注，可以在普通截图框内按 `Ctrl + R`，原有截图框和编辑元素会留在原位。

### 调整标注

- 单击工具按钮开始绘制，再点一次同一按钮即可关闭工具。
- 未启用绘图工具时，可拖出选择框批量选中元素；元素重叠时，在同一位置连续单击可以轮换选中。
- 多选后，移动、缩放和旋转可以作用于整组元素。
- 单选元素后可拖动手柄缩放。箭头调整起点和终点；图片拖动四角时保持比例，拖动边中手柄时调整对应方向。
- 元素靠近彼此时可以自动吸附对齐，快速双击 `Ctrl` 可切换吸附。拖动或缩放时按住 `Ctrl`，可以按固定像素步长调整。
- “图片修改”页可以调整移动方式、旋转步进、吸附距离、拖动步长和工具粗细范围。
- `Esc` 会逐层退出当前状态，例如完成文字输入、取消选中或关闭绘图工具；没有编辑状态时才结束截图。

</details>

## 插件：按需要多做一点

在“插件模块”页可以启用、禁用、删除插件，或点击“管理配置”调整它的参数。删除后也可以从 [模块下载页](https://xdioez.github.io/CutCut/modules.html) 装回来。

| 插件 | 可以用来做什么 | 详细说明 |
| --- | --- | --- |
| 贴图悬浮窗 | 把截图置顶，随时对照；配合支持的 OCR 插件可在图上拖选文字 | [贴图使用说明](docs/pinned-image-addon.md) |
| 截图收藏夹 | 把整理好的截图直接存入单独的收藏文件夹 | [收藏夹设置](docs/favorites-addon.md) |
| 长截图 | 向上、向下滚动采集，预览拼接后继续编辑 | [长截图说明](docs/long-capture-addon.md) |
| 本地 OCR | 使用 Windows 自带能力提取图片文字，不附带模型 | [本地 OCR 说明](docs/ocr-addon.md) |
| PP-OCR Tiny / Small | 使用本机模型识别文字，按需要选择模型规模 | [PP-OCR 说明](docs/paddle-ocr-addon.md) |
| 二维码扫描 | 读取二维码内容，再由你决定复制或如何使用 | [二维码说明](docs/qr-code-addon.md) |
| 录屏 | 录制选区、系统声音、麦克风和实时批注 | [录屏设置](docs/screen-recording-addon.md) |

文字识别在本机完成，不上传图片。识别结果可以编辑、复制；主动点击翻译时，才会使用联网翻译。

插件放在程序旁的 `Modules` 文件夹中，支持运行时添加、替换和删除。正在进行的截图会继续使用原来的插件实例，新会话使用更新后的插件。

## 几个你可能会关心的问题

**能改截图快捷键吗？**

可以。“截图设置”支持最多三组全局快捷键，也可以全部清空，改用托盘菜单或“立即截图”按钮。

**截图保存在哪里？**

默认在“图片”目录下的“轻截”文件夹，可以在“保存路径”修改。支持 PNG / JPEG、按日期分类，以及日期时间、递增序号或图片内文字命名。这里的“图片内文字”指你添加的文字编辑元素。

**怎么找回之前的截图？**

“查看截图”页可以按文件名搜索、按时间或名称排序。图片可以重新编辑，保存时会生成新文件。

**能开机启动吗？**

在“通用设置”开启即可，不需要管理员权限。也可以设置手动启动后最小化。

**怎么更新？**

进入“软件更新”，检查新版本后选择下载并安装。更新完成后会自动重启，已经永久删除的插件不会被重新装回来。

## 从源码运行

轻截使用 C#、.NET 8 和 WinForms 开发。在 Windows 上安装 .NET 8 SDK 后，可在仓库根目录运行：

```powershell
dotnet build .\ScreenshotTool.sln -c Release
dotnet run --project .\src\ScreenshotTool\ScreenshotTool.csproj
```

<details>
<summary>构建发布包</summary>

仅构建轻量主程序，目标电脑需安装 .NET 8 Desktop Runtime：

```powershell
dotnet publish .\src\ScreenshotTool\ScreenshotTool.csproj -p:PublishProfile=LightweightWinX64 -o .\artifacts\lightweight-win-x64
```

构建自带运行库的主程序：

```powershell
dotnet publish .\src\ScreenshotTool\ScreenshotTool.csproj -p:PublishProfile=PortableCompressedWinX64 -o .\artifacts\portable-compressed-win-x64
```

生成四种完整包、独立插件包和免解压运行目录，使用 [发布脚本](scripts/Publish-Release.ps1)：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1
```

两套发布配置使用相同的业务代码，主要区别是是否携带运行库。项目不启用 WinForms 程序集裁剪。正式包的固定命名、体积限制和软件内更新所需资产，见 [发布资产约定](docs/project-memory.md#发布资产契约)。

</details>

<details>
<summary>工程结构与扩展开发</summary>

主程序位于 [src/ScreenshotTool](src/ScreenshotTool)，主要分为以下几部分：

| 目录 | 职责 |
| --- | --- |
| `Abstractions` | 服务接口，隔离界面与系统实现 |
| `Application` | 应用服务与对象组装 |
| `Core` | 设置、快捷键和几何等基础逻辑 |
| `Editing` | 标注文档、选择、变换和元素复制 |
| `Infrastructure` | 屏幕采集、剪贴板、文件保存和系统能力 |
| `Presentation` | 工作台、设置页、托盘和截图编辑界面 |

外部插件通过 [ScreenshotTool.Contracts](src/ScreenshotTool.Contracts) 接入，插件实现放在各自的独立项目中。主窗体负责呈现与交互，编辑规则和系统服务保持独立。

配置保存在 `%LocalAppData%\LightShotCN\Profiles\local.json`，旧配置会自动迁移并保留原文件。

继续阅读：[模块化架构](docs/modular-architecture.md) · [用户偏好配置](docs/user-preferences.md) · [工程维护规则](AGENTS.md)

</details>

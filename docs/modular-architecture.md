# 轻截模块化架构

## 目标

主程序只负责稳定的截图生命周期、选区与编辑核心、系统服务和模块编排。后续相对独立的功能以模块组合进每次截图会话，避免模块直接依赖窗体或彼此调用。

```text
ScreenshotTool.Contracts       稳定公共契约
          ↑
外部模块 DLL                   只引用 Contracts
          ↑ 运行时发现/组合
ModuleHost                     加载、版本刷新、租约、卸载
          ↓                    可选模块设置页
CaptureFeatureSession          隔离异常并分发输入与渲染
          ↓
CaptureOverlayForm             提供受控宿主能力
```

## 目录与职责

- `src/ScreenshotTool.Contracts`：公共模块契约，不引用主程序。
- `src/ScreenshotTool/Application`：组合根，只创建和连接对象。
- `src/ScreenshotTool/Abstractions`：主程序内部服务边界。
- `src/ScreenshotTool/Infrastructure/Modules`：DLL 发现、可卸载加载上下文、热更新和功能租约。
- `src/ScreenshotTool/Presentation/CaptureFeatureSession.cs`：为一次截图创建模块功能快照，隔离单个模块异常。
- `src/ScreenshotTool.LongCapture`：随程序发布的第一方长截图模块，仅引用 `ScreenshotTool.Contracts`。
- `src/ScreenshotTool.Ocr`：可独立装卸的本地 OCR 模块，仅引用 `ScreenshotTool.Contracts`，对多种预处理候选使用 Windows 系统 OCR。
- `src/ScreenshotTool.PaddleOcr`：PP-OCRv6 Tiny/Small 入口共用的 ONNX 识别引擎，只依赖公共契约和模块私有运行库。
- `src/ScreenshotTool.PaddleOcr.Tiny` 与 `src/ScreenshotTool.PaddleOcr.Small`：两个稳定 ID、独立目录、独立安装包的 PP-OCR 入口模块。
- `src/ScreenshotTool.PinnedImage`：可独立装卸的贴图悬浮窗模块，拥有置顶窗口、移动缩放和右键菜单。
- `src/ScreenshotTool.ScreenRecording`：可选录屏模块，仅引用 `ScreenshotTool.Contracts`，提供选区入口、录制控制和编码器编排；批注由宿主核心会话提供。
- `src/ScreenshotTool.ScreenRecording.Recorder`：录屏模块私有的 x64 编码器进程，承载 Media Foundation 视频与音频依赖。
- `Modules`：运行时扩展目录；每个一级子文件夹是一个独立模块包，复制、替换或删除整个文件夹即可触发刷新。

## 第一方模块的构建与发布

宿主项目对随完整包预装的第一方模块使用 `ReferenceOutputAssembly="false"` 的项目引用。该引用只保证构建顺序并取得模块输出，不会把模块加入宿主的编译引用或 `.deps.json` 依赖。普通构建会将贴图悬浮窗、长截图和本地 OCR DLL 复制到宿主输出目录中对应的 `Modules/<模块名>` 文件夹；单文件发布会将其标记为 `ExcludeFromSingleFile`，保留为可替换、可删除的独立模块包。PP-OCR Tiny/Small 不进入宿主项目依赖树，也不进入轻量版和重量版；它们先由各自的独立发布脚本组装，再仅在发布阶段复制进全插件完全版。

`scripts/Publish-Release.ps1` 会同时验证轻量版和便携压缩版都包含贴图悬浮窗、长截图与本地 OCR 模块，并按整个发布目录的文件总和执行 5 MiB / 90 MiB 体积门槛，不只统计主 EXE。标准交付物是 `complete-lightweight-win-x64.zip`、`complete-portable-win-x64.zip`、`complete-lightweight-full-win-x64.zip` 和 `complete-full-win-x64.zip`：前两者包含主程序、贴图悬浮窗、长截图、本地 OCR、二维码扫描、录屏模块和编码器；后两者再装入 PP-OCR Tiny/Small 的入口、私有依赖和四个模型文件，分别以轻量版和便携压缩版为底包，并对最终 ZIP 执行 80 MiB / 130 MiB 门槛。脚本还会生成 `pinned-image-addon-win-x64.zip`、`long-capture-addon-win-x64.zip`、`ocr-addon-win-x64.zip`、`paddle-ocr-tiny-addon-win-x64.zip` 与 `paddle-ocr-small-addon-win-x64.zip` 等独立包，供发布页按需下载。PP-OCR 模型下载脚本固定来源、版本与 SHA-256，校验不一致时停止组包。

录屏仍不进入宿主的编译依赖树。标准发布脚本会额外调用 `scripts/Publish-ScreenRecordingModule.ps1`，保留可单独下载的 `screen-recording-addon-win-x64.zip`，然后将其 `Modules` 内容复制到四种完整包中。这只是发布阶段的预安装；运行时仍通过稳定契约加载可替换、可删除的录屏模块。

## 模块生命周期

1. 程序启动后创建 `Modules` 目录，并每秒比较各一级模块文件夹内全部文件的相对路径、长度和修改时间。
2. 每个模块文件夹只允许一个 `IScreenshotToolModule` 入口；入口和托管依赖 DLL 通过流加载到独立、可回收的 `AssemblyLoadContext`。ONNX Runtime、SkiaSharp 等原生 DLL 先复制到当前进程专属临时影子目录再加载，避免锁住模块目录，并在上下文卸载或下次进程启动时尽力清理影子文件。
3. 每次开始截图时，已加载模块分别创建新的 `ICaptureFeature`，按 `Order` 和 `Id` 排序后组合。
4. 模块可以处理键盘、鼠标，并分别参与预览和最终导出渲染。
5. 实现 `IModuleSettingsPageProvider` 的模块可创建自己的设置页；宿主只按通用元数据把页面加入导航，不引用具体页面类型。
6. 设置工作台通过模块文件夹内的 `.lightshot-module-disabled.json` 保存禁用状态；禁用会退役当前程序集但保留全部文件，重新启用时删除标记并重新加载。标记同时保留模块 ID、名称和版本，因此重启后无需加载 DLL 也能展示禁用项。
7. 模块文件夹或其中任一文件更新、禁用、删除后，宿主立即移除并释放对应设置页，也不再给新截图创建旧功能；已经打开的截图继续使用原实例。永久删除会先写入禁用标记并退役程序集，再递归删除该模块自己的一级文件夹。
8. 最后一个活动功能或设置页租约释放后，宿主释放模块对象并调用 `AssemblyLoadContext.Unload()`。

## 模块自带设置页

设置 UI 与功能 DLL 使用同一生命周期。模块按需实现 `IModuleSettingsPageProvider`，通过 `IModuleSettingsHost` 读取和写入稳定的布尔、整数、字符串键值；页面布局、控件事件、默认值和参数归一化全部留在模块程序集。宿主只负责：

- 从当前已加载模块创建 `IModuleSettingsPage` 租约；
- 按页面 `Order`、`Id`、标题和说明组合左侧导航；
- 在模块更新或卸载时移除控件并释放租约；
- 将通用模块偏好保存进用户配置。

因此未安装 `LongCapture` 时没有“长截图”设置，未安装 `ScreenRecording` 时没有“录屏设置”；宿主源码和 `.deps.json` 都不依赖这两个具体设置页类型。模块页面不得引用 `MainForm`、`AppTheme`、`JsonSettingsStore` 或宿主内部设置模型。

## 可组合工具栏命令与实时截图能力

模块需要在截图工具栏增加入口时，可让会话级 `ICaptureFeature` 同时实现
`ICaptureToolbarCommandProvider`。命令只声明稳定 ID、文字、提示和宽度；宿主负责创建按钮、
隔离异常并传入随截图会话结束而取消的 `CancellationToken`。`ModuleHost` 的功能租约会转发该可选
接口，因此 DLL 已从模块目录删除后，已经打开的截图会话仍可安全结束当前异步命令。

需要重新读取真实屏幕内容的模块可检测宿主是否实现 `ILiveCaptureFeatureHost`：

- `SelectionScreenBounds` 是物理屏幕坐标，可包含多显示器的负坐标。
- `SetOverlayVisible(false)` 用于在实时采集前移除遮罩和工具栏；宿主保持模态窗体可见状态，
  仅把它停放到虚拟桌面之外，避免 `Hide()` 提前结束 `ShowDialog()`；模块必须在 `finally` 中恢复。
- `CaptureLiveSelection()` 每次返回一张由模块负责释放的新位图。
- `ReplaceCaptureResult()` 成功后接管传入位图的所有权；调用失败时仍由模块释放。
- `HasEdits` 用于阻止会破坏既有编辑坐标的采集流程。

这些能力保持通用，不向模块暴露 `CaptureOverlayForm`、标注文档或保存服务实现。

## 核心批注会话

批注模型、编辑规则和菜单栏只有一套，保留在主程序的 `CaptureAnnotationEditor`、`AnnotationDocument`、
`CaptureEditorToolbar` 和各类 `Annotation` 实现中。截图框直接组合该核心；需要在实时画面上批注的扩展模块则检测宿主是否实现
`ICaptureAnnotationHost`，通过 `CreateAnnotationSession()` 创建 `ICaptureAnnotationSession`。需要暂停、停止等模块命令时，模块可检测
`ICaptureAnnotationToolbarSession`，仅提交通用命令定义和状态，具体按钮仍由宿主的同一菜单栏组件创建。模块只能：

- 读取宿主提供的工具目录与调色板；
- 切换操作、选择、矩形、椭圆、箭头、画笔、文字和马赛克工具；
- 使用同一个粗细控制器调整粗细，并调用撤销、清空和窗口生命周期操作。

模块不会取得或复制可变批注文档、具体标注类型、命中测试、渲染代码或菜单栏控件。核心会话统一提供文字输入与
双击重编辑、元素选择和框选、`Ctrl` 多选、`Alt + 左键` 移动、普通元素八手柄缩放、箭头首尾端点缩放、`Ctrl` 固定步长拖动、
元素边缘/中心吸附、`Alt + 滚轮` 旋转、`Ctrl + 滚轮` 缩放、剪贴板贴纸、删除和核心马赛克渲染。以后需要实时批注的模块复用同一契约即可，
不再各自实现一套形状模型。

实时批注会话使用宿主内容层、交互层和共享工具栏：内容层只渲染最终批注并参与录制；交互层渲染文字编辑、
选择框、控制柄和笔刷光标，工具栏与截图模式复用 `CaptureEditorToolbar`。交互层和工具栏都通过
`SetWindowDisplayAffinity` 排除录制。由于 Windows 会把透明色像素上的点击直接送给下层程序，选中绘图工具后，宿主会话使用
`LiveAnnotationPointerHook` 在编辑或绘图工具启用时捕获并消费选区内的鼠标输入，再转交同一个 `CaptureAnnotationEditor`；鼠标穿透状态若启用左键提示，则观察左键按下、移动与松开而不消费事件，使现场圆圈在整个长按期间持续跟随指针。录屏模块通过通用 `SetToolVisible` 契约启用宿主内置且默认隐藏的“选择”工具，退出当前工具时立即释放编辑捕获并恢复鼠标穿透。透明内容层和输入层分别保留持久离屏画布，把元素变化前后的脏矩形合成完成后一次性提交到窗口；画笔与马赛克只刷新新增轨迹附近，拖动期间复用缓存的吸附参考，既避免全屏录制时反复刷新整个窗口，也避免清空旧位置与绘制新位置之间的可见闪烁。框选填充使用独立、鼠标穿透且捕获排除的低透明度窗口，解决透明色键输入层无法呈现逐像素半透明的问题，并保持与截图模式相同的蓝色透明填充。
这样视频能记录批注本身，但不会把编辑辅助线和控制柄写入成品。

## 第一方长截图模块

`ScreenshotTool.LongCapture` 以独立 DLL 组合进截图会话。用户确认选区后点击“长截图”，模块隐藏
宿主遮罩，以不抢焦点、可穿透鼠标的边框窗保留原选区，并在选区旁边打开独立的实时长图预览窗。
用户在选区内使用滚轮向上或向下滚动实际内容；模块只观察滚轮，不吞掉或代替用户输入。`Enter`
完成并采用当前结果，`Esc` 取消并丢弃本次结果，预览窗也提供等价的完成和取消操作。

滚动期间会周期采集中间帧，滚轮暂歇后再采集稳定帧，避免一次快速滚动跨过可验证的重叠区域。
双向匹配器通过多横向分块、行描述符和原像素采样验证垂直重叠，并以页面坐标记录当前视口；向上
滚动只补充上方尚未覆盖的内容，向下滚动只补充下方尚未覆盖的内容，回访已经采集的区间只更新
当前位置，不会重复添加像素。耗时的匹配和拼接不在全局输入钩子的回调线程中执行，保证滚轮仍
由目标程序顺畅处理，预览窗则随着每次可信拼接实时更新。

长截图仍采用保守策略：固定页头和页脚只保留一次；正文内部固定悬浮层、周期性重复内容、无足够
纹理、持续动画或多个近似候选都不会被猜测为接缝。匹配被拒绝、帧数或尺寸达到限制时会安全停止，
只允许用户保留此前已经验证的可信部分。结果限制为 30,000 像素高和 1.2 亿像素，并在完成后继续
复用宿主的 `Ctrl+C`、`Ctrl+S`、剪贴板和保存通知流程。

选区边框、实时预览窗、输入监听、稳定帧采样和双向拼接会话全部封装在长截图模块内部。模块只通过
`ILiveCaptureFeatureHost` 使用宿主提供的通用选区、显隐、实时采集和结果替换能力，不引用宿主窗体、
标注文档或基础设施实现，也不为该交互向 `ScreenshotTool.Contracts` 泄漏具体 UI 或可变状态。

## 三种可选 OCR 模块

`ScreenshotTool.Ocr` 1.2.0 要求轻截 1.11.6 或更高版本，并作为截图会话功能提供“OCR 本地”工具栏命令。用户框选区域后点击命令，模块通过
`ICaptureFeatureHost.CopyDesktopSelection()` 取得由自己负责释放的选区位图，并调用 Windows 自带的
离线 OCR。识别前会构造原图、高清放大、自动对比度灰度增强和 Otsu 二值化四种候选图，在同一个
PowerShell/WinRT 工作进程内依次识别，并按有效文字、词数、行数和异常字符情况选择最完整的结果。
模块不上传图片、不保存识别历史，也不携带大型模型；可识别语言由系统已安装的语言组件决定。

识别成功后，模块通过通用 `ICaptureTextResultHost` 把标题和纯文本交给宿主，并通过
`ICaptureArtifactHost.CompleteCaptureSession()` 结束冻结的截图界面。宿主在原选区右侧优先放置独立的
可编辑结果小窗；右侧空间不足时改放左侧，两侧都不足时限制在当前显示器工作区内。结果窗由宿主拥有，
因此截图功能租约可以立即释放，OCR 模块随后仍可安全禁用、替换或删除；结果窗支持继续编辑和一键复制。

Windows Runtime 的完整 .NET 投影会显著增加轻量包体，因此 OCR 模块通过隐藏的系统 Windows PowerShell
进程调用同一套系统 OCR 接口。辅助脚本以内嵌资源随模块 DLL 加载，输入只使用会话级临时 PNG；会话取消
时模块终止辅助进程，完成或失败后删除全部候选临时文件。模块包仍只有 `ScreenshotTool.Ocr.dll` 一个入口文件。

`ScreenshotTool.PaddleOcr.Tiny` 与 `ScreenshotTool.PaddleOcr.Small` 1.1.0 同样要求轻截 1.11.6
或更高版本，分别提供 `OCR Tiny` 与 `OCR Small` 命令。两个入口模块只保存稳定元数据，每次截图会话
各自创建识别器；共用代码位于 `ScreenshotTool.PaddleOcr.dll`，通过 RapidOcrNet 调用 PP-OCRv6 的
检测、方向分类和识别 ONNX 模型。Tiny 使用较小的检测/识别模型以平衡体积和速度，Small 使用较大的
模型优先处理复杂背景、密集小字和中英混排。模型及 ONNX Runtime、SkiaSharp、RapidOcrNet 等依赖都
是对应模块目录的私有文件，不进入宿主或另一个 OCR 模块。

三个 OCR 模块分别使用稳定且互不冲突的模块、功能和命令 ID，因而可以同时安装；产品默认建议用户
按需求只启用其中一个。Tiny 和 Small 分别位于 `Modules\PaddleOcrTiny` 与
`Modules\PaddleOcrSmall`，删除或替换任一目录不会影响本地 OCR 或另一个 PP-OCR 模块。正在识别的
会话由租约延迟释放；每个已加载模块在首次创建截图功能时建立一份进程临时模型快照，原生和托管
依赖也在模块激活时进入进程专属影子目录。因此截图窗口已经打开后，即使源模块目录先被删除，用户
仍可启动并完成识别。最后一个功能释放后关闭 ONNX 会话、释放模型快照并卸载模块上下文；被系统
占用的临时原生文件最迟在下次进程启动时清理。

## 可选二维码扫描模块

`ScreenshotTool.QrCode` 1.0.0 要求轻截 1.11.0 或更高版本，并提供稳定命令
`screenshot-tool.qr-code.scan`。用户完整框选二维码后点击“二维码”，模块通过
`ICaptureFeatureHost.CopyDesktopSelection()` 取得选区位图，在后台使用 ZXing.Net 只尝试
`QR_CODE` 格式的本机离线解码。图片不会上传，模块也不会自动打开网址或执行二维码内容。

扫描成功后，模块通过现有 `ICaptureTextResultHost` 把原始内容交给宿主，并通过
`ICaptureArtifactHost.CompleteCaptureSession()` 结束冻结的截图界面。宿主复用 OCR 的通用侧边结果窗，
但标题、复制按钮和状态文案保持结果类型无关；同一选区识别到多个二维码时，各项内容以空行分隔。
模块释放时会取消活动扫描，新扫描不可再进入；从 `Modules\QrCode` 删除入口 DLL、`zxing.dll` 和许可
文本后，新截图会话立即失去入口，已经打开的会话则由模块租约安全延迟释放。

二维码模块只引用稳定的 `ScreenshotTool.Contracts` 边界，ZXing.Net 是模块目录内的私有依赖，
不进入宿主程序集或其他模块。独立安装包固定为 `qr-code-addon-win-x64.zip`。

## 贴图悬浮窗模块

`ScreenshotTool.PinnedImage` 1.0.0 使用稳定模块 ID `screenshot-tool.pinned-image` 和命令 ID
`screenshot-tool.pinned-image.pin`。用户完成选区与批注后点击“贴图”，模块通过
`ICaptureArtifactHost.RenderSelection()` 取得与复制、保存一致的最终选区位图，并使用
`SelectionScreenBounds` 在原选区位置创建置顶无边框窗口；创建成功后请求宿主结束原截图会话。

贴图窗口、位图和右键菜单都由模块拥有。左键拖动内部移动窗口；四边和四角使用对应双向箭头并默认
保持原比例缩放，按住 `Shift` 时切换为独立宽高缩放。右键菜单固定提供删除、复制、保存和编辑：
复制与保存通过 `IModuleImageHost` 复用宿主剪贴板、命名、输出目录和保存通知；编辑会先隐藏贴图，
再把当前像素交给宿主现有图片编辑入口，避免桌面重新采集时把贴图自身截入底图。

模块维护自己创建的全部活动窗口。用户关闭窗口时立即移除记录；模块禁用、删除、替换或宿主退出时，
`Dispose` 会关闭窗口并释放位图、菜单和句柄。模块入口及独立恢复包分别位于
`Modules\PinnedImage\ScreenshotTool.PinnedImage.dll` 和 `pinned-image-addon-win-x64.zip`。

## 可选录屏模块

`ScreenshotTool.ScreenRecording` 作为截图会话功能提供“录屏”工具栏命令，并在同一个模块程序集内提供独立“录屏设置”页。该页面通过通用模块设置宿主保存系统声音、麦克风、左键黄色圆圈、30/60 FPS、2/4/8/12/20 Mbps 视频码率和范围提示偏好，并按照目标视频码率和启用时的一路 128 kbps 混合音频实时估算 1 分钟、10 分钟和 1 小时的储存占用。模块通过通用布尔与整数偏好契约读取已保存参数，点击“录屏”后直接开始，不创建额外设置窗口。模块随后停放冻结的截图遮罩，通过 `IConfigurableCaptureAnnotationHost` 请求宿主核心批注会话并传入范围提示和左键圆圈选项，再通过 `ICaptureAnnotationToolbarSession` 启用宿主默认隐藏的橙色“选择”工具并注入暂停、停止保存和取消命令。宿主在捕获排除的输入层按用户偏好绘制实线、虚线或不显示的录屏范围提示，因此它不会混入 MP4 内容层；启用左键圆圈时，宿主显示一个捕获排除的半透明现场提示，编码器独立把同色圆圈写入视频，避免现场预览与成片效果重叠。宿主使用与截图模式相同的 `CaptureEditorToolbar` 渲染唯一单行菜单，录屏模块内不存在编辑按钮或独立控制窗。未选择工具时输入层鼠标穿透；点击“选择”后左键进入截图核心的元素编辑，点击绘图工具则添加实时批注，再次点击当前工具返回穿透状态。

录屏编码采用独立的 `ScreenshotTool.ScreenRecording.Recorder` 辅助进程。模块通过每次会话唯一的命名管道发送暂停、继续、停止和取消命令；辅助进程使用 ScreenRecorderLib / Media Foundation 写入 H.264 MP4，并可混合系统声音与默认麦克风。原生 C++/CLI 编码器不会进入可回收 `AssemblyLoadContext`，模块被删除或会话取消时会先通知辅助进程停止，超时后终止其进程树，避免后台录制阻止模块卸载。

录制输出通过通用 `ICaptureArtifactHost` 获取宿主输出目录、报告已保存文件，并在产物确认保存后请求宿主完成当前捕获会话。该契约不暴露具体窗体、JSON 设置或保存服务；宿主继续统一显示保存通知、文件定位入口并决定如何关闭当前截图界面。取消或失败时模块不会请求完成会话，宿主会恢复截图框。

## 最小模块示例

```csharp
using ScreenshotTool.Contracts;

public sealed class WatermarkModule : ScreenshotToolModuleBase
{
    public override string Id => "example.watermark";
    public override string DisplayName => "水印";

    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures() =>
        [new WatermarkFeature()];
}

public sealed class WatermarkFeature : CaptureFeatureBase
{
    public override string Id => "example.watermark.feature";

    public override void Render(Graphics graphics, CaptureRenderTarget target)
    {
        if (!Host.HasSelection) return;
        using var brush = new SolidBrush(Color.White);
        graphics.DrawString("Watermark", SystemFonts.DefaultFont, brush, Host.Selection.Location);
    }
}
```

模块项目目标框架应为 `net8.0-windows`，引用 `ScreenshotTool.Contracts.csproj` 或与宿主兼容的 `ScreenshotTool.Contracts.dll`。生成的模块入口 DLL、私有依赖和资源放入程序旁 `Modules` 下的同一个独立一级子文件夹。

## 坐标、线程与资源

- 模块输入和渲染回调运行在 WinForms UI STA 线程；耗时任务必须异步执行，并在回到 UI 线程后更新状态。
- `Host.Selection` 和绘制坐标均使用截图覆盖层的虚拟桌面客户区坐标。导出阶段宿主已设置平移与裁剪，模块无需切换坐标系。
- 模块通过 `Host.GetBooleanPreference(id, defaultValue)` 读取宿主提供的布尔功能偏好；偏好键必须稳定，模块不得直接引用主程序设置类型或自行读取宿主 JSON。
- 模块设置页通过 `IModuleSettingsHost` 读写布尔、整数和字符串偏好并请求保存；设置 UI、默认值与校验逻辑必须留在模块内。
- `CopyDesktopSelection()` 返回由调用者负责释放的新位图。
- `ICaptureArtifactHost.RenderSelection()` 返回由调用者负责释放、包含宿主核心批注的最终选区位图；`SelectionScreenBounds` 使用物理屏幕坐标。
- 模块通过 `IModuleContext.ImageHost` 取得通用复制、保存和再次编辑能力，不得引用宿主窗体或保存服务。
- 模块具有与主程序相同的本机权限，只能加载可信 DLL。热加载不是安全沙箱。
- 模块必须在 `Dispose` 中解除静态事件、停止线程/计时器并释放 GDI 对象，否则 CLR 无法真正回收加载上下文。

## 兼容性原则

- 公共契约只做向后兼容的增量修改；需要破坏性变化时发布新的契约主版本。
- 不在契约中暴露 `MainForm`、`CaptureOverlayForm`、内部标注文档或具体基础设施类。
- 通用宿主能力进入 `ICaptureFeatureHost`；只服务单一模块的细节留在模块内部。
- 批注实现和编辑菜单栏不进入外部模块；扩展只通过 `ICaptureAnnotationHost`、`ICaptureAnnotationSession` 和可选的 `ICaptureAnnotationToolbarSession` 组合宿主核心。

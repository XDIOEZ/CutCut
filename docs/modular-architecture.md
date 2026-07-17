# 轻截模块化架构

## 目标

主程序只负责稳定的截图生命周期、选区与编辑核心、系统服务和模块编排。后续相对独立的功能以模块组合进每次截图会话，避免模块直接依赖窗体或彼此调用。

```text
ScreenshotTool.Contracts       稳定公共契约
          ↑
外部模块 DLL                   只引用 Contracts
          ↑ 运行时发现/组合
ModuleHost                     加载、版本刷新、租约、卸载
          ↓
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
- `Modules`：运行时扩展目录；复制、替换或删除 DLL 即可触发刷新。

## 第一方模块的构建与发布

宿主项目对第一方模块使用 `ReferenceOutputAssembly="false"` 的项目引用。该引用只保证构建顺序并取得模块输出，不会把模块加入宿主的编译引用或 `.deps.json` 依赖。普通构建会将 DLL 复制到宿主输出目录的 `Modules`；单文件发布会将其标记为 `ExcludeFromSingleFile`，保留为可替换、可删除的独立 DLL。

`scripts/Publish-Release.ps1` 会同时验证轻量版和便携压缩版都包含 `Modules/ScreenshotTool.LongCapture.dll`，并按整个发布目录的文件总和执行 5 MiB / 90 MiB 体积门槛，不只统计主 EXE。

## 模块生命周期

1. 程序启动后创建 `Modules` 目录，并每秒比较 DLL 的长度和修改时间。
2. 新 DLL 通过流加载到独立、可回收的 `AssemblyLoadContext`，因此磁盘文件不会被锁住。
3. 每次开始截图时，已加载模块分别创建新的 `ICaptureFeature`，按 `Order` 和 `Id` 排序后组合。
4. 模块可以处理键盘、鼠标，并分别参与预览和最终导出渲染。
5. DLL 更新或删除后，不再给新截图创建功能；已经打开的截图继续使用原实例。
6. 最后一个活动功能实例释放后，宿主释放模块对象并调用 `AssemblyLoadContext.Unload()`。

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

模块项目目标框架应为 `net8.0-windows`，引用 `ScreenshotTool.Contracts.csproj` 或与宿主兼容的 `ScreenshotTool.Contracts.dll`。生成的模块 DLL 与私有依赖放入程序旁的 `Modules` 目录。

## 坐标、线程与资源

- 模块输入和渲染回调运行在 WinForms UI STA 线程；耗时任务必须异步执行，并在回到 UI 线程后更新状态。
- `Host.Selection` 和绘制坐标均使用截图覆盖层的虚拟桌面客户区坐标。导出阶段宿主已设置平移与裁剪，模块无需切换坐标系。
- 模块通过 `Host.GetBooleanPreference(id, defaultValue)` 读取宿主提供的布尔功能偏好；偏好键必须稳定，模块不得直接引用主程序设置类型或自行读取宿主 JSON。
- `CopyDesktopSelection()` 返回由调用者负责释放的新位图。
- 模块具有与主程序相同的本机权限，只能加载可信 DLL。热加载不是安全沙箱。
- 模块必须在 `Dispose` 中解除静态事件、停止线程/计时器并释放 GDI 对象，否则 CLR 无法真正回收加载上下文。

## 兼容性原则

- 公共契约只做向后兼容的增量修改；需要破坏性变化时发布新的契约主版本。
- 不在契约中暴露 `MainForm`、`CaptureOverlayForm`、内部标注文档或具体基础设施类。
- 通用宿主能力进入 `ICaptureFeatureHost`；只服务单一模块的细节留在模块内部。

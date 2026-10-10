# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-05 | 核对插件启停状态的用户偏好落盘 | `UserPreferences.cs`、`JsonSettingsStore.cs`、`UserPreferenceModuleActivationStore.cs` | 设置模型与持久化入口准确，无导航缺口
- 2026-08-11 | 保存页新增按日期分类开关并持久化 | `UserPreferences.cs`、`SavePathSettingsPage.cs`、`MainForm.cs`、设置兼容测试 | 现有设置入口准确，无导航缺口
- 2026-08-11 | 持久化用户独立绑定的日期分类父目录 | `UserPreferences.cs`、`JsonSettingsStore.cs`、保存页 UI | 现有设置入口准确，无导航缺口
- 2026-08-31 | 保存页明确截图与录屏共用目录 | `SavePathSettingsPage.cs`、`MainForm.cs`、`UserPreferences.cs` | 保留旧 JSON 字段并扩展其产品语义
- 2026-09-01 | 保存页新增图片格式选择并持久化 | `SavePathSettingsPage.cs`、`UserPreferences.cs`、`JsonSettingsStore.cs` | 旧配置默认 PNG，异常格式回退 PNG
- 2026-10-10 | 截图快捷键创建多个独立框与可配置上限 | CaptureSessionLimitPolicy、UserPreferences、JsonSettingsStore、ScreenshotSettingsPage、MainForm、CaptureSessionCoordinator | 补录默认3、范围1至10与旧配置兼容入口；按用户要求未运行验证
- 2026-10-10 | 工作台底部添加打开保存目录快捷按钮 | AppShellControl、MainForm | 补录侧栏底部布局与事件连接；按用户要求未运行验证

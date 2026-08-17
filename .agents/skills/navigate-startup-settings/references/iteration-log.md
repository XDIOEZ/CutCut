# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-05 | 核对插件启停状态的用户偏好落盘 | `UserPreferences.cs`、`JsonSettingsStore.cs`、`UserPreferenceModuleActivationStore.cs` | 设置模型与持久化入口准确，无导航缺口
- 2026-08-11 | 保存页新增按日期分类开关并持久化 | `UserPreferences.cs`、`SavePathSettingsPage.cs`、`MainForm.cs`、设置兼容测试 | 现有设置入口准确，无导航缺口
- 2026-08-11 | 持久化用户独立绑定的日期分类父目录 | `UserPreferences.cs`、`JsonSettingsStore.cs`、保存页 UI | 现有设置入口准确，无导航缺口

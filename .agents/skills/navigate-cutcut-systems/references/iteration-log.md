# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-11 | 路由截图按日期子目录保存与设置持久化 | `navigate-saved-artifacts`、`navigate-startup-settings` | 跨系统入口完整，补录日期目录策略
- 2026-08-11 | 路由日期分类独立父目录绑定 | `navigate-saved-artifacts`、`navigate-startup-settings` | 跨系统入口完整，无新增边界
- 2026-08-17 | 路由并发布当前测试版 v1.11.7 | `publish-cutcut-release` | 现有发布入口完整，无新增边界
- 2026-08-31 | 路由截图与录屏共用日期保存目录 | `navigate-saved-artifacts`、`navigate-screen-recording`、`navigate-startup-settings` | 跨系统入口完整，修正录屏存储导航
- 2026-08-31 | 路由截图保存卡顿与图库刷新性能修复 | `navigate-capture-editor`、`navigate-saved-artifacts` | 补录后台保存、剪贴板重试和可取消图库快照边界
- 2026-09-01 | 路由收藏夹 Mod 与按模块配置窗口 | `navigate-module-runtime`、`navigate-capture-editor`、`navigate-saved-artifacts` | 跨系统入口完整，补录配置窗口租约和显式目录保存边界
- 2026-09-01 | 路由拖动时透明裁剪黑屏修复 | `navigate-capture-editor` | 单系统覆盖完整，无新增边界
- 2026-09-01 | 路由图片保存格式选择 | `navigate-saved-artifacts`、`navigate-startup-settings`、`navigate-capture-editor` | 格式策略覆盖普通截图、贴图与收藏夹，无新增边界
- 2026-09-14 | 路由贴图内文字选择 | navigate-capture-editor、navigate-module-runtime、navigate-ocr-results、navigate-saved-artifacts | 补录位置识别能力、请求租约与文字选择入口
- 2026-09-15 | Ctrl+C 复制编辑元素，Ctrl+Shift+C 复制整图 | navigate-capture-editor、navigate-saved-artifacts；AnnotationClipboard、CaptureOverlayForm、IClipboardService | 补录同会话对象快照与通用剪贴板数据入口；未运行测试

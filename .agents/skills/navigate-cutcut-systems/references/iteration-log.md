# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-10-05 | 路由自定义录屏分辨率与超级压缩 | navigate-screen-recording | 编码策略留在模块与帮助进程，复用通用偏好；补录成片尺寸与实际媒体验证入口
- 2026-10-05 | 路由本地最新测试包 | Update-LatestTestPackage.ps1 | 补录测试打包与正式发布的独立入口，保持固定测试目录、版本与全部八个模块
- 2026-10-05 | 路由图库与贴图双向 Windows 拖放 | navigate-saved-artifacts、navigate-capture-editor、navigate-module-runtime | 补录源码链接共用拖放、无修饰键贴图把手、拖入新建与原生 OLE 验证入口
- 2026-10-05 | 生成当前拖拽功能本地测试包 | Update-LatestTestPackage.ps1、Directory.Build.props | 统一排除旧生成树，修复独立输出路径的重复程序集属性；仍只更新固定测试目录
- 2026-10-06 | 移除悬浮贴图拖出按钮 | navigate-capture-editor、navigate-saved-artifacts、navigate-module-runtime | 贴图仅接收外部图片，不叠加交互控件；文件拖出留在图库
- 2026-10-10 | 录屏元素菜单、临时工具与多个独立截图框 | navigate-screen-recording、navigate-capture-editor、navigate-startup-settings | 补录会话协调、输入归属、采集停放与数量上限；按用户要求未运行验证
- 2026-10-10 | 当前录屏编辑与多框功能测试打包 | Update-LatestTestPackage.ps1 | 固定目录已更新，8模块、94.45 MiB，编译与包内必要文件检查完成；未运行交互验收
- 2026-10-10 | 录屏文件日期时间在前、类型在后 | navigate-screen-recording、navigate-saved-artifacts | 同步实际占位与路径生成入口；未运行验证、未重新打包
- 2026-10-10 | 工作台左侧底部打开保存目录 | navigate-startup-settings、navigate-saved-artifacts | 补录侧栏快捷按钮与宿主事件连接入口；按用户要求未运行验证
- 2026-10-10 | v1.11.9 正式打包与 GitHub 发布 | publish-cutcut-release | 正式包生成后推送 872f276，13个资产上传并正式发布；未运行独立验证

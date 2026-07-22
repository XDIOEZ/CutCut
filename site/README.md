# 轻截迷你发布页

这是部署到 GitHub Pages 的纯静态发布站。首页会在浏览器中读取
`XDIOEZ/CutCut` 的最新 GitHub Release，并自动寻找文件名以
`complete-lightweight-win-x64.zip` 结尾的轻量完整包，以及以
`complete-portable-win-x64.zip` 结尾、内置 .NET 8 运行库的重量完整包。

`modules.html` 是独立模块分页，会自动识别：

- `long-capture-addon-win-x64.zip`
- `screen-recording-addon-win-x64.zip`

当前 Release 缺少某个独立模块包时，对应卡片会回退到 Releases 页面，并继续推荐
已经整合常用模块的完整包。

本地预览：

```powershell
python -m http.server 4173 --directory .\site
```

推送到 `main` 后，`.github/workflows/pages.yml` 会自动部署 `site` 目录。

# 轻截迷你发布页

这是部署到 GitHub Pages 的纯静态单页。页面会在浏览器中读取
`XDIOEZ/CutCut` 的最新 GitHub Release，并自动寻找文件名以
`complete-lightweight-win-x64.zip` 结尾的资源作为下载目标。

本地预览：

```powershell
python -m http.server 4173 --directory .\site
```

推送到 `main` 后，`.github/workflows/pages.yml` 会自动部署 `site` 目录。

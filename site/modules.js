const repository = "XDIOEZ/CutCut";
const releasesUrl = `https://github.com/${repository}/releases`;
const latestReleaseApi = `https://api.github.com/repos/${repository}/releases/latest`;

const modules = [
  {
    pattern: /long-capture-addon-win-x64\.zip$/i,
    status: document.querySelector("#long-capture-status"),
    meta: document.querySelector("#long-capture-meta"),
    button: document.querySelector("#long-capture-download"),
  },
  {
    pattern: /ocr-addon-win-x64\.zip$/i,
    status: document.querySelector("#ocr-status"),
    meta: document.querySelector("#ocr-meta"),
    button: document.querySelector("#ocr-download"),
  },
  {
    pattern: /paddle-ocr-tiny-addon-win-x64\.zip$/i,
    status: document.querySelector("#paddle-ocr-tiny-status"),
    meta: document.querySelector("#paddle-ocr-tiny-meta"),
    button: document.querySelector("#paddle-ocr-tiny-download"),
  },
  {
    pattern: /paddle-ocr-small-addon-win-x64\.zip$/i,
    status: document.querySelector("#paddle-ocr-small-status"),
    meta: document.querySelector("#paddle-ocr-small-meta"),
    button: document.querySelector("#paddle-ocr-small-download"),
  },
  {
    pattern: /qr-code-addon-win-x64\.zip$/i,
    status: document.querySelector("#qr-code-status"),
    meta: document.querySelector("#qr-code-meta"),
    button: document.querySelector("#qr-code-download"),
  },
  {
    pattern: /screen-recording-addon-win-x64\.zip$/i,
    status: document.querySelector("#screen-recording-status"),
    meta: document.querySelector("#screen-recording-meta"),
    button: document.querySelector("#screen-recording-download"),
  },
];

const catalogStatus = document.querySelector("#catalog-status");

function formatBytes(bytes) {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return "大小未知";
  }

  return `${(bytes / 1024 / 1024).toFixed(2)} MiB`;
}

function showModuleAsset(module, asset, releaseName) {
  if (!asset) {
    module.status.textContent = "当前版本未单独提供";
    module.status.dataset.state = "fallback";
    module.meta.textContent = `${releaseName} · 可改用已整合完整包`;
    module.button.href = releasesUrl;
    module.button.textContent = "查看 Releases";
    module.button.removeAttribute("download");
    return false;
  }

  module.status.textContent = "可独立下载";
  module.status.dataset.state = "ready";
  module.meta.textContent = `${releaseName} · ${formatBytes(asset.size)} · Windows x64`;
  module.button.href = asset.browser_download_url;
  module.button.textContent = "下载模块 ZIP";
  module.button.setAttribute("download", "");
  return true;
}

function showFallback() {
  for (const module of modules) {
    module.status.textContent = "暂时无法检查";
    module.status.dataset.state = "fallback";
    module.meta.textContent = "可前往 GitHub Releases 手动查看";
    module.button.href = releasesUrl;
    module.button.textContent = "查看 Releases";
    module.button.removeAttribute("download");
  }
  catalogStatus.textContent = "暂时无法读取版本信息，可前往 GitHub Releases 查看。";
  catalogStatus.dataset.state = "fallback";
}

async function loadModuleAssets() {
  try {
    const response = await fetch(latestReleaseApi, {
      headers: { Accept: "application/vnd.github+json" },
    });
    if (!response.ok) {
      throw new Error(`GitHub API returned ${response.status}`);
    }

    const release = await response.json();
    const releaseName = release.tag_name || release.name || "最新版";
    let availableCount = 0;
    for (const module of modules) {
      const asset = release.assets?.find(({ name }) => module.pattern.test(name));
      if (showModuleAsset(module, asset, releaseName)) {
        availableCount += 1;
      }
    }

    catalogStatus.textContent = availableCount > 0
      ? `已从 ${releaseName} 找到 ${availableCount} 个可独立下载的模块。`
      : `${releaseName} 暂未附带独立模块包，建议下载已整合完整包。`;
    catalogStatus.dataset.state = availableCount > 0 ? "ready" : "fallback";
  } catch {
    showFallback();
  }
}

loadModuleAssets();

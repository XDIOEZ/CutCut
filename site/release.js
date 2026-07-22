const repository = "XDIOEZ/CutCut";
const releasesUrl = `https://github.com/${repository}/releases`;
const latestReleaseApi = `https://api.github.com/repos/${repository}/releases/latest`;
const lightweightAssetPattern = /complete-lightweight-win-x64\.zip$/i;

const versionElement = document.querySelector("#release-version");
const sizeElement = document.querySelector("#release-size");
const statusElement = document.querySelector("#release-status");
const downloadButton = document.querySelector("#download-button");
const downloadLabel = document.querySelector("#download-label");

function formatBytes(bytes) {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return "< 5 MiB";
  }

  return `${(bytes / 1024 / 1024).toFixed(2)} MiB`;
}

function formatDate(dateText) {
  const date = new Date(dateText);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "long",
    day: "numeric",
  }).format(date);
}

function showFallback(message) {
  versionElement.textContent = "暂未发布";
  sizeElement.textContent = "< 5 MiB";
  downloadButton.href = releasesUrl;
  downloadLabel.textContent = "查看发布版本";
  statusElement.textContent = message;
  statusElement.dataset.state = "fallback";
}

async function loadLatestRelease() {
  try {
    const response = await fetch(latestReleaseApi, {
      headers: { Accept: "application/vnd.github+json" },
    });

    if (!response.ok) {
      throw new Error(`GitHub API returned ${response.status}`);
    }

    const release = await response.json();
    const asset = release.assets?.find(({ name }) => lightweightAssetPattern.test(name));

    if (!asset) {
      showFallback("最新版尚未附带轻量版文件，可前往 Releases 查看详情。");
      return;
    }

    const publishedDate = formatDate(release.published_at);
    versionElement.textContent = release.tag_name || release.name || "最新版";
    sizeElement.textContent = formatBytes(asset.size);
    downloadButton.href = asset.browser_download_url;
    downloadButton.setAttribute("download", "");
    downloadLabel.textContent = "下载轻量版 ZIP";
    statusElement.textContent = publishedDate
      ? `${publishedDate} 发布 · 由 GitHub Releases 提供下载`
      : "由 GitHub Releases 提供下载";
    statusElement.dataset.state = "ready";
  } catch {
    showFallback("暂时无法读取版本信息，可前往 GitHub Releases 下载。");
  }
}

loadLatestRelease();

const repository = "XDIOEZ/CutCut";
const releasesUrl = `https://github.com/${repository}/releases`;
const latestReleaseApi = `https://api.github.com/repos/${repository}/releases/latest`;
const lightweightAssetPattern = /complete-lightweight-win-x64\.zip$/i;
const portableAssetPattern = /complete-portable-win-x64\.zip$/i;
const lightweightFullAssetPattern = /complete-lightweight-full-win-x64\.zip$/i;
const fullAssetPattern = /complete-full-win-x64\.zip$/i;

const versionElement = document.querySelector("#release-version");
const sizeElement = document.querySelector("#release-size");
const statusElement = document.querySelector("#release-status");
const downloadButton = document.querySelector("#download-button");
const downloadLabel = document.querySelector("#download-label");
const portableDownloadButton = document.querySelector("#portable-download-button");
const portableDownloadLabel = document.querySelector("#portable-download-label");
const portableDownloadSize = document.querySelector("#portable-download-size");
const lightweightFullDownloadButton = document.querySelector("#lightweight-full-download-button");
const lightweightFullDownloadLabel = document.querySelector("#lightweight-full-download-label");
const lightweightFullDownloadSize = document.querySelector("#lightweight-full-download-size");
const fullDownloadButton = document.querySelector("#full-download-button");
const fullDownloadLabel = document.querySelector("#full-download-label");
const fullDownloadSize = document.querySelector("#full-download-size");

function formatBytes(bytes, fallback = "< 5 MiB") {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return fallback;
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
  downloadButton.removeAttribute("download");
  downloadLabel.textContent = "查看发布版本";
  portableDownloadButton.href = releasesUrl;
  portableDownloadButton.removeAttribute("download");
  portableDownloadLabel.textContent = "重量版";
  portableDownloadSize.textContent = "80+ MiB";
  lightweightFullDownloadButton.href = releasesUrl;
  lightweightFullDownloadButton.removeAttribute("download");
  lightweightFullDownloadLabel.textContent = "轻量完全版";
  lightweightFullDownloadSize.textContent = "55+ MiB";
  fullDownloadButton.href = releasesUrl;
  fullDownloadButton.removeAttribute("download");
  fullDownloadLabel.textContent = "完全版";
  fullDownloadSize.textContent = "110+ MiB";
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
    const lightweightAsset = release.assets?.find(({ name }) =>
      lightweightAssetPattern.test(name));
    const portableAsset = release.assets?.find(({ name }) => portableAssetPattern.test(name));
    const lightweightFullAsset = release.assets?.find(({ name }) =>
      lightweightFullAssetPattern.test(name));
    const fullAsset = release.assets?.find(({ name }) => fullAssetPattern.test(name));

    if (!lightweightAsset && !portableAsset && !lightweightFullAsset && !fullAsset) {
      showFallback("最新版尚未附带可下载文件，可前往 Releases 查看详情。");
      return;
    }

    const publishedDate = formatDate(release.published_at);
    versionElement.textContent = release.tag_name || release.name || "最新版";
    if (lightweightAsset) {
      sizeElement.textContent = formatBytes(lightweightAsset.size);
      downloadButton.href = lightweightAsset.browser_download_url;
      downloadButton.setAttribute("download", "");
      downloadLabel.textContent = "下载轻量版 ZIP";
    } else {
      sizeElement.textContent = "< 5 MiB";
      downloadButton.href = releasesUrl;
      downloadButton.removeAttribute("download");
      downloadLabel.textContent = "轻量版暂未附带";
    }

    if (portableAsset) {
      portableDownloadButton.href = portableAsset.browser_download_url;
      portableDownloadButton.setAttribute("download", "");
      portableDownloadLabel.textContent = "重量版";
      portableDownloadSize.textContent = formatBytes(portableAsset.size, "80+ MiB");
    } else {
      portableDownloadButton.href = releasesUrl;
      portableDownloadButton.removeAttribute("download");
      portableDownloadLabel.textContent = "重量版未附带";
      portableDownloadSize.textContent = "查看 Releases";
    }

    if (lightweightFullAsset) {
      lightweightFullDownloadButton.href = lightweightFullAsset.browser_download_url;
      lightweightFullDownloadButton.setAttribute("download", "");
      lightweightFullDownloadLabel.textContent = "轻量完全版";
      lightweightFullDownloadSize.textContent = formatBytes(
        lightweightFullAsset.size,
        "55+ MiB");
    } else {
      lightweightFullDownloadButton.href = releasesUrl;
      lightweightFullDownloadButton.removeAttribute("download");
      lightweightFullDownloadLabel.textContent = "轻量完全版未附带";
      lightweightFullDownloadSize.textContent = "查看 Releases";
    }

    if (fullAsset) {
      fullDownloadButton.href = fullAsset.browser_download_url;
      fullDownloadButton.setAttribute("download", "");
      fullDownloadLabel.textContent = "完全版";
      fullDownloadSize.textContent = formatBytes(fullAsset.size, "110+ MiB");
    } else {
      fullDownloadButton.href = releasesUrl;
      fullDownloadButton.removeAttribute("download");
      fullDownloadLabel.textContent = "完全版未附带";
      fullDownloadSize.textContent = "查看 Releases";
    }

    const availableEditions = [
      lightweightAsset && "轻量版",
      portableAsset && "重量版",
      lightweightFullAsset && "轻量完全版",
      fullAsset && "完全版",
    ]
      .filter(Boolean)
      .join("、");
    statusElement.textContent = publishedDate
      ? `${publishedDate} 发布 · ${availableEditions}由 GitHub Releases 提供下载`
      : `${availableEditions}由 GitHub Releases 提供下载`;
    statusElement.dataset.state =
      lightweightAsset || portableAsset || lightweightFullAsset || fullAsset
        ? "ready"
        : "fallback";
  } catch {
    showFallback("暂时无法读取版本信息，可前往 GitHub Releases 下载。");
  }
}

loadLatestRelease();

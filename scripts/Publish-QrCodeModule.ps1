[CmdletBinding()]
param(
    [string]$OutputRoot = "artifacts"
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}
$outputRootPrefix = $outputRootPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$packageDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $outputRootPath "qr-code-addon-win-x64"))
$buildDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $outputRootPath ".qr-code-addon-build"))

foreach ($targetPath in @($packageDirectory, $buildDirectory)) {
    if (-not $targetPath.StartsWith(
            $outputRootPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "QR code package paths must remain inside the selected output root: $targetPath"
    }
}

foreach ($directory in @($packageDirectory, $buildDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

$moduleProject = Join-Path $repoRoot "src\ScreenshotTool.QrCode\ScreenshotTool.QrCode.csproj"
$moduleDirectory = Join-Path $packageDirectory "Modules\QrCode"

& dotnet publish $moduleProject -c Release -p:DebugSymbols=false -p:DebugType=None -o $buildDirectory
if ($LASTEXITCODE -ne 0) {
    throw "QR code module publish failed with exit code $LASTEXITCODE."
}

$requiredModuleFiles = @(
    "ScreenshotTool.QrCode.dll",
    "zxing.dll",
    "LICENSE-ZXING.NET.txt"
)
foreach ($fileName in $requiredModuleFiles) {
    $sourcePath = Join-Path $buildDirectory $fileName
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "QR code module dependency was not published: $sourcePath"
    }
}

New-Item -ItemType Directory -Path $moduleDirectory -Force | Out-Null
foreach ($fileName in $requiredModuleFiles) {
    Copy-Item -LiteralPath (Join-Path $buildDirectory $fileName) -Destination $moduleDirectory
}
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\qr-code-addon.md") `
    -Destination (Join-Path $packageDirectory "QrCode-README.md")

$zipPath = "$packageDirectory.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $packageDirectory "*") -DestinationPath $zipPath

$modulePath = Join-Path $moduleDirectory "ScreenshotTool.QrCode.dll"
$files = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse)
$packageBytes = ($files | Measure-Object -Property Length -Sum).Sum
if ($packageBytes -gt 2MB) {
    throw "QR code add-on size $([math]::Round($packageBytes / 1MB, 2)) MiB exceeds 2 MiB."
}

[pscustomobject]@{
    Package = "QrCodeAddon"
    SizeMiB = [math]::Round($packageBytes / 1MB, 2)
    FileCount = $files.Count
    Path = $packageDirectory
    ZipPath = $zipPath
    ModuleSha256 = (Get-FileHash -LiteralPath $modulePath -Algorithm SHA256).Hash
} | Format-Table -AutoSize

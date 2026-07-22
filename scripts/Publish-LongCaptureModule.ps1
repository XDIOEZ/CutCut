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
    (Join-Path $outputRootPath "long-capture-addon-win-x64"))
$buildDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $outputRootPath ".long-capture-addon-build"))

foreach ($targetPath in @($packageDirectory, $buildDirectory)) {
    if (-not $targetPath.StartsWith(
            $outputRootPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Long capture package paths must remain inside the selected output root: $targetPath"
    }
}

foreach ($directory in @($packageDirectory, $buildDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

$moduleProject = Join-Path $repoRoot "src\ScreenshotTool.LongCapture\ScreenshotTool.LongCapture.csproj"
$moduleDirectory = Join-Path $packageDirectory "Modules\LongCapture"

& dotnet publish $moduleProject -c Release -p:DebugSymbols=false -p:DebugType=None -o $buildDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Long capture module publish failed with exit code $LASTEXITCODE."
}

$moduleSource = Join-Path $buildDirectory "ScreenshotTool.LongCapture.dll"
if (-not (Test-Path -LiteralPath $moduleSource -PathType Leaf)) {
    throw "Long capture module was not published: $moduleSource"
}

New-Item -ItemType Directory -Path $moduleDirectory -Force | Out-Null
Copy-Item -LiteralPath $moduleSource -Destination $moduleDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\long-capture-addon.md") `
    -Destination (Join-Path $packageDirectory "LongCapture-README.md")

$zipPath = "$packageDirectory.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $packageDirectory "*") -DestinationPath $zipPath

$modulePath = Join-Path $moduleDirectory "ScreenshotTool.LongCapture.dll"
$files = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse)
$packageBytes = ($files | Measure-Object -Property Length -Sum).Sum
[pscustomobject]@{
    Package = "LongCaptureAddon"
    SizeMiB = [math]::Round($packageBytes / 1MB, 2)
    FileCount = $files.Count
    Path = $packageDirectory
    ZipPath = $zipPath
    ModuleSha256 = (Get-FileHash -LiteralPath $modulePath -Algorithm SHA256).Hash
} | Format-Table -AutoSize

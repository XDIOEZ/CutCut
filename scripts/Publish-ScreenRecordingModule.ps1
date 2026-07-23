[CmdletBinding()]
param(
    [string]$OutputRoot = "artifacts",

    [string]$BuildArtifactsRoot,

    [switch]$SkipArchive
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}
$packageDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $outputRootPath "screen-recording-addon-win-x64"))
$buildDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $outputRootPath ".screen-recording-addon-build"))

if (-not $packageDirectory.StartsWith($outputRootPath, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $buildDirectory.StartsWith($outputRootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Screen recording package paths must remain inside the selected output root."
}

foreach ($directory in @($packageDirectory, $buildDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

$moduleProject = Join-Path $repoRoot "src\ScreenshotTool.ScreenRecording\ScreenshotTool.ScreenRecording.csproj"
$helperProject = Join-Path $repoRoot "src\ScreenshotTool.ScreenRecording.Recorder\ScreenshotTool.ScreenRecording.Recorder.csproj"
$moduleBuild = Join-Path $buildDirectory "module"
$helperBuild = Join-Path $buildDirectory "helper"
$modulesDirectory = Join-Path $packageDirectory "Modules"
$moduleDirectory = Join-Path $modulesDirectory "ScreenRecording"
$helperDirectory = Join-Path $moduleDirectory "Recorder"

$modulePublishArguments = @(
    "publish",
    $moduleProject,
    "-c",
    "Release",
    "-p:DebugSymbols=false",
    "-p:DebugType=None",
    "-o",
    $moduleBuild
)
$helperPublishArguments = @(
    "publish",
    $helperProject,
    "-c",
    "Release",
    "-p:Platform=x64",
    "-r",
    "win-x64",
    "--self-contained",
    "false",
    "-p:DebugSymbols=false",
    "-p:DebugType=None",
    "-o",
    $helperBuild
)
if (-not [string]::IsNullOrWhiteSpace($BuildArtifactsRoot)) {
    $buildArtifactsPath = if ([System.IO.Path]::IsPathRooted($BuildArtifactsRoot)) {
        [System.IO.Path]::GetFullPath($BuildArtifactsRoot)
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRoot $BuildArtifactsRoot))
    }
    $modulePublishArguments += @(
        "--artifacts-path",
        (Join-Path $buildArtifactsPath "module")
    )
    $helperPublishArguments += @(
        "--artifacts-path",
        (Join-Path $buildArtifactsPath "helper")
    )
}

& dotnet @modulePublishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Screen recording module publish failed with exit code $LASTEXITCODE."
}

& dotnet @helperPublishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Screen recording helper publish failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $helperDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $moduleBuild "ScreenshotTool.ScreenRecording.dll") `
    -Destination $moduleDirectory

$helperFiles = @(
    "ScreenshotTool.ScreenRecording.Recorder.exe",
    "ScreenshotTool.ScreenRecording.Recorder.dll",
    "ScreenshotTool.ScreenRecording.Recorder.deps.json",
    "ScreenshotTool.ScreenRecording.Recorder.runtimeconfig.json",
    "ScreenRecorderLib.dll"
)
foreach ($file in $helperFiles) {
    $source = Join-Path $helperBuild $file
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Screen recording helper file was not published: $source"
    }
    Copy-Item -LiteralPath $source -Destination $helperDirectory
}

Copy-Item -LiteralPath (Join-Path $repoRoot "docs\screen-recording-addon.md") `
    -Destination (Join-Path $packageDirectory "ScreenRecording-README.md")
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\third-party\ScreenRecorderLib-LICENSE.txt") `
    -Destination (Join-Path $packageDirectory "ScreenRecorderLib-LICENSE.txt")

$zipPath = $null
if (-not $SkipArchive) {
    $zipPath = "$packageDirectory.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $packageDirectory "*") -DestinationPath $zipPath
}

$files = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse)
$packageBytes = ($files | Measure-Object -Property Length -Sum).Sum
[pscustomobject]@{
    Package = "ScreenRecordingAddon"
    SizeMiB = [math]::Round($packageBytes / 1MB, 2)
    FileCount = $files.Count
    Path = $packageDirectory
    ZipPath = $zipPath
    ModuleSha256 = (Get-FileHash -LiteralPath (
        Join-Path $moduleDirectory "ScreenshotTool.ScreenRecording.dll") -Algorithm SHA256).Hash
} | Format-Table -AutoSize

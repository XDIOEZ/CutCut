[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    return [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $RelativePath))
}

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ParentPath,

        [Parameter(Mandatory = $true)]
        [string]$ChildPath,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $parentPrefix = $ParentPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $ChildPath.StartsWith(
            $parentPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must remain inside '$ParentPath': $ChildPath"
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE."
    }
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$testRoot = Resolve-RepositoryPath -RepositoryRoot $repoRoot -RelativePath "测试打包"
$latestPackageName = "轻截-最新测试版"
$destinationDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $testRoot $latestPackageName))
$artifactsRoot = Resolve-RepositoryPath -RepositoryRoot $repoRoot -RelativePath "artifacts"
$workRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $artifactsRoot ".latest-test-package-work"))
$modelCacheRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $artifactsRoot "latest-test-package-model-cache"))
$hostOutput = Join-Path $workRoot "host"
$addonOutput = Join-Path $workRoot "addons"
$dotnetArtifactsRoot = Join-Path $workRoot "dotnet-artifacts"
$stagedPackage = Join-Path $workRoot $latestPackageName
$previousPackage = Join-Path $workRoot ".previous-package"

Assert-ChildPath -ParentPath $repoRoot -ChildPath $testRoot -Description "Test package root"
Assert-ChildPath -ParentPath $testRoot -ChildPath $destinationDirectory `
    -Description "Latest test package"
Assert-ChildPath -ParentPath $repoRoot -ChildPath $artifactsRoot -Description "Artifacts root"
Assert-ChildPath -ParentPath $artifactsRoot -ChildPath $workRoot `
    -Description "Test package work directory"
Assert-ChildPath -ParentPath $artifactsRoot -ChildPath $modelCacheRoot `
    -Description "PP-OCR model cache"

if (Test-Path -LiteralPath $testRoot) {
    $unexpectedEntries = @(
        Get-ChildItem -LiteralPath $testRoot -Force |
            Where-Object { $_.Name -ne $latestPackageName }
    )
    if ($unexpectedEntries.Count -gt 0) {
        $names = $unexpectedEntries.Name -join ", "
        throw "The test package root contains unexpected entries. Remove or move them first: $names"
    }
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
if (Test-Path -LiteralPath $workRoot) {
    Remove-Item -LiteralPath $workRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $hostOutput -Force | Out-Null
New-Item -ItemType Directory -Path $addonOutput -Force | Out-Null
New-Item -ItemType Directory -Path $stagedPackage -Force | Out-Null

Write-Host "Publishing the lightweight host..."
Invoke-DotNet -Arguments @(
    "publish",
    (Join-Path $repoRoot "src\ScreenshotTool\ScreenshotTool.csproj"),
    "-p:PublishProfile=LightweightWinX64",
    "-o",
    $hostOutput,
    "--artifacts-path",
    (Join-Path $dotnetArtifactsRoot "host")
) -FailureMessage "Lightweight host publish failed."
Copy-Item -Path (Join-Path $hostOutput "*") -Destination $stagedPackage -Recurse -Force

Write-Host "Publishing the screen-recording module..."
& (Join-Path $PSScriptRoot "Publish-ScreenRecordingModule.ps1") `
    -OutputRoot $addonOutput `
    -BuildArtifactsRoot (Join-Path $dotnetArtifactsRoot "screen-recording") `
    -SkipArchive
if ($LASTEXITCODE -ne 0) {
    throw "Screen-recording module publish failed with exit code $LASTEXITCODE."
}

foreach ($variant in @("Tiny", "Small")) {
    Write-Host "Publishing the PaddleOCR $variant module..."
    & (Join-Path $PSScriptRoot "Publish-PaddleOcrModule.ps1") `
        -OutputRoot $addonOutput `
        -Variant $variant `
        -ModelCacheDirectory (Join-Path $modelCacheRoot $variant) `
        -BuildArtifactsRoot (Join-Path $dotnetArtifactsRoot "paddle-ocr-$($variant.ToLowerInvariant())") `
        -SkipArchive
    if ($LASTEXITCODE -ne 0) {
        throw "PaddleOCR $variant module publish failed with exit code $LASTEXITCODE."
    }
}

$addonDirectories = @(
    (Join-Path $addonOutput "screen-recording-addon-win-x64"),
    (Join-Path $addonOutput "paddle-ocr-tiny-addon-win-x64"),
    (Join-Path $addonOutput "paddle-ocr-small-addon-win-x64")
)
foreach ($addonDirectory in $addonDirectories) {
    $addonModules = Join-Path $addonDirectory "Modules"
    if (-not (Test-Path -LiteralPath $addonModules -PathType Container)) {
        throw "The generated add-on is missing its Modules directory: $addonDirectory"
    }

    New-Item -ItemType Directory -Path (Join-Path $stagedPackage "Modules") `
        -Force | Out-Null
    Copy-Item -Path (Join-Path $addonModules "*") `
        -Destination (Join-Path $stagedPackage "Modules") `
        -Recurse `
        -Force

    Get-ChildItem -LiteralPath $addonDirectory -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $stagedPackage -Force
    }
}

$requiredFiles = @(
    "ScreenshotTool.exe",
    "Modules\PinnedImage\ScreenshotTool.PinnedImage.dll",
    "Modules\LongCapture\ScreenshotTool.LongCapture.dll",
    "Modules\Ocr\ScreenshotTool.Ocr.dll",
    "Modules\QrCode\ScreenshotTool.QrCode.dll",
    "Modules\QrCode\zxing.dll",
    "Modules\ScreenRecording\ScreenshotTool.ScreenRecording.dll",
    "Modules\ScreenRecording\Recorder\ScreenshotTool.ScreenRecording.Recorder.exe",
    "Modules\ScreenRecording\Recorder\ScreenRecorderLib.dll",
    "Modules\PaddleOcrTiny\ScreenshotTool.PaddleOcr.Tiny.dll",
    "Modules\PaddleOcrTiny\ScreenshotTool.PaddleOcr.dll",
    "Modules\PaddleOcrTiny\Models\ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
    "Modules\PaddleOcrTiny\Models\PP-OCRv6_det_tiny.onnx",
    "Modules\PaddleOcrTiny\Models\PP-OCRv6_rec_tiny.onnx",
    "Modules\PaddleOcrTiny\Models\ppocrv6_tiny_dict.txt",
    "Modules\PaddleOcrSmall\ScreenshotTool.PaddleOcr.Small.dll",
    "Modules\PaddleOcrSmall\ScreenshotTool.PaddleOcr.dll",
    "Modules\PaddleOcrSmall\Models\ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
    "Modules\PaddleOcrSmall\Models\PP-OCRv6_det_small.onnx",
    "Modules\PaddleOcrSmall\Models\PP-OCRv6_rec_small.onnx",
    "Modules\PaddleOcrSmall\Models\ppocrv6_dict.txt"
)
foreach ($relativePath in $requiredFiles) {
    $requiredPath = Join-Path $stagedPackage $relativePath
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "The latest test package is incomplete: $relativePath"
    }
}

$disabledMarkers = @(
    Get-ChildItem -LiteralPath (Join-Path $stagedPackage "Modules") `
        -Filter ".lightshot-module-disabled.json" `
        -File `
        -Recurse
)
if ($disabledMarkers.Count -gt 0) {
    throw "The latest test package contains disabled module markers."
}

$expectedModules = @(
    "LongCapture",
    "Ocr",
    "PaddleOcrSmall",
    "PaddleOcrTiny",
    "PinnedImage",
    "QrCode",
    "ScreenRecording"
)
$actualModules = @(
    Get-ChildItem -LiteralPath (Join-Path $stagedPackage "Modules") -Directory |
        Sort-Object -Property Name |
        Select-Object -ExpandProperty Name
)
if (($actualModules -join "|") -ne ($expectedModules -join "|")) {
    throw "Unexpected module set. Expected: $($expectedModules -join ', '). Actual: $($actualModules -join ', ')."
}

[xml]$project = Get-Content -LiteralPath (
    Join-Path $repoRoot "src\ScreenshotTool\ScreenshotTool.csproj")
$version = [string]$project.Project.PropertyGroup.Version
$commit = (& git -C $repoRoot rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    $commit = "unknown"
}
$hasLocalChanges = -not [string]::IsNullOrWhiteSpace(
    ((& git -C $repoRoot status --porcelain) -join [Environment]::NewLine))
$packageFiles = @(Get-ChildItem -LiteralPath $stagedPackage -File -Recurse)
$packageBytes = ($packageFiles | Measure-Object -Property Length -Sum).Sum
$packageSizeMiB = [math]::Round($packageBytes / 1MB, 2)
if ($packageBytes -gt 110MB) {
    throw "The lightweight full test package is $packageSizeMiB MiB, exceeding the 110 MiB limit."
}

$informationLines = @(
    "轻截最新测试包",
    "版本：$version",
    "生成时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')",
    "发布配置：LightweightWinX64",
    "运行要求：Windows x64；已安装 .NET 8 Desktop Runtime",
    "源码提交：$commit",
    "包含未提交改动：$(if ($hasLocalChanges) { '是' } else { '否' })",
    "模块：$($expectedModules -join '、')",
    "更新方式：在仓库根目录运行 powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Update-LatestTestPackage.ps1",
    "说明：这是本地测试包，不代表正式 GitHub Release。"
)
[System.IO.File]::WriteAllLines(
    (Join-Path $stagedPackage "测试包信息.txt"),
    $informationLines,
    [System.Text.UTF8Encoding]::new($true))

New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$movedPreviousPackage = $false
try {
    if (Test-Path -LiteralPath $destinationDirectory) {
        Move-Item -LiteralPath $destinationDirectory -Destination $previousPackage
        $movedPreviousPackage = $true
    }
    Move-Item -LiteralPath $stagedPackage -Destination $destinationDirectory
} catch {
    if ($movedPreviousPackage -and
        -not (Test-Path -LiteralPath $destinationDirectory) -and
        (Test-Path -LiteralPath $previousPackage)) {
        Move-Item -LiteralPath $previousPackage -Destination $destinationDirectory
    }
    throw
}

if (Test-Path -LiteralPath $previousPackage) {
    Remove-Item -LiteralPath $previousPackage -Recurse -Force
}

$rootEntries = @(Get-ChildItem -LiteralPath $testRoot -Force)
if ($rootEntries.Count -ne 1 -or
    $rootEntries[0].FullName -ne $destinationDirectory -or
    -not $rootEntries[0].PSIsContainer) {
    throw "The test package root must contain only '$latestPackageName'."
}

$finalRequiredFiles = foreach ($relativePath in $requiredFiles) {
    Get-Item -LiteralPath (Join-Path $destinationDirectory $relativePath)
}
$exePath = Join-Path $destinationDirectory "ScreenshotTool.exe"
$exeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash

Remove-Item -LiteralPath $workRoot -Recurse -Force

[pscustomobject]@{
    Version = $version
    Profile = "LightweightWinX64"
    Modules = $expectedModules.Count
    VerifiedFiles = @($finalRequiredFiles).Count
    SizeMiB = $packageSizeMiB
    Path = $destinationDirectory
    ExeSha256 = $exeHash
} | Format-List

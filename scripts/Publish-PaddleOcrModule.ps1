[CmdletBinding()]
param(
    [string]$OutputRoot = "artifacts",

    [Parameter(Mandatory = $true)]
    [ValidateSet("Tiny", "Small")]
    [string]$Variant
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
$variantSlug = $Variant.ToLowerInvariant()
$packageName = "paddle-ocr-$variantSlug-addon-win-x64"
$packageDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $outputRootPath $packageName))
$buildDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $outputRootPath ".$packageName-build"))
$moduleFolderName = "PaddleOcr$Variant"
$moduleDirectory = Join-Path $packageDirectory "Modules\$moduleFolderName"
$modelDirectory = Join-Path $moduleDirectory "Models"

foreach ($targetPath in @($packageDirectory, $buildDirectory)) {
    if (-not $targetPath.StartsWith(
            $outputRootPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "PP-OCR package paths must remain inside the selected output root: $targetPath"
    }
}

foreach ($directory in @($packageDirectory, $buildDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

$moduleProject = Join-Path $repoRoot (
    "src\ScreenshotTool.PaddleOcr.$Variant\ScreenshotTool.PaddleOcr.$Variant.csproj")
& dotnet publish $moduleProject `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    -o $buildDirectory
if ($LASTEXITCODE -ne 0) {
    throw "PP-OCR $Variant module publish failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $moduleDirectory -Force | Out-Null
$excludedNames = @(
    "ScreenshotTool.Contracts.dll",
    "ScreenshotTool.Contracts.pdb",
    "ScreenshotTool.PaddleOcr.$Variant.deps.json"
)
$buildFiles = Get-ChildItem -LiteralPath $buildDirectory -File |
    Where-Object {
        $_.Name -notin $excludedNames -and
        $_.Extension -notin @(".pdb", ".xml")
    }
foreach ($file in $buildFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination $moduleDirectory
}

& (Join-Path $PSScriptRoot "Get-PaddleOcrModels.ps1") `
    -Variant $Variant `
    -Destination $modelDirectory
if ($LASTEXITCODE -ne 0) {
    throw "PP-OCR $Variant model download failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $repoRoot "docs\paddle-ocr-addon.md") `
    -Destination (Join-Path $packageDirectory "PaddleOcr-README.md")
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\third-party\PaddleOcr-THIRD-PARTY-NOTICES.txt") `
    -Destination (Join-Path $moduleDirectory "THIRD-PARTY-NOTICES.txt")

$entryPath = Join-Path $moduleDirectory "ScreenshotTool.PaddleOcr.$Variant.dll"
$sharedPath = Join-Path $moduleDirectory "ScreenshotTool.PaddleOcr.dll"
$runtimePath = Join-Path $moduleDirectory "onnxruntime.dll"
$skiaPath = Join-Path $moduleDirectory "libSkiaSharp.dll"
foreach ($requiredPath in @(
        $entryPath,
        $sharedPath,
        $runtimePath,
        $skiaPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "PP-OCR $Variant package is incomplete: $requiredPath"
    }
}

$zipPath = "$packageDirectory.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $packageDirectory "*") -DestinationPath $zipPath

$files = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse)
$packageBytes = ($files | Measure-Object -Property Length -Sum).Sum
[pscustomobject]@{
    Package = "PaddleOcr$Variant"
    SizeMiB = [math]::Round($packageBytes / 1MB, 2)
    FileCount = $files.Count
    Path = $packageDirectory
    ZipPath = $zipPath
    ModuleSha256 = (Get-FileHash -LiteralPath $entryPath -Algorithm SHA256).Hash
} | Format-Table -AutoSize

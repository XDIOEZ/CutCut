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
$project = Join-Path $repoRoot "src\ScreenshotTool\ScreenshotTool.csproj"
$lightOutput = Join-Path $outputRootPath "lightweight-win-x64"
$portableOutput = Join-Path $outputRootPath "portable-compressed-win-x64"
$moduleRelativePath = "Modules\ScreenshotTool.LongCapture.dll"

& dotnet publish $project -p:PublishProfile=LightweightWinX64 -o $lightOutput
if ($LASTEXITCODE -ne 0) {
    throw "Lightweight publish failed with exit code $LASTEXITCODE."
}

& dotnet publish $project -p:PublishProfile=PortableCompressedWinX64 -o $portableOutput
if ($LASTEXITCODE -ne 0) {
    throw "Portable compressed publish failed with exit code $LASTEXITCODE."
}

$packages = @(
    [pscustomobject]@{
        Name = "Lightweight"
        Directory = $lightOutput
        MaximumBytes = 5MB
    },
    [pscustomobject]@{
        Name = "PortableCompressed"
        Directory = $portableOutput
        MaximumBytes = 90MB
    }
)

$results = foreach ($package in $packages) {
    $entryPoint = Join-Path $package.Directory "ScreenshotTool.exe"
    $module = Join-Path $package.Directory $moduleRelativePath
    if (-not (Test-Path -LiteralPath $entryPoint -PathType Leaf)) {
        throw "$($package.Name) entry point was not published: $entryPoint"
    }
    if (-not (Test-Path -LiteralPath $module -PathType Leaf)) {
        throw "$($package.Name) long capture module was not published: $module"
    }

    $files = @(Get-ChildItem -LiteralPath $package.Directory -File -Recurse)
    $packageBytes = ($files | Measure-Object -Property Length -Sum).Sum
    if ($null -eq $packageBytes) {
        $packageBytes = 0
    }
    if ($packageBytes -gt $package.MaximumBytes) {
        throw "$($package.Name) package size $([math]::Round($packageBytes / 1MB, 2)) MiB exceeds $([math]::Round($package.MaximumBytes / 1MB, 2)) MiB."
    }

    [pscustomobject]@{
        Package = $package.Name
        SizeMiB = [math]::Round($packageBytes / 1MB, 2)
        FileCount = $files.Count
        Path = (Get-Item -LiteralPath $package.Directory).FullName
        EntryPointSha256 = (Get-FileHash -LiteralPath $entryPoint -Algorithm SHA256).Hash
        ModuleSha256 = (Get-FileHash -LiteralPath $module -Algorithm SHA256).Hash
    }
}

$results | Format-Table -AutoSize

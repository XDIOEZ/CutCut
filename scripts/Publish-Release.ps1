[CmdletBinding()]
param(
    [string]$OutputRoot = "artifacts",
    [switch]$SkipScreenRecordingAddon
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}
$project = Join-Path $repoRoot "src\ScreenshotTool\ScreenshotTool.csproj"
$versionNode = Select-Xml -Path $project -XPath "/Project/PropertyGroup/Version" |
    Select-Object -First 1
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.Node.InnerText)) {
    throw "Main application version was not found in $project."
}
$releaseVersion = $versionNode.Node.InnerText.Trim()
$lightOutput = Join-Path $outputRootPath "lightweight-win-x64"
$portableOutput = Join-Path $outputRootPath "portable-compressed-win-x64"
$moduleRelativePath = "Modules\LongCapture\ScreenshotTool.LongCapture.dll"

function Get-FileSha256WithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $attempts = 10
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        try {
            return (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash
        } catch [System.IO.IOException] {
            if ($attempt -eq $attempts) {
                throw
            }
            Start-Sleep -Milliseconds 250
        }
    }
}

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
        EntryPointSha256 = Get-FileSha256WithRetry -Path $entryPoint
        ModuleSha256 = Get-FileSha256WithRetry -Path $module
    }
}

$results | Format-Table -AutoSize

if (-not $SkipScreenRecordingAddon) {
    & (Join-Path $PSScriptRoot "Publish-ScreenRecordingModule.ps1") -OutputRoot $outputRootPath
    if ($LASTEXITCODE -ne 0) {
        throw "Screen recording add-on publish failed with exit code $LASTEXITCODE."
    }

    $addonDirectory = Join-Path $outputRootPath "screen-recording-addon-win-x64"
    $addonModulesDirectory = Join-Path $addonDirectory "Modules"
    $recordingModuleRelativePath = "Modules\ScreenRecording\ScreenshotTool.ScreenRecording.dll"
    $recorderRelativePath = "Modules\ScreenRecording\Recorder\ScreenshotTool.ScreenRecording.Recorder.exe"
    $recorderLibraryRelativePath = "Modules\ScreenRecording\Recorder\ScreenRecorderLib.dll"
    $outputRootPrefix = $outputRootPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not (Test-Path -LiteralPath $addonModulesDirectory -PathType Container)) {
        throw "Screen recording add-on modules were not published: $addonModulesDirectory"
    }

    function New-CompletePackage {
        param(
            [Parameter(Mandatory = $true)]
            [string]$Name,
            [Parameter(Mandatory = $true)]
            [string]$BaseDirectory,
            [Parameter(Mandatory = $true)]
            [long]$MaximumBytes
        )

        $packageDirectory = [System.IO.Path]::GetFullPath((Join-Path $outputRootPath $Name))
        $zipPath = [System.IO.Path]::GetFullPath("$packageDirectory.zip")
        foreach ($targetPath in @($packageDirectory, $zipPath)) {
            if (-not $targetPath.StartsWith(
                    $outputRootPrefix,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Complete package path must remain inside the selected output root: $targetPath"
            }
        }

        if (Test-Path -LiteralPath $packageDirectory) {
            Remove-Item -LiteralPath $packageDirectory -Recurse -Force
        }
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }

        New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
        foreach ($item in Get-ChildItem -LiteralPath $BaseDirectory -Force) {
            Copy-Item -LiteralPath $item.FullName -Destination $packageDirectory -Recurse -Force
        }

        $completeModulesDirectory = Join-Path $packageDirectory "Modules"
        New-Item -ItemType Directory -Path $completeModulesDirectory -Force | Out-Null
        foreach ($item in Get-ChildItem -LiteralPath $addonModulesDirectory -Force) {
            Copy-Item -LiteralPath $item.FullName -Destination $completeModulesDirectory -Recurse -Force
        }
        foreach ($documentationFile in Get-ChildItem -LiteralPath $addonDirectory -File -Force) {
            Copy-Item -LiteralPath $documentationFile.FullName -Destination $packageDirectory -Force
        }

        $entryPoint = Join-Path $packageDirectory "ScreenshotTool.exe"
        $longCaptureModule = Join-Path $packageDirectory $moduleRelativePath
        $recordingModule = Join-Path $packageDirectory $recordingModuleRelativePath
        $recorder = Join-Path $packageDirectory $recorderRelativePath
        $recorderLibrary = Join-Path $packageDirectory $recorderLibraryRelativePath
        foreach ($requiredFile in @(
                $entryPoint,
                $longCaptureModule,
                $recordingModule,
                $recorder,
                $recorderLibrary)) {
            if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
                throw "Complete package is missing a required file: $requiredFile"
            }
        }

        $files = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse)
        $packageBytes = ($files | Measure-Object -Property Length -Sum).Sum
        if ($null -eq $packageBytes) {
            $packageBytes = 0
        }
        if ($packageBytes -gt $MaximumBytes) {
            throw "Complete package $Name size $([math]::Round($packageBytes / 1MB, 2)) MiB exceeds $([math]::Round($MaximumBytes / 1MB, 2)) MiB."
        }

        Compress-Archive -Path (Join-Path $packageDirectory "*") -DestinationPath $zipPath

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $zipEntries = @($archive.Entries | ForEach-Object {
                    $_.FullName.Replace("\", "/")
                })
            $requiredEntries = @(
                "ScreenshotTool.exe",
                $moduleRelativePath.Replace("\", "/"),
                $recordingModuleRelativePath.Replace("\", "/"),
                $recorderRelativePath.Replace("\", "/"),
                $recorderLibraryRelativePath.Replace("\", "/"))
            $missingEntries = @($requiredEntries | Where-Object { $_ -notin $zipEntries })
            if ($missingEntries.Count -gt 0) {
                throw "Complete package archive is missing required entries: $($missingEntries -join ', ')"
            }
        } finally {
            $archive.Dispose()
        }

        [pscustomobject]@{
            Package = $Name
            SizeMiB = [math]::Round($packageBytes / 1MB, 2)
            FileCount = $files.Count
            Path = $packageDirectory
            ZipPath = $zipPath
            EntryPointSha256 = Get-FileSha256WithRetry -Path $entryPoint
            RecordingModuleSha256 = Get-FileSha256WithRetry -Path $recordingModule
        }
    }

    $completeResults = @(
        New-CompletePackage `
            -Name "complete-lightweight-win-x64" `
            -BaseDirectory $lightOutput `
            -MaximumBytes 5MB
        New-CompletePackage `
            -Name "complete-portable-win-x64" `
            -BaseDirectory $portableOutput `
            -MaximumBytes 90MB
    )
    $completeResults | Format-Table -AutoSize

    $readyToRunName = "LightShotCN-v$releaseVersion-ready-to-run"
    $readyToRunDirectory = [System.IO.Path]::GetFullPath(
        (Join-Path $outputRootPath $readyToRunName))
    if (-not $readyToRunDirectory.StartsWith(
            $outputRootPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Ready-to-run package path must remain inside the selected output root."
    }
    if (Test-Path -LiteralPath $readyToRunDirectory) {
        Remove-Item -LiteralPath $readyToRunDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $readyToRunDirectory -Force | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $completeResults[1].Path -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $readyToRunDirectory -Recurse -Force
    }

    $readyEntryPoint = Join-Path $readyToRunDirectory "ScreenshotTool.exe"
    $readyRecordingModule = Join-Path $readyToRunDirectory $recordingModuleRelativePath
    foreach ($requiredFile in @($readyEntryPoint, $readyRecordingModule)) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Ready-to-run package is missing a required file: $requiredFile"
        }
    }

    [pscustomobject]@{
        Package = "ReadyToRun"
        Version = $releaseVersion
        Path = $readyToRunDirectory
        EntryPoint = $readyEntryPoint
        EntryPointSha256 = Get-FileSha256WithRetry -Path $readyEntryPoint
    } | Format-Table -AutoSize
}

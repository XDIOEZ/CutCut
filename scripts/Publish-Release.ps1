[CmdletBinding()]
param(
    [string]$OutputRoot = "artifacts",
    [switch]$SkipLongCaptureAddon,
    [switch]$SkipOcrAddon,
    [switch]$SkipPaddleOcrTinyAddon,
    [switch]$SkipPaddleOcrSmallAddon,
    [switch]$SkipQrCodeAddon,
    [switch]$SkipScreenRecordingAddon,
    [switch]$SkipFullPackage
)

$ErrorActionPreference = "Stop"
if (-not $SkipFullPackage -and (
        $SkipPaddleOcrTinyAddon -or
        $SkipPaddleOcrSmallAddon -or
        $SkipScreenRecordingAddon)) {
    throw "The full package requires PP-OCR Tiny, PP-OCR Small, and screen recording. Use -SkipFullPackage when skipping any of those add-ons."
}

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
$ocrModuleRelativePath = "Modules\Ocr\ScreenshotTool.Ocr.dll"
$qrCodeModuleRelativePath = "Modules\QrCode\ScreenshotTool.QrCode.dll"
$qrCodeDecoderRelativePath = "Modules\QrCode\zxing.dll"
$paddleOcrTinyRequiredRelativePaths = @(
    "Modules\PaddleOcrTiny\ScreenshotTool.PaddleOcr.Tiny.dll",
    "Modules\PaddleOcrTiny\ScreenshotTool.PaddleOcr.dll",
    "Modules\PaddleOcrTiny\Models\ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
    "Modules\PaddleOcrTiny\Models\PP-OCRv6_det_tiny.onnx",
    "Modules\PaddleOcrTiny\Models\PP-OCRv6_rec_tiny.onnx",
    "Modules\PaddleOcrTiny\Models\ppocrv6_tiny_dict.txt"
)
$paddleOcrSmallRequiredRelativePaths = @(
    "Modules\PaddleOcrSmall\ScreenshotTool.PaddleOcr.Small.dll",
    "Modules\PaddleOcrSmall\ScreenshotTool.PaddleOcr.dll",
    "Modules\PaddleOcrSmall\Models\ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
    "Modules\PaddleOcrSmall\Models\PP-OCRv6_det_small.onnx",
    "Modules\PaddleOcrSmall\Models\PP-OCRv6_rec_small.onnx",
    "Modules\PaddleOcrSmall\Models\ppocrv6_dict.txt"
)

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
    $ocrModule = Join-Path $package.Directory $ocrModuleRelativePath
    $qrCodeModule = Join-Path $package.Directory $qrCodeModuleRelativePath
    $qrCodeDecoder = Join-Path $package.Directory $qrCodeDecoderRelativePath
    if (-not (Test-Path -LiteralPath $entryPoint -PathType Leaf)) {
        throw "$($package.Name) entry point was not published: $entryPoint"
    }
    if (-not (Test-Path -LiteralPath $module -PathType Leaf)) {
        throw "$($package.Name) long capture module was not published: $module"
    }
    if (-not (Test-Path -LiteralPath $ocrModule -PathType Leaf)) {
        throw "$($package.Name) OCR module was not published: $ocrModule"
    }
    if (-not (Test-Path -LiteralPath $qrCodeModule -PathType Leaf)) {
        throw "$($package.Name) QR code module was not published: $qrCodeModule"
    }
    if (-not (Test-Path -LiteralPath $qrCodeDecoder -PathType Leaf)) {
        throw "$($package.Name) QR code decoder was not published: $qrCodeDecoder"
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
        OcrModuleSha256 = Get-FileSha256WithRetry -Path $ocrModule
        QrCodeModuleSha256 = Get-FileSha256WithRetry -Path $qrCodeModule
    }
}

$results | Format-Table -AutoSize

if (-not $SkipLongCaptureAddon) {
    & (Join-Path $PSScriptRoot "Publish-LongCaptureModule.ps1") -OutputRoot $outputRootPath
    if ($LASTEXITCODE -ne 0) {
        throw "Long capture add-on publish failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipOcrAddon) {
    & (Join-Path $PSScriptRoot "Publish-OcrModule.ps1") -OutputRoot $outputRootPath
    if ($LASTEXITCODE -ne 0) {
        throw "OCR add-on publish failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipPaddleOcrTinyAddon) {
    & (Join-Path $PSScriptRoot "Publish-PaddleOcrModule.ps1") `
        -OutputRoot $outputRootPath `
        -Variant Tiny
    if ($LASTEXITCODE -ne 0) {
        throw "PP-OCR Tiny add-on publish failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipPaddleOcrSmallAddon) {
    & (Join-Path $PSScriptRoot "Publish-PaddleOcrModule.ps1") `
        -OutputRoot $outputRootPath `
        -Variant Small
    if ($LASTEXITCODE -ne 0) {
        throw "PP-OCR Small add-on publish failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipQrCodeAddon) {
    & (Join-Path $PSScriptRoot "Publish-QrCodeModule.ps1") -OutputRoot $outputRootPath
    if ($LASTEXITCODE -ne 0) {
        throw "QR code add-on publish failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipScreenRecordingAddon) {
    & (Join-Path $PSScriptRoot "Publish-ScreenRecordingModule.ps1") -OutputRoot $outputRootPath
    if ($LASTEXITCODE -ne 0) {
        throw "Screen recording add-on publish failed with exit code $LASTEXITCODE."
    }

    $addonDirectory = Join-Path $outputRootPath "screen-recording-addon-win-x64"
    $addonModulesDirectory = Join-Path $addonDirectory "Modules"
    $paddleOcrTinyModulesDirectory = Join-Path $outputRootPath (
        "paddle-ocr-tiny-addon-win-x64\Modules")
    $paddleOcrSmallModulesDirectory = Join-Path $outputRootPath (
        "paddle-ocr-small-addon-win-x64\Modules")
    $recordingModuleRelativePath = "Modules\ScreenRecording\ScreenshotTool.ScreenRecording.dll"
    $recorderRelativePath = "Modules\ScreenRecording\Recorder\ScreenshotTool.ScreenRecording.Recorder.exe"
    $recorderLibraryRelativePath = "Modules\ScreenRecording\Recorder\ScreenRecorderLib.dll"
    $outputRootPrefix = $outputRootPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not (Test-Path -LiteralPath $addonModulesDirectory -PathType Container)) {
        throw "Screen recording add-on modules were not published: $addonModulesDirectory"
    }
    if (-not $SkipFullPackage) {
        foreach ($requiredModulesDirectory in @(
                $paddleOcrTinyModulesDirectory,
                $paddleOcrSmallModulesDirectory)) {
            if (-not (Test-Path -LiteralPath $requiredModulesDirectory -PathType Container)) {
                throw "Full package modules were not published: $requiredModulesDirectory"
            }
        }
    }

    function New-CompletePackage {
        param(
            [Parameter(Mandatory = $true)]
            [string]$Name,
            [Parameter(Mandatory = $true)]
            [string]$BaseDirectory,
            [Parameter(Mandatory = $true)]
            [long]$MaximumBytes,
            [Parameter(Mandatory = $true)]
            [string[]]$ModuleSourceDirectories,
            [string[]]$AdditionalRequiredRelativePaths = @(),
            [long]$MaximumZipBytes = [long]::MaxValue
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
        foreach ($moduleSourceDirectory in $ModuleSourceDirectories) {
            if (-not (Test-Path -LiteralPath $moduleSourceDirectory -PathType Container)) {
                throw "Complete package module source is missing: $moduleSourceDirectory"
            }
            foreach ($item in Get-ChildItem -LiteralPath $moduleSourceDirectory -Force) {
                Copy-Item `
                    -LiteralPath $item.FullName `
                    -Destination $completeModulesDirectory `
                    -Recurse `
                    -Force
            }

            $sourcePackageDirectory = Split-Path -Path $moduleSourceDirectory -Parent
            foreach ($documentationFile in Get-ChildItem `
                    -LiteralPath $sourcePackageDirectory `
                    -File `
                    -Force) {
                Copy-Item `
                    -LiteralPath $documentationFile.FullName `
                    -Destination $packageDirectory `
                    -Force
            }
        }

        $entryPoint = Join-Path $packageDirectory "ScreenshotTool.exe"
        $longCaptureModule = Join-Path $packageDirectory $moduleRelativePath
        $ocrModule = Join-Path $packageDirectory $ocrModuleRelativePath
        $qrCodeModule = Join-Path $packageDirectory $qrCodeModuleRelativePath
        $qrCodeDecoder = Join-Path $packageDirectory $qrCodeDecoderRelativePath
        $recordingModule = Join-Path $packageDirectory $recordingModuleRelativePath
        $recorder = Join-Path $packageDirectory $recorderRelativePath
        $recorderLibrary = Join-Path $packageDirectory $recorderLibraryRelativePath
        $requiredFiles = @(
                $entryPoint,
                $longCaptureModule,
                $ocrModule,
                $qrCodeModule,
                $qrCodeDecoder,
                $recordingModule,
                $recorder,
                $recorderLibrary)
        $requiredFiles += $AdditionalRequiredRelativePaths | ForEach-Object {
            Join-Path $packageDirectory $_
        }
        foreach ($requiredFile in $requiredFiles) {
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
        $zipBytes = (Get-Item -LiteralPath $zipPath).Length
        if ($zipBytes -gt $MaximumZipBytes) {
            throw "Complete package archive $Name size $([math]::Round($zipBytes / 1MB, 2)) MiB exceeds $([math]::Round($MaximumZipBytes / 1MB, 2)) MiB."
        }

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $zipEntries = @($archive.Entries | ForEach-Object {
                    $_.FullName.Replace("\", "/")
                })
            $requiredEntries = @(
                "ScreenshotTool.exe",
                $moduleRelativePath.Replace("\", "/"),
                $ocrModuleRelativePath.Replace("\", "/"),
                $qrCodeModuleRelativePath.Replace("\", "/"),
                $qrCodeDecoderRelativePath.Replace("\", "/"),
                $recordingModuleRelativePath.Replace("\", "/"),
                $recorderRelativePath.Replace("\", "/"),
                $recorderLibraryRelativePath.Replace("\", "/"))
            $requiredEntries += $AdditionalRequiredRelativePaths | ForEach-Object {
                $_.Replace("\", "/")
            }
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
            ZipSizeMiB = [math]::Round($zipBytes / 1MB, 2)
            FileCount = $files.Count
            Path = $packageDirectory
            ZipPath = $zipPath
            EntryPointSha256 = Get-FileSha256WithRetry -Path $entryPoint
            RecordingModuleSha256 = Get-FileSha256WithRetry -Path $recordingModule
            OcrModuleSha256 = Get-FileSha256WithRetry -Path $ocrModule
            QrCodeModuleSha256 = Get-FileSha256WithRetry -Path $qrCodeModule
        }
    }

    $completeResults = @(
        New-CompletePackage `
            -Name "complete-lightweight-win-x64" `
            -BaseDirectory $lightOutput `
            -MaximumBytes 5MB `
            -ModuleSourceDirectories @($addonModulesDirectory)
        New-CompletePackage `
            -Name "complete-portable-win-x64" `
            -BaseDirectory $portableOutput `
            -MaximumBytes 90MB `
            -ModuleSourceDirectories @($addonModulesDirectory)
    )
    if (-not $SkipFullPackage) {
        $fullModuleSourceDirectories = @(
            $addonModulesDirectory,
            $paddleOcrTinyModulesDirectory,
            $paddleOcrSmallModulesDirectory
        )
        $fullRequiredRelativePaths = @(
            $paddleOcrTinyRequiredRelativePaths +
            $paddleOcrSmallRequiredRelativePaths
        )
        $completeResults += New-CompletePackage `
            -Name "complete-full-win-x64" `
            -BaseDirectory $portableOutput `
            -MaximumBytes 180MB `
            -MaximumZipBytes 130MB `
            -ModuleSourceDirectories $fullModuleSourceDirectories `
            -AdditionalRequiredRelativePaths $fullRequiredRelativePaths
    }
    $completeResults | Format-Table -AutoSize

    $portableCompleteResult = $completeResults |
        Where-Object { $_.Package -eq "complete-portable-win-x64" } |
        Select-Object -First 1
    if ($null -eq $portableCompleteResult) {
        throw "Portable complete package result was not generated."
    }

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
    foreach ($item in Get-ChildItem -LiteralPath $portableCompleteResult.Path -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $readyToRunDirectory -Recurse -Force
    }

    $readyEntryPoint = Join-Path $readyToRunDirectory "ScreenshotTool.exe"
    $readyRecordingModule = Join-Path $readyToRunDirectory $recordingModuleRelativePath
    $readyOcrModule = Join-Path $readyToRunDirectory $ocrModuleRelativePath
    $readyQrCodeModule = Join-Path $readyToRunDirectory $qrCodeModuleRelativePath
    $readyQrCodeDecoder = Join-Path $readyToRunDirectory $qrCodeDecoderRelativePath
    foreach ($requiredFile in @(
            $readyEntryPoint,
            $readyRecordingModule,
            $readyOcrModule,
            $readyQrCodeModule,
            $readyQrCodeDecoder)) {
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

$releaseArchiveNames = @()
if (-not $SkipScreenRecordingAddon) {
    $releaseArchiveNames += @(
        "complete-lightweight-win-x64.zip",
        "complete-portable-win-x64.zip",
        "screen-recording-addon-win-x64.zip"
    )
    if (-not $SkipFullPackage) {
        $releaseArchiveNames += "complete-full-win-x64.zip"
    }
}
if (-not $SkipLongCaptureAddon) {
    $releaseArchiveNames += "long-capture-addon-win-x64.zip"
}
if (-not $SkipOcrAddon) {
    $releaseArchiveNames += "ocr-addon-win-x64.zip"
}
if (-not $SkipPaddleOcrTinyAddon) {
    $releaseArchiveNames += "paddle-ocr-tiny-addon-win-x64.zip"
}
if (-not $SkipPaddleOcrSmallAddon) {
    $releaseArchiveNames += "paddle-ocr-small-addon-win-x64.zip"
}
if (-not $SkipQrCodeAddon) {
    $releaseArchiveNames += "qr-code-addon-win-x64.zip"
}

if ($releaseArchiveNames.Count -eq 0) {
    throw "No release archives were generated, so SHA256SUMS.txt cannot be created."
}

$checksumLines = foreach ($archiveName in $releaseArchiveNames | Sort-Object) {
    $archivePath = Join-Path $outputRootPath $archiveName
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw "Release archive was not generated: $archivePath"
    }

    $hash = (Get-FileSha256WithRetry -Path $archivePath).ToLowerInvariant()
    "$hash  $archiveName"
}

$checksumPath = Join-Path $outputRootPath "SHA256SUMS.txt"
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines(
    $checksumPath,
    [string[]]$checksumLines,
    $utf8WithoutBom)

[pscustomobject]@{
    Package = "SHA256SUMS"
    FileCount = $checksumLines.Count
    Path = $checksumPath
    Sha256 = Get-FileSha256WithRetry -Path $checksumPath
} | Format-Table -AutoSize

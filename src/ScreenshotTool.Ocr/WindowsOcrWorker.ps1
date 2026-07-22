$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime

    $asTaskMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object {
            $_.Name -eq "AsTask" -and
            $_.IsGenericMethod -and
            $_.GetParameters().Count -eq 1 -and
            $_.GetParameters()[0].ParameterType.Name -eq "IAsyncOperation``1"
        } |
        Select-Object -First 1

    function Wait-WinRtOperation {
        param(
            [Parameter(Mandatory = $true)] $Operation,
            [Parameter(Mandatory = $true)] [Type] $ResultType
        )

        $task = $asTaskMethod.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
        $task.GetAwaiter().GetResult()
    }

    $storageFileType = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
    $randomAccessStreamType = [Windows.Storage.Streams.IRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]
    $bitmapDecoderType = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType = WindowsRuntime]
    $softwareBitmapType = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Foundation, ContentType = WindowsRuntime]
    $ocrEngineType = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
    $ocrResultType = [Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType = WindowsRuntime]

    $engine = $ocrEngineType::TryCreateFromUserProfileLanguages()
    if ($null -eq $engine) {
        throw "Windows 没有可用的 OCR 语言。请在系统语言和区域设置中安装当前语言的 OCR 组件。"
    }

    $inputPath = [Environment]::GetEnvironmentVariable("LIGHTSHOT_OCR_INPUT")
    $file = Wait-WinRtOperation ($storageFileType::GetFileFromPathAsync($inputPath)) $storageFileType
    $stream = Wait-WinRtOperation ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) $randomAccessStreamType

    try {
        $decoder = Wait-WinRtOperation ($bitmapDecoderType::CreateAsync($stream)) $bitmapDecoderType
        $pixelFormat = [Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8
        $alphaMode = [Windows.Graphics.Imaging.BitmapAlphaMode]::Premultiplied
        $maximumDimension = [uint32]$ocrEngineType::MaxImageDimension
        $largestDimension = [Math]::Max($decoder.OrientedPixelWidth, $decoder.OrientedPixelHeight)

        if ($largestDimension -le $maximumDimension) {
            $bitmapOperation = $decoder.GetSoftwareBitmapAsync($pixelFormat, $alphaMode)
        }
        else {
            $scale = $maximumDimension / [double]$largestDimension
            $transform = [Windows.Graphics.Imaging.BitmapTransform]::new()
            $transform.ScaledWidth = [Math]::Max(1, [Math]::Round($decoder.OrientedPixelWidth * $scale))
            $transform.ScaledHeight = [Math]::Max(1, [Math]::Round($decoder.OrientedPixelHeight * $scale))
            $transform.InterpolationMode = [Windows.Graphics.Imaging.BitmapInterpolationMode]::Fant
            $bitmapOperation = $decoder.GetSoftwareBitmapAsync(
                $pixelFormat,
                $alphaMode,
                $transform,
                [Windows.Graphics.Imaging.ExifOrientationMode]::RespectExifOrientation,
                [Windows.Graphics.Imaging.ColorManagementMode]::ColorManageToSRgb)
        }

        $bitmap = Wait-WinRtOperation $bitmapOperation $softwareBitmapType
        try {
            $result = Wait-WinRtOperation ($engine.RecognizeAsync($bitmap)) $ocrResultType
            $resultBytes = [Text.Encoding]::UTF8.GetBytes($result.Text)
            [Console]::Out.Write("OK:" + [Convert]::ToBase64String($resultBytes))
        }
        finally {
            $bitmap.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}
catch {
    $errorBytes = [Text.Encoding]::UTF8.GetBytes($_.Exception.Message)
    [Console]::Out.Write("ERROR:" + [Convert]::ToBase64String($errorBytes))
    exit 1
}

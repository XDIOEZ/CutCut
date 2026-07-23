[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Tiny", "Small")]
    [string]$Variant,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = "Stop"
$destinationPath = [System.IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null

$commonModel = [pscustomobject]@{
    FileName = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"
    Uri = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv5/cls/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"
    Sha256 = "54379ae5174d026780215fc748a7f31910dee36818e63d49e17dc598ecc82df7"
}

$variantModels = if ($Variant -eq "Tiny") {
    @(
        [pscustomobject]@{
            FileName = "PP-OCRv6_det_tiny.onnx"
            Uri = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv6/det/PP-OCRv6_det_tiny.onnx"
            Sha256 = "f42c0fbd294d95eac1a550e131b277dac97462c8025fa4b6c3cec1b7894bd3d5"
        },
        [pscustomobject]@{
            FileName = "PP-OCRv6_rec_tiny.onnx"
            Uri = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv6/rec/PP-OCRv6_rec_tiny.onnx"
            Sha256 = "e16e242de5937ad92609223f19bc2aff3727ee40b095f996907c24749bad251b"
        },
        [pscustomobject]@{
            FileName = "ppocrv6_tiny_dict.txt"
            Uri = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/paddle/PP-OCRv6/rec/PP-OCRv6_rec_tiny/ppocrv6_tiny_dict.txt"
            Sha256 = "c5cbe34ef40c29c4df07ed012bf96569cb69a2d2a01a07027e9f13cb832bd9cd"
        }
    )
} else {
    @(
        [pscustomobject]@{
            FileName = "PP-OCRv6_det_small.onnx"
            Uri = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv6/det/PP-OCRv6_det_small.onnx"
            Sha256 = "090f04abcd9d9a7498bc4ebf677e4cb9bdce1fe4197ddb7e529f1ef44e1ff94f"
        },
        [pscustomobject]@{
            FileName = "PP-OCRv6_rec_small.onnx"
            Uri = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv6/rec/PP-OCRv6_rec_small.onnx"
            Sha256 = "6f327246b50388f3c176ae304bd95767ea6dc0c9ae92153ef8cbe210b3c14884"
        },
        [pscustomobject]@{
            FileName = "ppocrv6_dict.txt"
            Uri = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/paddle/PP-OCRv6/rec/PP-OCRv6_rec_small/ppocrv6_dict.txt"
            Sha256 = "b5f2bfe2bdd9448429e3e82b51c789775d9b42f2403d082b00662eb77e401c5d"
        }
    )
}

function Get-VerifiedModel {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Model
    )

    $targetPath = [System.IO.Path]::GetFullPath(
        (Join-Path $destinationPath $Model.FileName))
    $destinationPrefix = $destinationPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $targetPath.StartsWith(
            $destinationPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "PP-OCR model path must remain inside the destination: $targetPath"
    }

    if (Test-Path -LiteralPath $targetPath -PathType Leaf) {
        $existingHash = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
        if ($existingHash.Equals(
                $Model.Sha256,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            return Get-Item -LiteralPath $targetPath
        }
    }

    $temporaryPath = "$targetPath.$([Guid]::NewGuid().ToString('N')).download"
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $Model.Uri -OutFile $temporaryPath
        $actualHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash
        if (-not $actualHash.Equals(
                $Model.Sha256,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "PP-OCR model checksum mismatch for $($Model.FileName): $actualHash"
        }

        Move-Item -LiteralPath $temporaryPath -Destination $targetPath -Force
        return Get-Item -LiteralPath $targetPath
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

$files = @($commonModel) + $variantModels | ForEach-Object {
    Get-VerifiedModel -Model $_
}

$files |
    Select-Object Name, Length, @{ Name = "Sha256"; Expression = {
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    } } |
    Format-Table -AutoSize

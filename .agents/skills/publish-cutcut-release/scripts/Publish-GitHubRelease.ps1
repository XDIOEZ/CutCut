[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^\d+\.\d+\.\d+$")]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$AssetRoot,
    [Parameter(Mandatory = $true)]
    [string]$NotesPath,
    [string]$Repository = "XDIOEZ/CutCut",
    [string]$Target = "main",
    [switch]$ValidateOnly,
    [switch]$ResumeDraft
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\..\.."))

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

$assetRootPath = Resolve-RepositoryPath -Path $AssetRoot
$notesPathValue = Resolve-RepositoryPath -Path $NotesPath
$projectPath = Join-Path $repoRoot "src\ScreenshotTool\ScreenshotTool.csproj"
$tag = "v$Version"
$expectedArchives = @(
    "complete-full-win-x64.zip",
    "complete-lightweight-full-win-x64.zip",
    "complete-lightweight-win-x64.zip",
    "complete-portable-win-x64.zip",
    "long-capture-addon-win-x64.zip",
    "ocr-addon-win-x64.zip",
    "paddle-ocr-small-addon-win-x64.zip",
    "paddle-ocr-tiny-addon-win-x64.zip",
    "qr-code-addon-win-x64.zip",
    "screen-recording-addon-win-x64.zip"
)

if (-not (Test-Path -LiteralPath $assetRootPath -PathType Container)) {
    throw "Release asset directory does not exist: $assetRootPath"
}
if (-not (Test-Path -LiteralPath $notesPathValue -PathType Leaf)) {
    throw "Release notes file does not exist: $notesPathValue"
}

$versionNode = Select-Xml -Path $projectPath -XPath "/Project/PropertyGroup/Version" |
    Select-Object -First 1
$projectVersion = $versionNode.Node.InnerText.Trim()
if ($projectVersion -ne $Version) {
    throw "Project version $projectVersion does not match requested release $Version."
}

$checksumPath = Join-Path $assetRootPath "SHA256SUMS.txt"
if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
    throw "Release checksum file does not exist: $checksumPath"
}

$manifest = @{}
foreach ($line in Get-Content -LiteralPath $checksumPath -Encoding UTF8) {
    if ($line -notmatch "^([0-9a-f]{64})  (.+)$") {
        throw "Invalid SHA256SUMS.txt line: $line"
    }
    $manifest[$Matches[2]] = $Matches[1]
}
if ($manifest.Count -ne $expectedArchives.Count) {
    throw "Expected $($expectedArchives.Count) checksum entries, found $($manifest.Count)."
}

$assetRows = foreach ($archiveName in $expectedArchives) {
    $archivePath = Join-Path $assetRootPath $archiveName
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw "Expected release asset is missing: $archivePath"
    }

    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not $manifest.ContainsKey($archiveName) -or $manifest[$archiveName] -ne $hash) {
        throw "Checksum mismatch for $archiveName."
    }

    [pscustomobject]@{
        Name = $archiveName
        Path = $archivePath
        Size = (Get-Item -LiteralPath $archivePath).Length
        Digest = "sha256:$hash"
    }
}

$checksumHash = (Get-FileHash -LiteralPath $checksumPath -Algorithm SHA256).Hash.ToLowerInvariant()
$assetRows += [pscustomobject]@{
    Name = "SHA256SUMS.txt"
    Path = $checksumPath
    Size = (Get-Item -LiteralPath $checksumPath).Length
    Digest = "sha256:$checksumHash"
}

$assetRows | Select-Object Name, Size, Digest | Format-Table -AutoSize
if ($ValidateOnly) {
    return
}

if ($null -eq (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) is required."
}
& gh auth status
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI is not authenticated."
}

$existingJson = & gh release view $tag --repo $Repository --json isDraft,assets,url 2>$null
$releaseExists = $LASTEXITCODE -eq 0
if ($releaseExists) {
    $existingRelease = $existingJson | ConvertFrom-Json
    if (-not $existingRelease.isDraft) {
        throw "Public release $tag already exists and will not be overwritten."
    }
    if (-not $ResumeDraft) {
        throw "Draft release $tag already exists. Inspect it, then use -ResumeDraft to continue."
    }
} else {
    & gh release create $tag `
        --repo $Repository `
        --target $Target `
        --title "轻截 $tag" `
        --notes-file $notesPathValue `
        --draft
    if ($LASTEXITCODE -ne 0) {
        throw "Creating draft release $tag failed."
    }
}

$uploadArguments = @("release", "upload", $tag, "--repo", $Repository)
$uploadArguments += @($assetRows | Select-Object -ExpandProperty Path)
if ($ResumeDraft) {
    $uploadArguments += "--clobber"
}
& gh $uploadArguments
if ($LASTEXITCODE -ne 0) {
    throw "Uploading release assets failed. The release remains a draft."
}

$uploadedJson = & gh release view $tag `
    --repo $Repository `
    --json isDraft,assets,targetCommitish,url
if ($LASTEXITCODE -ne 0) {
    throw "Reading the uploaded draft release failed."
}
$uploadedRelease = $uploadedJson | ConvertFrom-Json
if (-not $uploadedRelease.isDraft) {
    throw "Release $tag unexpectedly became public before verification."
}
if ($uploadedRelease.assets.Count -ne $assetRows.Count) {
    throw "Expected $($assetRows.Count) online assets, found $($uploadedRelease.assets.Count)."
}

foreach ($localAsset in $assetRows) {
    $onlineAsset = $uploadedRelease.assets |
        Where-Object { $_.name -eq $localAsset.Name } |
        Select-Object -First 1
    if ($null -eq $onlineAsset) {
        throw "Uploaded release is missing $($localAsset.Name)."
    }
    if ([long]$onlineAsset.size -ne [long]$localAsset.Size) {
        throw "Uploaded size mismatch for $($localAsset.Name)."
    }
    if ($onlineAsset.digest -ne $localAsset.Digest) {
        throw "Uploaded digest mismatch for $($localAsset.Name)."
    }
}

& gh release edit $tag --repo $Repository --draft=false
if ($LASTEXITCODE -ne 0) {
    throw "Publishing verified release $tag failed; it remains a draft."
}

$targetSha = (& gh api "repos/$Repository/commits/$Target" --jq ".sha").Trim()
$tagSha = (& gh api "repos/$Repository/git/ref/tags/$tag" --jq ".object.sha").Trim()
if ($LASTEXITCODE -ne 0 -or $tagSha -ne $targetSha) {
    throw "Published tag $tag does not point to $Target."
}

& gh release view $tag --repo $Repository --json tagName,name,publishedAt,url,isDraft,isPrerelease

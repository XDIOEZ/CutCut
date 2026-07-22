[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HelperDirectory,
    [string]$OutputRoot = "artifacts\screen-recording-smoke",
    [ValidateSet(30, 60)]
    [int]$FramesPerSecond = 30,
    [ValidateSet(2000000, 4000000, 8000000, 12000000, 20000000)]
    [int]$VideoBitrate = 2000000,
    [switch]$CaptureSystemAudio,
    [switch]$CaptureMicrophone,
    [switch]$ShowMouseClickIndicator
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$helperDirectoryPath = [System.IO.Path]::GetFullPath($HelperDirectory)
$helperPath = Join-Path $helperDirectoryPath "ScreenshotTool.ScreenRecording.Recorder.exe"
if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) {
    throw "Screen recording helper was not found: $helperPath"
}

$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}
New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null
$outputPath = Join-Path $outputRootPath ("helper-smoke-{0}.mp4" -f [Guid]::NewGuid().ToString("N"))

$display = [System.Windows.Forms.Screen]::PrimaryScreen
if ($null -eq $display -or $display.Bounds.Width -lt 320 -or $display.Bounds.Height -lt 240) {
    throw "A primary display of at least 320 x 240 is required for the recorder smoke test."
}

$pipeName = "LightShotCN.ScreenRecording.Smoke.{0}" -f [Guid]::NewGuid().ToString("N")
$pipe = [System.IO.Pipes.NamedPipeServerStream]::new(
    $pipeName,
    [System.IO.Pipes.PipeDirection]::InOut,
    1,
    [System.IO.Pipes.PipeTransmissionMode]::Byte,
    [System.IO.Pipes.PipeOptions]::Asynchronous)
$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $helperPath
$startInfo.WorkingDirectory = $helperDirectoryPath
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$arguments = @(
    "--pipe", $pipeName,
    "--output", $outputPath,
    "--display", $display.DeviceName,
    "--x", "0",
    "--y", "0",
    "--width", "320",
    "--height", "240",
    "--fps", $FramesPerSecond.ToString(),
    "--bitrate", $VideoBitrate.ToString(),
    "--system-audio", $CaptureSystemAudio.IsPresent.ToString(),
    "--microphone", $CaptureMicrophone.IsPresent.ToString(),
    "--mouse-click-indicator", $ShowMouseClickIndicator.IsPresent.ToString()
)
$startInfo.Arguments = ($arguments | ForEach-Object {
    '"' + $_.Replace('"', '\"') + '"'
}) -join " "

$process = [System.Diagnostics.Process]::Start($startInfo)
if ($null -eq $process) {
    throw "Unable to start the screen recording helper."
}
$reader = $null
$writer = $null
try {
    $pipe.WaitForConnection()
    $reader = [System.IO.StreamReader]::new(
        $pipe,
        [System.Text.Encoding]::UTF8,
        $false,
        1024,
        $true)
    $writer = [System.IO.StreamWriter]::new(
        $pipe,
        [System.Text.UTF8Encoding]::new($false),
        1024,
        $true)
    $writer.AutoFlush = $true

    $started = $reader.ReadLine()
    if ($started -ne "started") {
        throw "Recorder did not start: $started"
    }

    Start-Sleep -Seconds 2
    $writer.WriteLine("stop")
    $result = $reader.ReadLine()
    if ($null -eq $result -or -not $result.StartsWith("completed`t")) {
        throw "Recorder did not complete: $result"
    }
    if (-not $process.WaitForExit(10000)) {
        throw "Recorder did not exit after completing the smoke test."
    }

    $file = Get-Item -LiteralPath $outputPath
    if ($file.Length -lt 1024) {
        throw "Recorder produced an unexpectedly small MP4 file: $($file.Length) bytes."
    }
    [pscustomobject]@{
        ExitCode = $process.ExitCode
        Bytes = $file.Length
        Path = $file.FullName
    } | Format-List
} finally {
    if ($null -ne $writer) {
        $writer.Dispose()
    }
    if ($null -ne $reader) {
        $reader.Dispose()
    }
    $pipe.Dispose()
    if ($null -ne $process -and -not $process.HasExited) {
        $process.Kill($true)
    }
    if ($null -ne $process) {
        $process.Dispose()
    }
}

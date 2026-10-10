[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HelperDirectory,
    [string]$OutputRoot = "artifacts\screen-recording-smoke",
    [string]$DisplayDeviceName,
    [ValidateRange(0, 32768)]
    [int]$X = 0,
    [ValidateRange(0, 32768)]
    [int]$Y = 0,
    [switch]$PauseAndResume,
    [ValidateSet(1, 5, 10, 15, 24, 30, 60)]
    [int]$FramesPerSecond = 30,
    [ValidateSet(32000, 64000, 128000, 256000, 512000, 1000000, 2000000, 4000000, 8000000, 12000000, 20000000)]
    [int]$VideoBitrate = 2000000,
    [ValidateRange(2, 8192)]
    [int]$OutputWidth = 320,
    [ValidateRange(2, 8192)]
    [int]$OutputHeight = 240,
    [ValidateSet(96000, 128000, 160000, 192000)]
    [int]$AudioBitrate = 128000,
    [switch]$MonoAudio,
    [ValidateRange(2, 30)]
    [int]$RecordSeconds = 2,
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

$display = if ([string]::IsNullOrWhiteSpace($DisplayDeviceName)) {
    [System.Windows.Forms.Screen]::PrimaryScreen
} else {
    [System.Windows.Forms.Screen]::AllScreens | Where-Object DeviceName -eq $DisplayDeviceName | Select-Object -First 1
}
if ($null -eq $display -or $X + 320 -gt $display.Bounds.Width -or $Y + 240 -gt $display.Bounds.Height) {
    throw "The selected display must contain the requested 320 x 240 region."
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
    "--x", $X.ToString(),
    "--y", $Y.ToString(),
    "--width", "320",
    "--height", "240",
    "--output-width", $OutputWidth.ToString(),
    "--output-height", $OutputHeight.ToString(),
    "--fps", $FramesPerSecond.ToString(),
    "--bitrate", $VideoBitrate.ToString(),
    "--audio-bitrate", $AudioBitrate.ToString(),
    "--mono-audio", $MonoAudio.IsPresent.ToString(),
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
    if (-not $pipe.WaitForConnectionAsync().Wait(15000)) {
        throw "Recorder pipe connection timed out."
    }
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

    $startedTask = $reader.ReadLineAsync()
    if (-not $startedTask.Wait(15000)) {
        throw "Recorder startup timed out."
    }
    $started = $startedTask.Result
    if ($started -ne "started") {
        throw "Recorder did not start: $started"
    }

    Start-Sleep -Seconds $RecordSeconds
    if ($PauseAndResume) {
        $writer.WriteLine("pause")
        Start-Sleep -Milliseconds 500
        $writer.WriteLine("resume")
        Start-Sleep -Seconds 1
    }
    $writer.WriteLine("stop")
    $resultTask = $reader.ReadLineAsync()
    if (-not $resultTask.Wait(30000)) {
        throw "Recorder completion timed out."
    }
    $result = $resultTask.Result
    if ($null -eq $result -or -not $result.StartsWith("completed`t")) {
        if ($null -ne $result -and $result.StartsWith("failed`t")) {
            $result = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($result.Substring(7)))
        }
        throw "Recorder did not complete: $result"
    }
    if (-not $process.WaitForExit(10000)) {
        throw "Recorder did not exit after completing the smoke test."
    }
    if ($process.ExitCode -ne 0) {
        throw "Recorder exited with code $($process.ExitCode)."
    }

    $file = Get-Item -LiteralPath $outputPath
    if ($file.Length -lt 1024) {
        throw "Recorder produced an unexpectedly small MP4 file: $($file.Length) bytes."
    }
    [pscustomobject]@{
        Display = $display.DeviceName
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
        $process.Kill()
    }
    if ($null -ne $process) {
        $process.Dispose()
    }
}

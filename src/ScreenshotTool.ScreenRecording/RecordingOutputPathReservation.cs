namespace ScreenshotTool.ScreenRecording;

// A delete-on-close sidecar reserves a name before the helper creates its MP4.
internal sealed class RecordingOutputPathReservation : IDisposable
{
    private readonly FileStream _reservation;

    private RecordingOutputPathReservation(string path, FileStream reservation)
    {
        Path = path;
        _reservation = reservation;
    }

    public string Path { get; }

    public static RecordingOutputPathReservation Create(string outputFolder, DateTime timestamp)
    {
        var baseName = $"{timestamp:yyyyMMdd_HHmmss}_录屏";
        for (var suffix = 1; ; suffix++)
        {
            var name = suffix == 1 ? baseName : $"{baseName}_{suffix}";
            var path = System.IO.Path.Combine(outputFolder, name + ".mp4");
            if (File.Exists(path))
            {
                continue;
            }
            var reservationPath = path + ".recording";
            FileStream reservation;
            try
            {
                reservation = new FileStream(
                    reservationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.DeleteOnClose);
            }
            catch (IOException) when (File.Exists(reservationPath))
            {
                continue;
            }
            if (File.Exists(path))
            {
                reservation.Dispose();
                continue;
            }
            return new RecordingOutputPathReservation(path, reservation);
        }
    }

    public void Dispose() => _reservation.Dispose();
}

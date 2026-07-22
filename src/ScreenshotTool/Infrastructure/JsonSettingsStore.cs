using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;

namespace ScreenshotTool.Infrastructure;

internal sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly string _settingsPath;
    private readonly string? _legacySettingsPath;

    public JsonSettingsStore()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LightShotCN"),
            "local")
    {
    }

    internal JsonSettingsStore(string settingsRoot, string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        ProfileId = profileId.Trim();
        _settingsPath = Path.Combine(settingsRoot, "Profiles", GetProfileFileName(ProfileId));
        _legacySettingsPath = string.Equals(ProfileId, "local", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(settingsRoot, "settings.json")
            : null;
    }

    public string ProfileId { get; }

    internal string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        var current = TryLoadCurrentDocument();
        if (current is not null)
        {
            return current;
        }

        var migrated = TryMigrateLegacySettings();
        if (migrated is not null)
        {
            return migrated;
        }

        return CreateDefaults();
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = Normalize(settings);
        var document = new UserSettingsDocument
        {
            SchemaVersion = UserSettingsDocument.CurrentSchemaVersion,
            ProfileId = ProfileId,
            Settings = normalized
        };
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(document, SerializerOptions),
            Encoding.UTF8);
        File.Move(temporaryPath, _settingsPath, true);
    }

    private AppSettings? TryLoadCurrentDocument()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
            var document = JsonSerializer.Deserialize<UserSettingsDocument>(json, SerializerOptions);
            if (document?.Settings is null ||
                !string.Equals(document.ProfileId, ProfileId, StringComparison.Ordinal))
            {
                return null;
            }

            return Normalize(document.Settings);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private AppSettings? TryMigrateLegacySettings()
    {
        if (_legacySettingsPath is null || !File.Exists(_legacySettingsPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_legacySettingsPath, Encoding.UTF8);
            var legacy = JsonSerializer.Deserialize<LegacyAppSettings>(json, SerializerOptions);
            if (legacy is null)
            {
                return null;
            }

            var settings = Normalize(legacy.ToCurrentSettings());
            Save(settings);
            return settings;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static AppSettings CreateDefaults() => Normalize(new AppSettings());

    private static AppSettings Normalize(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.OutputFolder))
        {
            settings.OutputFolder = new AppSettings().OutputFolder;
        }

        settings.OutputFolder = Environment.ExpandEnvironmentVariables(settings.OutputFolder.Trim());
        if (!settings.GetHotkey().IsValid)
        {
            settings.SetHotkey(HotkeyDefinition.Default);
        }

        settings.LastLaunchedVersion = string.IsNullOrWhiteSpace(settings.LastLaunchedVersion)
            ? null
            : settings.LastLaunchedVersion.Trim();

        settings.Preferences ??= new UserPreferences();
        if (!Enum.IsDefined(settings.Preferences.StickerSelectionMoveMode))
        {
            settings.Preferences.StickerSelectionMoveMode = StickerSelectionMoveMode.FollowSelection;
        }

        var toolWidthRange = settings.Preferences.GetToolWidthRange();
        settings.Preferences.MinimumToolWidth = toolWidthRange.Minimum;
        settings.Preferences.MaximumToolWidth = toolWidthRange.Maximum;
        settings.Preferences.RememberToolWidth(settings.Preferences.LastToolWidth);
        settings.Preferences.AnnotationRotationStepDegrees = AnnotationRotationStep.Normalize(
            settings.Preferences.AnnotationRotationStepDegrees);
        if (!Enum.IsDefined(settings.Preferences.DrawingCursorShape))
        {
            settings.Preferences.DrawingCursorShape = DrawingCursorShape.Circle;
        }
        settings.Preferences.AnnotationSnapThresholdPixels =
            AnnotationLayoutOptions.NormalizeSnapThreshold(
                settings.Preferences.AnnotationSnapThresholdPixels);
        settings.Preferences.CtrlDragStepPixels = AnnotationLayoutOptions.NormalizeCtrlDragStep(
            settings.Preferences.CtrlDragStepPixels);
        if (!Enum.IsDefined(settings.Preferences.RecordingRegionIndicatorStyle))
        {
            settings.Preferences.RecordingRegionIndicatorStyle =
                RecordingRegionIndicatorStyle.Dashed;
        }
        settings.Preferences.ScreenRecordingFramesPerSecond =
            NormalizeNearest(
                settings.Preferences.ScreenRecordingFramesPerSecond,
                [30, 60]);
        settings.Preferences.ScreenRecordingVideoBitrate =
            NormalizeNearest(
                settings.Preferences.ScreenRecordingVideoBitrate,
                [2_000_000, 4_000_000, 8_000_000, 12_000_000, 20_000_000]);
        settings.Preferences.ModuleBooleanPreferences = new Dictionary<string, bool>(
            settings.Preferences.ModuleBooleanPreferences ?? [],
            StringComparer.Ordinal);
        settings.Preferences.ModuleIntegerPreferences = new Dictionary<string, int>(
            settings.Preferences.ModuleIntegerPreferences ?? [],
            StringComparer.Ordinal);
        settings.Preferences.ModuleStringPreferences = new Dictionary<string, string>(
            settings.Preferences.ModuleStringPreferences ?? [],
            StringComparer.Ordinal);
        settings.Preferences.ModuleBooleanPreferences.TryAdd(
            "screenshot-tool.long-capture.safety-checks",
            settings.Preferences.LongCaptureSafetyChecksEnabled);
        settings.Preferences.ModuleBooleanPreferences.TryAdd(
            "screenshot-tool.screen-recording.capture-system-audio",
            settings.Preferences.ScreenRecordingCaptureSystemAudio);
        settings.Preferences.ModuleBooleanPreferences.TryAdd(
            "screenshot-tool.screen-recording.capture-microphone",
            settings.Preferences.ScreenRecordingCaptureMicrophone);
        settings.Preferences.ModuleBooleanPreferences.TryAdd(
            "screenshot-tool.screen-recording.show-mouse-click-indicator",
            settings.Preferences.ScreenRecordingShowMouseClickIndicator);
        settings.Preferences.ModuleIntegerPreferences.TryAdd(
            "screenshot-tool.screen-recording.frames-per-second",
            settings.Preferences.ScreenRecordingFramesPerSecond);
        settings.Preferences.ModuleIntegerPreferences.TryAdd(
            "screenshot-tool.screen-recording.video-bitrate",
            settings.Preferences.ScreenRecordingVideoBitrate);
        settings.Preferences.ModuleIntegerPreferences.TryAdd(
            "screenshot-tool.screen-recording.region-indicator-style",
            (int)settings.Preferences.RecordingRegionIndicatorStyle);
        if (!Enum.IsDefined(settings.Preferences.ScreenshotFileNameMode))
        {
            settings.Preferences.ScreenshotFileNameMode = ScreenshotFileNameMode.DateTime;
        }
        settings.Preferences.DrawingToolCoefficients ??= new DrawingToolCoefficients();
        settings.Preferences.DrawingToolCoefficients.Normalize();
        return settings;
    }

    private static int NormalizeNearest(int value, IReadOnlyList<int> supportedValues)
    {
        var nearest = supportedValues[0];
        var nearestDistance = long.MaxValue;
        foreach (var supportedValue in supportedValues)
        {
            var distance = Math.Abs((long)supportedValue - value);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearest = supportedValue;
            nearestDistance = distance;
        }
        return nearest;
    }

    private static string GetProfileFileName(string profileId)
    {
        if (string.Equals(profileId, "local", StringComparison.OrdinalIgnoreCase))
        {
            return "local.json";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(profileId));
        return $"profile-{Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant()}.json";
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed class LegacyAppSettings
    {
        public string? OutputFolder { get; set; }

        public HotkeyModifiers HotkeyModifiers { get; set; } = HotkeyDefinition.Default.Modifiers;

        public int HotkeyVirtualKey { get; set; } = HotkeyDefinition.Default.VirtualKey;

        public bool StartMinimized { get; set; }

        public StickerSelectionMoveMode StickerSelectionMoveMode { get; set; } =
            StickerSelectionMoveMode.FollowSelection;

        public int MinimumToolWidth { get; set; } = 2;

        public int MaximumToolWidth { get; set; } = 8;

        public AppSettings ToCurrentSettings() => new()
        {
            OutputFolder = OutputFolder ?? string.Empty,
            HotkeyModifiers = HotkeyModifiers,
            HotkeyVirtualKey = HotkeyVirtualKey,
            StartMinimized = StartMinimized,
            Preferences = new UserPreferences
            {
                StickerSelectionMoveMode = StickerSelectionMoveMode,
                MinimumToolWidth = MinimumToolWidth,
                MaximumToolWidth = MaximumToolWidth
            }
        };
    }
}

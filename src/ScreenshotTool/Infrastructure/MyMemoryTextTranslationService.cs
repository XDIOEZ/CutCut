using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ScreenshotTool.Abstractions;

namespace ScreenshotTool.Infrastructure;

internal sealed class MyMemoryTextTranslationService :
    ITextTranslationService,
    IDisposable
{
    private const int MaximumSegmentBytes = 480;
    private static readonly Uri TranslationEndpoint = new(
        "https://api.mymemory.translated.net/get");
    private static readonly TimeSpan TranslationTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly Regex WordSeparator = new(
        @"[^\p{L}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public MyMemoryTextTranslationService()
        : this(CreateHttpClient(), ownsHttpClient: true)
    {
    }

    internal MyMemoryTextTranslationService(HttpClient httpClient)
        : this(httpClient, ownsHttpClient: false)
    {
    }

    private MyMemoryTextTranslationService(
        HttpClient httpClient,
        bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    public async Task<string> TranslateToSimplifiedChineseAsync(
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TranslationTimeout);

        try
        {
            var translatedText = new StringBuilder(text.Length);
            foreach (var segment in SplitByUtf8ByteCount(text, MaximumSegmentBytes))
            {
                timeoutCancellation.Token.ThrowIfCancellationRequested();
                await AppendTranslatedSegmentAsync(
                    translatedText,
                    segment,
                    timeoutCancellation.Token);
            }

            return translatedText.ToString();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("联网翻译超时，请检查网络后重试。");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "无法连接在线翻译服务，请检查网络后重试。",
                exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "在线翻译服务返回了无法读取的结果，请稍后重试。",
                exception);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    internal static string DetectSourceLanguage(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var scripts = CountScripts(text);
        if (scripts.HiraganaOrKatakana > 0)
        {
            return "ja";
        }
        if (scripts.Hangul > 0)
        {
            return "ko";
        }
        if (scripts.Arabic > 0)
        {
            return "ar";
        }
        if (scripts.Cyrillic > 0)
        {
            return "ru";
        }
        if (scripts.Greek > 0)
        {
            return "el";
        }
        if (scripts.Hebrew > 0)
        {
            return "he";
        }
        if (scripts.Thai > 0)
        {
            return "th";
        }
        if (scripts.Devanagari > 0)
        {
            return "hi";
        }
        if (scripts.Bengali > 0)
        {
            return "bn";
        }
        if (scripts.Tamil > 0)
        {
            return "ta";
        }
        if (scripts.Telugu > 0)
        {
            return "te";
        }
        if (scripts.Armenian > 0)
        {
            return "hy";
        }
        if (scripts.Georgian > 0)
        {
            return "ka";
        }
        if (scripts.Latin > 0)
        {
            return DetectLatinLanguage(text);
        }
        if (scripts.Han > 0)
        {
            return "zh-CN";
        }

        return "und";
    }

    internal static IReadOnlyList<string> SplitByUtf8ByteCount(
        string text,
        int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 4);

        if (text.Length == 0)
        {
            return [string.Empty];
        }

        var segments = new List<string>();
        var segmentStart = 0;
        while (segmentStart < text.Length)
        {
            var index = segmentStart;
            var byteCount = 0;
            var preferredBreak = -1;
            while (index < text.Length)
            {
                var status = Rune.DecodeFromUtf16(
                    text.AsSpan(index),
                    out var rune,
                    out var consumed);
                if (status != OperationStatus.Done)
                {
                    rune = Rune.ReplacementChar;
                    consumed = 1;
                }

                if (byteCount + rune.Utf8SequenceLength > maximumBytes)
                {
                    break;
                }

                index += consumed;
                byteCount += rune.Utf8SequenceLength;
                if (IsPreferredBreak(rune))
                {
                    preferredBreak = index;
                }
            }

            if (index == segmentStart)
            {
                index++;
            }
            else if (index < text.Length &&
                     preferredBreak > segmentStart &&
                     preferredBreak - segmentStart >= (index - segmentStart) / 2)
            {
                index = preferredBreak;
            }

            segments.Add(text[segmentStart..index]);
            segmentStart = index;
        }

        return segments;
    }

    private async Task AppendTranslatedSegmentAsync(
        StringBuilder output,
        string segment,
        CancellationToken cancellationToken)
    {
        var contentStart = 0;
        while (contentStart < segment.Length &&
               char.IsWhiteSpace(segment[contentStart]))
        {
            contentStart++;
        }

        var contentEnd = segment.Length;
        while (contentEnd > contentStart &&
               char.IsWhiteSpace(segment[contentEnd - 1]))
        {
            contentEnd--;
        }

        output.Append(segment.AsSpan(0, contentStart));
        if (contentStart == contentEnd)
        {
            return;
        }

        var content = segment[contentStart..contentEnd];
        var sourceLanguage = DetectSourceLanguage(content);
        if (sourceLanguage is "zh-CN" or "und")
        {
            output.Append(content);
        }
        else
        {
            output.Append(await TranslateSegmentAsync(
                content,
                sourceLanguage,
                cancellationToken));
        }
        output.Append(segment.AsSpan(contentEnd));
    }

    private async Task<string> TranslateSegmentAsync(
        string text,
        string sourceLanguage,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            $"{TranslationEndpoint}?q={Uri.EscapeDataString(text)}" +
            $"&langpair={Uri.EscapeDataString($"{sourceLanguage}|zh-CN")}&mt=1");
        using var response = await _httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonSerializer.DeserializeAsync<MyMemoryResponse>(
            content,
            JsonOptions,
            cancellationToken);

        if (document is null ||
            !IsSuccessfulStatus(document.ResponseStatus) ||
            string.IsNullOrWhiteSpace(document.ResponseData?.TranslatedText))
        {
            var details = ReadResponseDetails(document?.ResponseDetails);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(details)
                    ? "在线翻译服务暂时不可用，请稍后重试。"
                    : $"在线翻译失败：{details}");
        }

        return WebUtility.HtmlDecode(document.ResponseData.TranslatedText);
    }

    private static bool IsSuccessfulStatus(JsonElement status) =>
        status.ValueKind switch
        {
            JsonValueKind.Number => status.TryGetInt32(out var value) && value == 200,
            JsonValueKind.String => string.Equals(
                status.GetString(),
                "200",
                StringComparison.Ordinal),
            _ => false
        };

    private static string? ReadResponseDetails(JsonElement? details)
    {
        if (details is not { } value ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static bool IsPreferredBreak(Rune rune) =>
        Rune.IsWhiteSpace(rune) ||
        rune.Value is '.' or ',' or ';' or ':' or '!' or '?' or
            '。' or '，' or '；' or '：' or '！' or '？';

    private static ScriptCounts CountScripts(string text)
    {
        var result = new ScriptCounts();
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            if (IsInRange(value, 0x3040, 0x30FF) ||
                IsInRange(value, 0x31F0, 0x31FF) ||
                IsInRange(value, 0xFF66, 0xFF9D))
            {
                result.HiraganaOrKatakana++;
            }
            else if (IsInRange(value, 0x1100, 0x11FF) ||
                     IsInRange(value, 0x3130, 0x318F) ||
                     IsInRange(value, 0xA960, 0xA97F) ||
                     IsInRange(value, 0xAC00, 0xD7FF))
            {
                result.Hangul++;
            }
            else if (IsInRange(value, 0x0600, 0x06FF) ||
                     IsInRange(value, 0x0750, 0x077F) ||
                     IsInRange(value, 0x08A0, 0x08FF) ||
                     IsInRange(value, 0xFB50, 0xFDFF) ||
                     IsInRange(value, 0xFE70, 0xFEFF))
            {
                result.Arabic++;
            }
            else if (IsInRange(value, 0x0400, 0x052F) ||
                     IsInRange(value, 0x2DE0, 0x2DFF) ||
                     IsInRange(value, 0xA640, 0xA69F))
            {
                result.Cyrillic++;
            }
            else if (IsInRange(value, 0x0370, 0x03FF) ||
                     IsInRange(value, 0x1F00, 0x1FFF))
            {
                result.Greek++;
            }
            else if (IsInRange(value, 0x0590, 0x05FF) ||
                     IsInRange(value, 0xFB1D, 0xFB4F))
            {
                result.Hebrew++;
            }
            else if (IsInRange(value, 0x0E00, 0x0E7F))
            {
                result.Thai++;
            }
            else if (IsInRange(value, 0x0900, 0x097F))
            {
                result.Devanagari++;
            }
            else if (IsInRange(value, 0x0980, 0x09FF))
            {
                result.Bengali++;
            }
            else if (IsInRange(value, 0x0B80, 0x0BFF))
            {
                result.Tamil++;
            }
            else if (IsInRange(value, 0x0C00, 0x0C7F))
            {
                result.Telugu++;
            }
            else if (IsInRange(value, 0x0530, 0x058F))
            {
                result.Armenian++;
            }
            else if (IsInRange(value, 0x10A0, 0x10FF))
            {
                result.Georgian++;
            }
            else if (IsInRange(value, 0x3400, 0x4DBF) ||
                     IsInRange(value, 0x4E00, 0x9FFF) ||
                     IsInRange(value, 0xF900, 0xFAFF) ||
                     IsInRange(value, 0x20000, 0x2FA1F))
            {
                result.Han++;
            }
            else if (IsInRange(value, 0x0041, 0x007A) ||
                     IsInRange(value, 0x00C0, 0x024F) ||
                     IsInRange(value, 0x1E00, 0x1EFF))
            {
                result.Latin++;
            }
        }

        return result;
    }

    private static string DetectLatinLanguage(string text)
    {
        var normalized = text.ToLowerInvariant();
        var words = WordSeparator.Split(normalized)
            .Where(word => word.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var scores = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["de"] = ScoreLanguage(
                normalized,
                words,
                "äöüß",
                ["der", "die", "das", "und", "ist", "nicht", "mit", "guten", "morgen"]),
            ["es"] = ScoreLanguage(
                normalized,
                words,
                "ñ¿¡",
                ["el", "los", "las", "una", "que", "para", "con", "hola", "mundo"]),
            ["fr"] = ScoreLanguage(
                normalized,
                words,
                "àâçèêëîïôùûüÿœ",
                ["le", "les", "des", "une", "est", "que", "pour", "avec", "bonjour", "monde"]),
            ["it"] = ScoreLanguage(
                normalized,
                words,
                "",
                ["il", "lo", "gli", "una", "che", "non", "per", "con", "ciao", "mondo"]),
            ["nl"] = ScoreLanguage(
                normalized,
                words,
                "",
                ["een", "het", "van", "niet", "voor", "met", "hallo", "wereld"]),
            ["pl"] = ScoreLanguage(
                normalized,
                words,
                "ąćęłńóśźż",
                ["jest", "nie", "oraz", "dla", "przez", "dzień", "dobry"]),
            ["pt"] = ScoreLanguage(
                normalized,
                words,
                "ãõ",
                ["uma", "não", "que", "para", "com", "por", "olá", "mundo"]),
            ["tr"] = ScoreLanguage(
                normalized,
                words,
                "ğış",
                ["bir", "ve", "için", "ile", "değil", "merhaba", "dünya"]),
            ["vi"] = ScoreLanguage(
                normalized,
                words,
                "ăđĩũơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ",
                ["và", "của", "không", "cho", "với", "xin", "chào"])
        };

        var best = scores.MaxBy(pair => pair.Value);
        return best.Value >= 2 ? best.Key : "en";
    }

    private static int ScoreLanguage(
        string text,
        IReadOnlySet<string> words,
        string distinctiveCharacters,
        IReadOnlyList<string> commonWords)
    {
        var score = distinctiveCharacters.Count(text.Contains);
        score += commonWords.Count(words.Contains);
        return score;
    }

    private static bool IsInRange(int value, int minimum, int maximum) =>
        value >= minimum && value <= maximum;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
            "LightShotCN",
            "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private sealed class MyMemoryResponse
    {
        [JsonPropertyName("responseData")]
        public MyMemoryResponseData? ResponseData { get; init; }

        [JsonPropertyName("responseStatus")]
        public JsonElement ResponseStatus { get; init; }

        [JsonPropertyName("responseDetails")]
        public JsonElement? ResponseDetails { get; init; }
    }

    private sealed class MyMemoryResponseData
    {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; init; }
    }

    private sealed class ScriptCounts
    {
        public int HiraganaOrKatakana { get; set; }
        public int Hangul { get; set; }
        public int Arabic { get; set; }
        public int Cyrillic { get; set; }
        public int Greek { get; set; }
        public int Hebrew { get; set; }
        public int Thai { get; set; }
        public int Devanagari { get; set; }
        public int Bengali { get; set; }
        public int Tamil { get; set; }
        public int Telugu { get; set; }
        public int Armenian { get; set; }
        public int Georgian { get; set; }
        public int Han { get; set; }
        public int Latin { get; set; }
    }
}

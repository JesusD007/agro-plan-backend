using System.Globalization;
using System.Text.Json;
using System.Buffers.Binary;
using System.Text;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Voice;

namespace AgroPlan.Application.Abstractions;

/// <summary>Only the media bytes and explicitly selected language cross the provider boundary.</summary>
public sealed record SpeechTranscriptionRequest(byte[] Audio, SpeechLanguage Language);

/// <summary>The provider returns untrusted JSON so the application can validate it before persistence.</summary>
public interface ISpeechTranscriptionProvider
{
    Task<string> TranscribeAsync(SpeechTranscriptionRequest request, CancellationToken cancellationToken);
}

public enum SpeechTranscriptionFailureKind
{
    TemporarilyUnavailable = 1,
    NotConfigured = 2
}

public sealed class SpeechTranscriptionProviderException(SpeechTranscriptionFailureKind failureKind)
    : Exception("Speech transcription provider failed.")
{
    public SpeechTranscriptionFailureKind FailureKind { get; } = failureKind;
}

/// <summary>Accepts only mono 16 kHz, signed 16-bit PCM WAV and derives duration from its bytes.</summary>
public static class PcmWaveAudioInspector
{
    private static readonly byte[] Riff = Encoding.ASCII.GetBytes("RIFF");
    private static readonly byte[] Wave = Encoding.ASCII.GetBytes("WAVE");
    private static readonly byte[] Format = Encoding.ASCII.GetBytes("fmt ");
    private static readonly byte[] Data = Encoding.ASCII.GetBytes("data");

    public static bool TryGetDurationSeconds(ReadOnlySpan<byte> bytes, out int durationSeconds)
    {
        durationSeconds = 0;
        if (bytes.Length is < 44 or > 10 * 1024 * 1024 ||
            !bytes[..4].SequenceEqual(Riff) || !bytes.Slice(8, 4).SequenceEqual(Wave) ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)) != bytes.Length - 8)
            return false;

        var hasFormat = false;
        var hasData = false;
        uint dataLength = 0;
        ushort blockAlign = 0;
        var offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            var chunkId = bytes.Slice(offset, 4);
            var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            offset += 8;
            if (chunkLength > bytes.Length - offset) return false;
            var chunk = bytes.Slice(offset, checked((int)chunkLength));
            if (chunkId.SequenceEqual(Format))
            {
                if (hasFormat || chunk.Length < 16 || BinaryPrimitives.ReadUInt16LittleEndian(chunk[..2]) != 1 ||
                    BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(2, 2)) != 1 ||
                    BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(4, 4)) != 16000 ||
                    BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(8, 4)) != 32000)
                    return false;
                blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(12, 2));
                if (blockAlign != 2 || BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(14, 2)) != 16) return false;
                hasFormat = true;
            }
            else if (chunkId.SequenceEqual(Data))
            {
                if (hasData) return false;
                dataLength = chunkLength;
                hasData = true;
            }
            offset += checked((int)chunkLength);
            if ((chunkLength & 1) == 1) offset++;
        }
        if (offset != bytes.Length || !hasFormat || !hasData || blockAlign == 0 || dataLength == 0 || dataLength % blockAlign != 0)
            return false;
        var bytesPerSecond = 16000 * blockAlign;
        durationSeconds = checked((int)Math.Ceiling(dataLength / (double)bytesPerSecond));
        return durationSeconds is > 0 and <= 300;
    }
}

public sealed record SpeechTranscriptionResult(
    SpeechLanguage Language,
    LogbookActivityType? ActivityType,
    DateTimeOffset? OccurredAt,
    string? Details,
    IReadOnlyList<string> UncertainFields,
    double Confidence);

public static class SpeechTranscriptionResultValidator
{
    private static readonly HashSet<string> AllowedProperties =
    ["schemaVersion", "language", "activityType", "occurredAt", "details", "uncertainFields", "confidence"];
    private static readonly HashSet<string> AllowedUncertainFields = ["activityType", "occurredAt", "details"];

    public static bool TryValidate(string? payload, SpeechLanguage expectedLanguage, out SpeechTranscriptionResult? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(payload) || !Enum.IsDefined(expectedLanguage)) return false;

        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Any(property => !AllowedProperties.Contains(property.Name)))
                return false;
            if (!root.TryGetProperty("schemaVersion", out var schema) || !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1)
                return false;
            if (!root.TryGetProperty("language", out var languageElement) || languageElement.ValueKind != JsonValueKind.String ||
                !Enum.TryParse<SpeechLanguage>(languageElement.GetString(), ignoreCase: false, out var language) ||
                !Enum.IsDefined(language) || language != expectedLanguage)
                return false;

            if (!root.TryGetProperty("activityType", out var activityElement) ||
                !TryActivityType(activityElement, out var activityType)) return false;
            if (!root.TryGetProperty("occurredAt", out var occurredElement) ||
                !TryDateTimeOffset(occurredElement, out var occurredAt)) return false;
            if (!root.TryGetProperty("details", out var detailsElement) || !TryDetails(detailsElement, out var details)) return false;
            if (!root.TryGetProperty("uncertainFields", out var uncertainElement) || uncertainElement.ValueKind != JsonValueKind.Array)
                return false;
            var uncertainFields = new List<string>();
            foreach (var item in uncertainElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) return false;
                var field = item.GetString();
                if (field is null || !AllowedUncertainFields.Contains(field) || uncertainFields.Contains(field, StringComparer.Ordinal))
                    return false;
                uncertainFields.Add(field);
            }
            if (!root.TryGetProperty("confidence", out var confidenceElement) ||
                confidenceElement.ValueKind != JsonValueKind.Number || !confidenceElement.TryGetDouble(out var confidence) ||
                !double.IsFinite(confidence) || confidence is < 0 or > 1)
                return false;

            if (activityType is null && !uncertainFields.Contains("activityType", StringComparer.Ordinal) ||
                occurredAt is null && !uncertainFields.Contains("occurredAt", StringComparer.Ordinal) ||
                details is null && !uncertainFields.Contains("details", StringComparer.Ordinal))
                return false;

            result = new SpeechTranscriptionResult(language, activityType, occurredAt, details, uncertainFields, confidence);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryActivityType(JsonElement element, out LogbookActivityType? activityType)
    {
        activityType = null;
        if (element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.String) return false;
        var value = element.GetString();
        if (value is null || !Enum.TryParse<LogbookActivityType>(value, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
            return false;
        activityType = parsed;
        return true;
    }

    private static bool TryDateTimeOffset(JsonElement element, out DateTimeOffset? value)
    {
        value = null;
        if (element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) return false;
        value = parsed;
        return true;
    }

    private static bool TryDetails(JsonElement element, out string? value)
    {
        value = null;
        if (element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.String) return false;
        var text = element.GetString();
        if (text is null || text.Length > 4000) return false;
        value = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return true;
    }
}

using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Voice;
using Xunit;

namespace AgroPlan.UnitTests.Voice;

public sealed class VoiceActivityDraftTests
{
    [Fact]
    public void Draft_requires_per_recording_consent_and_enforces_upload_bounds()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => Create(now, consented: false));
        Assert.Throws<ArgumentException>(() => Create(now, contentType: "audio/mpeg"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(now, sizeBytes: 10 * 1024 * 1024 + 1));

        var draft = Create(now);
        Assert.Equal(VoiceActivityDraftStatus.AwaitingUpload, draft.Status);
        Assert.True(draft.ConsentedToExternalProcessing);
        Assert.Equal("producer-1", draft.ConsentActorUserId);
    }

    [Fact]
    public void Draft_preserves_partial_values_until_explicit_confirmation_and_confirmation_is_idempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var draft = Create(now);
        draft.FinalizeAudio("private/farm/voice/final", "audio/wav", draft.ExpectedSizeBytes, 300, now.AddSeconds(1));

        Assert.True(draft.TryStartProcessing(now.AddSeconds(2), now.AddMinutes(-15)));
        Assert.True(draft.MarkDraftReady(LogbookActivityType.Irrigation, null, null,
            ["occurredAt", "details"], 0.73, now.AddSeconds(3)));
        Assert.Equal(VoiceActivityDraftStatus.DraftReady, draft.Status);
        Assert.Null(draft.OccurredAt);
        Assert.Null(draft.Details);
        Assert.Null(draft.LogbookEntryId);
        Assert.False(draft.Confirm("producer-1", Guid.NewGuid(), draft.Version, now.AddSeconds(4)).ConfirmedNow);

        var editVersion = draft.Version;
        Assert.True(draft.Edit(editVersion, LogbookActivityType.Irrigation, now, "Irrigated north bed", now.AddSeconds(5)));
        var entryId = Guid.NewGuid();
        var confirmed = draft.Confirm("producer-1", entryId, draft.Version, now.AddSeconds(6));
        Assert.True(confirmed.ConfirmedNow);
        Assert.Equal(entryId, confirmed.LogbookEntryId);
        Assert.Equal(VoiceActivityDraftStatus.Confirmed, draft.Status);
        Assert.Equal(now.AddSeconds(6), draft.AudioDeleteAfter);
        Assert.True(draft.Confirm("producer-1", Guid.NewGuid(), 1, now.AddSeconds(7)).AlreadyConfirmed);
        Assert.Equal(entryId, draft.LogbookEntryId);
    }

    [Fact]
    public void Consent_revocation_cancels_unprocessed_work_and_schedules_private_audio_deletion()
    {
        var now = DateTimeOffset.UtcNow;
        var draft = Create(now);
        draft.FinalizeAudio("private/farm/voice/final", "audio/wav", draft.ExpectedSizeBytes, 1, now.AddSeconds(1));

        draft.RevokeConsent("producer-1", now.AddSeconds(2));

        Assert.Equal(VoiceActivityDraftStatus.Cancelled, draft.Status);
        Assert.False(draft.ConsentedToExternalProcessing);
        Assert.NotNull(draft.ConsentRevokedAt);
        Assert.Equal(now.AddSeconds(2), draft.AudioDeleteAfter);
        Assert.False(draft.TryStartProcessing(now.AddSeconds(3), now.AddMinutes(-15)));
    }

    [Fact]
    public void Speech_contract_keeps_dominican_spanish_and_haitian_creole_distinct()
    {
        Assert.NotEqual(SpeechLanguage.DominicanSpanish, SpeechLanguage.HaitianCreole);
        Assert.Contains(SpeechLanguage.DominicanSpanish, Enum.GetValues<SpeechLanguage>());
        Assert.Contains(SpeechLanguage.HaitianCreole, Enum.GetValues<SpeechLanguage>());
    }

    [Fact]
    public void Audio_inspector_accepts_only_bounded_mono_pcm_wave_and_derives_duration_from_data()
    {
        var wave = CreateWave(300);
        Assert.True(PcmWaveAudioInspector.TryGetDurationSeconds(wave, out var duration));
        Assert.Equal(300, duration);
        Assert.False(PcmWaveAudioInspector.TryGetDurationSeconds(wave[..^1], out _));
        Assert.False(PcmWaveAudioInspector.TryGetDurationSeconds(new byte[44], out _));
        Assert.False(PcmWaveAudioInspector.TryGetDurationSeconds(CreateWave(301), out _));
    }

    [Fact]
    public void Validator_preserves_a_partial_result_and_marks_missing_fields()
    {
        const string payload = """
            {"schemaVersion":1,"language":"DominicanSpanish","activityType":"Irrigation","occurredAt":null,"details":null,"uncertainFields":["occurredAt","details"],"confidence":0.72}
            """;

        var accepted = SpeechTranscriptionResultValidator.TryValidate(
            payload, SpeechLanguage.DominicanSpanish, out var result);

        Assert.True(accepted);
        Assert.NotNull(result);
        Assert.Equal(SpeechLanguage.DominicanSpanish, result.Language);
        Assert.Equal(LogbookActivityType.Irrigation, result.ActivityType);
        Assert.Null(result.OccurredAt);
        Assert.Null(result.Details);
        Assert.Contains("occurredAt", result.UncertainFields);
        Assert.Contains("details", result.UncertainFields);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":2,\"language\":\"DominicanSpanish\",\"activityType\":\"Irrigation\",\"occurredAt\":null,\"details\":null,\"uncertainFields\":[],\"confidence\":0.9}")]
    [InlineData("{\"schemaVersion\":1,\"language\":\"HaitianCreole\",\"activityType\":\"Irrigation\",\"occurredAt\":null,\"details\":null,\"uncertainFields\":[],\"confidence\":0.9}")]
    [InlineData("{\"schemaVersion\":1,\"language\":\"DominicanSpanish\",\"activityType\":\"Planting\",\"occurredAt\":null,\"details\":null,\"uncertainFields\":[],\"confidence\":0.9}")]
    [InlineData("{\"schemaVersion\":1,\"language\":\"DominicanSpanish\",\"activityType\":\"Irrigation\",\"occurredAt\":null,\"details\":null,\"uncertainFields\":[],\"confidence\":1.1}")]
    [InlineData("{\"schemaVersion\":1,\"language\":\"DominicanSpanish\",\"activityType\":\"Irrigation\",\"occurredAt\":null,\"details\":null,\"uncertainFields\":[],\"confidence\":0.9,\"farmId\":\"leak\"}")]
    public void Validator_rejects_malformed_or_unsupported_provider_output(string payload)
    {
        var accepted = SpeechTranscriptionResultValidator.TryValidate(
            payload, SpeechLanguage.DominicanSpanish, out var result);

        Assert.False(accepted);
        Assert.Null(result);
    }

    private static VoiceActivityDraft Create(DateTimeOffset now, bool consented = true, string contentType = "audio/wav",
        long sizeBytes = 32044) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
        "producer-1", "producer-1", "voice-consent-v1", consented, SpeechLanguage.DominicanSpanish,
        $"private/farm/voice/{Guid.NewGuid():N}/staging", contentType, sizeBytes, now, now.AddMinutes(10));

    private static byte[] CreateWave(int seconds)
    {
        var dataLength = 32000 * seconds;
        var bytes = new byte[44 + dataLength];
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        System.Text.Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(bytes, 8);
        BitConverter.GetBytes(16).CopyTo(bytes, 16);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 20);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 22);
        BitConverter.GetBytes(16000).CopyTo(bytes, 24);
        BitConverter.GetBytes(32000).CopyTo(bytes, 28);
        BitConverter.GetBytes((short)2).CopyTo(bytes, 32);
        BitConverter.GetBytes((short)16).CopyTo(bytes, 34);
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
        BitConverter.GetBytes(dataLength).CopyTo(bytes, 40);
        return bytes;
    }
}

using AgroPlan.Application.Abstractions;

namespace AgroPlan.Worker.AI;

/// <summary>Development/test-only implementation that makes no network request.</summary>
public sealed class UnconfiguredSpeechTranscriptionProvider : ISpeechTranscriptionProvider
{
    public Task<string> TranscribeAsync(SpeechTranscriptionRequest request, CancellationToken cancellationToken) =>
        Task.FromException<string>(new SpeechTranscriptionProviderException(SpeechTranscriptionFailureKind.NotConfigured));
}

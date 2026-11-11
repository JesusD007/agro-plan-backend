namespace AgroPlan.Worker.Jobs;

public sealed class WeeklySummaryRetryOptions
{
    public const int DefaultMaximumAttempts = 3;
    public const int MaximumAllowedAttempts = 10;

    public int MaxAttempts { get; set; } = DefaultMaximumAttempts;
}

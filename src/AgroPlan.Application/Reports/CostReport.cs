namespace AgroPlan.Application.Reports;

public sealed record CostReport(
    Guid FarmId,
    Guid? CropCycleId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    IReadOnlyList<CostCurrencyTotal> CurrencyTotals,
    IReadOnlyList<SourceQuantityTotal> RecordedQuantities,
    CostPerQuintalResult CostPerQuintal);

public sealed record CostCurrencyTotal(string CurrencyCode, decimal Amount, IReadOnlyList<Guid> SourceEntryIds)
{
    public string AmountExact => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record SourceQuantityTotal(string Unit, decimal Quantity, IReadOnlyList<Guid> SourceEntryIds);

public sealed record CostPerQuintalResult(string Status, decimal? Amount, string? CurrencyCode, IReadOnlyList<string> MissingInputs)
{
    public string? AmountExact => Amount?.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

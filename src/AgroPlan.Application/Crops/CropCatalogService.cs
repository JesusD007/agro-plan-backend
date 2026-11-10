namespace AgroPlan.Application.Crops;

public sealed class CropCatalogService(ICropCatalogRepository repository)
{
    private static readonly string[] RequiredParameterKeys =
    [
        "variety",
        "plantingWindow",
        "spacing",
        "density",
        "cycleDuration",
        "growthStages",
        "waterRequirement",
        "zoneSuitability"
    ];

    public async Task<IReadOnlyList<CropCatalogItem>> ListAsync(CancellationToken cancellationToken)
    {
        var entries = await repository.ListAsync(cancellationToken);
        var today = DateTimeOffset.UtcNow;
        return entries.Select(entry =>
        {
            var usableParameters = entry.Parameters
                .Where(parameter => parameter.IsUsableAt(today))
                .Select(parameter => new AgronomicParameterItem(
                    parameter.Key,
                    parameter.NumericValue!.Value,
                    parameter.Unit!,
                    parameter.SourceName!,
                    parameter.SourceUrl,
                    parameter.Region,
                    parameter.ValidFrom,
                    parameter.ValidTo))
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .ToArray();
            var usableKeys = usableParameters.Select(parameter => parameter.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = RequiredParameterKeys.Where(key => !usableKeys.Contains(key)).ToArray();

            return new CropCatalogItem(
                entry.Id,
                entry.Code,
                entry.DisplayName,
                entry.SourceName,
                entry.ValidationStatus.ToString(),
                entry.Region,
                entry.ValidFrom,
                entry.ValidTo,
                usableParameters,
                missing);
        }).ToArray();
    }
}

public sealed record CropCatalogItem(
    Guid Id,
    string Code,
    string DisplayName,
    string SourceName,
    string ValidationStatus,
    string? Region,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    IReadOnlyList<AgronomicParameterItem> ValidatedParameters,
    IReadOnlyList<string> MissingParameters);

public sealed record AgronomicParameterItem(
    string Key,
    decimal Value,
    string Unit,
    string SourceName,
    string? SourceUrl,
    string? Region,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo);

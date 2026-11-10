namespace AgroPlan.Application.Reports;

public sealed record ProductionReport(Guid FarmId, IReadOnlyList<ProductionCropCycleReport> CropCycles);

public sealed record ProductionCropCycleReport(
    Guid CropCycleId,
    Guid ParcelId,
    string CropCode,
    string CropName,
    decimal AreaHectares,
    DateOnly? PlantingDate,
    IReadOnlyList<ProductionQuantity> HarvestedQuantities);

public sealed record ProductionQuantity(string Unit, decimal Quantity, IReadOnlyList<Guid> SourceHarvestIds);

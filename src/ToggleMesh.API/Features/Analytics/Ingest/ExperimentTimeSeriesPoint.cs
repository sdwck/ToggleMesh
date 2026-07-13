namespace ToggleMesh.API.Features.Analytics.Ingest;

public record ExperimentTimeSeriesPoint(
    DateTimeOffset TimeBucket,
    Guid VariationId,
    long Exposures,
    long Conversions
);

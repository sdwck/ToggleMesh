namespace ToggleMesh.API.Features.Analytics.Ingest;

public record ExperimentTimeSeriesPoint(
    DateTime TimeBucket,
    bool Variant,
    long Exposures,
    long Conversions
);

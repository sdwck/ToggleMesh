namespace ToggleMesh.API.Features.Analytics.GetExperimentTimeSeries;

public record TimeSeriesResponsePoint(string Time, Guid VariationId, long Exposures, long Conversions, double ConversionRate);

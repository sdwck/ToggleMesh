namespace ToggleMesh.API.Features.Analytics.GetExperimentTimeSeries;

public record TimeSeriesResponsePoint(string Time, bool Variant, long Exposures, long Conversions, double ConversionRate);
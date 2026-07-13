using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Metrics.SdkIngest;

public class SdkIngestMetricsValidator : Validator<SdkIngestMetricsRequest>
{
    public SdkIngestMetricsValidator(IConfiguration config)
    {
        var maxBatchSize = config.GetValue<int>("Ingestion:MaxBatchSize", 2000);
        
        RuleFor(x => x.Metrics)
            .Must(x => x == null || x.Count <= maxBatchSize)
            .WithMessage($"Batch size exceeds the maximum limit of {maxBatchSize} metrics.");
    }
}

using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class IngestEventsValidator : Validator<IngestEventsRequest>
{
    public IngestEventsValidator(IConfiguration config)
    {
        var maxBatchSize = config.GetValue<int>("Ingestion:MaxBatchSize", 2000);
        
        RuleFor(x => x.Events)
            .Must(x => x == null || x.Count <= maxBatchSize)
            .WithMessage($"Batch size exceeds the maximum limit of {maxBatchSize} events.");
    }
}

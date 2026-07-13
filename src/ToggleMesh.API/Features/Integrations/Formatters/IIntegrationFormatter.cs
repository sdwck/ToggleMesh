namespace ToggleMesh.API.Features.Integrations.Formatters;

public interface IIntegrationFormatter
{
    object FormatMessage(Domain.IntegrationEvent evt);
}

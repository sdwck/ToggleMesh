namespace ToggleMesh.API.Infrastructure.Email;

public interface IEmailTemplateService
{
    Task<string> RenderAsync<TModel>(string templateName, TModel model, CancellationToken ct = default);
}

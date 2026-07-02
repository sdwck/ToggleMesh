using Scriban;

namespace ToggleMesh.API.Infrastructure.Email;

public class ScribanEmailTemplateService : IEmailTemplateService
{
    private readonly string _templatesPath;

    public ScribanEmailTemplateService(IWebHostEnvironment env)
    {
        _templatesPath = Path.Combine(env.ContentRootPath, "Infrastructure", "Email", "Templates");
    }

    public async Task<string> RenderAsync<TModel>(string templateName, TModel model, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_templatesPath, $"{templateName}.html");
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Template {templateName}.html not found at {filePath}");

        var templateContent = await File.ReadAllTextAsync(filePath, ct);
        var template = Template.Parse(templateContent);

        if (template.HasErrors)
        {
            var errors = string.Join("\n", template.Messages.Select(m => m.ToString()));
            throw new InvalidOperationException($"Template parsing failed:\n{errors}");
        }

        return await template.RenderAsync(model, member => member.Name);
    }
}

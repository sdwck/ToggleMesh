using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Projects.CreateKey;

public class CreateKeyValidator : Validator<CreateKeyRequest>
{
    public CreateKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Key name is required.")
            .MaximumLength(128).WithMessage("Key name must not exceed 128 characters.");
        
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid key type. Supported: Server (0), Client (1).");
    }
}
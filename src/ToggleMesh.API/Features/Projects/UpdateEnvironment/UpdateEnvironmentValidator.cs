using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Projects.UpdateEnvironment;

public class UpdateEnvironmentValidator : Validator<UpdateEnvironmentRequest>
{
    public UpdateEnvironmentValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Environment name is required.")
            .MinimumLength(2).WithMessage("Environment name must be at least 2 characters.")
            .MaximumLength(50).WithMessage("Environment name must not exceed 50 characters.");
    }
}
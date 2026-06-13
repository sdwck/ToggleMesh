using FluentValidation;
using FastEndpoints;

namespace ToggleMesh.API.Features.Projects.UpdateProject;

public class UpdateProjectValidator : Validator<UpdateProjectRequest>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MinimumLength(3).WithMessage("Project name must be at least 3 characters.")
            .MaximumLength(100).WithMessage("Project name must not exceed 100 characters.");
    }
}
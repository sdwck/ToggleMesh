using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Projects.AddMember;

public class AddMemberValidator : Validator<AddMemberRequest>
{
    public AddMemberValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid project role. Supported: Owner (0), Admin (1), Editor (2), Viewer (3).");
    }
}
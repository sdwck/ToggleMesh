using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Projects.UpdateMember;

public class UpdateMemberValidator : Validator<UpdateMemberRequest>
{
    public UpdateMemberValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid project role.");

        RuleForEach(x => x.EnvironmentRoles)
            .ChildRules(er =>
            {
                er.RuleFor(e => e.EnvironmentId)
                    .NotEmpty().WithMessage("Environment ID is required.");
                
                er.RuleFor(e => e.Role)
                    .IsInEnum().WithMessage("Invalid environment role.");
            })
            .When(x => x.EnvironmentRoles != null);
    }
}
using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Auth.CreateToken;

public class CreateTokenValidator : Validator<CreateTokenRequest>
{
    public CreateTokenValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Token name is required.")
            .MinimumLength(3).WithMessage("Token name must be at least 3 characters.")
            .MaximumLength(100).WithMessage("Token name must not exceed 100 characters.");

        RuleFor(x => x.ExpiresInDays)
            .GreaterThan(0).WithMessage("Expiration days must be a positive number.")
            .When(x => x.ExpiresInDays.HasValue);
    }
}
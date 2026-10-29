using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Users
{
    public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
    {
        public CreateUserRequestValidator()
        {
            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Email is required.")
                .MaximumLength(256).WithMessage("Email is too long.")
                .EmailAddress().WithMessage("Email must be a valid email address.");

            RuleFor(x => x.DisplayName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Display name is required.")
                .MaximumLength(120).WithMessage("Display name is too long.");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .MaximumLength(128).WithMessage("Password is too long.")
                .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
                .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
                .Matches("[0-9]").WithMessage("Password must contain a digit.")
                .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a symbol.");

            RuleFor(x => x.Role)
                .NotNull().WithMessage("Role is required.");

            RuleFor(x => x.Role)
                .IsInEnum().WithMessage("Role is not valid.")
                .When(x => x.Role.HasValue);
        }
    }

    public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
    {
        public UpdateUserRequestValidator()
        {
            RuleFor(x => x)
                .Must(x => x.DisplayName is not null || x.Role is not null || x.IsActive is not null)
                .WithMessage("Provide at least one field to update.")
                .OverridePropertyName("request");

            RuleFor(x => x.DisplayName)
                .NotEmpty().WithMessage("Display name cannot be empty.")
                .MaximumLength(120).WithMessage("Display name is too long.")
                .When(x => x.DisplayName is not null);

            RuleFor(x => x.Role)
                .IsInEnum().WithMessage("Role is not valid.")
                .When(x => x.Role.HasValue);
        }
    }
}

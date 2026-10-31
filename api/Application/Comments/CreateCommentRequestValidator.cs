using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Comments
{
    public sealed class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
    {
        public CreateCommentRequestValidator()
        {
            RuleFor(x => x.Body)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Comment is required.")
                .Must(body => body.Trim().Length <= 2000).WithMessage("Comment must be at most 2000 characters.");
        }
    }
}

using FluentValidation;
using System.Linq.Expressions;

namespace OpsPilot.Application.Postmortems
{
    public sealed class SavePostmortemRequestValidator : AbstractValidator<SavePostmortemRequest>
    {
        public SavePostmortemRequestValidator()
        {
            AddRules(x => x.Summary, "Summary", 2000);
            AddRules(x => x.RootCause, "Root cause", 4000);
            AddRules(x => x.Impact, "Impact", 2000);
            AddRules(x => x.CorrectiveActions, "Corrective actions", 4000);
        }

        private void AddRules(Expression<Func<SavePostmortemRequest, string>> field, string label, int maxLength)
        {
            RuleFor(field)
                .Cascade(CascadeMode.Stop)

                // NotEmpty menolak null, "" dan teks yang hanya berisi spasi
                .NotEmpty().WithMessage($"{label} is required.")

                // Panjang dihitung setelah spasi di ujung dibuang, sama dengan yang nanti disimpan
                .Must(value => value.Trim().Length <= maxLength)
                .WithMessage($"{label} must be at most {maxLength} characters.");
        }
    }
}

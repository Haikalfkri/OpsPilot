using FluentValidation;

namespace OpsPilot.Application.Dashboard
{
    public sealed class DashboardQueryValidator : AbstractValidator<DashboardQuery>
    {
        public DashboardQueryValidator()
        {
            // Hanya periode dari daftar izin. Nilai lain (0, 5, 365, -7) ditolak dengan 400.
            RuleFor(x => x.Days)
                .Must(days => DashboardPeriods.Allowed.Contains(days))
                .WithMessage($"Days must be one of: {string.Join(", ", DashboardPeriods.Allowed)}.");
        }
    }
}

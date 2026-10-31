using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Incidents
{
    public sealed class CreateIncidentRequestValidator : AbstractValidator<CreateIncidentRequest>
    {
        public CreateIncidentRequestValidator() 
        {
            // Judul: 5 sampai 160 karakter (dihitung setelah spasi di ujung dibuang)
            RuleFor(x => x.Title)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Title is required.")
                .Must(t => t.Trim().Length >= 5).WithMessage("Title must be at least 5 characters.")
                .Must(t => t.Trim().Length <= 160).WithMessage("Title must be at most 160 characters.");

            // Deskripsi: wajib, maksimum 5.000 karakter
            RuleFor(x => x.Description)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Description is required.")
                .Must(d => d.Trim().Length <= 5000).WithMessage("Description must be at most 5000 characters.");

            // Severity wajib (null berarti client lupa mengirim)
            RuleFor(x => x.Severity)
                .NotNull().WithMessage("Severity is required.");

            RuleFor(x => x.Severity)
                .IsInEnum().WithMessage("Severity is not valid.")
                .When(x => x.Severity.HasValue);
        }

        public sealed class UpdateIncidentRequestValidator : AbstractValidator<UpdateIncidentRequest>
        {
            public UpdateIncidentRequestValidator() 
            {
                // Request kosong ({}) tidak mengubah apa pun, jadi dianggap salah.
                RuleFor(x => x)
                    .Must(x => x.Title is not null || x.Description is not null || x.Severity is not null)
                    .WithMessage("Provide at least one field to update.")
                    .OverridePropertyName("request");

                // Aturan per field hanya berlaku kalau field itu dikirim (tidak null)
                RuleFor(x => x.Title)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty().WithMessage("Title cannot be empty.")
                    .Must(t => t!.Trim().Length >= 5).WithMessage("Title must be at least 5 characters.")
                    .Must(t => t!.Trim().Length <= 160).WithMessage("Title must be at most 160 characters.")
                    .When(x => x.Title is not null);

                RuleFor(x => x.Description)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty().WithMessage("Description cannot be empty.")
                    .Must(d => d!.Trim().Length <= 5000).WithMessage("Description must be at most 5000 characters.")
                    .When(x => x.Description is not null);

                RuleFor(x => x.Severity)
                    .IsInEnum().WithMessage("Severity is not valid.")
                    .When(x => x.Severity.HasValue);
            }
        }

        public sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
        {
            public ChangeStatusRequestValidator()
            {
                RuleFor(x => x.Status)
                    .NotNull().WithMessage("Status is required.");

                RuleFor(x => x.Status)
                    .IsInEnum().WithMessage("Status is not valid.")
                    .When(x => x.Status.HasValue);
            }
        }

        public sealed class ListIncidentsQueryValidator : AbstractValidator<ListIncidentsQuery>
        {
            public ListIncidentsQueryValidator()
            {
                // Paging wajib dibatasi supaya tidak ada yang meminta jutaan baris sekaligus
                RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");
                RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

                RuleFor(x => x.Search).MaximumLength(100).WithMessage("Search is too long.");

                RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
                RuleFor(x => x.Severity).IsInEnum().When(x => x.Severity.HasValue);

                // Rentang tanggal harus masuk akal
                RuleFor(x => x.CreatedTo)
                    .Must((query, createdTo) => createdTo >= query.CreatedFrom)
                    .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue)
                    .WithMessage("CreatedTo must be on or after CreatedFrom.");

                // Sorting hanya boleh memakai kolom dari daftar izin
                RuleFor(x => x.SortBy)
                    .Must(s => s is not null && IncidentSortFields.All.Contains(s, StringComparer.OrdinalIgnoreCase))
                    .WithMessage($"SortBy must be one of: {string.Join(", ", IncidentSortFields.All)}.");

                RuleFor(x => x.SortDirection)
                    .Must(d => d is not null &&
                               (d.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                                d.Equals("desc", StringComparison.OrdinalIgnoreCase)))
                    .WithMessage("SortDirection must be 'asc' or 'desc'.");
            }
        }
    }
}

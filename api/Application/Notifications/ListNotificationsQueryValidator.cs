using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Notifications
{
    public sealed class ListNotificationsQueryValidator : AbstractValidator<ListNotificationsQuery>
    {
        public ListNotificationsQueryValidator()
        {
            RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
        }
    }
}

using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Common.Interfaces
{
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }
        Guid UserId { get; }
        Guid TeamId { get; }
        UserRole Role { get; }
    }
}

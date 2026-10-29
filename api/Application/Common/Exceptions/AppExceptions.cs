using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Common.Exceptions
{
    public sealed class AuthenticationFailedException() 
        : Exception("Invalid email or password.");

    public sealed class ForbiddenException(string? message = null)
        : Exception(message ?? "You do not have permission to perform this action.");

    public sealed class NotFoundException(string? message = null)
        : Exception(message ?? "The requested resource was not found.");

    public sealed class ConflictException(string message) 
        : Exception(message);
}

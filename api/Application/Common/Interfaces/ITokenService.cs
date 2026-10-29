using OpsPilot.Application.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Common.Interfaces
{
    public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAtUtc);
    public interface ITokenService
    {
        AccessTokenResult Create(UserInfo user);
    }
}

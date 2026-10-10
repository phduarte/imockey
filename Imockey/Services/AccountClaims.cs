using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Imockey.Domain;

namespace Imockey.Services;

public static class AccountClaims
{
    public static ClaimsPrincipal CreatePrincipal(UserModel user)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
            },
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }

    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

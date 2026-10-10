using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Imockey.Domain;
using Imockey.Services;

namespace Imockey.Pages.Account;

[AllowAnonymous]
public abstract class AccountPageModel : PageModel
{
    private readonly IAuthenticationSchemeProvider _schemes;

    protected AccountPageModel(IAuthenticationSchemeProvider schemes)
    {
        _schemes = schemes;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; protected set; }

    public bool GitHubEnabled { get; private set; }

    // Only local URLs are honored to avoid open redirects through ?returnUrl=.
    protected string SafeReturnUrl => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/";

    public override async Task OnPageHandlerExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutionDelegate next)
    {
        GitHubEnabled = await _schemes.GetSchemeAsync(GitHubAuthenticationDefaults.AuthenticationScheme) is not null;
        await next();
    }

    public IActionResult OnPostGitHub()
    {
        if (!GitHubEnabled)
            return RedirectToPage();

        return Challenge(
            new AuthenticationProperties { RedirectUri = SafeReturnUrl },
            GitHubAuthenticationDefaults.AuthenticationScheme);
    }

    protected async Task<IActionResult> SignInAndRedirectAsync(UserModel user)
    {
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AccountClaims.CreatePrincipal(user),
            new AuthenticationProperties { IsPersistent = true });

        return LocalRedirect(SafeReturnUrl);
    }
}

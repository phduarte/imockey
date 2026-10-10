using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Imockey.Services;

namespace Imockey.Pages.Account;

public class RegisterModel : AccountPageModel
{
    private readonly UserService _users;

    public RegisterModel(UserService users, IAuthenticationSchemeProvider schemes) : base(schemes)
    {
        _users = users;
    }

    [BindProperty]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    public string? DisplayName { get; set; }

    [BindProperty, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [BindProperty, DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? LocalRedirect(SafeReturnUrl) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (Password != ConfirmPassword)
        {
            Error = "As senhas não conferem.";
            return Page();
        }

        var (user, error) = _users.Register(UserName, Password, DisplayName);
        if (user is null)
        {
            Error = error;
            return Page();
        }

        return await SignInAndRedirectAsync(user);
    }
}

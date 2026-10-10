using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Imockey.Services;

namespace Imockey.Pages.Account;

public class LoginModel : AccountPageModel
{
    private readonly UserService _users;

    public LoginModel(UserService users, IAuthenticationSchemeProvider schemes) : base(schemes)
    {
        _users = users;
    }

    [BindProperty]
    public string UserName { get; set; } = string.Empty;

    [BindProperty, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? LocalRedirect(SafeReturnUrl) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var user = _users.ValidateCredentials(UserName, Password);
        if (user is null)
        {
            Error = "Usuário ou senha inválidos.";
            return Page();
        }

        return await SignInAndRedirectAsync(user);
    }
}

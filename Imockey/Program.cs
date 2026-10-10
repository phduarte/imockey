using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Imockey.Middlewares;
using Imockey.Repositories;
using Imockey.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<SqliteDatabase>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<ApplicationRepository>();
builder.Services.AddSingleton<RouteRepository>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<ApplicationService>();
builder.Services.AddSingleton<RouteService>();
builder.Services.AddSingleton<DataProtectionKeyRepository>();

builder.Services.AddDataProtection().SetApplicationName("Imockey");
builder.Services.AddOptions<KeyManagementOptions>()
    .Configure<DataProtectionKeyRepository>((options, keys) => options.XmlRepository = keys);

var authentication = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Imockey.Auth";
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        // A cookie can outlive its user (e.g. mocks.db deleted); reject it so the user signs in again.
        options.Events.OnValidatePrincipal = async context =>
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<UserService>();
            var userId = context.Principal?.GetUserId();
            if (userId is null || users.GetById(userId.Value) is null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// GitHub login is only offered when the OAuth app credentials are configured (user-secrets or environment variables).
var gitHub = builder.Configuration.GetSection("Authentication:GitHub");
if (!string.IsNullOrWhiteSpace(gitHub["ClientId"]) && !string.IsNullOrWhiteSpace(gitHub["ClientSecret"]))
{
    authentication.AddGitHub(options =>
    {
        options.ClientId = gitHub["ClientId"]!;
        options.ClientSecret = gitHub["ClientSecret"]!;
        options.Events.OnCreatingTicket = context =>
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<UserService>();
            var gitHubPrincipal = context.Principal!;
            var user = users.FindOrCreateGitHubUser(
                gitHubPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!,
                gitHubPrincipal.FindFirstValue(ClaimTypes.Name),
                gitHubPrincipal.FindFirstValue(GitHubAuthenticationConstants.Claims.Name));

            // Replace GitHub's claims with ours so the rest of the app only deals with the imockey user id.
            context.Principal = AccountClaims.CreatePrincipal(user);
            return Task.CompletedTask;
        };
    });
}

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseStaticFiles();

app.UseMiddleware<RewritterMiddleware>();

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub().RequireAuthorization();
app.MapFallbackToPage("/_Host");

app.Run();

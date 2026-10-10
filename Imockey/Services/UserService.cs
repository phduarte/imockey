using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Imockey.Domain;
using Imockey.Repositories;

namespace Imockey.Services;

public partial class UserService
{
    public const int MinPasswordLength = 8;
    public const int MinUserNameLength = 3;
    public const int MaxUserNameLength = 32;

    private readonly UserRepository _repository;
    private readonly PasswordHasher<UserModel> _hasher = new();

    public UserService(UserRepository repository)
    {
        _repository = repository;
    }

    public UserModel? GetById(Guid id) => _repository.GetById(id);

    public (UserModel? User, string? Error) Register(string userName, string password, string? displayName)
    {
        // The user name prefixes every application slug (/{userName}-{slug}/...), so it must be URL-safe.
        userName = userName?.Trim().ToLowerInvariant() ?? string.Empty;

        if (userName.Length < MinUserNameLength || userName.Length > MaxUserNameLength || !UserNameRegex().IsMatch(userName))
            return (null, $"O usuário deve ter de {MinUserNameLength} a {MaxUserNameLength} caracteres: letras minúsculas, números e '-' (sem '-' no início, no fim ou repetido).");

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return (null, $"A senha deve ter pelo menos {MinPasswordLength} caracteres.");

        if (_repository.GetByUserName(userName) is not null)
            return (null, "Este nome de usuário já está em uso.");

        var user = new UserModel
        {
            UserName = userName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim(),
        };
        user.PasswordHash = _hasher.HashPassword(user, password);

        _repository.Add(user);
        return (user, null);
    }

    public UserModel? ValidateCredentials(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            return null;

        var user = _repository.GetByUserName(userName.Trim());
        if (user?.PasswordHash is null)
            return null;

        return _hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed
            ? null
            : user;
    }

    public UserModel FindOrCreateGitHubUser(string gitHubId, string? login, string? name)
    {
        if (_repository.GetByGitHubId(gitHubId) is UserModel existing)
        {
            // GitHub accounts created before user names prefixed the URLs have none yet.
            if (existing.UserName is null)
            {
                existing.UserName = AvailableUserName(login, gitHubId);
                _repository.SetUserName(existing.Id, existing.UserName);
            }
            return existing;
        }

        var user = new UserModel
        {
            GitHubId = gitHubId,
            UserName = AvailableUserName(login, gitHubId),
            DisplayName = !string.IsNullOrWhiteSpace(name) ? name.Trim()
                : !string.IsNullOrWhiteSpace(login) ? login.Trim()
                : $"github-{gitHubId}",
        };
        _repository.Add(user);
        return user;
    }

    // Derives a URL-safe user name from the GitHub login, adding a numeric suffix when it is already taken.
    private string AvailableUserName(string? login, string gitHubId)
    {
        var baseName = ApplicationService.Slugify(login ?? string.Empty);
        if (baseName.Length < MinUserNameLength)
            baseName = $"github-{gitHubId}";
        baseName = Truncate(baseName, MaxUserNameLength);

        var candidate = baseName;
        for (var i = 2; _repository.GetByUserName(candidate) is not null; i++)
        {
            var suffix = $"-{i}";
            candidate = Truncate(baseName, MaxUserNameLength - suffix.Length) + suffix;
        }
        return candidate;
    }

    private static string Truncate(string value, int length) =>
        value.Length > length ? value[..length].TrimEnd('-') : value;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex UserNameRegex();
}

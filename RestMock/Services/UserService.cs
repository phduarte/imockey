using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using RestMock.Domain;
using RestMock.Repositories;

namespace RestMock.Services;

public partial class UserService
{
    public const int MinPasswordLength = 8;

    private readonly UserRepository _repository;
    private readonly PasswordHasher<UserModel> _hasher = new();

    public UserService(UserRepository repository)
    {
        _repository = repository;
    }

    public UserModel? GetById(Guid id) => _repository.GetById(id);

    public (UserModel? User, string? Error) Register(string userName, string password, string? displayName)
    {
        userName = userName?.Trim() ?? string.Empty;

        if (!UserNameRegex().IsMatch(userName))
            return (null, "O usuário deve ter de 3 a 32 caracteres: letras, números, '.', '_' ou '-'.");

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
            return existing;

        var user = new UserModel
        {
            GitHubId = gitHubId,
            DisplayName = !string.IsNullOrWhiteSpace(name) ? name.Trim()
                : !string.IsNullOrWhiteSpace(login) ? login.Trim()
                : $"github-{gitHubId}",
        };
        _repository.Add(user);
        return user;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{3,32}$")]
    private static partial Regex UserNameRegex();
}

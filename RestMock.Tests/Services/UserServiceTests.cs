using FluentAssertions;
using RestMock.Domain;
using RestMock.Services;

namespace RestMock.Tests.Services;

public class UserServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly UserService _service;

    public UserServiceTests()
    {
        _service = new UserService(_db.Users);
    }

    public void Dispose() => _db.Dispose();

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public void Register_Valid_PersistsUserWithHashedPassword()
    {
        var (user, error) = _service.Register("alice", "s3cret-pass", "Alice Doe");

        error.Should().BeNull();
        var stored = _db.Users.GetById(user!.Id)!;
        stored.UserName.Should().Be("alice");
        stored.DisplayName.Should().Be("Alice Doe");
        stored.PasswordHash.Should().NotBeNullOrEmpty().And.NotContain("s3cret-pass");
    }

    [Fact]
    public void Register_NoDisplayName_UsesUserName()
    {
        var (user, _) = _service.Register("alice", "s3cret-pass", "  ");

        user!.DisplayName.Should().Be("alice");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("acentuação")]
    [InlineData("this-user-name-is-way-too-long-for-us")]
    public void Register_InvalidUserName_ReturnsError(string userName)
    {
        var (user, error) = _service.Register(userName, "s3cret-pass", null);

        user.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Register_ShortPassword_ReturnsError()
    {
        var (user, error) = _service.Register("alice", "short", null);

        user.Should().BeNull();
        error.Should().Contain(UserService.MinPasswordLength.ToString());
    }

    [Fact]
    public void Register_DuplicateUserNameDifferentCase_ReturnsError()
    {
        _service.Register("alice", "s3cret-pass", null);

        var (user, error) = _service.Register("ALICE", "s3cret-pass", null);

        user.Should().BeNull();
        error.Should().Contain("em uso");
    }

    // ── ValidateCredentials ───────────────────────────────────────────────────

    [Fact]
    public void ValidateCredentials_CorrectPassword_ReturnsUser()
    {
        var (registered, _) = _service.Register("alice", "s3cret-pass", null);

        _service.ValidateCredentials(" Alice ", "s3cret-pass")!.Id.Should().Be(registered!.Id);
    }

    [Fact]
    public void ValidateCredentials_WrongPassword_ReturnsNull()
    {
        _service.Register("alice", "s3cret-pass", null);

        _service.ValidateCredentials("alice", "wrong-pass").Should().BeNull();
    }

    [Fact]
    public void ValidateCredentials_UnknownUser_ReturnsNull()
    {
        _service.ValidateCredentials("nobody", "s3cret-pass").Should().BeNull();
    }

    [Fact]
    public void ValidateCredentials_EmptyInput_ReturnsNull()
    {
        _service.ValidateCredentials("", "").Should().BeNull();
    }

    // ── GitHub ────────────────────────────────────────────────────────────────

    [Fact]
    public void FindOrCreateGitHubUser_NewId_CreatesUserWithoutPassword()
    {
        var user = _service.FindOrCreateGitHubUser("42", "octocat", "The Octocat");

        var stored = _db.Users.GetByGitHubId("42")!;
        stored.Id.Should().Be(user.Id);
        stored.DisplayName.Should().Be("The Octocat");
        stored.UserName.Should().BeNull();
        stored.PasswordHash.Should().BeNull();
    }

    [Fact]
    public void FindOrCreateGitHubUser_ExistingId_ReturnsSameUser()
    {
        var first = _service.FindOrCreateGitHubUser("42", "octocat", null);

        var second = _service.FindOrCreateGitHubUser("42", "renamed", "New Name");

        second.Id.Should().Be(first.Id);
    }

    [Theory]
    [InlineData("octocat", null, "octocat")]
    [InlineData(null, null, "github-42")]
    public void FindOrCreateGitHubUser_DisplayNameFallbacks(string? login, string? name, string expected)
    {
        _service.FindOrCreateGitHubUser("42", login, name).DisplayName.Should().Be(expected);
    }

    [Fact]
    public void GitHubUser_CannotSignInWithPassword()
    {
        _db.Users.Add(new UserModel { GitHubId = "7", UserName = "octo", DisplayName = "octo" });

        _service.ValidateCredentials("octo", "anything-long").Should().BeNull();
    }
}

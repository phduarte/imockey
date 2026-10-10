using FluentAssertions;
using Microsoft.Data.Sqlite;
using Imockey.Domain;

namespace Imockey.Tests.Repositories;

public class UserRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Add_PasswordUser_RoundTripsAllFields()
    {
        var user = new UserModel
        {
            UserName = "alice",
            DisplayName = "Alice",
            PasswordHash = "hash",
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        };

        _db.Users.Add(user);
        var loaded = _db.Users.GetById(user.Id)!;

        loaded.UserName.Should().Be("alice");
        loaded.DisplayName.Should().Be("Alice");
        loaded.PasswordHash.Should().Be("hash");
        loaded.GitHubId.Should().BeNull();
        loaded.CreatedAt.Should().Be(user.CreatedAt);
    }

    [Fact]
    public void Add_GitHubUser_HasNullCredentials()
    {
        var user = new UserModel { GitHubId = "123", DisplayName = "Octo" };

        _db.Users.Add(user);
        var loaded = _db.Users.GetByGitHubId("123")!;

        loaded.Id.Should().Be(user.Id);
        loaded.UserName.Should().BeNull();
        loaded.PasswordHash.Should().BeNull();
    }

    [Fact]
    public void GetByUserName_IsCaseInsensitive()
    {
        _db.Users.Add(new UserModel { UserName = "Alice", DisplayName = "Alice" });

        _db.Users.GetByUserName("ALICE").Should().NotBeNull();
    }

    [Fact]
    public void Add_DuplicateUserNameDifferentCase_Throws()
    {
        _db.Users.Add(new UserModel { UserName = "alice", DisplayName = "a" });

        var act = () => _db.Users.Add(new UserModel { UserName = "ALICE", DisplayName = "b" });

        act.Should().Throw<SqliteException>();
    }

    [Fact]
    public void Add_TwoGitHubOnlyUsers_AllowsNullUserNames()
    {
        _db.Users.Add(new UserModel { GitHubId = "1", DisplayName = "a" });

        var act = () => _db.Users.Add(new UserModel { GitHubId = "2", DisplayName = "b" });

        act.Should().NotThrow();
    }

    [Fact]
    public void SetUserName_UpdatesOnlyThatUser()
    {
        var user = new UserModel { GitHubId = "1", DisplayName = "a" };
        var other = new UserModel { GitHubId = "2", DisplayName = "b" };
        _db.Users.Add(user);
        _db.Users.Add(other);

        _db.Users.SetUserName(user.Id, "octocat");

        _db.Users.GetById(user.Id)!.UserName.Should().Be("octocat");
        _db.Users.GetById(other.Id)!.UserName.Should().BeNull();
    }

    [Fact]
    public void Getters_UnknownUser_ReturnNull()
    {
        _db.Users.GetById(Guid.NewGuid()).Should().BeNull();
        _db.Users.GetByUserName("nobody").Should().BeNull();
        _db.Users.GetByGitHubId("999").Should().BeNull();
    }
}

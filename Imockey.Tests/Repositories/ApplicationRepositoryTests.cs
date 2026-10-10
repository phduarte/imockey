using FluentAssertions;
using Microsoft.Data.Sqlite;
using Imockey.Domain;

namespace Imockey.Tests.Repositories;

public class ApplicationRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly UserModel _user;

    public ApplicationRepositoryTests()
    {
        _user = _db.AddUser();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Add_RoundTripsAllFields()
    {
        var application = new ApplicationModel
        {
            UserId = _user.Id,
            Name = "Loja API",
            Slug = "loja-api",
            Description = "catálogo",
            CreatedAt = new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc),
        };

        _db.Applications.Add(application);
        var loaded = _db.Applications.GetById(application.Id)!;

        loaded.UserId.Should().Be(_user.Id);
        loaded.Name.Should().Be("Loja API");
        loaded.Slug.Should().Be("loja-api");
        loaded.Description.Should().Be("catálogo");
        loaded.CreatedAt.Should().Be(application.CreatedAt);
    }

    [Fact]
    public void GetBySlug_IsCaseInsensitive()
    {
        var application = _db.AddApplication(_user.Id, "loja-api");

        _db.Applications.GetBySlug("LOJA-API")!.Id.Should().Be(application.Id);
    }

    [Fact]
    public void GetByUser_ReturnsOnlyThatUsersApplications()
    {
        var other = _db.AddUser("bob");
        _db.AddApplication(_user.Id, "mine-1");
        _db.AddApplication(_user.Id, "mine-2");
        _db.AddApplication(other.Id, "theirs");

        _db.Applications.GetByUser(_user.Id).Select(a => a.Slug).Should().BeEquivalentTo("mine-1", "mine-2");
    }

    [Fact]
    public void Add_DuplicateSlugAcrossUsers_Throws()
    {
        var other = _db.AddUser("bob");
        _db.AddApplication(_user.Id, "shared");

        var act = () => _db.AddApplication(other.Id, "SHARED");

        act.Should().Throw<SqliteException>();
    }

    [Fact]
    public void Add_UnknownUser_ThrowsForeignKeyViolation()
    {
        var act = () => _db.AddApplication(Guid.NewGuid(), "orphan");

        act.Should().Throw<SqliteException>();
    }

    [Fact]
    public void Delete_RemovesApplicationAndItsRoutes()
    {
        var application = _db.AddApplication(_user.Id, "gone");
        var route = new RouteModel { ApplicationId = application.Id, Pattern = "/x" };
        _db.Routes.Add(route);

        _db.Applications.Delete(application.Id).Should().BeTrue();

        _db.Applications.GetById(application.Id).Should().BeNull();
        _db.Routes.GetById(route.Id).Should().BeNull();
    }

    [Fact]
    public void Delete_Unknown_ReturnsFalse()
    {
        _db.Applications.Delete(Guid.NewGuid()).Should().BeFalse();
    }
}

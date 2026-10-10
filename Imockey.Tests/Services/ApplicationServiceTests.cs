using FluentAssertions;
using Imockey.Domain;
using Imockey.Services;

namespace Imockey.Tests.Services;

public class ApplicationServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ApplicationService _service;
    private readonly UserModel _user;

    public ApplicationServiceTests()
    {
        _service = new ApplicationService(_db.Applications, _db.Routes, _db.Users);
        _user = _db.AddUser();
    }

    public void Dispose() => _db.Dispose();

    private string Prefixed(string slug) => $"{_user.UserName}-{slug}";

    private UserModel AddNamedUser(string userName)
    {
        var user = new UserModel { UserName = userName, DisplayName = userName };
        _db.Users.Add(user);
        return user;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Valid_PersistsApplication()
    {
        var (application, error) = _service.Create(_user.Id, " Loja API ", "loja", " catálogo ");

        error.Should().BeNull();
        var stored = _db.Applications.GetById(application!.Id)!;
        stored.UserId.Should().Be(_user.Id);
        stored.Name.Should().Be("Loja API");
        stored.Slug.Should().Be(Prefixed("loja"));
        stored.Description.Should().Be("catálogo");
    }

    [Fact]
    public void Create_NoSlug_GeneratesFromName()
    {
        var (application, _) = _service.Create(_user.Id, "Serviço de Pagamentos", null, null);

        application!.Slug.Should().Be(Prefixed("servico-de-pagamentos"));
    }

    [Fact]
    public void Create_UpperCaseSlug_IsLowerCased()
    {
        var (application, _) = _service.Create(_user.Id, "x", " Loja-API ", null);

        application!.Slug.Should().Be(Prefixed("loja-api"));
    }

    [Fact]
    public void Create_SlugIsPrefixedWithUserName()
    {
        var user = AddNamedUser("phduarte");

        var (application, _) = _service.Create(user.Id, "BFF", "bff", null);

        application!.Slug.Should().Be("phduarte-bff");
    }

    [Fact]
    public void Create_LegacyUserNameWithUrlUnsafeChars_PrefixIsSlugified()
    {
        var user = AddNamedUser("John.Doe_");

        var (application, _) = _service.Create(user.Id, "x", "api", null);

        application!.Slug.Should().Be("john-doe-api");
    }

    [Fact]
    public void Create_UserWithoutUserName_ReturnsError()
    {
        var user = new UserModel { GitHubId = "1", DisplayName = "Octo" };
        _db.Users.Add(user);

        var (application, error) = _service.Create(user.Id, "x", "api", null);

        application.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetSlugPrefix_ReturnsUserNameWithDash()
    {
        _service.GetSlugPrefix(_user.Id).Should().Be($"{_user.UserName}-");
    }

    [Fact]
    public void GetSlugPrefix_UnknownUser_ReturnsNull()
    {
        _service.GetSlugPrefix(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void Create_BlankDescription_StoresNull()
    {
        var (application, _) = _service.Create(_user.Id, "x", "x", "   ");

        application!.Description.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyName_ReturnsError()
    {
        var (application, error) = _service.Create(_user.Id, "  ", "slug", null);

        application.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--dash")]
    [InlineData("under_score")]
    [InlineData("with/slash")]
    [InlineData("a23456789012345678901234567890123456789012345678901")]
    public void Create_InvalidSlug_ReturnsError(string slug)
    {
        var (application, error) = _service.Create(_user.Id, "x", slug, null);

        application.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_NameWithoutUsableChars_ReturnsError()
    {
        var (application, error) = _service.Create(_user.Id, "!!!", null, null);

        application.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_PrefixedSlugIsReserved_ReturnsError()
    {
        var user = AddNamedUser("signin");

        var (application, error) = _service.Create(user.Id, "x", "github", null);

        application.Should().BeNull();
        error.Should().Contain("reservado");
    }

    [Fact]
    public void Create_SameSlugForDifferentUsers_BothSucceed()
    {
        var other = _db.AddUser("bob");
        _service.Create(other.Id, "x", "shared", null);

        var (application, error) = _service.Create(_user.Id, "x", "shared", null);

        error.Should().BeNull();
        application!.Slug.Should().Be(Prefixed("shared"));
    }

    [Fact]
    public void Create_SameSlugTwiceForSameUser_ReturnsError()
    {
        _service.Create(_user.Id, "x", "shared", null);

        var (application, error) = _service.Create(_user.Id, "x", "shared", null);

        application.Should().BeNull();
        error.Should().Contain("em uso");
    }

    [Fact]
    public void Create_PrefixedSlugCollidesAcrossUsers_ReturnsError()
    {
        // "ana-beta" + "api" and "ana" + "beta-api" both produce "ana-beta-api".
        _service.Create(AddNamedUser("ana-beta").Id, "x", "api", null);

        var (application, error) = _service.Create(AddNamedUser("ana").Id, "x", "beta-api", null);

        application.Should().BeNull();
        error.Should().Contain("em uso");
    }

    // ── Ownership ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetOwned_Owner_ReturnsApplication()
    {
        var (application, _) = _service.Create(_user.Id, "x", "mine", null);

        _service.GetOwned(application!.Id, _user.Id).Should().NotBeNull();
    }

    [Fact]
    public void GetOwned_OtherUser_ReturnsNull()
    {
        var other = _db.AddUser("bob");
        var (application, _) = _service.Create(_user.Id, "x", "mine", null);

        _service.GetOwned(application!.Id, other.Id).Should().BeNull();
    }

    [Fact]
    public void GetOwned_Unknown_ReturnsNull()
    {
        _service.GetOwned(Guid.NewGuid(), _user.Id).Should().BeNull();
    }

    [Fact]
    public void GetByUser_ReturnsOnlyOwnApplications()
    {
        var other = _db.AddUser("bob");
        _service.Create(_user.Id, "x", "mine", null);
        _service.Create(other.Id, "x", "theirs", null);

        _service.GetByUser(_user.Id).Should().ContainSingle().Which.Slug.Should().Be(Prefixed("mine"));
    }

    [Fact]
    public void Delete_Owner_RemovesApplication()
    {
        var (application, _) = _service.Create(_user.Id, "x", "mine", null);

        _service.Delete(application!.Id, _user.Id).Should().BeTrue();

        _db.Applications.GetById(application.Id).Should().BeNull();
    }

    [Fact]
    public void Delete_OtherUser_KeepsApplication()
    {
        var other = _db.AddUser("bob");
        var (application, _) = _service.Create(_user.Id, "x", "mine", null);

        _service.Delete(application!.Id, other.Id).Should().BeFalse();

        _db.Applications.GetById(application.Id).Should().NotBeNull();
    }

    [Fact]
    public void CountRoutes_ReturnsApplicationRouteCount()
    {
        var (application, _) = _service.Create(_user.Id, "x", "mine", null);
        _db.Routes.Add(new RouteModel { ApplicationId = application!.Id, Pattern = "/a" });

        _service.CountRoutes(application.Id).Should().Be(1);
    }

    // ── Slugify ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Loja API", "loja-api")]
    [InlineData("  Ação & Reação!  ", "acao-reacao")]
    [InlineData("v2.0_beta", "v2-0-beta")]
    [InlineData("---", "")]
    public void Slugify_ProducesUrlSafeSlug(string input, string expected)
    {
        ApplicationService.Slugify(input).Should().Be(expected);
    }

    [Fact]
    public void Slugify_LongName_IsTruncatedWithoutTrailingDash()
    {
        var slug = ApplicationService.Slugify(new string('a', 49) + " bbbb");

        slug.Should().HaveLength(49).And.NotEndWith("-");
    }
}

using FluentAssertions;
using Imockey.Domain;
using Imockey.Services;

namespace Imockey.Tests.Services;

public class RouteServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly RouteService _service;
    private readonly ApplicationModel _app;
    private readonly ApplicationModel _otherApp;

    public RouteServiceTests()
    {
        _service = new RouteService(_db.Routes);
        var user = _db.AddUser();
        _app = _db.AddApplication(user.Id, "app");
        _otherApp = _db.AddApplication(user.Id, "other");
    }

    public void Dispose() => _db.Dispose();

    private static RouteModel MakeRoute(string pattern = "/test") =>
        new() { HttpMethod = "GET", Pattern = pattern };

    // ── Add ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_Route_AppearsInItsApplicationOnly()
    {
        var ep = MakeRoute();

        _service.Add(_app.Id, ep);

        _service.GetByApplication(_app.Id).Should().ContainSingle().Which.Id.Should().Be(ep.Id);
        _service.GetByApplication(_otherApp.Id).Should().BeEmpty();
    }

    [Fact]
    public void Add_Route_SetsApplicationId()
    {
        var ep = MakeRoute();

        _service.Add(_app.Id, ep);

        _db.Routes.GetById(ep.Id)!.ApplicationId.Should().Be(_app.Id);
    }

    [Fact]
    public void Add_Route_FiresOnChangeWithApplicationId()
    {
        Guid? changed = null;
        _service.OnChange += id => changed = id;

        _service.Add(_app.Id, MakeRoute());

        changed.Should().Be(_app.Id);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_Route_ChangesFields()
    {
        var ep = MakeRoute("/original");
        _service.Add(_app.Id, ep);

        _service.Update(_app.Id, ep.Id, new RouteModel { HttpMethod = "POST", Pattern = "/updated", StatusCode = 201 });

        var result = _db.Routes.GetById(ep.Id)!;
        result.Pattern.Should().Be("/updated");
        result.StatusCode.Should().Be(201);
        result.ApplicationId.Should().Be(_app.Id);
    }

    [Fact]
    public void Update_Route_FiresOnChangeEvent()
    {
        var ep = MakeRoute();
        _service.Add(_app.Id, ep);
        Guid? changed = null;
        _service.OnChange += id => changed = id;

        _service.Update(_app.Id, ep.Id, MakeRoute("/new"));

        changed.Should().Be(_app.Id);
    }

    [Fact]
    public void Update_RouteOfAnotherApplication_ThrowsAndKeepsIt()
    {
        var ep = MakeRoute("/original");
        _service.Add(_otherApp.Id, ep);

        var act = () => _service.Update(_app.Id, ep.Id, MakeRoute("/hijacked"));

        act.Should().Throw<ArgumentException>();
        _db.Routes.GetById(ep.Id)!.Pattern.Should().Be("/original");
    }

    [Fact]
    public void Update_UnknownRoute_Throws()
    {
        var act = () => _service.Update(_app.Id, Guid.NewGuid(), MakeRoute());

        act.Should().Throw<ArgumentException>();
    }

    // ── SetEnabled ────────────────────────────────────────────────────────────

    [Fact]
    public void SetEnabled_Route_ChangesState()
    {
        var ep = MakeRoute();
        _service.Add(_app.Id, ep);

        _service.SetEnabled(_app.Id, ep.Id, false);

        _db.Routes.GetById(ep.Id)!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void SetEnabled_Route_FiresOnChangeEvent()
    {
        var ep = MakeRoute();
        _service.Add(_app.Id, ep);
        Guid? changed = null;
        _service.OnChange += id => changed = id;

        _service.SetEnabled(_app.Id, ep.Id, false);

        changed.Should().Be(_app.Id);
    }

    [Fact]
    public void SetEnabled_RouteOfAnotherApplication_ThrowsAndKeepsIt()
    {
        var ep = MakeRoute();
        _service.Add(_otherApp.Id, ep);

        var act = () => _service.SetEnabled(_app.Id, ep.Id, false);

        act.Should().Throw<ArgumentException>();
        _db.Routes.GetById(ep.Id)!.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Update_DisabledRoute_StaysDisabled()
    {
        var ep = MakeRoute("/original");
        _service.Add(_app.Id, ep);
        _service.SetEnabled(_app.Id, ep.Id, false);

        _service.Update(_app.Id, ep.Id, MakeRoute("/updated"));

        _db.Routes.GetById(ep.Id)!.Enabled.Should().BeFalse();
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_Route_DisappearsFromApplication()
    {
        var ep = MakeRoute();
        _service.Add(_app.Id, ep);

        _service.Remove(_app.Id, ep.Id);

        _service.GetByApplication(_app.Id).Should().BeEmpty();
    }

    [Fact]
    public void Remove_Route_FiresOnChangeEvent()
    {
        var ep = MakeRoute();
        _service.Add(_app.Id, ep);
        Guid? changed = null;
        _service.OnChange += id => changed = id;

        _service.Remove(_app.Id, ep.Id);

        changed.Should().Be(_app.Id);
    }

    [Fact]
    public void Remove_RouteOfAnotherApplication_ThrowsAndKeepsIt()
    {
        var ep = MakeRoute();
        _service.Add(_otherApp.Id, ep);

        var act = () => _service.Remove(_app.Id, ep.Id);

        act.Should().Throw<ArgumentException>();
        _db.Routes.GetById(ep.Id).Should().NotBeNull();
    }
}

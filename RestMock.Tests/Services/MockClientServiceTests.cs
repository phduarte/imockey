using FluentAssertions;
using RestMock.Domain;
using RestMock.Services;

namespace RestMock.Tests.Services;

public class MockClientServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly MockClientService _service;
    private readonly ApplicationModel _app;
    private readonly ApplicationModel _otherApp;

    public MockClientServiceTests()
    {
        _service = new MockClientService(_db.Mocks);
        var user = _db.AddUser();
        _app = _db.AddApplication(user.Id, "app");
        _otherApp = _db.AddApplication(user.Id, "other");
    }

    public void Dispose() => _db.Dispose();

    private static EndpointModel MakeEndpoint(string pattern = "/test") =>
        new() { HttpMethod = "GET", Pattern = pattern };

    // ── Add ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_Endpoint_AppearsInItsApplicationOnly()
    {
        var ep = MakeEndpoint();

        _service.Add(_app.Id, ep);

        _service.GetByApplication(_app.Id).Should().ContainSingle().Which.Id.Should().Be(ep.Id);
        _service.GetByApplication(_otherApp.Id).Should().BeEmpty();
    }

    [Fact]
    public void Add_Endpoint_SetsApplicationId()
    {
        var ep = MakeEndpoint();

        _service.Add(_app.Id, ep);

        _db.Mocks.GetById(ep.Id)!.ApplicationId.Should().Be(_app.Id);
    }

    [Fact]
    public void Add_Endpoint_FiresOnChangeWithApplicationId()
    {
        Guid? changed = null;
        _service.OnChange += id => changed = id;

        _service.Add(_app.Id, MakeEndpoint());

        changed.Should().Be(_app.Id);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_Endpoint_ChangesFields()
    {
        var ep = MakeEndpoint("/original");
        _service.Add(_app.Id, ep);

        _service.Update(_app.Id, ep.Id, new EndpointModel { HttpMethod = "POST", Pattern = "/updated", StatusCode = 201 });

        var result = _db.Mocks.GetById(ep.Id)!;
        result.Pattern.Should().Be("/updated");
        result.StatusCode.Should().Be(201);
        result.ApplicationId.Should().Be(_app.Id);
    }

    [Fact]
    public void Update_Endpoint_FiresOnChangeEvent()
    {
        var ep = MakeEndpoint();
        _service.Add(_app.Id, ep);
        Guid? changed = null;
        _service.OnChange += id => changed = id;

        _service.Update(_app.Id, ep.Id, MakeEndpoint("/new"));

        changed.Should().Be(_app.Id);
    }

    [Fact]
    public void Update_EndpointOfAnotherApplication_ThrowsAndKeepsIt()
    {
        var ep = MakeEndpoint("/original");
        _service.Add(_otherApp.Id, ep);

        var act = () => _service.Update(_app.Id, ep.Id, MakeEndpoint("/hijacked"));

        act.Should().Throw<ArgumentException>();
        _db.Mocks.GetById(ep.Id)!.Pattern.Should().Be("/original");
    }

    [Fact]
    public void Update_UnknownEndpoint_Throws()
    {
        var act = () => _service.Update(_app.Id, Guid.NewGuid(), MakeEndpoint());

        act.Should().Throw<ArgumentException>();
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_Endpoint_DisappearsFromApplication()
    {
        var ep = MakeEndpoint();
        _service.Add(_app.Id, ep);

        _service.Remove(_app.Id, ep.Id);

        _service.GetByApplication(_app.Id).Should().BeEmpty();
    }

    [Fact]
    public void Remove_Endpoint_FiresOnChangeEvent()
    {
        var ep = MakeEndpoint();
        _service.Add(_app.Id, ep);
        Guid? changed = null;
        _service.OnChange += id => changed = id;

        _service.Remove(_app.Id, ep.Id);

        changed.Should().Be(_app.Id);
    }

    [Fact]
    public void Remove_EndpointOfAnotherApplication_ThrowsAndKeepsIt()
    {
        var ep = MakeEndpoint();
        _service.Add(_otherApp.Id, ep);

        var act = () => _service.Remove(_app.Id, ep.Id);

        act.Should().Throw<ArgumentException>();
        _db.Mocks.GetById(ep.Id).Should().NotBeNull();
    }
}

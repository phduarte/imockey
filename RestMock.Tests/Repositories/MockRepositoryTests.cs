using FluentAssertions;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using RestMock.Domain;

namespace RestMock.Tests.Repositories;

public class MockRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ApplicationModel _app;

    public MockRepositoryTests()
    {
        _app = _db.AddApplication(_db.AddUser().Id, "app");
    }

    public void Dispose() => _db.Dispose();

    private EndpointModel MakeEndpoint(string method = "GET", string pattern = "/test", Guid? applicationId = null) =>
        new() { ApplicationId = applicationId ?? _app.Id, HttpMethod = method, Pattern = pattern, StatusCode = 200 };

    [Fact]
    public void Add_RoundTripsAllFields()
    {
        var ep = MakeEndpoint("POST", "/api/orders");
        ep.StatusCode = 201;
        ep.ProcessingTime = 150;
        ep.ContentType = "text/plain";
        ep.Description = "creates an order";
        ep.ResponseBody = "{\"id\":\"{{uuid}}\"}";

        _db.Mocks.Add(ep);
        var loaded = _db.Mocks.GetById(ep.Id)!;

        loaded.ApplicationId.Should().Be(_app.Id);
        loaded.HttpMethod.Should().Be("POST");
        loaded.Pattern.Should().Be("/api/orders");
        loaded.StatusCode.Should().Be(201);
        loaded.ProcessingTime.Should().Be(150);
        loaded.ContentType.Should().Be("text/plain");
        loaded.Description.Should().Be("creates an order");
        loaded.ResponseBody.Should().Be("{\"id\":\"{{uuid}}\"}");
    }

    [Fact]
    public void Add_NullDescriptionAndBody_StayNull()
    {
        var ep = MakeEndpoint();

        _db.Mocks.Add(ep);
        var loaded = _db.Mocks.GetById(ep.Id)!;

        loaded.Description.Should().BeNull();
        loaded.ResponseBody.Should().BeNull();
    }

    [Fact]
    public void Add_JObjectBody_ComesBackAsJObject()
    {
        var ep = MakeEndpoint();
        ep.ResponseBody = JObject.Parse("{\"name\":\"x\",\"items\":[1,2]}");

        _db.Mocks.Add(ep);
        var body = _db.Mocks.GetById(ep.Id)!.ResponseBody;

        body.Should().BeOfType<JObject>();
        JToken.DeepEquals((JObject)body!, (JObject)ep.ResponseBody).Should().BeTrue();
    }

    [Fact]
    public void Add_JArrayBody_ComesBackAsJArray()
    {
        var ep = MakeEndpoint();
        ep.ResponseBody = JArray.Parse("[1,2,3]");

        _db.Mocks.Add(ep);

        _db.Mocks.GetById(ep.Id)!.ResponseBody.Should().BeOfType<JArray>();
    }

    [Fact]
    public void Add_UnknownApplication_ThrowsForeignKeyViolation()
    {
        var act = () => _db.Mocks.Add(MakeEndpoint(applicationId: Guid.NewGuid()));

        act.Should().Throw<SqliteException>();
    }

    [Fact]
    public void GetByApplication_PreservesInsertionOrder()
    {
        _db.Mocks.Add(MakeEndpoint("GET", "/c"));
        _db.Mocks.Add(MakeEndpoint("POST", "/a"));
        _db.Mocks.Add(MakeEndpoint("DELETE", "/b"));

        _db.Mocks.GetByApplication(_app.Id).Select(e => e.Pattern).Should().Equal("/c", "/a", "/b");
    }

    [Fact]
    public void GetByApplication_ReturnsOnlyThatApplicationsMocks()
    {
        var other = _db.AddApplication(_app.UserId, "other");
        _db.Mocks.Add(MakeEndpoint(pattern: "/mine"));
        _db.Mocks.Add(MakeEndpoint(pattern: "/theirs", applicationId: other.Id));

        _db.Mocks.GetByApplication(_app.Id).Should().ContainSingle().Which.Pattern.Should().Be("/mine");
    }

    [Fact]
    public void CountByApplication_CountsOnlyThatApplication()
    {
        var other = _db.AddApplication(_app.UserId, "other");
        _db.Mocks.Add(MakeEndpoint());
        _db.Mocks.Add(MakeEndpoint());
        _db.Mocks.Add(MakeEndpoint(applicationId: other.Id));

        _db.Mocks.CountByApplication(_app.Id).Should().Be(2);
    }

    [Fact]
    public void Update_ChangesFieldsAndKeepsPosition()
    {
        var first = MakeEndpoint(pattern: "/first");
        _db.Mocks.Add(first);
        _db.Mocks.Add(MakeEndpoint(pattern: "/second"));

        var updated = MakeEndpoint("PUT", "/first-updated");
        updated.Id = first.Id;
        updated.StatusCode = 404;
        updated.Description = "changed";

        _db.Mocks.Update(updated).Should().BeTrue();

        var all = _db.Mocks.GetByApplication(_app.Id);
        all[0].Pattern.Should().Be("/first-updated");
        all[0].HttpMethod.Should().Be("PUT");
        all[0].StatusCode.Should().Be(404);
        all[0].Description.Should().Be("changed");
        all[1].Pattern.Should().Be("/second");
    }

    [Fact]
    public void Update_Unknown_ReturnsFalse()
    {
        _db.Mocks.Update(MakeEndpoint()).Should().BeFalse();
    }

    [Fact]
    public void Delete_RemovesMock()
    {
        var ep = MakeEndpoint();
        _db.Mocks.Add(ep);

        _db.Mocks.Delete(ep.Id).Should().BeTrue();

        _db.Mocks.GetById(ep.Id).Should().BeNull();
    }

    [Fact]
    public void Delete_Unknown_ReturnsFalse()
    {
        _db.Mocks.Delete(Guid.NewGuid()).Should().BeFalse();
    }
}

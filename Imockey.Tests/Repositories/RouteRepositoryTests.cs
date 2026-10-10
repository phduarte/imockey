using FluentAssertions;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Imockey.Domain;

namespace Imockey.Tests.Repositories;

public class RouteRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ApplicationModel _app;

    public RouteRepositoryTests()
    {
        _app = _db.AddApplication(_db.AddUser().Id, "app");
    }

    public void Dispose() => _db.Dispose();

    private RouteModel MakeRoute(string method = "GET", string pattern = "/test", Guid? applicationId = null) =>
        new() { ApplicationId = applicationId ?? _app.Id, HttpMethod = method, Pattern = pattern, StatusCode = 200 };

    [Fact]
    public void Add_RoundTripsAllFields()
    {
        var ep = MakeRoute("POST", "/api/orders");
        ep.StatusCode = 201;
        ep.ProcessingTime = 150;
        ep.ContentType = "text/plain";
        ep.Description = "creates an order";
        ep.ResponseBody = "{\"id\":\"{{uuid}}\"}";

        _db.Routes.Add(ep);
        var loaded = _db.Routes.GetById(ep.Id)!;

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
        var ep = MakeRoute();

        _db.Routes.Add(ep);
        var loaded = _db.Routes.GetById(ep.Id)!;

        loaded.Description.Should().BeNull();
        loaded.ResponseBody.Should().BeNull();
    }

    [Fact]
    public void Add_JObjectBody_ComesBackAsJObject()
    {
        var ep = MakeRoute();
        ep.ResponseBody = JObject.Parse("{\"name\":\"x\",\"items\":[1,2]}");

        _db.Routes.Add(ep);
        var body = _db.Routes.GetById(ep.Id)!.ResponseBody;

        body.Should().BeOfType<JObject>();
        JToken.DeepEquals((JObject)body!, (JObject)ep.ResponseBody).Should().BeTrue();
    }

    [Fact]
    public void Add_JArrayBody_ComesBackAsJArray()
    {
        var ep = MakeRoute();
        ep.ResponseBody = JArray.Parse("[1,2,3]");

        _db.Routes.Add(ep);

        _db.Routes.GetById(ep.Id)!.ResponseBody.Should().BeOfType<JArray>();
    }

    [Fact]
    public void Add_UnknownApplication_ThrowsForeignKeyViolation()
    {
        var act = () => _db.Routes.Add(MakeRoute(applicationId: Guid.NewGuid()));

        act.Should().Throw<SqliteException>();
    }

    [Fact]
    public void GetByApplication_PreservesInsertionOrder()
    {
        _db.Routes.Add(MakeRoute("GET", "/c"));
        _db.Routes.Add(MakeRoute("POST", "/a"));
        _db.Routes.Add(MakeRoute("DELETE", "/b"));

        _db.Routes.GetByApplication(_app.Id).Select(e => e.Pattern).Should().Equal("/c", "/a", "/b");
    }

    [Fact]
    public void GetByApplication_ReturnsOnlyThatApplicationsRoutes()
    {
        var other = _db.AddApplication(_app.UserId, "other");
        _db.Routes.Add(MakeRoute(pattern: "/mine"));
        _db.Routes.Add(MakeRoute(pattern: "/theirs", applicationId: other.Id));

        _db.Routes.GetByApplication(_app.Id).Should().ContainSingle().Which.Pattern.Should().Be("/mine");
    }

    [Fact]
    public void CountByApplication_CountsOnlyThatApplication()
    {
        var other = _db.AddApplication(_app.UserId, "other");
        _db.Routes.Add(MakeRoute());
        _db.Routes.Add(MakeRoute());
        _db.Routes.Add(MakeRoute(applicationId: other.Id));

        _db.Routes.CountByApplication(_app.Id).Should().Be(2);
    }

    [Fact]
    public void Update_ChangesFieldsAndKeepsPosition()
    {
        var first = MakeRoute(pattern: "/first");
        _db.Routes.Add(first);
        _db.Routes.Add(MakeRoute(pattern: "/second"));

        var updated = MakeRoute("PUT", "/first-updated");
        updated.Id = first.Id;
        updated.StatusCode = 404;
        updated.Description = "changed";

        _db.Routes.Update(updated).Should().BeTrue();

        var all = _db.Routes.GetByApplication(_app.Id);
        all[0].Pattern.Should().Be("/first-updated");
        all[0].HttpMethod.Should().Be("PUT");
        all[0].StatusCode.Should().Be(404);
        all[0].Description.Should().Be("changed");
        all[1].Pattern.Should().Be("/second");
    }

    [Fact]
    public void Add_IsEnabledByDefault()
    {
        var ep = MakeRoute();

        _db.Routes.Add(ep);

        _db.Routes.GetById(ep.Id)!.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Add_Disabled_RoundTrips()
    {
        var ep = MakeRoute();
        ep.Enabled = false;

        _db.Routes.Add(ep);

        _db.Routes.GetById(ep.Id)!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void SetEnabled_TogglesState()
    {
        var ep = MakeRoute();
        _db.Routes.Add(ep);

        _db.Routes.SetEnabled(ep.Id, false).Should().BeTrue();
        _db.Routes.GetById(ep.Id)!.Enabled.Should().BeFalse();

        _db.Routes.SetEnabled(ep.Id, true).Should().BeTrue();
        _db.Routes.GetById(ep.Id)!.Enabled.Should().BeTrue();
    }

    [Fact]
    public void SetEnabled_Unknown_ReturnsFalse()
    {
        _db.Routes.SetEnabled(Guid.NewGuid(), false).Should().BeFalse();
    }

    [Fact]
    public void Update_KeepsEnabledState()
    {
        var ep = MakeRoute(pattern: "/original");
        _db.Routes.Add(ep);
        _db.Routes.SetEnabled(ep.Id, false);

        var updated = MakeRoute(pattern: "/updated");
        updated.Id = ep.Id;
        _db.Routes.Update(updated);

        var loaded = _db.Routes.GetById(ep.Id)!;
        loaded.Pattern.Should().Be("/updated");
        loaded.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Update_Unknown_ReturnsFalse()
    {
        _db.Routes.Update(MakeRoute()).Should().BeFalse();
    }

    [Fact]
    public void Delete_RemovesRoute()
    {
        var ep = MakeRoute();
        _db.Routes.Add(ep);

        _db.Routes.Delete(ep.Id).Should().BeTrue();

        _db.Routes.GetById(ep.Id).Should().BeNull();
    }

    [Fact]
    public void Delete_Unknown_ReturnsFalse()
    {
        _db.Routes.Delete(Guid.NewGuid()).Should().BeFalse();
    }
}

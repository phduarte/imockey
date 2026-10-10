using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Imockey.Domain;
using Imockey.Middlewares;

namespace Imockey.Tests.Middlewares;

public class RewritterMiddlewareTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ApplicationModel _app;
    private readonly ApplicationModel _otherApp;
    private bool _nextCalled;
    private readonly RewritterMiddleware _middleware;

    public RewritterMiddlewareTests()
    {
        var user = _db.AddUser();
        _app = _db.AddApplication(user.Id, "loja");
        _otherApp = _db.AddApplication(user.Id, "outra");
        _middleware = new RewritterMiddleware(_ => { _nextCalled = true; return Task.CompletedTask; });
    }

    public void Dispose() => _db.Dispose();

    private void AddRoute(Guid applicationId, string method, string pattern, int status = 200, object? body = null, bool enabled = true) =>
        _db.Routes.Add(new RouteModel
        {
            ApplicationId = applicationId,
            HttpMethod = method,
            Pattern = pattern,
            StatusCode = status,
            ResponseBody = body,
            Enabled = enabled,
        });

    private async Task<(HttpContext Context, string Body)> SendAsync(string method, string path, string query = "", string? requestBody = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 5087);
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        if (requestBody is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(requestBody);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }
        context.Response.Body = new MemoryStream();

        await _middleware.InvokeAsync(context, _db.Applications, _db.Routes);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context, body);
    }

    [Fact]
    public async Task SlugAndPatternMatch_ReturnsRoute()
    {
        AddRoute(_app.Id, "GET", "/v1/products", 201, "{\"ok\":true}");

        var (context, body) = await SendAsync("GET", "/loja/v1/products");

        _nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(201);
        context.Response.ContentType.Should().Be("application/json");
        body.Should().Be("{\"ok\":true}");
    }

    [Fact]
    public async Task SlugIsCaseInsensitive()
    {
        AddRoute(_app.Id, "GET", "/v1/products", body: "x");

        var (_, body) = await SendAsync("GET", "/LOJA/v1/products");

        body.Should().Be("x");
    }

    [Fact]
    public async Task SlugOnly_MatchesRootPattern()
    {
        AddRoute(_app.Id, "GET", "/", body: "root");

        var (_, body) = await SendAsync("GET", "/loja");

        body.Should().Be("root");
    }

    [Fact]
    public async Task QueryString_IsForwardedToMatcher()
    {
        AddRoute(_app.Id, "GET", "/movies?year=[int]", body: "found");

        var (_, matched) = await SendAsync("GET", "/loja/movies", "?year=2024");
        matched.Should().Be("found");

        var (context, _) = await SendAsync("GET", "/loja/movies", "?year=abc");
        context.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ResponseTemplate_UsesRequestBody()
    {
        AddRoute(_app.Id, "POST", "/heroes", 201, "{\"name\":\"{{$.name}}\"}");

        var (_, body) = await SendAsync("POST", "/loja/heroes", requestBody: "{\"name\":\"Batman\"}");

        body.Should().Be("{\"name\":\"Batman\"}");
    }

    [Fact]
    public async Task RouteOfAnotherApplication_IsNotMatched()
    {
        AddRoute(_otherApp.Id, "GET", "/v1/products", body: "other");

        var (context, body) = await SendAsync("GET", "/loja/v1/products");

        context.Response.StatusCode.Should().Be(404);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task DisabledRoute_Returns404WithoutCallingNext()
    {
        AddRoute(_app.Id, "GET", "/v1/products", body: "x", enabled: false);

        var (context, body) = await SendAsync("GET", "/loja/v1/products");

        context.Response.StatusCode.Should().Be(404);
        body.Should().BeEmpty();
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task DisabledRoute_FallsThroughToNextMatchingRoute()
    {
        AddRoute(_app.Id, "GET", "/v1/products/{int}", body: "disabled", enabled: false);
        AddRoute(_app.Id, "GET", "/v1/products/*", body: "fallback");

        var (_, body) = await SendAsync("GET", "/loja/v1/products/5");

        body.Should().Be("fallback");
    }

    [Fact]
    public async Task ReenabledRoute_IsMatchedAgain()
    {
        var route = new RouteModel { ApplicationId = _app.Id, Pattern = "/v1/products", ResponseBody = "x", Enabled = false };
        _db.Routes.Add(route);

        _db.Routes.SetEnabled(route.Id, true);
        var (context, body) = await SendAsync("GET", "/loja/v1/products");

        context.Response.StatusCode.Should().Be(200);
        body.Should().Be("x");
    }

    [Fact]
    public async Task PatternWithoutSlug_IsNotMatched()
    {
        AddRoute(_app.Id, "GET", "/v1/products", body: "x");

        await SendAsync("GET", "/v1/products");

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownSlug_CallsNext()
    {
        await SendAsync("GET", "/desconhecida/v1/products");

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task KnownSlugWithoutMatchingRoute_Returns404WithoutCallingNext()
    {
        AddRoute(_app.Id, "GET", "/v1/products", body: "x");

        var (context, _) = await SendAsync("DELETE", "/loja/v1/products");

        context.Response.StatusCode.Should().Be(404);
        _nextCalled.Should().BeFalse();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("")]
    [InlineData("/account/login")]
    [InlineData("/apps/3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData("/_blazor")]
    [InlineData("/css/site.css")]
    public async Task ReservedOrEmptyPaths_CallNext(string path)
    {
        await SendAsync("GET", path);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task NullResponseBody_WritesNothing()
    {
        AddRoute(_app.Id, "DELETE", "/heroes/{int}", 204);

        var (context, body) = await SendAsync("DELETE", "/loja/heroes/5");

        context.Response.StatusCode.Should().Be(204);
        body.Should().BeEmpty();
    }
}

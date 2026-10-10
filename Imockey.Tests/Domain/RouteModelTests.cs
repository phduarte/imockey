using FluentAssertions;
using Imockey.Domain;
using Xunit;

namespace Imockey.Tests.Domain;

public class RouteModelTests
{
    private static RouteModel Make(string method, string pattern) =>
        new() { HttpMethod = method, Pattern = pattern };

    // ── Exact path ────────────────────────────────────────────────────────────

    [Fact]
    public void Match_ExactPath_ReturnsTrue()
    {
        var ep = Make("GET", "/api/users");
        ep.Match("GET", "http://localhost/api/users").Should().BeTrue();
    }

    [Fact]
    public void Match_ExactPath_CaseInsensitive_ReturnsTrue()
    {
        var ep = Make("get", "/API/Users");
        ep.Match("GET", "http://localhost/api/users").Should().BeTrue();
    }

    [Fact]
    public void Match_ExactPath_WrongMethod_ReturnsFalse()
    {
        var ep = Make("POST", "/api/users");
        ep.Match("GET", "http://localhost/api/users").Should().BeFalse();
    }

    [Fact]
    public void Match_ExactPath_WrongPath_ReturnsFalse()
    {
        var ep = Make("GET", "/api/users");
        ep.Match("GET", "http://localhost/api/orders").Should().BeFalse();
    }

    [Fact]
    public void Match_EmptyUrl_ReturnsFalse()
    {
        var ep = Make("GET", "/api/users");
        ep.Match("GET", "").Should().BeFalse();
    }

    // ── Wildcard * ────────────────────────────────────────────────────────────

    [Fact]
    public void Match_Wildcard_MatchesSingleSegment()
    {
        var ep = Make("GET", "/v*/users");
        ep.Match("GET", "http://localhost/v1/users").Should().BeTrue();
        ep.Match("GET", "http://localhost/v2/users").Should().BeTrue();
        ep.Match("GET", "http://localhost/vbeta/users").Should().BeTrue();
    }

    [Fact]
    public void Match_Wildcard_DoesNotMatchSlash()
    {
        var ep = Make("GET", "/v*/users");
        ep.Match("GET", "http://localhost/v1/extra/users").Should().BeFalse();
    }

    // ── {guid} / {uuid} ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("{guid}")]
    [InlineData("{uuid}")]
    public void Match_GuidType_ValidGuid_ReturnsTrue(string token)
    {
        var ep = Make("GET", $"/items/{token}");
        ep.Match("GET", "http://localhost/items/3fa85f64-5717-4562-b3fc-2c963f66afa6").Should().BeTrue();
    }

    [Fact]
    public void Match_GuidType_InvalidValue_ReturnsFalse()
    {
        var ep = Make("GET", "/items/{guid}");
        ep.Match("GET", "http://localhost/items/not-a-guid").Should().BeFalse();
    }

    // ── {int} / {long} / {number} ─────────────────────────────────────────────

    [Theory]
    [InlineData("{int}")]
    [InlineData("{long}")]
    [InlineData("{number}")]
    public void Match_IntType_ValidInt_ReturnsTrue(string token)
    {
        var ep = Make("GET", $"/items/{token}");
        ep.Match("GET", "http://localhost/items/42").Should().BeTrue();
    }

    [Fact]
    public void Match_IntType_NegativeInt_ReturnsTrue()
    {
        var ep = Make("GET", "/items/{int}");
        ep.Match("GET", "http://localhost/items/-5").Should().BeTrue();
    }

    [Fact]
    public void Match_IntType_FloatValue_ReturnsFalse()
    {
        var ep = Make("GET", "/items/{int}");
        ep.Match("GET", "http://localhost/items/3.14").Should().BeFalse();
    }

    // ── {date} ────────────────────────────────────────────────────────────────

    [Fact]
    public void Match_DateType_ValidDate_ReturnsTrue()
    {
        var ep = Make("GET", "/events/{date}");
        ep.Match("GET", "http://localhost/events/2024-12-31").Should().BeTrue();
    }

    [Fact]
    public void Match_DateType_InvalidDate_ReturnsFalse()
    {
        var ep = Make("GET", "/events/{date}");
        ep.Match("GET", "http://localhost/events/not-a-date").Should().BeFalse();
    }

    // ── {datetime} ────────────────────────────────────────────────────────────

    [Fact]
    public void Match_DatetimeType_ValidDatetime_ReturnsTrue()
    {
        var ep = Make("GET", "/logs/{datetime}");
        ep.Match("GET", "http://localhost/logs/2024-12-31T10:00:00Z").Should().BeTrue();
    }

    [Fact]
    public void Match_DatetimeType_InvalidDatetime_ReturnsFalse()
    {
        var ep = Make("GET", "/logs/{datetime}");
        ep.Match("GET", "http://localhost/logs/not-a-datetime").Should().BeFalse();
    }

    // ── Custom type {id} (any non-slash segment) ──────────────────────────────

    [Fact]
    public void Match_CustomType_AnyNonSlashValue_ReturnsTrue()
    {
        var ep = Make("GET", "/users/{id}");
        ep.Match("GET", "http://localhost/users/abc123").Should().BeTrue();
        ep.Match("GET", "http://localhost/users/42").Should().BeTrue();
    }

    [Fact]
    public void Match_CustomType_MultiSegmentValue_ReturnsFalse()
    {
        var ep = Make("GET", "/users/{id}");
        ep.Match("GET", "http://localhost/users/a/b").Should().BeFalse();
    }

    // ── Optional segment {uuid?} ──────────────────────────────────────────────

    [Fact]
    public void Match_OptionalSegment_WithValue_ReturnsTrue()
    {
        var ep = Make("GET", "/users/{uuid?}");
        ep.Match("GET", "http://localhost/users/3fa85f64-5717-4562-b3fc-2c963f66afa6").Should().BeTrue();
    }

    [Fact]
    public void Match_OptionalSegment_WithoutValue_ReturnsTrue()
    {
        var ep = Make("GET", "/users/{uuid?}");
        ep.Match("GET", "http://localhost/users").Should().BeTrue();
    }

    // ── Query string ──────────────────────────────────────────────────────────

    [Fact]
    public void Match_QueryString_AllRequiredParamsPresent_ReturnsTrue()
    {
        var ep = Make("GET", "/search?q=[string]&page=[int]");
        ep.Match("GET", "http://localhost/search?q=hello&page=2").Should().BeTrue();
    }

    [Fact]
    public void Match_QueryString_MissingRequiredParam_ReturnsFalse()
    {
        var ep = Make("GET", "/search?q=[string]&page=[int]");
        ep.Match("GET", "http://localhost/search?q=hello").Should().BeFalse();
    }

    [Fact]
    public void Match_QueryString_InvalidIntParam_ReturnsFalse()
    {
        var ep = Make("GET", "/search?page=[int]");
        ep.Match("GET", "http://localhost/search?page=abc").Should().BeFalse();
    }

    [Fact]
    public void Match_QueryString_InvalidGuidParam_ReturnsFalse()
    {
        var ep = Make("GET", "/items?id=[guid]");
        ep.Match("GET", "http://localhost/items?id=not-a-guid").Should().BeFalse();
    }

    [Fact]
    public void Match_QueryString_InvalidDateParam_ReturnsFalse()
    {
        var ep = Make("GET", "/events?on=[date]");
        ep.Match("GET", "http://localhost/events?on=not-a-date").Should().BeFalse();
    }

    [Fact]
    public void Match_QueryString_ExtraParamsIgnored_ReturnsTrue()
    {
        var ep = Make("GET", "/search?q=[string]");
        ep.Match("GET", "http://localhost/search?q=hello&extra=ignored").Should().BeTrue();
    }

    // ── ProcessingTime ────────────────────────────────────────────────────────

    [Fact]
    public void ProcessingTime_NegativeValue_ClampedToZero()
    {
        var ep = new RouteModel { HttpMethod = "GET", Pattern = "/", ProcessingTime = -100 };
        ep.ProcessingTime.Should().Be(0);
    }

    [Fact]
    public void ProcessingTime_PositiveValue_Preserved()
    {
        var ep = new RouteModel { HttpMethod = "GET", Pattern = "/", ProcessingTime = 500 };
        ep.ProcessingTime.Should().Be(500);
    }
}

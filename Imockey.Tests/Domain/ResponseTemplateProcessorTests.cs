using FluentAssertions;
using Imockey.Domain;
using Xunit;

namespace Imockey.Tests.Domain;

public class ResponseTemplateProcessorTests
{
    // ── {{uuid}} ──────────────────────────────────────────────────────────────

    [Fact]
    public void Process_UuidPlaceholder_ReturnsValidGuid()
    {
        var result = ResponseTemplateProcessor.Process("{{uuid}}", null);
        Guid.TryParse(result, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("{{uuid}}")]
    [InlineData("{{UUID}}")]
    [InlineData("{{Uuid}}")]
    public void Process_UuidPlaceholder_CaseInsensitive(string placeholder)
    {
        var result = ResponseTemplateProcessor.Process(placeholder, null);
        Guid.TryParse(result, out _).Should().BeTrue();
    }

    [Fact]
    public void Process_UuidPlaceholder_MultipleOccurrences_EachDifferent()
    {
        var result = ResponseTemplateProcessor.Process("{{uuid}} {{uuid}}", null);
        var parts = result.Split(' ');
        parts[0].Should().NotBe(parts[1]);
    }

    // ── {{$.path}} ────────────────────────────────────────────────────────────

    [Fact]
    public void Process_JsonPath_SimpleProperty_ReturnsValue()
    {
        var result = ResponseTemplateProcessor.Process("{{$.name}}", @"{""name"":""Alice""}");
        result.Should().Be("Alice");
    }

    [Fact]
    public void Process_JsonPath_NestedProperty_ReturnsValue()
    {
        var result = ResponseTemplateProcessor.Process(
            "{{$.user.city}}",
            @"{""user"":{""city"":""Brasília""}}");
        result.Should().Be("Brasília");
    }

    [Fact]
    public void Process_JsonPath_ArrayAccess_ReturnsValue()
    {
        var result = ResponseTemplateProcessor.Process(
            "{{$.items[0]}}",
            @"{""items"":[""first"",""second""]}");
        result.Should().Be("first");
    }

    [Fact]
    public void Process_JsonPath_NumericValue_ReturnsStringRepresentation()
    {
        var result = ResponseTemplateProcessor.Process("{{$.age}}", @"{""age"":30}");
        result.Should().Be("30");
    }

    [Fact]
    public void Process_JsonPath_MissingProperty_ReturnsEmpty()
    {
        var result = ResponseTemplateProcessor.Process("{{$.missing}}", @"{""name"":""Alice""}");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Process_JsonPath_NullBody_ReturnsPlaceholderUnchanged()
    {
        var result = ResponseTemplateProcessor.Process("{{$.name}}", null);
        result.Should().Be("{{$.name}}");
    }

    [Fact]
    public void Process_JsonPath_EmptyBody_ReturnsPlaceholderUnchanged()
    {
        var result = ResponseTemplateProcessor.Process("{{$.name}}", "");
        result.Should().Be("{{$.name}}");
    }

    [Fact]
    public void Process_JsonPath_InvalidJson_ReturnsPlaceholderUnchanged()
    {
        var result = ResponseTemplateProcessor.Process("{{$.name}}", "not json at all");
        result.Should().Be("{{$.name}}");
    }

    // ── Unknown placeholders ──────────────────────────────────────────────────

    [Fact]
    public void Process_UnknownPlaceholder_KeptAsIs()
    {
        var result = ResponseTemplateProcessor.Process("{{unknown}}", null);
        result.Should().Be("{{unknown}}");
    }

    // ── Template without placeholders ─────────────────────────────────────────

    [Fact]
    public void Process_NoPlaceholders_ReturnsTemplateUnchanged()
    {
        const string template = @"{""message"":""hello world""}";
        ResponseTemplateProcessor.Process(template, null).Should().Be(template);
    }

    // ── Mixed placeholders ────────────────────────────────────────────────────

    [Fact]
    public void Process_MixedPlaceholders_AllResolved()
    {
        var template = @"{""id"":""{{uuid}}"",""name"":""{{$.name}}"",""raw"":""{{unknown}}""}";
        var body = @"{""name"":""Bob""}";

        var result = ResponseTemplateProcessor.Process(template, body);

        result.Should().Contain("\"name\":\"Bob\"");
        result.Should().Contain("\"raw\":\"{{unknown}}\"");
        // uuid part: verify the id field is a valid guid
        var idValue = result.Split('"')[3]; // crude extraction
        Guid.TryParse(idValue, out _).Should().BeTrue();
    }
}

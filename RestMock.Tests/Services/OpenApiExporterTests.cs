using FluentAssertions;
using RestMock.Domain;
using RestMock.Services;

namespace RestMock.Tests.Services;

public class OpenApiExporterTests
{
    private static readonly EndpointModel[] Endpoints =
    {
        new() { HttpMethod = "GET", Pattern = "/v1/heroes/{uuid}" },
    };

    [Fact]
    public void ExportYaml_Defaults_UsesRestMockTitleAndNoServers()
    {
        var yaml = OpenApiExporter.ExportYaml(Endpoints);

        yaml.Should().Contain("title: RestMock");
        yaml.Should().NotContain("servers:");
        yaml.Should().Contain("/v1/heroes/{uuid}:");
    }

    [Fact]
    public void ExportYaml_WithApplication_UsesTitleAndServerUrl()
    {
        var yaml = OpenApiExporter.ExportYaml(Endpoints, "Loja API", "http://localhost:5087/loja");

        yaml.Should().Contain("title: Loja API");
        yaml.Should().Contain("servers:");
        yaml.Should().Contain("url: http://localhost:5087/loja");
    }
}

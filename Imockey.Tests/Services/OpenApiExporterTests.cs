using FluentAssertions;
using Imockey.Domain;
using Imockey.Services;

namespace Imockey.Tests.Services;

public class OpenApiExporterTests
{
    private static readonly RouteModel[] Routes =
    {
        new() { HttpMethod = "GET", Pattern = "/v1/heroes/{uuid}" },
    };

    [Fact]
    public void ExportYaml_Defaults_UsesImockeyTitleAndNoServers()
    {
        var yaml = OpenApiExporter.ExportYaml(Routes);

        yaml.Should().Contain("title: imockey");
        yaml.Should().NotContain("servers:");
        yaml.Should().Contain("/v1/heroes/{uuid}:");
    }

    [Fact]
    public void ExportYaml_WithApplication_UsesTitleAndServerUrl()
    {
        var yaml = OpenApiExporter.ExportYaml(Routes, "Loja API", "http://localhost:5087/loja");

        yaml.Should().Contain("title: Loja API");
        yaml.Should().Contain("servers:");
        yaml.Should().Contain("url: http://localhost:5087/loja");
    }
}

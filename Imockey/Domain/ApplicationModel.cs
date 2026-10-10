namespace Imockey.Domain;

public class ApplicationModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    // First URL segment of every mocked request: GET /{Slug}/v1/products. Globally unique.
    // Built as "{owner user name}-{application slug}", e.g. "phduarte-bff".
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

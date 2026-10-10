using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RestMock.Domain;
using RestMock.Repositories;

namespace RestMock.Services;

public partial class ApplicationService
{
    public const int MaxSlugLength = 50;

    private readonly ApplicationRepository _applications;
    private readonly MockRepository _mocks;

    public ApplicationService(ApplicationRepository applications, MockRepository mocks)
    {
        _applications = applications;
        _mocks = mocks;
    }

    public IReadOnlyList<ApplicationModel> GetByUser(Guid userId) => _applications.GetByUser(userId);

    // Returns null both when the application does not exist and when it belongs to someone else.
    public ApplicationModel? GetOwned(Guid applicationId, Guid userId)
    {
        var application = _applications.GetById(applicationId);
        return application?.UserId == userId ? application : null;
    }

    public int CountMocks(Guid applicationId) => _mocks.CountByApplication(applicationId);

    public (ApplicationModel? Application, string? Error) Create(Guid userId, string name, string? slug, string? description)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return (null, "O nome da aplicação é obrigatório.");

        slug = string.IsNullOrWhiteSpace(slug) ? Slugify(name) : slug.Trim().ToLowerInvariant();

        if (slug.Length == 0 || slug.Length > MaxSlugLength || !SlugRegex().IsMatch(slug))
            return (null, $"O slug deve ter até {MaxSlugLength} caracteres: letras minúsculas, números e '-' (sem '-' no início ou fim).");

        if (ReservedRoutes.Contains(slug))
            return (null, $"O slug '{slug}' é reservado pelo sistema.");

        if (_applications.GetBySlug(slug) is not null)
            return (null, $"O slug '{slug}' já está em uso.");

        var application = new ApplicationModel
        {
            UserId = userId,
            Name = name,
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        };
        _applications.Add(application);
        return (application, null);
    }

    public bool Delete(Guid applicationId, Guid userId) =>
        GetOwned(applicationId, userId) is not null && _applications.Delete(applicationId);

    public static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        }

        var slug = MultipleDashesRegex().Replace(sb.ToString(), "-").Trim('-');
        return slug.Length > MaxSlugLength ? slug[..MaxSlugLength].TrimEnd('-') : slug;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleDashesRegex();
}

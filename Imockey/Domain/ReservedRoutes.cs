namespace Imockey.Domain;

public static class ReservedRoutes
{
    // First path segments owned by the system. They never reach the route dispatcher and cannot be used as application slugs.
    public static readonly IReadOnlySet<string> Segments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "account",
        "apps",
        "signin-github",
        "_blazor",
        "_framework",
        "_content",
        "css",
        "js",
        "favicon.ico",
    };

    public static bool Contains(string segment) => Segments.Contains(segment);
}

using System.Text;
using System.Text.RegularExpressions;

namespace Imockey.Domain;

public class RouteModel
{
    int processtime = 0;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public int StatusCode { get; set; } = 200;
    public string HttpMethod { get; init; } = "GET";
    public string Pattern { get; set; }
    public int ProcessingTime
    {
        get
        {
            return processtime;
        }
        set
        {
            processtime = Math.Max(0, value);
        }
    }
    public object? ResponseBody { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;

    public bool Match(string method, string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (!method.Equals(HttpMethod, StringComparison.OrdinalIgnoreCase)) return false;

        var uri = new Uri(url);
        var qStart = IndexOfQuerySeparator(Pattern);
        var patternPath = qStart >= 0 ? Pattern[..qStart] : Pattern;

        if (!PathMatches(patternPath, uri.LocalPath))
            return false;

        if (qStart < 0)
            return true;

        var patternFields = Pattern[(qStart + 1)..];

        var patternArgs = patternFields.Split('&');

        var requestedArgs = uri.Query.Replace("?", string.Empty).Split('&');

        foreach (var arg in patternArgs)
        {
            var parametro = arg.Split('=')[0];
            var tipo = arg.Split('=')[1].ToLower(); //[string]

            var requestedArg = requestedArgs.FirstOrDefault(a => a.StartsWith(parametro, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(requestedArg)) return false;

            var valor = requestedArg.Split('=')[1];

            if (tipo.StartsWith('['))
            {
                if (tipo.Contains("guid") && !Guid.TryParse(valor, out _))
                {
                    return false;
                }
                else if (tipo.Contains("int") && !int.TryParse(valor, out _))
                {
                    return false;
                }
                else if ((tipo.Contains("date") || tipo.Contains("datetime")) && !DateTime.TryParse(valor, out _))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int IndexOfQuerySeparator(string pattern)
    {
        int depth = 0;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '{') depth++;
            else if (pattern[i] == '}') depth--;
            else if (pattern[i] == '?' && depth == 0) return i;
        }
        return -1;
    }

    private static bool PathMatches(string pattern, string path)
    {
        if (!pattern.Contains('*') && !pattern.Contains('{'))
            return pattern.Equals(path, StringComparison.OrdinalIgnoreCase);

        return BuildPathRegex(pattern).IsMatch(path);
    }

    private static Regex BuildPathRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        int i = 0;
        while (i < pattern.Length)
        {
            char c = pattern[i];
            if (c == '*')
            {
                sb.Append("[^/]*");
                i++;
            }
            else if (c == '{')
            {
                int end = pattern.IndexOf('}', i);
                if (end < 0)
                {
                    sb.Append(Regex.Escape(pattern.Substring(i)));
                    break;
                }
                var raw = pattern.Substring(i + 1, end - i - 1).Trim();
                var optional = raw.EndsWith("?");
                var type = (optional ? raw.Substring(0, raw.Length - 1) : raw).Trim().ToLowerInvariant();
                var typeRegex = TypeToRegex(type);

                if (optional)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] == '/')
                    {
                        sb.Length--;
                        sb.Append("(?:/").Append(typeRegex).Append(")?");
                    }
                    else
                    {
                        sb.Append("(?:").Append(typeRegex).Append(")?");
                    }
                }
                else
                {
                    sb.Append(typeRegex);
                }
                i = end + 1;
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
                i++;
            }
        }
        sb.Append("$");
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase);
    }

    private static string TypeToRegex(string type) => type switch
    {
        "uuid" or "guid" => "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        "int" or "long" or "number" => @"-?\d+",
        "date" => @"\d{4}-\d{2}-\d{2}",
        "datetime" => @"\d{4}-\d{2}-\d{2}[Tt ]\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+\-]\d{2}:?\d{2})?",
        _ => "[^/]+",
    };
}

using System.Text;
using Newtonsoft.Json.Linq;
using RestMock.Domain;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RestMock.Services;

public static class OpenApiExporter
{
    public static string ExportYaml(IEnumerable<EndpointModel> endpoints, string title = "RestMock", string? serverUrl = null)
    {
        var spec = BuildSpec(endpoints.ToList(), title, serverUrl);
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .DisableAliases()
            .Build();
        return serializer.Serialize(spec);
    }

    private static Dictionary<string, object> BuildSpec(List<EndpointModel> endpoints, string title, string? serverUrl)
    {
        var paths = new Dictionary<string, object>();

        foreach (var ep in endpoints)
        {
            var (openApiPath, pathParams, queryParams) = ParsePattern(ep.Pattern);

            if (!paths.TryGetValue(openApiPath, out var pathItem))
            {
                pathItem = new Dictionary<string, object>();
                paths[openApiPath] = pathItem;
            }

            var pathObj = (Dictionary<string, object>)pathItem;
            pathObj[ep.HttpMethod.ToLowerInvariant()] = BuildOperation(ep, pathParams, queryParams);
        }

        var spec = new Dictionary<string, object>
        {
            ["openapi"] = "3.0.3",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = title,
                ["version"] = "1.0.0"
            }
        };

        if (serverUrl is not null)
            spec["servers"] = new List<object> { new Dictionary<string, object> { ["url"] = serverUrl } };

        spec["paths"] = paths;
        return spec;
    }

    private static Dictionary<string, object> BuildOperation(
        EndpointModel ep,
        List<PathParam> pathParams,
        List<QueryParam> queryParams)
    {
        var parameters = new List<object>();

        foreach (var p in pathParams)
        {
            var schema = BuildSchema(p.Type, p.Format);
            parameters.Add(new Dictionary<string, object>
            {
                ["name"] = p.Name,
                ["in"] = "path",
                ["required"] = p.Required,
                ["schema"] = schema
            });
        }

        foreach (var p in queryParams)
        {
            var schema = BuildSchema(p.Type, p.Format);
            parameters.Add(new Dictionary<string, object>
            {
                ["name"] = p.Name,
                ["in"] = "query",
                ["required"] = false,
                ["schema"] = schema
            });
        }

        var operation = new Dictionary<string, object>
        {
            ["summary"] = (string.IsNullOrWhiteSpace(ep.Description)
                ? $"{ep.HttpMethod} {ep.Pattern}"
                : ep.Description)!,
            ["responses"] = new Dictionary<string, object>
            {
                [ep.StatusCode.ToString()] = BuildResponse(ep)
            }
        };

        if (parameters.Count > 0)
            operation["parameters"] = parameters;

        return operation;
    }

    private static Dictionary<string, object> BuildSchema(string type, string? format)
    {
        var schema = new Dictionary<string, object> { ["type"] = type };
        if (format is not null) schema["format"] = format;
        return schema;
    }

    private static Dictionary<string, object> BuildResponse(EndpointModel ep)
    {
        var response = new Dictionary<string, object>
        {
            ["description"] = StatusDescription(ep.StatusCode)
        };

        var bodyStr = ep.ResponseBody?.ToString();
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            object example;
            try
            {
                var token = JToken.Parse(bodyStr);
                example = TokenToPlain(token) ?? bodyStr;
            }
            catch
            {
                example = bodyStr;
            }

            response["content"] = new Dictionary<string, object>
            {
                [ep.ContentType] = new Dictionary<string, object>
                {
                    ["schema"] = new Dictionary<string, object> { ["type"] = "object" },
                    ["example"] = example
                }
            };
        }

        return response;
    }

    private static object? TokenToPlain(JToken? token) => token?.Type switch
    {
        JTokenType.Object => ((JObject)token).Properties()
            .ToDictionary(p => p.Name, p => TokenToPlain(p.Value)!),
        JTokenType.Array => ((JArray)token).Select(t => TokenToPlain(t)!).ToList(),
        JTokenType.Integer => token.Value<long>(),
        JTokenType.Float => token.Value<double>(),
        JTokenType.Boolean => token.Value<bool>(),
        JTokenType.Null => null,
        _ => token!.Value<string>()
    };

    private static (string openApiPath, List<PathParam> pathParams, List<QueryParam> queryParams)
        ParsePattern(string pattern)
    {
        var qSep = IndexOfQuerySeparator(pattern);
        var pathPart = qSep >= 0 ? pattern[..qSep] : pattern;
        var queryPart = qSep >= 0 ? pattern[(qSep + 1)..] : string.Empty;

        var pathParams = new List<PathParam>();
        var sb = new StringBuilder();
        int wildcardCount = 0;
        int i = 0;

        while (i < pathPart.Length)
        {
            char c = pathPart[i];
            if (c == '*')
            {
                wildcardCount++;
                var wName = wildcardCount == 1 ? "wildcard" : $"wildcard{wildcardCount}";
                sb.Append('{').Append(wName).Append('}');
                pathParams.Add(new PathParam(wName, "string", null, true));
                i++;
            }
            else if (c == '{')
            {
                int end = pathPart.IndexOf('}', i);
                if (end < 0) { sb.Append(pathPart[i..]); break; }
                var raw = pathPart[(i + 1)..end].Trim();
                var optional = raw.EndsWith('?');
                var typeName = (optional ? raw[..^1] : raw).ToLowerInvariant();
                var (oaType, oaFormat) = TypeToOpenApi(typeName);
                var paramName = optional ? raw[..^1] : raw;
                sb.Append('{').Append(paramName).Append('}');
                pathParams.Add(new PathParam(paramName, oaType, oaFormat, !optional));
                i = end + 1;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        var queryParams = new List<QueryParam>();
        if (!string.IsNullOrEmpty(queryPart))
        {
            foreach (var part in queryPart.Split('&'))
            {
                var eq = part.IndexOf('=');
                if (eq < 0) continue;
                var name = part[..eq];
                var typeRaw = part[(eq + 1)..].Trim('[', ']').ToLowerInvariant();
                var (oaType, oaFormat) = TypeToOpenApi(typeRaw);
                queryParams.Add(new QueryParam(name, oaType, oaFormat));
            }
        }

        return (sb.ToString(), pathParams, queryParams);
    }

    private static (string type, string? format) TypeToOpenApi(string typeName) => typeName switch
    {
        "uuid" or "guid" => ("string", "uuid"),
        "int" or "long" or "number" => ("integer", null),
        "date" => ("string", "date"),
        "datetime" => ("string", "date-time"),
        _ => ("string", null)
    };

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

    private static string StatusDescription(int code) => code switch
    {
        200 => "OK",
        201 => "Created",
        202 => "Accepted",
        204 => "No Content",
        301 => "Moved Permanently",
        302 => "Found",
        304 => "Not Modified",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        _ => "Response"
    };

    private record PathParam(string Name, string Type, string? Format, bool Required);
    private record QueryParam(string Name, string Type, string? Format);
}

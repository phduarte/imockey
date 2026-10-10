using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Imockey.Domain;

public static class ResponseTemplateProcessor
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    public static string Process(string template, string? requestBody)
    {
        JToken? requestJson = null;
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            try { requestJson = JToken.Parse(requestBody); } catch { }
        }

        return PlaceholderRegex.Replace(template, match =>
        {
            var expression = match.Groups[1].Value.Trim();

            if (expression.Equals("uuid", StringComparison.OrdinalIgnoreCase))
                return Guid.NewGuid().ToString();

            if (expression.StartsWith("$.") && requestJson != null)
            {
                try
                {
                    var token = requestJson.SelectToken(expression);
                    if (token != null)
                        return token.Type == JTokenType.String ? token.Value<string>()! : token.ToString();
                }
                catch { }
                return string.Empty;
            }

            return match.Value;
        });
    }
}

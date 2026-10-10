using RestMock.Domain;
using RestMock.Repositories;

namespace RestMock.Middlewares;

public class RewritterMiddleware
{
    private readonly RequestDelegate _next;

    public RewritterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationRepository applications, MockRepository mocks)
    {
        var (application, endpoint) = FindEndpoint(context.Request, applications, mocks);
        if (application is null)
        {
            await _next(context);
            return;
        }

        // The slug belongs to an application, so this is a mock call: answer 404 instead of falling into the UI (login redirect).
        if (endpoint is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await Task.Delay(endpoint.ProcessingTime);
        context.Response.StatusCode = endpoint.StatusCode;
        context.Response.ContentType = endpoint.ContentType;

        if (endpoint.ResponseBody != null)
        {
            string? requestBody = null;
            if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }

            var responseBody = ResponseTemplateProcessor.Process(endpoint.ResponseBody.ToString()!, requestBody);
            await context.Response.WriteAsync(responseBody);
        }
    }

    // URL layout: /{application slug}/{path matched against the application's patterns}?{query}
    private static (ApplicationModel? Application, EndpointModel? Endpoint) FindEndpoint(
        HttpRequest request, ApplicationRepository applications, MockRepository mocks)
    {
        var path = request.Path.Value ?? string.Empty;
        if (path.Length <= 1)
            return (null, null);

        var slugEnd = path.IndexOf('/', 1);
        var slug = slugEnd < 0 ? path[1..] : path[1..slugEnd];
        if (slug.Length == 0 || ReservedRoutes.Contains(slug))
            return (null, null);

        var application = applications.GetBySlug(slug);
        if (application is null)
            return (null, null);

        var mockPath = slugEnd < 0 ? "/" : path[slugEnd..];
        // Match() only looks at path and query; a fixed authority avoids failures on requests without a Host header.
        var url = $"http://localhost{mockPath}{request.QueryString}";

        return (application, mocks.GetByApplication(application.Id).FirstOrDefault(e => e.Match(request.Method, url)));
    }
}

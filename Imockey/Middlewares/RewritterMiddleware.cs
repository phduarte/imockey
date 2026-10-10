using Imockey.Domain;
using Imockey.Repositories;

namespace Imockey.Middlewares;

public class RewritterMiddleware
{
    private readonly RequestDelegate _next;

    public RewritterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationRepository applications, RouteRepository routes)
    {
        var (application, route) = FindRoute(context.Request, applications, routes);
        if (application is null)
        {
            await _next(context);
            return;
        }

        // The slug belongs to an application, so this is a mocked call: answer 404 instead of falling into the UI (login redirect).
        if (route is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await Task.Delay(route.ProcessingTime);
        context.Response.StatusCode = route.StatusCode;
        context.Response.ContentType = route.ContentType;

        if (route.ResponseBody != null)
        {
            string? requestBody = null;
            if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }

            var responseBody = ResponseTemplateProcessor.Process(route.ResponseBody.ToString()!, requestBody);
            await context.Response.WriteAsync(responseBody);
        }
    }

    // URL layout: /{application slug}/{path matched against the application's patterns}?{query}
    private static (ApplicationModel? Application, RouteModel? Route) FindRoute(
        HttpRequest request, ApplicationRepository applications, RouteRepository routes)
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

        var routePath = slugEnd < 0 ? "/" : path[slugEnd..];
        // Match() only looks at path and query; a fixed authority avoids failures on requests without a Host header.
        var url = $"http://localhost{routePath}{request.QueryString}";

        return (application, routes.GetByApplication(application.Id).FirstOrDefault(e => e.Enabled && e.Match(request.Method, url)));
    }
}

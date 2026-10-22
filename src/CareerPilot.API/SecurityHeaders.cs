namespace CareerPilot.API;

public static class SecurityHeaders
{
    /// <summary>
    /// Headers for every response from the API. The API only ever returns JSON or a file download, so it needs no scripts,
    /// frames or sub-resources at all, and nothing it returns should be cached by a browser or shared proxy.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            h["Cross-Origin-Resource-Policy"] = "same-origin";
            if (!h.ContainsKey("Cache-Control")) h["Cache-Control"] = "no-store";
            return Task.CompletedTask;
        });
        await next();
    });
}

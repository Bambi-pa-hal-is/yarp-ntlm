using System.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        // The legacy app hands the browser its JWT as an HttpOnly cookie, which JavaScript can't read,
        // while the modern app only accepts JWTs in the Authorization header.
        // Every route to a cluster with "JwtCookie" metadata gets the cookie moved into the header,
        // so migrating an endpoint only needs a route pointing at that cluster.
        if (context.Cluster?.Metadata?.TryGetValue("JwtCookie", out var cookieName) != true || string.IsNullOrEmpty(cookieName))
        {
            return;
        }

        context.AddRequestTransform(transformContext =>
        {
            var token = transformContext.HttpContext.Request.Cookies[cookieName];
            var authorization = transformContext.ProxyRequest.Headers.Authorization;

            // Keep an explicit bearer token, but replace anything else, such as a leftover NTLM/Negotiate header.
            if (!string.IsNullOrEmpty(token) && !string.Equals(authorization?.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return ValueTask.CompletedTask;
        });
    })
    // Must be last: wraps the HTTP client factory configured above.
    .AddNtlmSupport();

var app = builder.Build();

// Lets the legacy app's Windows authentication (NTLM/Kerberos) work through the proxy.
app.SupportNtlm();
app.MapReverseProxy();

app.Run();

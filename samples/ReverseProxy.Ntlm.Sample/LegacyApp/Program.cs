using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Yarp.Sample.Ntlm.LegacyApp.Data;
using Yarp.Sample.Ntlm.LegacyApp.Security;

var builder = WebApplication.CreateBuilder(args);

// Windows authentication. Under IIS Express this defers to IIS (windowsAuthentication in launchSettings.json);
// under Kestrel the Negotiate handler does the NTLM/Kerberos handshake itself.
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.Configure<RoleMappingOptions>(builder.Configuration.GetSection("Authorization"));
builder.Services.AddTransient<IClaimsTransformation, WindowsGroupRoleTransformation>();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
    // RequireClaim rather than RequireRole: WindowsPrincipal.IsInRole would first look the name up as a Windows group.
    options.AddPolicy(AppRoles.CaseOfficer,
        policy => policy.RequireClaim(ClaimTypes.Role, AppRoles.CaseOfficer, AppRoles.Admin));
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<JwtIssuer>();

builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/Residents", AppRoles.CaseOfficer));
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

// The app runs behind the gateway, so trust its X-Forwarded-Proto to decide whether cookies are Secure.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), pages =>
{
    pages.UseStatusCodePagesWithReExecute("/status/{0}");
    pages.Use(async (context, next) =>
    {
        await next(context);

        // 401s are steps of the NTLM/Kerberos handshake. Re-executing them would run authentication again
        // in the middle of the handshake ("Attempting to use an incomplete authentication context").
        if (context.Response.StatusCode == StatusCodes.Status401Unauthorized
            && context.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
        {
            statusCodePages.Enabled = false;
        }
    });
});
app.UseStaticFiles();
// After the status code pages, so the re-executed /status/{code} request is routed.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Exchanges the Windows identity for a JWT the modern app understands.
// The token is stored in an HttpOnly cookie; the gateway moves it into the Authorization header for the modern app.
app.MapGet("/api/users/me", (HttpContext context, JwtIssuer issuer, IOptions<JwtOptions> options) =>
{
    var token = issuer.Issue(context.User);
    var employee = Employees.Find(token.UserName);

    context.Response.Cookies.Append(options.Value.CookieName, token.Token, new CookieOptions
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = token.ExpiresAt,
    });

    return Results.Ok(new
    {
        account = token.UserName,
        name = employee.DisplayName,
        department = employee.Department,
        title = employee.Title,
        roles = token.Roles,
        expiresAt = token.ExpiresAt,
    });
});

// Legacy JSON API that hasn't been migrated yet.
app.MapGet("/api/cases", (string? status) => CaseRegistry.All
    .Where(c => status is null || string.Equals(c.Status, status, StringComparison.OrdinalIgnoreCase))
    .Select(c => new { c.Number, c.Title, c.Type, c.Department, c.Status, c.Received, c.Officer }));

app.Run();

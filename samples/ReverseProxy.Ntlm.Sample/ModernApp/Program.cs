using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Yarp.Sample.Ntlm.ModernApp.Data;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt");

// Validates the JWTs issued by the legacy app. The gateway moves them from the cookie into the Authorization header.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Employee", policy => policy.RequireRole("Employee", "Admin"))
    .AddPolicy("PermitOfficer", policy => policy.RequireRole("PermitOfficer"))
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"));

builder.Services.AddSingleton<PermitStore>();

var app = builder.Build();

// The React app, built by ModernApp.csproj into ClientApp/dist (see the comment there).
var clientApp = Path.Combine(app.Environment.ContentRootPath, "ClientApp", "dist");
var clientAppFiles = Directory.Exists(clientApp) ? new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(clientApp),
    RequestPath = "/app",
} : null;

if (clientAppFiles is not null)
{
    app.UseStaticFiles(clientAppFiles);
}

app.UseAuthentication();
app.UseAuthorization();

// Endpoints that have been migrated from the legacy app. They keep the URLs they had in the legacy app,
// so the gateway can move them one at a time: each needs a route to the "modern" cluster in Gateway/appsettings.json.
var api = app.MapGroup("/api").RequireAuthorization("Employee");

api.MapGet("/permits", (PermitStore store, string? status) => store.GetAll()
    .Where(permit => status is null || string.Equals(permit.Status, status, StringComparison.OrdinalIgnoreCase))
    .Select(permit => new { permit.Id, permit.Type, permit.Address, permit.Applicant, permit.Submitted, permit.Status, permit.Officer }));

api.MapGet("/permits/{id}", (PermitStore store, string id) =>
    store.Find(id) is { } permit ? Results.Ok(permit) : Results.NotFound());

api.MapPost("/permits/{id}/decision", (PermitStore store, ClaimsPrincipal user, string id, PermitDecision decision) =>
{
    if (!PermitStatus.Decisions.Contains(decision.Decision))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["decision"] = [$"Must be one of: {string.Join(", ", PermitStatus.Decisions)}."],
        });
    }

    return store.Decide(id, decision, user.Identity!.Name!) is { } permit ? Results.Ok(permit) : Results.NotFound();
}).RequireAuthorization("PermitOfficer");

api.MapGet("/reports/permits", (PermitStore store) =>
{
    var permits = store.GetAll();
    var decided = permits.Where(permit => permit.Status is PermitStatus.Approved or PermitStatus.Rejected).ToList();

    return new
    {
        total = permits.Count,
        byStatus = permits.GroupBy(permit => permit.Status).ToDictionary(group => group.Key, group => group.Count()),
        byType = permits.GroupBy(permit => permit.Type).ToDictionary(group => group.Key, group => group.Count()),
        approvalRate = decided.Count == 0 ? 0 : (double)decided.Count(permit => permit.Status == PermitStatus.Approved) / decided.Count,
        feesInvoiced = permits.Where(permit => permit.Status == PermitStatus.Approved).Sum(permit => permit.Fee),
    };
}).RequireAuthorization("Admin");

// The React app's client-side routes. Anonymous: it signs in by calling the legacy /api/users/me.
// "nonfile" leaves requests for files (e.g. /app/assets/index.js) to the static file middleware.
if (clientAppFiles is not null)
{
    // No RequestPath here: the fallback rewrites the request to /index.html before serving it.
    app.MapFallbackToFile("/app/{*path:nonfile}", "index.html", new StaticFileOptions { FileProvider = clientAppFiles.FileProvider });
}
else
{
    app.MapGet("/app/{**path}", () => Results.Content(
        "The React app hasn't been built. Install Node.js (https://nodejs.org) and rebuild ModernApp.", statusCode: 503));
}
app.MapGet("/", () => Results.Redirect("/app/"));

app.Run();

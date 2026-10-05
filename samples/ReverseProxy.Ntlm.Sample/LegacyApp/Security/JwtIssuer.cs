using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Yarp.Sample.Ntlm.LegacyApp.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";

    public string Audience { get; set; } = "";

    /// <summary>
    /// Shared with the modern app, which validates the tokens. At least 32 bytes.
    /// </summary>
    public string SigningKey { get; set; } = "";

    public int LifetimeMinutes { get; set; } = 30;

    public string CookieName { get; set; } = "access_token";
}

public sealed record IssuedToken(string Token, string UserName, IReadOnlyList<string> Roles, DateTimeOffset ExpiresAt);

/// <summary>
/// Creates JWTs for Windows-authenticated users, containing the application roles
/// added by <see cref="WindowsGroupRoleTransformation"/>.
/// </summary>
public sealed class JwtIssuer
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtIssuer(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        var key = Encoding.UTF8.GetBytes(_options.SigningKey);
        if (key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
        }

        _signingCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
    }

    public IssuedToken Issue(ClaimsPrincipal user)
    {
        var userName = user.Identity?.Name ?? throw new InvalidOperationException("The user is not authenticated.");
        var roles = user.GetRoles();
        var employee = Data.Employees.Find(userName);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userName),
            new(JwtRegisteredClaimNames.Name, employee.DisplayName),
            new("department", employee.Department),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.LifetimeMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
        });

        return new IssuedToken(token, userName, roles, expiresAt);
    }
}

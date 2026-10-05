using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Yarp.Sample.Ntlm.LegacyApp.Security;

public sealed class RoleMappingOptions
{
    /// <summary>
    /// Windows/AD groups that grant application roles. A group can grant several roles.
    /// </summary>
    public List<RoleMapping> RoleMappings { get; set; } = [];
}

public sealed class RoleMapping
{
    /// <summary>
    /// Windows group name, e.g. "EKDALA\SG-Building-Permits", or group SID, e.g. "S-1-5-32-545".
    /// Prefer SIDs for built-in groups, whose names depend on the Windows display language.
    /// </summary>
    public string Group { get; set; } = "";

    public string Role { get; set; } = "";
}

/// <summary>
/// Adds application role claims to Windows-authenticated users based on their Windows group membership.
/// </summary>
public sealed class WindowsGroupRoleTransformation : IClaimsTransformation
{
    private const string AuthenticationType = "ApplicationRoles";

    private readonly RoleMappingOptions _options;

    public WindowsGroupRoleTransformation(IOptions<RoleMappingOptions> options)
    {
        _options = options.Value;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Transformations can run more than once per request.
        if (principal.Identity?.IsAuthenticated != true || principal.Identities.Any(identity => identity.AuthenticationType == AuthenticationType))
        {
            return Task.FromResult(principal);
        }

        var roles = _options.RoleMappings
            .Where(mapping => IsInGroup(principal, mapping.Group))
            .Select(mapping => mapping.Role)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(role => new Claim(ClaimTypes.Role, role));

        principal.AddIdentity(new ClaimsIdentity(roles, AuthenticationType, ClaimTypes.Name, ClaimTypes.Role));
        return Task.FromResult(principal);
    }

    private static bool IsInGroup(ClaimsPrincipal user, string group)
    {
        // SIDs work regardless of the Windows display language. Deny-only groups, such as Administrators
        // for a non-elevated UAC token, are not included in the GroupSid claims.
        if (group.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
        {
            return user.HasClaim(ClaimTypes.GroupSid, group) || user.HasClaim(ClaimTypes.PrimarySid, group);
        }

        // WindowsPrincipal.IsInRole checks Windows group membership by name.
        return user.IsInRole(group);
    }
}

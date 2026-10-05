using System.Security.Claims;

namespace Yarp.Sample.Ntlm.LegacyApp.Security;

/// <summary>
/// Application roles, granted through Windows group membership (see <see cref="RoleMappingOptions"/>).
/// The same roles are put in the JWT for the modern app.
/// </summary>
public static class AppRoles
{
    public const string Employee = "Employee";
    public const string CaseOfficer = "CaseOfficer";
    public const string PermitOfficer = "PermitOfficer";
    public const string Admin = "Admin";

    public static bool HasRole(this ClaimsPrincipal user, string role) => user.HasClaim(ClaimTypes.Role, role);

    public static IReadOnlyList<string> GetRoles(this ClaimsPrincipal user) =>
        user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

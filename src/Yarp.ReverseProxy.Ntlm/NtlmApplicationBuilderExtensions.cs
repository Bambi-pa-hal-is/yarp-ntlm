using System;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Ntlm;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extensions for adding NTLM and Kerberos (Negotiate) support to the request pipeline.
/// </summary>
public static class NtlmApplicationBuilderExtensions
{
    /// <summary>
    /// Enables proxying NTLM and Kerberos (Negotiate) authentication to destination servers.
    /// Client connections that authenticate with NTLM or Negotiate get their own dedicated destination connections,
    /// so the connection-bound authentication state is preserved and never shared with other clients.
    /// Must be called before <c>MapReverseProxy()</c>, and requires <c>AddReverseProxy().AddNtlmSupport()</c>.
    /// </summary>
    public static IApplicationBuilder SupportNtlm(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.ApplicationServices.GetService<NtlmConnectionTracker>() is null)
        {
            throw new InvalidOperationException("Unable to find the required services. Add them by calling 'services.AddReverseProxy().AddNtlmSupport()'.");
        }

        return app.UseMiddleware<NtlmConnectionMiddleware>();
    }
}

using System;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Ntlm;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for registering NTLM and Kerberos (Negotiate) support for the reverse proxy.
/// </summary>
public static class NtlmReverseProxyBuilderExtensions
{
    /// <summary>
    /// Registers the services needed to proxy NTLM and Kerberos (Negotiate) authentication to destination servers.
    /// Also call <c>app.SupportNtlm()</c> before mapping the reverse proxy.
    /// </summary>
    /// <remarks>
    /// This wraps the currently registered <see cref="IForwarderHttpClientFactory"/>, so call it after any
    /// customizations of the factory, such as <c>ConfigureHttpClient</c>.
    /// </remarks>
    public static IReverseProxyBuilder AddNtlmSupport(this IReverseProxyBuilder builder, Action<NtlmSupportOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        if (configure is not null)
        {
            services.Configure(configure);
        }

        for (var i = 0; i < services.Count; i++)
        {
            if (services[i].ServiceType == typeof(NtlmConnectionTracker))
            {
                // Already registered.
                return builder;
            }
        }

        var index = -1;
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IForwarderHttpClientFactory) && !services[i].IsKeyedService)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            throw new InvalidOperationException($"No {nameof(IForwarderHttpClientFactory)} is registered. Call AddReverseProxy() before AddNtlmSupport().");
        }

        var innerDescriptor = services[index];
        services[index] = ServiceDescriptor.Singleton<IForwarderHttpClientFactory>(serviceProvider =>
            new NtlmForwarderHttpClientFactory(
                CreateInnerFactory(serviceProvider, innerDescriptor),
                serviceProvider.GetRequiredService<ILogger<NtlmForwarderHttpClientFactory>>()));

        services.AddSingleton<NtlmConnectionTracker>();

        return builder;
    }

    private static IForwarderHttpClientFactory CreateInnerFactory(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return (IForwarderHttpClientFactory)descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IForwarderHttpClientFactory)descriptor.ImplementationFactory(serviceProvider);
        }

        return (IForwarderHttpClientFactory)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!);
    }
}

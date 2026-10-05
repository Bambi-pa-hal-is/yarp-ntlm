// Based on TestEnvironment from YARP's functional tests (https://github.com/dotnet/yarp).
// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Ntlm.Tests;

/// <summary>
/// Runs a destination server and a YARP proxy in front of it on loopback ports.
/// </summary>
public class TestEnvironment
{
    private static readonly Lazy<X509Certificate2> Certificate = new(CreateCertificate);

    public ITestOutputHelper? TestOutput { get; set; }

    public HttpProtocols ProxyProtocol { get; set; } = HttpProtocols.Http1AndHttp2;

    public bool UseHttpsOnProxy { get; set; }

    public Action<IReverseProxyBuilder> ConfigureProxy { get; set; } = _ => { };

    public Action<IApplicationBuilder> ConfigureProxyApp { get; set; } = _ => { };

    public HttpProtocols DestinationProtocol { get; set; } = HttpProtocols.Http1AndHttp2;

    public Action<IApplicationBuilder> ConfigureDestinationApp { get; set; } = _ => { };

    public TestEnvironment(RequestDelegate destinationGetDelegate)
    {
        ConfigureDestinationApp = destinationApp => destinationApp.Run(destinationGetDelegate);
    }

    public async Task Invoke(Func<string, Task> clientFunc, CancellationToken cancellationToken = default)
    {
        using var destination = CreateHost(DestinationProtocol, useHttps: false, _ => { }, ConfigureDestinationApp);
        await destination.StartAsync(cancellationToken);

        Exception? proxyException = null;
        using var proxy = CreateProxy(GetAddress(destination), ex => proxyException = ex);
        await proxy.StartAsync(cancellationToken);

        try
        {
            await clientFunc(GetAddress(proxy));
        }
        finally
        {
            await proxy.StopAsync(cancellationToken);
            await destination.StopAsync(cancellationToken);
        }

        Assert.Null(proxyException);
    }

    private IHost CreateProxy(string destinationAddress, Action<Exception> onProxyException)
    {
        return CreateHost(ProxyProtocol, UseHttpsOnProxy,
            services =>
            {
                var route = new RouteConfig
                {
                    RouteId = "route1",
                    ClusterId = "cluster1",
                    Match = new RouteMatch { Path = "/{**catchall}" }
                };

                var cluster = new ClusterConfig
                {
                    ClusterId = "cluster1",
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1", new DestinationConfig { Address = destinationAddress } }
                    }
                };

                var proxyBuilder = services.AddReverseProxy().LoadFromMemory(new[] { route }, new[] { cluster });
                ConfigureProxy(proxyBuilder);
            },
            app =>
            {
                app.Use(async (context, next) =>
                {
                    try
                    {
                        await next();
                    }
                    catch (Exception ex)
                    {
                        onProxyException(ex);
                        throw;
                    }
                });

                ConfigureProxyApp(app);
                app.UseRouting();
                app.UseEndpoints(builder => builder.MapReverseProxy());
            });
    }

    private IHost CreateHost(HttpProtocols protocols, bool useHttps,
        Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configureApp)
    {
        return new HostBuilder()
            .ConfigureLogging(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                if (TestOutput is not null)
                {
                    loggingBuilder.AddProvider(new TestOutputLoggerProvider(TestOutput));
                }
            })
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        configureServices(services);
                    })
                    .UseKestrel(kestrel =>
                    {
                        kestrel.Listen(IPAddress.Loopback, 0, listenOptions =>
                        {
                            listenOptions.Protocols = protocols;
                            if (useHttps)
                            {
                                listenOptions.UseHttps(Certificate.Value);
                            }
                        });
                    })
                    .Configure(configureApp);
            }).Build();
    }

    private static string GetAddress(IHost host)
    {
        return host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, critical: false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        // SslStream on Windows can't use the ephemeral key of a certificate created in memory, so round-trip through PFX.
        var pfx = certificate.Export(X509ContentType.Pfx);
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(pfx, password: null);
#else
        return new X509Certificate2(pfx);
#endif
    }

    private sealed class TestOutputLoggerProvider(ITestOutputHelper output) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestOutputLogger(output, categoryName);

        public void Dispose() { }
    }

    private sealed class TestOutputLogger(ITestOutputHelper output, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                output.WriteLine($"{logLevel} {categoryName}: {formatter(state, exception)}{(exception is null ? "" : Environment.NewLine + exception)}");
            }
            catch (InvalidOperationException)
            {
                // Logged after the test finished.
            }
        }
    }
}

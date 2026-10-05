using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Ntlm;

/// <summary>
/// Sits in front of a cluster's <see cref="HttpMessageInvoker"/> and sends requests from client connections
/// that use NTLM or Negotiate over dedicated destination connections instead of the shared connection pool.
/// </summary>
internal sealed class NtlmPinningHandler : HttpMessageHandler
{
    // https://www.rfc-editor.org/rfc/rfc9113#name-error-codes
    private const int Http11Required = 0xd;

    private readonly IForwarderHttpClientFactory _innerFactory;
    private readonly string _clusterId;
    private readonly HttpClientConfig _config;
    private readonly IReadOnlyDictionary<string, string>? _metadata;
    private readonly ILogger _logger;

    public NtlmPinningHandler(HttpMessageInvoker sharedClient, IForwarderHttpClientFactory innerFactory, ForwarderHttpClientContext context, ILogger logger)
    {
        SharedClient = sharedClient;
        _innerFactory = innerFactory;
        _clusterId = context.ClusterId;
        _config = context.NewConfig ?? HttpClientConfig.Empty;
        _metadata = context.NewMetadata;
        _logger = logger;
    }

    /// <summary>
    /// The cluster's client created by the inner factory, used by all requests that don't need a dedicated connection.
    /// </summary>
    public HttpMessageInvoker SharedClient { get; }

    /// <summary>
    /// Creates a client that is only used by a single client connection.
    /// </summary>
    public HttpMessageInvoker CreateDedicatedClient()
    {
        return _innerFactory.CreateClient(new ForwarderHttpClientContext
        {
            ClusterId = _clusterId,
            OldConfig = HttpClientConfig.Empty,
            NewConfig = _config with
            {
                // One connection per destination, so the authenticated connection is always the one being reused.
                MaxConnectionsPerServer = 1,
                EnableMultipleHttp2Connections = false,
            },
            NewMetadata = _metadata,
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var scope = NtlmRequestScope.Current;

        if (scope?.Connection is { } connection)
        {
            // NTLM and Kerberos don't work over HTTP/2. Extended CONNECT (WebSockets over HTTP/2) is left alone.
            if (request.Version.Major >= 2 && request.Method != HttpMethod.Connect)
            {
                request.Version = HttpVersion.Version11;
                request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            }

            return await connection.GetClient(this).SendAsync(request, cancellationToken);
        }

        var response = await SharedClient.SendAsync(request, cancellationToken);

        if (scope is not null
            && scope.Options.RequireHttp11ForChallenges
            && !HttpProtocol.IsHttp11(scope.HttpContext.Request.Protocol)
            && !HttpProtocol.IsHttp10(scope.HttpContext.Request.Protocol)
            && IsConnectionBasedChallenge(response))
        {
            var resetFeature = scope.HttpContext.Features.Get<IHttpResetFeature>();
            if (resetFeature is not null)
            {
                Log.RequireHttp11(_logger, scope.HttpContext.Request.Protocol, _clusterId);
                resetFeature.Reset(Http11Required);
            }
        }

        return response;
    }

    private static bool IsConnectionBasedChallenge(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized
            || !response.Headers.NonValidated.TryGetValues("WWW-Authenticate", out var values))
        {
            return false;
        }

        foreach (var value in values)
        {
            if (NtlmConnectionMiddleware.IsConnectionBasedScheme(value))
            {
                return true;
            }
        }

        return false;
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, string, Exception?> _requireHttp11 = LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(2, "RequireHttp11"),
            "Cluster '{clusterId}' requested NTLM or Negotiate authentication for a {protocol} request. Resetting the request with HTTP_1_1_REQUIRED.");

        public static void RequireHttp11(ILogger logger, string protocol, string clusterId)
        {
            _requireHttp11(logger, clusterId, protocol, null);
        }
    }
}

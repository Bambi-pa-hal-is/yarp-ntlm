using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Ntlm;

/// <summary>
/// Decorates the registered <see cref="IForwarderHttpClientFactory"/> so that every cluster client
/// is wrapped in a <see cref="NtlmPinningHandler"/>.
/// </summary>
internal sealed class NtlmForwarderHttpClientFactory : IForwarderHttpClientFactory
{
    private readonly IForwarderHttpClientFactory _inner;
    private readonly ILogger<NtlmForwarderHttpClientFactory> _logger;

    public NtlmForwarderHttpClientFactory(IForwarderHttpClientFactory inner, ILogger<NtlmForwarderHttpClientFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _logger = logger;
    }

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        // The inner factory only knows about the clients it created itself.
        var oldClient = context.OldClient as NtlmHttpMessageInvoker;

        var sharedClient = _inner.CreateClient(new ForwarderHttpClientContext
        {
            ClusterId = context.ClusterId,
            OldConfig = context.OldConfig,
            OldMetadata = context.OldMetadata,
            OldClient = oldClient?.Handler.SharedClient ?? context.OldClient,
            NewConfig = context.NewConfig,
            NewMetadata = context.NewMetadata,
        });

        if (oldClient is not null && ReferenceEquals(sharedClient, oldClient.Handler.SharedClient))
        {
            return oldClient;
        }

        return new NtlmHttpMessageInvoker(new NtlmPinningHandler(sharedClient, _inner, context, _logger));
    }

    private sealed class NtlmHttpMessageInvoker : HttpMessageInvoker
    {
        // The handler isn't disposed: like any other cluster client, it may still be in use by in-flight requests.
        public NtlmHttpMessageInvoker(NtlmPinningHandler handler) : base(handler, disposeHandler: false)
        {
            Handler = handler;
        }

        public NtlmPinningHandler Handler { get; }
    }
}

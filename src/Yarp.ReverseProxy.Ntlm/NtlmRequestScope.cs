using System.Threading;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Ntlm;

/// <summary>
/// Flows the incoming request's state from <see cref="NtlmConnectionMiddleware"/> to <see cref="NtlmPinningHandler"/>,
/// which runs inside the forwarder's call to <see cref="System.Net.Http.HttpMessageInvoker.SendAsync"/>.
/// </summary>
internal sealed class NtlmRequestScope
{
    private static readonly AsyncLocal<NtlmRequestScope?> _current = new();

    public NtlmRequestScope(HttpContext httpContext, NtlmProxyConnection? connection, NtlmSupportOptions options)
    {
        HttpContext = httpContext;
        Connection = connection;
        Options = options;
    }

    public static NtlmRequestScope? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public HttpContext HttpContext { get; }

    /// <summary>
    /// The dedicated destination connections of the client connection, or null if the request should use the shared pool.
    /// </summary>
    public NtlmProxyConnection? Connection { get; }

    public NtlmSupportOptions Options { get; }
}

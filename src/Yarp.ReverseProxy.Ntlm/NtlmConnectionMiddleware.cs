using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Ntlm;

/// <summary>
/// Associates each HTTP/1.x client connection that uses NTLM or Negotiate with its own dedicated destination connections.
/// </summary>
internal sealed class NtlmConnectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly NtlmConnectionTracker _tracker;
    private readonly ILogger<NtlmConnectionMiddleware> _logger;

    public NtlmConnectionMiddleware(RequestDelegate next, NtlmConnectionTracker tracker, ILogger<NtlmConnectionMiddleware> logger)
    {
        _next = next;
        _tracker = tracker;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        NtlmProxyConnection? connection = null;

        var connectionId = context.Connection.Id;
        if (IsHttp1(context.Request.Protocol) && !string.IsNullOrEmpty(connectionId))
        {
            // Once a client connection has started authenticating, all of its following requests
            // must keep using the same destination connection, with or without an Authorization header.
            connection = _tracker.TryEnterExisting(connectionId);
            if (connection is null && HasConnectionBasedAuthorization(context.Request.Headers.Authorization))
            {
                connection = _tracker.EnterOrCreate(connectionId);
                Log.ConnectionPinned(_logger, connectionId);
            }
        }

        var previous = NtlmRequestScope.Current;
        NtlmRequestScope.Current = new NtlmRequestScope(context, connection, _tracker.Options);
        try
        {
            await _next(context);
        }
        finally
        {
            NtlmRequestScope.Current = previous;
            connection?.Exit();
        }
    }

    private static bool IsHttp1(string protocol)
    {
        return HttpProtocol.IsHttp11(protocol) || HttpProtocol.IsHttp10(protocol);
    }

    internal static bool HasConnectionBasedAuthorization(Microsoft.Extensions.Primitives.StringValues values)
    {
        foreach (var value in values)
        {
            if (IsConnectionBasedScheme(value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether an Authorization or WWW-Authenticate header value uses the NTLM or Negotiate scheme.
    /// </summary>
    internal static bool IsConnectionBasedScheme(string? value)
    {
        var span = value.AsSpan().TrimStart();
        return StartsWithScheme(span, "NTLM") || StartsWithScheme(span, "Negotiate");
    }

    private static bool StartsWithScheme(ReadOnlySpan<char> value, string scheme)
    {
        return value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
            && (value.Length == scheme.Length || value[scheme.Length] is ' ' or ',');
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _connectionPinned = LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, "ConnectionPinned"),
            "Client connection '{connectionId}' uses NTLM or Negotiate authentication and will be proxied over dedicated destination connections.");

        public static void ConnectionPinned(ILogger logger, string connectionId)
        {
            _connectionPinned(logger, connectionId, null);
        }
    }
}

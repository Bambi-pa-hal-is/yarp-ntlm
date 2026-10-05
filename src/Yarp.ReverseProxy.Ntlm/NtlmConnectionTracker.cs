using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Options;

namespace Yarp.ReverseProxy.Ntlm;

/// <summary>
/// Keeps track of the client connections that use NTLM or Negotiate and closes their dedicated
/// destination connections once they have been idle for <see cref="NtlmSupportOptions.ConnectionIdleTimeout"/>.
/// </summary>
internal sealed class NtlmConnectionTracker : IDisposable
{
    private readonly ConcurrentDictionary<string, NtlmProxyConnection> _connections = new(StringComparer.Ordinal);
    private readonly long _idleTimeoutMs;
    private readonly Timer _timer;

    public NtlmConnectionTracker(IOptions<NtlmSupportOptions> options)
    {
        Options = options.Value;
        _idleTimeoutMs = (long)Options.ConnectionIdleTimeout.TotalMilliseconds;

        var period = TimeSpan.FromMilliseconds(Math.Clamp(_idleTimeoutMs / 4, 1_000, 30_000));
        _timer = new Timer(static state => ((NtlmConnectionTracker)state!).RemoveIdleConnections(), this, period, period);
    }

    public NtlmSupportOptions Options { get; }

    internal int Count => _connections.Count;

    /// <summary>
    /// Returns the tracked connection for the client connection, or null if it hasn't used NTLM or Negotiate yet.
    /// The returned connection has been entered and must be exited by the caller.
    /// </summary>
    public NtlmProxyConnection? TryEnterExisting(string connectionId)
    {
        while (_connections.TryGetValue(connectionId, out var connection))
        {
            if (connection.TryEnter())
            {
                return connection;
            }

            // Disposed by the idle timer, but not yet removed.
            _connections.TryRemove(new(connectionId, connection));
        }

        return null;
    }

    /// <summary>
    /// Returns the tracked connection for the client connection, creating it if needed.
    /// The returned connection has been entered and must be exited by the caller.
    /// </summary>
    public NtlmProxyConnection EnterOrCreate(string connectionId)
    {
        while (true)
        {
            var connection = _connections.GetOrAdd(connectionId, static id => new NtlmProxyConnection(id));
            if (connection.TryEnter())
            {
                return connection;
            }

            _connections.TryRemove(new(connectionId, connection));
        }
    }

    internal void RemoveIdleConnections()
    {
        var now = Environment.TickCount64;
        foreach (var (connectionId, connection) in _connections)
        {
            if (connection.TryDisposeIfIdle(now, _idleTimeoutMs))
            {
                _connections.TryRemove(new(connectionId, connection));
            }
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }
        _connections.Clear();
    }
}

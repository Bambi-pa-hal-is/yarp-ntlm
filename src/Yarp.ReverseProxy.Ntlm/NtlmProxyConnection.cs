using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Yarp.ReverseProxy.Ntlm;

/// <summary>
/// The destination connections dedicated to a single client connection that uses NTLM or Negotiate.
/// NTLM and Kerberos authenticate the TCP connection rather than the individual request, so every request
/// from the client connection has to be sent over the same destination connection, and that destination
/// connection must never be shared with another client.
/// </summary>
internal sealed class NtlmProxyConnection : IDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<NtlmPinningHandler, HttpMessageInvoker> _clients = new();
    private int _activeRequests;
    private long _lastActivity = Environment.TickCount64;
    private bool _disposed;

    public NtlmProxyConnection(string connectionId)
    {
        ConnectionId = connectionId;
    }

    public string ConnectionId { get; }

    /// <summary>
    /// Marks the start of a request. Returns false if the connection has already been disposed.
    /// </summary>
    public bool TryEnter()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return false;
            }

            _activeRequests++;
            _lastActivity = Environment.TickCount64;
            return true;
        }
    }

    public void Exit()
    {
        lock (_lock)
        {
            _activeRequests--;
            _lastActivity = Environment.TickCount64;
        }
    }

    /// <summary>
    /// Gets the dedicated client for the given cluster handler, creating it on first use.
    /// </summary>
    public HttpMessageInvoker GetClient(NtlmPinningHandler handler)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_clients.TryGetValue(handler, out var client))
            {
                client = handler.CreateDedicatedClient();
                _clients.Add(handler, client);
            }

            return client;
        }
    }

    /// <summary>
    /// Disposes the connection if no requests are in flight and it has been idle for longer than <paramref name="idleTimeoutMs"/>.
    /// </summary>
    public bool TryDisposeIfIdle(long now, long idleTimeoutMs)
    {
        lock (_lock)
        {
            if (_disposed || _activeRequests > 0 || now - _lastActivity < idleTimeoutMs)
            {
                return false;
            }

            DisposeCore();
            return true;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                DisposeCore();
            }
        }
    }

    private void DisposeCore()
    {
        _disposed = true;
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();
    }
}

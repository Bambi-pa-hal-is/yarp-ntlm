using System;

namespace Yarp.ReverseProxy.Ntlm;

/// <summary>
/// Options for proxying connection-based Windows authentication (NTLM and Kerberos/Negotiate).
/// </summary>
public sealed class NtlmSupportOptions
{
    private TimeSpan _connectionIdleTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long a dedicated destination connection is kept for a client connection that has not sent any requests.
    /// Once expired, the destination connection is closed and the client has to authenticate again.
    /// Should be at least as long as the server's keep-alive timeout (Kestrel defaults to 130 seconds).
    /// Defaults to 2 minutes.
    /// </summary>
    public TimeSpan ConnectionIdleTimeout
    {
        get => _connectionIdleTimeout;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _connectionIdleTimeout = value;
        }
    }

    /// <summary>
    /// NTLM and Kerberos are bound to a connection and cannot be used over HTTP/2 or HTTP/3.
    /// When enabled, a request arriving over HTTP/2 that receives an NTLM/Negotiate challenge from the destination
    /// is reset with HTTP_1_1_REQUIRED, which makes clients such as browsers and HttpClient retry over HTTP/1.1.
    /// Defaults to true.
    /// </summary>
    public bool RequireHttp11ForChallenges { get; set; } = true;
}

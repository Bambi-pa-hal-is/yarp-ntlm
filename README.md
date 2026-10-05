# Bambi.Yarp.Ntlm

An extension for [YARP](https://github.com/dotnet/yarp) (Yet Another Reverse Proxy) that lets destination servers use Windows authentication (NTLM and Kerberos/Negotiate) through the proxy.

> This is a community package. It is not made or supported by Microsoft or the YARP team.

NTLM and Kerberos authenticate the TCP connection rather than each request. YARP normally sends requests from all clients over a shared pool of destination connections, which breaks the NTLM handshake and could let one client's authenticated connection be reused by another client. With this package, each client connection that sends an `Authorization: NTLM ...` or `Authorization: Negotiate ...` header gets its own dedicated destination connection, used for every following request on that client connection.

## Install

```
dotnet add package Bambi.Yarp.Ntlm
```

It requires `Yarp.ReverseProxy` 2.3.0 or later and .NET 8 or later.

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddNtlmSupport();

var app = builder.Build();

app.SupportNtlm();
app.MapReverseProxy();

app.Run();
```

## Notes

- Call `AddNtlmSupport()` after any other customization of the `IForwarderHttpClientFactory` (for example `ConfigureHttpClient`).
- Call `app.SupportNtlm()` before `app.MapReverseProxy()`.
- NTLM and Kerberos don't work over HTTP/2 or HTTP/3. Authenticated requests are sent to the destination over HTTP/1.1, and when a destination challenges an HTTP/2 client request, the request is reset with `HTTP_1_1_REQUIRED` so the client retries over HTTP/1.1 (disable with `NtlmSupportOptions.RequireHttp11ForChallenges`).
- If a cluster has several destinations, enable [session affinity](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/session-affinity) so a client keeps talking to the destination it authenticated with.
- Dedicated destination connections are closed after `NtlmSupportOptions.ConnectionIdleTimeout` (default 2 minutes) without requests. The client then re-authenticates on its next request.
- Don't put a component in front of the proxy that sends requests from different users over the same connection, because they'd share the authenticated destination connection. The same applies to any server that uses NTLM or Kerberos.

## Sample

[samples/ReverseProxy.Ntlm.Sample](https://github.com/Bambi-pa-hal-is/yarp-ntlm/tree/main/samples/ReverseProxy.Ntlm.Sample) shows a YARP gateway in front of a Windows-authenticated legacy app, migrating it one endpoint at a time to a modern app that uses JWT and React.

## Building

```
dotnet build yarp-ntlm.slnx
dotnet test --solution yarp-ntlm.slnx
dotnet pack src/Yarp.ReverseProxy.Ntlm -c Release -o artifacts
```

The sample's React app is built with Node.js when it's installed; otherwise that step is skipped.

## License

[MIT](LICENSE)

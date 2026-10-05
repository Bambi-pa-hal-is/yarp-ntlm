using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Yarp.ReverseProxy.Ntlm.Tests;

public class NtlmTests
{
    private readonly ITestOutputHelper _output;

    public NtlmTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Handshake_UsesSameDestinationConnection()
    {
        var test = CreateEnvironment();

        await test.Invoke(async uri =>
        {
            using var client = CreateClient();

            using var anonymous = await client.GetAsync(uri);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.Equal("NTLM", anonymous.Headers.WwwAuthenticate.ToString());

            using var negotiate = await SendAsync(client, uri, "type1");
            Assert.Equal(HttpStatusCode.Unauthorized, negotiate.StatusCode);
            Assert.Equal("NTLM type2", negotiate.Headers.WwwAuthenticate.ToString());

            using var authenticate = await SendAsync(client, uri, "type3");
            Assert.Equal(HttpStatusCode.OK, authenticate.StatusCode);
            var destinationConnection = await authenticate.Content.ReadAsStringAsync();

            // Following requests on the client connection don't need to authenticate again.
            for (var i = 0; i < 3; i++)
            {
                using var authenticated = await client.GetAsync(uri);
                Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
                Assert.Equal(destinationConnection, await authenticated.Content.ReadAsStringAsync());
            }
        });
    }

    [Fact]
    public async Task AuthenticatedDestinationConnection_IsNotSharedWithOtherClients()
    {
        var test = CreateEnvironment();

        await test.Invoke(async uri =>
        {
            using var clientA = CreateClient();
            using var clientB = CreateClient();

            (await SendAsync(clientA, uri, "type1")).Dispose();
            using var authenticated = await SendAsync(clientA, uri, "type3");
            Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);

            // Without dedicated connections client B would reuse client A's authenticated connection from the pool.
            for (var i = 0; i < 3; i++)
            {
                using var anonymous = await clientB.GetAsync(uri);
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

                using var stillAuthenticated = await clientA.GetAsync(uri);
                Assert.Equal(HttpStatusCode.OK, stillAuthenticated.StatusCode);
            }
        });
    }

    [Fact]
    public async Task Http2Challenge_ResetsWithHttp11Required()
    {
        var test = CreateEnvironment();
        test.UseHttpsOnProxy = true;
        test.ProxyProtocol = HttpProtocols.Http1AndHttp2;

        await test.Invoke(async uri =>
        {
            using var http2Client = CreateClient();
            http2Client.DefaultRequestVersion = HttpVersion.Version20;
            http2Client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

            using var http2Response = await http2Client.GetAsync(uri + "/no-auth");
            Assert.Equal(HttpStatusCode.OK, http2Response.StatusCode);
            Assert.Equal(HttpVersion.Version20, http2Response.Version);

            // Browsers retry over HTTP/1.1 when receiving HTTP_1_1_REQUIRED.
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => http2Client.GetAsync(uri));
            var protocolException = Assert.IsType<HttpProtocolException>(exception.InnerException);
            Assert.Equal(0xd, protocolException.ErrorCode);

            using var http11Client = CreateClient();
            using var challenge = await http11Client.GetAsync(uri);
            Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
            Assert.Equal("NTLM", challenge.Headers.WwwAuthenticate.ToString());
        });
    }

    [Fact]
    public async Task Http2Challenge_DisabledReset_ForwardsChallenge()
    {
        var test = CreateEnvironment();
        test.UseHttpsOnProxy = true;
        test.ProxyProtocol = HttpProtocols.Http1AndHttp2;
        test.ConfigureProxy = proxyBuilder => proxyBuilder.AddNtlmSupport(options => options.RequireHttp11ForChallenges = false);

        await test.Invoke(async uri =>
        {
            using var client = CreateClient();
            client.DefaultRequestVersion = HttpVersion.Version20;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

            using var challenge = await client.GetAsync(uri);
            Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
            Assert.Equal(HttpVersion.Version20, challenge.Version);
        });
    }

    [Fact]
    public async Task SupportNtlm_WithoutServices_Throws()
    {
        var test = new TestEnvironment(context => Task.CompletedTask)
        {
            ConfigureProxyApp = app => app.SupportNtlm(),
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => test.Invoke(_ => Task.CompletedTask));
        Assert.Contains("AddNtlmSupport", exception.Message);
    }

    [Fact]
    public async Task ConnectionTracker_RemovesIdleConnections()
    {
        using var tracker = new NtlmConnectionTracker(Options.Create(new NtlmSupportOptions
        {
            ConnectionIdleTimeout = TimeSpan.FromMilliseconds(1)
        }));

        var connection = tracker.EnterOrCreate("connection1");
        Assert.Same(connection, tracker.EnterOrCreate("connection1"));
        connection.Exit();

        await Task.Delay(50);

        // Still in use.
        tracker.RemoveIdleConnections();
        Assert.Equal(1, tracker.Count);

        connection.Exit();
        await Task.Delay(50);

        tracker.RemoveIdleConnections();
        Assert.Equal(0, tracker.Count);
        Assert.False(connection.TryEnter());
        Assert.Null(tracker.TryEnterExisting("connection1"));

        var newConnection = tracker.EnterOrCreate("connection1");
        Assert.NotSame(connection, newConnection);
    }

    [Theory]
    [InlineData("NTLM TlRMTVNTUAABAAAA", true)]
    [InlineData("Negotiate YIIGhgYGKwYBBQUCoIIGejCCBnag", true)]
    [InlineData("negotiate abc", true)]
    [InlineData("NTLM", true)]
    [InlineData("Basic dXNlcjpwYXNz", false)]
    [InlineData("Bearer token", false)]
    [InlineData("NTLMX abc", false)]
    [InlineData("", false)]
    public void IsConnectionBasedScheme(string value, bool expected)
    {
        Assert.Equal(expected, NtlmConnectionMiddleware.IsConnectionBasedScheme(value));
    }

    private TestEnvironment CreateEnvironment()
    {
        // Simulates a destination that authenticates connections, like IIS or Kestrel with Windows authentication.
        var connectionStates = new ConcurrentDictionary<string, string>();

        return new TestEnvironment(async context =>
        {
            if (context.Request.Path == "/no-auth")
            {
                return;
            }

            var connectionId = context.Connection.Id;
            var authorization = context.Request.Headers.Authorization.ToString();

            if (authorization == "NTLM type1")
            {
                connectionStates[connectionId] = "challenged";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "NTLM type2";
                return;
            }

            if (authorization == "NTLM type3" && connectionStates.TryGetValue(connectionId, out var state) && state == "challenged")
            {
                connectionStates[connectionId] = "authenticated";
            }

            if (connectionStates.TryGetValue(connectionId, out state) && state == "authenticated")
            {
                await context.Response.WriteAsync(connectionId);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "NTLM";
        })
        {
            TestOutput = _output,
            ProxyProtocol = HttpProtocols.Http1,
            DestinationProtocol = HttpProtocols.Http1,
            ConfigureProxy = proxyBuilder => proxyBuilder.AddNtlmSupport(),
            ConfigureProxyApp = app => app.SupportNtlm(),
        };
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient(new SocketsHttpHandler
        {
            // A single client connection, like a browser authenticating with NTLM.
            MaxConnectionsPerServer = 1,
            UseCookies = false,
            SslOptions = { RemoteCertificateValidationCallback = delegate { return true; } },
        });
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string uri, string ntlmMessage)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("NTLM", ntlmMessage);
        return client.SendAsync(request);
    }
}

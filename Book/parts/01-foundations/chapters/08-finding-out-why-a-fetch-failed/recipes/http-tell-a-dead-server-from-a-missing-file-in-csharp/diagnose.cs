// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Fetches with Curl.Api and says why a fetch failed, by the type of the failure and its code.
// Run: dotnet run diagnose.cs [-- case]
//   missing      a path the server does not have (the default)
//   up           a path it has
//   stopped      the same server after StopAsync
//   no-listener  a port where nothing was ever listening
//   unresolved   a host name that does not resolve
// The exit code is the one curl would have used: 0, 22 for an error status with -f, or the libcurl code.
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Net;
using System.Net.Sockets;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "missing";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
var server = await SurlServer.StartAsync(options);
string url = which switch
{
    "up" => $"{server.BoundUrl}hello.txt",
    "missing" => $"{server.BoundUrl}missing.txt",
    "no-listener" => $"http://127.0.0.1:{FreePort()}/hello.txt",
    "unresolved" => "http://no-such-host.invalid/hello.txt",
    _ => $"{server.BoundUrl}hello.txt",
};

if (which == "stopped")
{
    await server.StopAsync();
}

await using CurlClient client = CurlClient.Create();
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
    await response.Body.DisposeAsync();
    Console.WriteLine($"{which}: {response.StatusCode} {response.ReasonPhrase}");
    return response.StatusCode >= 400 ? 22 : 0;
}
catch (CurlException e)
{
    Console.WriteLine($"{which}: {e.GetType().Name} {e.Code} ({(int)e.Code})");
    return (int)e.Code;
}
finally
{
    await server.DisposeAsync();
}

// A port the system has just handed out and nothing holds: nothing listens there.
static int FreePort()
{
    var probe = new TcpListener(IPAddress.Loopback, 0);
    probe.Start();
    int port = ((IPEndPoint)probe.LocalEndpoint).Port;
    probe.Stop();
    return port;
}

// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test fixture asks Surl.Api for a fixed port; if the port is taken it catches the refusal
// and starts again on port 0. Run: dotnet run fixture.cs -- [case]
//   taken   another listener holds the port first (the default)
//   free    nothing holds it
//   bad     the port is 70000, which is no port at all
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Net;
using System.Net.Sockets;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "taken";

// The fixed port the fixture wants. In "taken", something else, a plain TcpListener, holds it first.
var holder = new TcpListener(IPAddress.Loopback, 0);
holder.Start();
int port = which == "bad" ? 70000 : ((IPEndPoint)holder.LocalEndpoint).Port;
if (which != "taken")
{
    holder.Stop();
}

SurlServer server;
try
{
    server = await StartOn(port);
}
catch (SurlServerStartException refused) when (refused.ExitCode == SurlExitCode.BindFailed)
{
    // 45: the port is taken. Port 0 asks the system for a free one; BoundUrl says which.
    Console.WriteLine($"refused: {refused.ExitCode} ({(int)refused.ExitCode})");
    Console.WriteLine($"surl: {refused.Message}");
    server = await StartOn(0);
}
catch (SurlServerStartException refused)
{
    // Any other refusal is a mistake port 0 would not fix: pass on the exit code surl would exit with.
    Console.WriteLine($"refused: {refused.ExitCode} ({(int)refused.ExitCode})");
    Console.WriteLine($"surl: {refused.Message}");
    return (int)refused.ExitCode;
}

await using (server)
{
    Console.WriteLine($"started on {(server.BoundUrl.Port == port ? "the port asked for" : "another port, " + (server.BoundUrl.Port > 0 ? "not 0" : "0"))}");

    // The server is up wherever it landed: fetch the file from the URL it bound.
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
    await using Stream body = response.Body;
    Console.Write($"{response.StatusCode} {response.ReasonPhrase}, {await new StreamReader(body).ReadToEndAsync()}");
}

holder.Stop();
return 0;

// One server with one file in memory, on the loopback address and the given port.
static Task<SurlServer> StartOn(int port)
{
    var options = new SurlServerOptions { ListenUrls = { $"http://127.0.0.1:{port}" } };
    options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
    return SurlServer.StartAsync(options);
}

// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a file on a Unix domain socket with Surl.Api and fetches it through the socket with Curl.Api.
// Run: dotnet run on-a-socket.cs -- <case> <port>
//   fetch        serve on surl.sock and fetch hello.txt through it (the default)
//   any-host     fetch through the socket with a URL whose host does not exist
//   no-tcp       fetch the listen URL over TCP, without the socket
//   file-exists  start on a path where a file already is
//   two-urls     start with two listen URLs on one socket
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "fetch";
string listenUrl = $"http://localhost:{(args.Length > 1 ? args[1] : "8080")}/";
// Each case has its own socket file, so one that fails cannot leave a file in the next one's way.
string socket = which switch { "fetch" => "surl.sock", "file-exists" => "taken.sock", _ => $"{which}.sock" };

// The server: surl's --unix-socket, as surl's own words. The listen URL names the server; no port opens.
var options = new SurlServerOptions { ListenUrls = { listenUrl }, AdditionalArguments = { "--unix-socket", socket } };
if (which == "two-urls")
{
    options.ListenUrls.Add("http://localhost:8081/");
}

options.Files["hello.txt"] = "hello over a socket\n"u8.ToArray();
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"refused: {e.ExitCode} {e.Message}");
    if (which == "file-exists")
    {
        Console.WriteLine($"{socket} still holds: {File.ReadAllText(socket).TrimEnd()}");
    }

    return 1;
}

Console.WriteLine($"listening on {server.BoundUrl}, socket file there: {File.Exists(socket)}");

// The client: the request names a URL, and UnixSocketPath says where to connect instead.
string url = which == "any-host" ? "http://files.example/hello.txt" : $"{listenUrl}hello.txt";
var network = which == "no-tcp" ? null : new CurlNetworkOptions { UnixSocketPath = socket };
try
{
    await using CurlClient client = CurlClient.Create();
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url) { Network = network }, CancellationToken.None);
        await using Stream body = response.Body;
        Console.Write($"{response.StatusCode} {await new StreamReader(body).ReadToEndAsync()}");
    }
    catch (CurlException e)
    {
        Console.WriteLine($"{e.Code}: {e.Message}");
    }
}
finally
{
    // Stopping the server in process removes the socket file it made, whatever the request did.
    Console.WriteLine($"stopped: {await server.StopAsync()}, socket file there: {File.Exists(socket)}");
}

return 0;

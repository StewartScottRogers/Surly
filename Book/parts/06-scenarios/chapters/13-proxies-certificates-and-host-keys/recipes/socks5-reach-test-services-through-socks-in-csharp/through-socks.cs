// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test on a network that allows only a SOCKS5 proxy: Surl.Api runs HTTP and FTP services and
// the proxy in one server, Curl.Api calls both services through the proxy, and the proxy's log
// says which connections it made.
// Run: dotnet run through-socks.cs [-- <case>]
//   (no argument)  HTTP and FTP through the proxy, then the proxy's connections
//   locked         FTP through the proxy to a service that takes no anonymous login
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string mode = args.Length > 0 ? args[0] : "calls";

// The test network: HTTP and FTP on localhost, and the SOCKS5 proxy, logging at verbose.
// AllowAnonymous is --allow-anonymous: the FTP service takes the client's anonymous login.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://localhost:0", "ftp://localhost:0", "socks5h://127.0.0.1:0" },
    AllowAnonymous = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
int httpPort = server.BoundUrls[0].Port, ftpPort = server.BoundUrls[1].Port, socksPort = server.BoundUrls[2].Port;

// An FTP service as surl starts by default: no anonymous login.
await using SurlServer locked = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "ftp://localhost:0" } });
int lockedPort = locked.BoundUrl.Port;

// The client: every call goes through the proxy, which resolves localhost (socks5h).
await using (CurlClient client = CurlClient.Create(new CurlClientOptions
{
    Proxy = new CurlProxyOptions { Proxy = $"socks5h://127.0.0.1:{socksPort}" },
    Timeout = TimeSpan.FromSeconds(20),
}))
{
    string[] targets = mode == "locked"
        ? [$"ftp://localhost:{lockedPort}/hello.txt"]
        : [$"http://localhost:{httpPort}/hello.txt", $"ftp://localhost:{ftpPort}/hello.txt"];
    foreach (string target in targets)
    {
        try
        {
            CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, target), CancellationToken.None);
            await using Stream body = response.Body;
            Console.WriteLine($"{target[..target.IndexOf(':')]}: {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
        }
        catch (CurlException failure)
        {
            Console.WriteLine($"{target[..target.IndexOf(':')]}: CurlException {failure.Code} ({(int)failure.Code})");
        }
    }
}

// The proxy's record: each "Connected to" line of its log, named by the port it went to. The FTP
// passive port is the one in the FTP service's 229 reply.
await server.StopAsync();
string text = log.ToString();
Match passive = Regex.Match(text, @"229 Entering Extended Passive Mode \(\|\|\|(\d+)\|\)");
foreach (Match connected in Regex.Matches(text, @"\* Connected to localhost:(\d+); answered 05 00\."))
{
    int port = int.Parse(connected.Groups[1].Value);
    string what = port == httpPort ? "the HTTP service"
        : port == ftpPort ? "the FTP service"
        : port == lockedPort ? "the locked FTP service"
        : passive.Success && port == int.Parse(passive.Groups[1].Value) ? "the FTP passive port from the 229 reply"
        : $"port {port}";
    Console.WriteLine($"proxy connected to {what}");
}

return 0;

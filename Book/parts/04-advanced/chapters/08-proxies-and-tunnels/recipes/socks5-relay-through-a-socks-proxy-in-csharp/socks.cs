// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of a client behind a SOCKS5 proxy: Surl.Api runs an origin and a SOCKS5 server in one
// server, Curl.Api fetches through it, and the server's log says what the SOCKS server did.
// Run: dotnet run socks.cs -- <case>
//   fetch        socks5h://: the server resolves localhost (the default)
//   local        socks5://: the client resolves origin.test itself, through Resolve
//   remote-name  socks5h:// with the same name: the server cannot resolve it
//   socks4       a SOCKS4 client
//   outside      an origin outside loopback, 192.0.2.1
//   no-login     a server with an account, and a client with no login
//   in-clear     the same server and the right login, sent in clear
//   plaintext    the same, to a server that allows passwords in clear
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "fetch";

// One server: the origin on localhost, and a SOCKS5 server, logging at verbose.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://localhost:0", "socks5h://127.0.0.1:0" },
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
    AllowPlaintextAuthentication = which == "plaintext",
};
if (which is "no-login" or "in-clear" or "plaintext")
{
    // -U names the accounts of every proxy listener, the SOCKS server's included.
    options.AdditionalArguments.Add("-U");
    options.AdditionalArguments.Add("app:socks-pw");
}
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri origin = server.BoundUrls[0], socks = server.BoundUrls[1];
int port = origin.Port;

// The client under test: the scheme of the proxy URL says who resolves names.
string scheme = which switch { "local" => "socks5", "socks4" => "socks4", _ => "socks5h" };
string target = which switch
{
    "local" or "remote-name" => $"http://origin.test:{port}/hello.txt",
    "outside" => "http://192.0.2.1/hello.txt",
    _ => $"{origin}hello.txt",
};
await using CurlClient client = CurlClient.Create(new CurlClientOptions
{
    Proxy = new CurlProxyOptions { Proxy = $"{scheme}://127.0.0.1:{socks.Port}" },
    Network = new CurlNetworkOptions { Resolve = [$"origin.test:{port}:127.0.0.1"] },
    Authentication = which is "in-clear" or "plaintext" ? new CurlAuthenticationOptions { ProxyUserPassword = "app:socks-pw" } : null,
    Timeout = TimeSpan.FromSeconds(20),
});
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, target), CancellationToken.None);
    await using Stream body = response.Body;
    Console.WriteLine($"{response.StatusCode}, {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
}
catch (CurlException failure)
{
    Console.WriteLine($"CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
}

// What the SOCKS server said it did: its "Connected to" and "Answered" event lines.
await server.StopAsync();
foreach (string line in log.ToString().Split('\n').Where(l => l.Contains(" * Connected to ") || l.Contains(" * Answered ")))
    Console.WriteLine(line.TrimEnd());
return 0;

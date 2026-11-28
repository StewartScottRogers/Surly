// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of a client that logs in to a SOCKS5 proxy: Surl.Api runs an origin and a SOCKS5 server
// with one account, Curl.Api logs in through it, and the server's log says how it judged the login.
// Run: dotnet run socks-login.cs -- <server> <login>
//   server  open (passwords in clear allowed, the default) or strict (as surl starts by default)
//   login   right (the default), none, wrong, unknown (a user with no account),
//           or split (the right login as separate user name and password)
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string mode = args.Length > 0 ? args[0] : "open";
string login = args.Length > 1 ? args[1] : "right";

// One server: the origin on localhost and a SOCKS5 server; -U gives every proxy listener an account.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://localhost:0", "socks5h://127.0.0.1:0" },
    AdditionalArguments = { "-U", "app:socks-pw" },
    AllowPlaintextAuthentication = mode == "open",
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri origin = server.BoundUrls[0], socks = server.BoundUrls[1];

// The client under test: the login as one user:password string, or as its two parts.
CurlAuthenticationOptions? authentication = login switch
{
    "right" => new CurlAuthenticationOptions { ProxyUserPassword = "app:socks-pw" },
    "wrong" => new CurlAuthenticationOptions { ProxyUserPassword = "app:wrong-pw" },
    "unknown" => new CurlAuthenticationOptions { ProxyUserPassword = "bob:socks-pw" },
    "split" => new CurlAuthenticationOptions { ProxyUserName = "app", ProxyPassword = "socks-pw" },
    _ => null,
};
await using CurlClient client = CurlClient.Create(new CurlClientOptions
{
    Proxy = new CurlProxyOptions { Proxy = $"socks5h://127.0.0.1:{socks.Port}" },
    Authentication = authentication,
    Timeout = TimeSpan.FromSeconds(20),
});
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{origin}hello.txt"), CancellationToken.None);
    await using Stream body = response.Body;
    Console.WriteLine($"{response.StatusCode}, {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
}
catch (CurlException failure)
{
    Console.WriteLine($"CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
}

// How the server judged it: the warning at start, and its SOCKS5 and login event lines.
await server.StopAsync();
string[] lines = log.ToString().Split('\n').Select(l => l.TrimEnd()).ToArray();
foreach (string line in lines.Where(l => l.StartsWith("surl: warning:") || l.Contains(" * SOCKS5 password") || l.Contains(" * No SOCKS5 method")
             || l.Contains(" * Answered 01") || l.Contains(" * Login ")))
    Console.WriteLine(line);
Console.WriteLine($"socks-pw in the log: {(lines.Any(l => l.Contains("socks-pw")) ? "yes" : "no")}");
return 0;

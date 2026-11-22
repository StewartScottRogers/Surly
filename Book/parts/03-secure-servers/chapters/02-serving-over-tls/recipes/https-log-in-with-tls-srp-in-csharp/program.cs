// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A TLS-SRP test server in-process: started with one account and no certificate, its accounts
// changed while it runs, and a CurlClient's attempt to log in.
// Run: dotnet run program.cs -- <scenario>
//   fetch     a CurlClient with the SRP options set fetches hello.txt
//   accounts  add, change and remove TLS-SRP accounts while the server runs
//   not-srp   the same calls on a server started without TLS-SRP
//   tls13     TLS-SRP with --tlsv1.3
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Tls;
using Surl.Api;

string scenario = args[0];

// The server: no property for TLS-SRP, so surl's own options go in AdditionalArguments.
// --tlsuser with --tlspassword is one account and implies --tlsauthtype SRP; no certificate.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "https://127.0.0.1:0" }, Log = TextWriter.Synchronized(log) };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
if (scenario == "not-srp")
{
    options.SelfSigned = true;
}
else
{
    foreach (string word in new[] { "--tlsuser", "device", "--tlspassword", "s3cret" })
    {
        options.AdditionalArguments.Add(word);
    }
}

if (scenario == "tls13")
{
    options.AdditionalArguments.Add("--tlsv1.3");
}

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"server refused to start: {(int)e.ExitCode} {e.ExitCode}");
    Console.WriteLine(e.Lines[0]);
    return (int)e.ExitCode;
}

await using var _ = server;
Console.WriteLine($"serving {server.BoundUrl}");

if (scenario == "fetch")
{
    // Curl.Api's TLS-SRP options, as curl's --tlsauthtype, --tlsuser and --tlspassword.
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
    {
        Authentication = new CurlAuthenticationOptions { TlsAuthType = "SRP", TlsAuthUserName = "device", TlsAuthPassword = "s3cret" },
    };
    var tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false } };
    await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = tls, Timeout = TimeSpan.FromSeconds(10) });
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        Console.WriteLine((int)response.StatusCode);
        return 0;
    }
    catch (CurlException e)
    {
        Console.WriteLine($"client failed: {e.Code}");
    }

    // The server's log says why, once it has written it.
    for (int i = 0; i < 50 && !log.ToString().Contains("TLS handshake failed"); i++)
    {
        await Task.Delay(100);
    }

    string line = log.ToString().Split('\n').First(l => l.Contains("TLS handshake failed"));
    Console.WriteLine("server: " + line[(line.IndexOf("* ") + 2)..].TrimEnd('\r'));
    return 1;
}

// TlsSrpAccounts: the accounts a TLS-SRP handshake runs against, changed while the server runs.
void Try(string what, Func<object> action)
{
    try
    {
        Console.WriteLine($"{what}: {action()}");
    }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException)
    {
        Console.WriteLine($"{what}: {e.GetType().Name}");
    }
}

SurlServerTlsSrpAccounts accounts = server.TlsSrpAccounts;
Try("add device-2", () => { accounts.Add("device-2", "an0ther"); return "done"; });
Try("add device-2 again", () => { accounts.Add("device-2", "other"); return "done"; });
Try("set device's password", () => { accounts.SetPassword("device", "n3w-secret"); return "done"; });
Try("set nobody's password", () => { accounts.SetPassword("nobody", "n3w-secret"); return "done"; });
Try("remove device", () => accounts.Remove("device"));
Try("remove device again", () => accounts.Remove("device"));
return 0;

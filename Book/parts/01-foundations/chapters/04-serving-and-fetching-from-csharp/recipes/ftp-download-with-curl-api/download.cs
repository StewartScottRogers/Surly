// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Downloads a file from an in-process FTP server that wants an account, with Curl.Api.
// Run: dotnet run download.cs [-- <case>]
//   ok         log in as alice with the right password and download hello.txt (the default)
//   plaintext  the same against a server that was not told to accept a password in clear
//   wrong      the wrong password
//   anonymous  no account at all
//   missing    a file the server does not have
//   dot        a dot file the server holds
//   commands   the control connection's commands, as the server recorded them
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "ok";

var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowPlaintextAuthentication = which != "plaintext",
    RecordExchanges = true,
};
options.Accounts.Add(new SurlAccount("alice", "wonderland"));
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files[".secret"] = "not for you\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{which switch { "missing" => "nope.txt", "dot" => ".secret", _ => "hello.txt" }}");
if (which != "anonymous")
{
    request = request with
    {
        Authentication = new CurlAuthenticationOptions
        {
            UserName = "alice",
            Password = which == "wrong" ? "looking-glass" : "wonderland",
        },
    };
}

await using CurlClient client = CurlClient.Create();
try
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    Console.Write(await new StreamReader(body).ReadToEndAsync());
}
catch (CurlException failure)
{
    Console.WriteLine($"{failure.Code} ({(int)failure.Code})");
}

if (which == "commands")
{
    // The record can trail the client by a moment: read it again until it holds the RETR that was
    // answered, for up to 10 s.
    string received = Encoding.ASCII.GetString(server.Exchanges[0].Received);
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); !received.Contains("RETR ") && waited.Elapsed.TotalSeconds < 10;)
    {
        await Task.Delay(100);
        received = Encoding.ASCII.GetString(server.Exchanges[0].Received);
    }

    foreach (string line in received.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
    {
        Console.WriteLine(line.StartsWith("PASS") ? "PASS ****" : line);
    }
}

return 0;

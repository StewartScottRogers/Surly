// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that moves an S3 server's clock and checks that a signed put is refused as too skewed,
// then accepted when the clock is put back. server.pem and server.key are in the folder.
// Run: dotnet run program.cs [-- <case>]
//   (none)    the clock twenty minutes ahead, then put back (the default)
//   window    the clock 14 minutes and 16 minutes ahead and behind
//   inside    the clock 10 minutes ahead
//   behind    the clock 20 minutes behind
//   message   the refusal's code and message, whole
//   record    what the server's record shows for the refused request
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "run";

// The server's clock: the real time plus an offset the test sets.
var clock = new OffsetClock();
var options = new SurlServerOptions
{
    ListenUrls = { "s3://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret") },
    AllowUploads = true,
    ListDirectories = true,
    RecordExchanges = true,
    TimeProvider = clock,
};
options.Files["reports/2026-q2.csv"] = "region,total\nnorth,1100\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create();
var network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] };
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };

if (which is "inside" or "behind" or "message")
{
    clock.Offset = TimeSpan.FromMinutes(which == "inside" ? 10 : which == "behind" ? -20 : 20);
    Console.WriteLine($"server {clock.Offset.TotalMinutes:+0;-0} min: {await Put("x.csv")}");
    Console.WriteLine($"store: {await Store()}");
    return 0;
}

if (which == "window")
{
    foreach (int minutes in new[] { 14, 16, -14, -16 })
    {
        clock.Offset = TimeSpan.FromMinutes(minutes);
        Console.WriteLine($"server {minutes:+0;-0} min: {await Put($"m{minutes + 20}.csv")}");
    }
    return 0;
}

clock.Offset = TimeSpan.FromMinutes(20);
Console.WriteLine($"server 20 min ahead: {await Put("2026-q3.csv")}");
Console.WriteLine($"store: {await Store()}");

if (which == "record")
{
    // The refused request was recorded as an exchange; wait, bounded, for the one that ended.
    var ended = new SurlEventQuery { Scheme = "s3", Kind = SurlEventKind.ExchangeEnded };
    await server.WaitForEventAsync(ended, TimeSpan.FromSeconds(30));
    Console.WriteLine($"exchanges recorded: {server.Exchanges.Count}");
    foreach (var judged in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
        Console.WriteLine($"login judged: {judged.Account} {(judged.IsAccepted ? "accepted" : "refused")}");
    return 0;
}

clock.Offset = TimeSpan.Zero;
Console.WriteLine($"clock put back: {await Put("2026-q3.csv")}");
Console.WriteLine($"store: {await Store()}");
return 0;

// One signed put. An S3 refusal is a CurlException whose message carries the code.
async Task<string> Put(string key)
{
    var put = new CurlRequest(HttpMethod.Put, $"s3://reports/{key}")
    {
        Network = network,
        Tls = trust,
        Authentication = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = "s3cret" },
        Body = Encoding.UTF8.GetBytes("region,total\nnorth,1200\n"),
    };
    try
    {
        CurlResponse response = await client.SendAsync(put, CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
        return response.StatusCode.ToString();
    }
    catch (CurlException failure)
    {
        return $"{failure.Code} {failure.Message}";
    }
}

// What the server holds, read from its store rather than over S3.
async Task<string> Store()
{
    SurlFolderPage page = await server.Files.ListAsync("/reports");
    return string.Join(", ", page.Entries.Select(e => e.Name));
}

class OffsetClock : TimeProvider
{
    public TimeSpan Offset { get; set; }
    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;
}

// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an upload pipeline: the report goes over FTP to an inbox, over HTTPS to staging, then into
// an S3 bucket. The in-process server cuts the S3 step's connection once; the pipeline runs that step
// again and does not repeat the first two. server.pem and server.key are in the folder.
// Run: dotnet run program.cs [-- <case>]
//   (none)   the pipeline with one more try for a failed step (the default)
//   stop     the pipeline as first written, which stops at the first failure
//   events   the default run, then the server's fault events
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "resume";
byte[] report = "region,total\r\nnorth,1200\r\nsouth,950\r\n"u8.ToArray();
byte[] yesterday = "region,total\r\nnorth,1180\r\nsouth,940\r\n"u8.ToArray();

// One server for the three services, over one store: FTP over TLS, HTTPS and S3, the job's login and
// its key pair, uploads on, and yesterday's report in each folder. It cuts one S3 answer after 1 byte.
var options = new SurlServerOptions
{
    ListenUrls = { "ftps://127.0.0.1:0", "https://127.0.0.1:0", "s3://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("nightly", "s3cret"), new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret") },
    AllowUploads = true,
    RecordExchanges = true,
};
foreach (string folder in new[] { "inbox", "staging", "archive" })
{
    options.Files[$"{folder}/2026-11-23.csv"] = yesterday;
}

options.Faults.Add(SurlFault.DropConnection() with { Scheme = "s3", AfterBytes = 1, Close = FaultClose.Graceful, Times = 1 });
await using SurlServer server = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create();
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
var job = new CurlAuthenticationOptions { UserName = "nightly", Password = "s3cret" };
const string Name = "2026-11-24.csv";

// The pipeline's three steps, in order; each returns what its server answered.
var steps = new List<(string Name, Func<Task<string>> Run)>
{
    ("ftps", async () => await Put(new CurlRequest(HttpMethod.Put, $"{server.BoundUrls[0]}inbox/{Name}") { Tls = trust, Authentication = job, Body = report })),
    ("https", async () => await Put(new CurlRequest(HttpMethod.Put, $"{server.BoundUrls[1]}staging/{Name}") { Tls = trust, Authentication = job, Body = report })),
    ("s3", async () => await Put(new CurlRequest(HttpMethod.Put, $"s3://archive/{Name}")
    {
        Network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrls[2].Port}"] },
        Tls = trust,
        Authentication = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = "s3cret" },
        Body = report,
    })),
};

// Run the steps in order. A failed step is run once more, alone, when the pipeline allows it;
// the steps before it are done and stay done.
int exit = 0;
foreach ((string name, Func<Task<string>> run) in steps)
{
    try
    {
        Console.WriteLine($"{name}: {await run()}");
    }
    catch (CurlException failure) when (which != "stop")
    {
        Console.WriteLine($"{name}: {failure.Code}, trying this step again");
        Console.WriteLine($"{name}: {await run()}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{name}: {failure.Code}, the pipeline stops");
        exit = 1;
        break;
    }
}

// What the server recorded: one exchange per connection, by scheme.
string Count(string scheme) => $"{scheme} {server.Exchanges.Count(e => e.Scheme == scheme)}";
Console.WriteLine($"exchanges: {Count("ftps")}, {Count("https")}, {Count("s3")}");

// The archive's copy, read from the server's store.
byte[] archived = (await server.Files.ReadAsync($"/archive/{Name}")).Bytes.ToArray();
Console.WriteLine($"archive: {archived.Length} bytes, {(archived.AsSpan().SequenceEqual(report) ? "identical to the report" : "DIFFERENT from the report")}");

if (which == "events")
{
    foreach (FaultAppliedEvent applied in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Cast<FaultAppliedEvent>())
    {
        Console.WriteLine($"fault {applied.RuleId}: {applied.Kind}, exchange of {server.Exchanges.Single(e => e.ExchangeId == applied.ExchangeId).Scheme}");
    }
}

return exit;

// One upload; the last reply code, 226 for FTP.
async Task<string> Put(CurlRequest request)
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await response.Body.DisposeAsync();
    return response.StatusCode.ToString();
}
